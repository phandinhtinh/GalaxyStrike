using System;
using System.Globalization;
using UnityEngine;
using GalaxyStrike.Data;

namespace GalaxyStrike.Save
{
    /// <summary>
    /// Quản lý dữ liệu người chơi tập trung (Giai đoạn 2).
    ///
    /// Nhiệm vụ:
    ///  - Hợp nhất Coin / Gem / Energy / Level / Tàu đã mở khóa / Cấp nâng cấp vào MỘT file save.
    ///  - Mã hóa AES-256 + chữ ký HMAC -> người chơi không sửa được file trên Android.
    ///  - Tự hồi 1 năng lượng mỗi <see cref="EnergyRefillMinutes"/> phút, tối đa <see cref="MaxEnergy"/>,
    ///    kể cả khi đã tắt game (dựa trên đồng hồ UTC).
    ///  - Tự động chuyển dữ liệu PlayerPrefs cũ (bản trước Giai đoạn 2) sang save mới 1 lần duy nhất.
    ///
    /// Singleton tự sinh (DontDestroyOnLoad) nên dùng được ở cả HomeScene và GameScene.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SaveManager : SingletonBehaviour<SaveManager>
    {
        private const string SaveKey = "GalaxyStrike_SaveV2";
        private const string LegacyMigrationKey = "GalaxyStrike_MigratedV2";

        // ---- Điểm nối cho test (Assets/Tests/Editor). Game luôn dùng giá trị mặc định. ----

        /// <summary>
        /// Tiền tố cho MỌI key PlayerPrefs mà SaveManager đọc/ghi. Test đặt một tiền tố riêng
        /// để không đọc hay ghi đè save thật của người chơi trong Editor.
        /// </summary>
        internal static string KeyPrefix = string.Empty;

        /// <summary>Đồng hồ UTC giả cho test (hồi năng lượng, reset ngày/tuần). null = giờ thật.</summary>
        internal static Func<DateTime> ClockOverride;

        private static string PrefKey(string key) => KeyPrefix + key;

        private static DateTime UtcNow => ClockOverride != null
            ? DateTime.SpecifyKind(ClockOverride(), DateTimeKind.Utc)
            : DateTime.UtcNow;

        private static long UnixNow => new DateTimeOffset(UtcNow).ToUnixTimeSeconds();

        /// <summary>Vào Play Mode luôn dùng key và đồng hồ thật, kể cả khi test bị ngắt giữa chừng.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTestSeams()
        {
            KeyPrefix = string.Empty;
            ClockOverride = null;
        }

        [Header("--- Energy ---")]
        [Tooltip("Số phút để hồi 1 năng lượng")]
        [SerializeField, Min(1)] private int energyRefillMinutes = 12;
        [Tooltip("Năng lượng tối đa")]
        [SerializeField, Min(1)] private int maxEnergy = 30;
        [Tooltip("Chi phí năng lượng cho 1 lượt chơi")]
        [SerializeField, Min(0)] private int energyCostPerRun = 5;

        [Header("--- New Player Defaults ---")]
        [SerializeField] private int startingCoins = 500;
        [SerializeField] private int startingGems = 20;

        private SaveData data;

        /// <summary>Phát mỗi khi dữ liệu thay đổi (UI lắng nghe để refresh).</summary>
        public event Action OnDataChanged;

        /// <summary>
        /// Truy cập dữ liệu save. Lần đầu được gọi sẽ tự nạp từ đĩa (EnsureLoaded),
        /// nên không bao giờ trả về null.
        /// </summary>
        public SaveData Data
        {
            get
            {
                EnsureLoaded();
                return data;
            }
        }

        public int EnergyRefillMinutes => energyRefillMinutes;
        public int MaxEnergy => maxEnergy;
        public int EnergyCostPerRun => energyCostPerRun;

        /// <summary>
        /// Callback của <see cref="SingletonBehaviour{T}"/>: nạp save ngay từ đầu.
        /// Việc giữ object qua Scene và huỷ bản trùng đã do base class lo.
        /// </summary>
        protected override void OnSingletonAwake()
        {
            EnsureLoaded();
            RecordLoginIfNewDay();
        }

        /// <summary>
        /// Android/iOS gọi khi app bị đưa xuống nền:
        /// - Bị pause -> Flush() để không mất dữ liệu nếu hệ điều hành kill app.
        /// - Quay lại -> tính năng lượng đã hồi trong lúc ở nền rồi báo UI refresh.
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                Flush();
            }
            else
            {
                // Quay lại từ nền: đồng hồ thực đã trôi, hồi năng lượng ngay.
                ApplyEnergyRefill();
                // Có thể đã sang ngày mới khi app nằm nền -> tính thêm một ngày đăng nhập.
                RecordLoginIfNewDay();
                NotifyChanged();
            }
        }

        /// <summary>
        /// Ghi dữ liệu xuống đĩa lần cuối trước khi thoát game.
        /// </summary>
        private void OnApplicationQuit()
        {
            Flush();
        }

        private float energyTickTimer;

        /// <summary>
        /// Đếm ngược năng lượng theo thời gian thực mỗi giây.
        /// Trước đây chỉ hồi khi mở HomeScene, nên ở Shop 10 phút sẽ không thấy thay đổi.
        /// </summary>
        private void Update()
        {
            if (data == null)
                return;

            if (data.energy >= data.maxEnergy)
            {
                energyTickTimer = 0f;
                return;
            }

            energyTickTimer += Time.unscaledDeltaTime;
            if (energyTickTimer < 1f)
                return;

            energyTickTimer = 0f;

            int before = data.energy;
            ApplyEnergyRefill();

            if (data.energy != before)
                NotifyChanged();
        }

        // =====================================================================
        // NẠP / LƯU
        // =====================================================================

        /// <summary>
        /// Đảm bảo dữ liệu đã được nạp (chỉ nạp 1 lần):
        /// đọc save từ đĩa hoặc tạo save mới, chuẩn hoá dữ liệu, ghi lần đầu,
        /// migrate dữ liệu PlayerPrefs cũ và áp năng lượng đã hồi.
        /// </summary>
        private void EnsureLoaded()
        {
            if (data != null)
                return;

            SaveData loaded = LoadFromDisk();
            data = loaded ?? CreateNewSave();

            // Normalize cả save mới để chặn giá trị cấu hình bất thường trong Inspector.
            Normalize(data);

            bool legacyMigrated = MigrateLegacyPlayerPrefs();
            bool campaignMigrated = data.campaignStage <= 0;
            if (campaignMigrated)
                MigrateCampaignProgress(loaded != null);

            if (loaded == null || legacyMigrated || campaignMigrated)
                Flush();

            ApplyEnergyRefill();
        }

        /// <summary>
        /// Tạo save cho người chơi mới: coin/gem khởi điểm, năng lượng đầy,
        /// mốc thời gian hồi = hiện tại, và mở khóa sẵn tàu đầu tiên (index 0).
        /// </summary>
        private SaveData CreateNewSave()
        {
            var fresh = new SaveData
            {
                coins = startingCoins,
                gems = startingGems,
                energy = maxEnergy,
                maxEnergy = maxEnergy,
                lastEnergyRefillUnixTime = UnixNow
            };

            fresh.UnlockShip(0);
            return fresh;
        }

        /// <summary>
        /// Đọc chuỗi save từ PlayerPrefs, giải mã (kiểm tra chữ ký HMAC), rồi parse JSON
        /// thành SaveData. Trả null nếu chưa có save, bị chỉnh sửa, hoặc JSON hỏng
        /// (khi đó game sẽ tạo save mới thay vì crash).
        /// </summary>
        private SaveData LoadFromDisk()
        {
            string stored = PlayerPrefs.GetString(PrefKey(SaveKey), string.Empty);
            if (string.IsNullOrEmpty(stored))
                return null;

            if (!SaveCrypto.TryDecrypt(stored, out string json))
            {
                // File bị sửa hoặc hỏng -> ghi log và bắt đầu lại để không crash.
                Debug.LogWarning("[SaveManager] File save không hợp lệ hoặc đã bị chỉnh sửa. Tạo dữ liệu mới.");
                return null;
            }

            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded == null)
                    return null;

                Normalize(loaded);
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Không đọc được save: {e.Message}");
                return null;
            }
        }

        /// <summary>Chặn giá trị vô lý (âm, vượt trần) tránh lỗi UI và economy.</summary>
        private void Normalize(SaveData save)
        {
            save.coins = Mathf.Max(0, save.coins);
            save.gems = Mathf.Max(0, save.gems);
            save.maxEnergy = Mathf.Max(1, maxEnergy);
            save.energy = Mathf.Clamp(save.energy, 0, save.maxEnergy);
            save.accountLevel = Mathf.Max(1, save.accountLevel);
            save.highestLevel = Mathf.Clamp(save.highestLevel, 0, LevelManager.CampaignLength);
            save.campaignStage = Mathf.Clamp(save.campaignStage, 0, LevelManager.CampaignLength);
            save.totalKills = Mathf.Max(0, save.totalKills);
            save.bestScore = Mathf.Max(0, save.bestScore);
            save.selectedShipIndex = Mathf.Max(0, save.selectedShipIndex);

            save.maxHealthUpgrade = Mathf.Clamp(save.maxHealthUpgrade, 0, ShipUpgradeCatalog.MaxLevel);
            save.fireRateUpgrade = Mathf.Clamp(save.fireRateUpgrade, 0, ShipUpgradeCatalog.MaxLevel);
            save.moveSpeedUpgrade = Mathf.Clamp(save.moveSpeedUpgrade, 0, ShipUpgradeCatalog.MaxLevel);
            save.damageUpgrade = Mathf.Clamp(save.damageUpgrade, 0, ShipUpgradeCatalog.MaxLevel);

            if (save.unlockedShipIndices == null)
                save.unlockedShipIndices = new System.Collections.Generic.List<int>();

            // ownedItemIds có thể null khi nâng cấp từ save cũ (chưa có field này).
            if (save.ownedItemIds == null)
                save.ownedItemIds = new System.Collections.Generic.List<string>();

            save.ownedItemIds.RemoveAll(string.IsNullOrEmpty);

            if (save.processedIapTransactions == null)
                save.processedIapTransactions = new System.Collections.Generic.List<string>();

            // Nhiệm vụ: save cũ chưa có các field này.
            bool hadLifetimeStats = save.lifetimeTaskStats != null && save.lifetimeTaskStats.Length > 0;
            save.dailyTaskStats = ResizeStats(save.dailyTaskStats);
            save.weeklyTaskStats = ResizeStats(save.weeklyTaskStats);
            save.lifetimeTaskStats = ResizeStats(save.lifetimeTaskStats);
            if (!hadLifetimeStats)
            {
                // Thành tựu tính cả công sức trước khi có hệ thống nhiệm vụ.
                save.lifetimeTaskStats[(int)TaskMetric.EnemiesDestroyed] = save.totalKills;
                save.lifetimeTaskStats[(int)TaskMetric.StagesCleared] = save.highestLevel;
            }

            if (save.claimedDailyTasks == null)
                save.claimedDailyTasks = new System.Collections.Generic.List<string>();
            if (save.claimedWeeklyTasks == null)
                save.claimedWeeklyTasks = new System.Collections.Generic.List<string>();
            if (save.claimedAchievements == null)
                save.claimedAchievements = new System.Collections.Generic.List<string>();

            // XP không được âm; level phải khớp với tổng XP (chống sửa tay).
            // Giới hạn XP tối đa để vòng lặp bên dưới luôn kết thúc (chống file save bị chỉnh).
            save.experience = Mathf.Clamp(save.experience, 0, MaxExperience);

            int levelFromXp = 1;
            while (save.experience >= GetTotalXpForLevel(levelFromXp + 1))
                levelFromXp++;

            if (save.accountLevel != levelFromXp)
                save.accountLevel = levelFromXp;

            save.UnlockShip(0);
            save.unlockedShipIndices.RemoveAll(index => index < 0);

            if (!save.HasShip(save.selectedShipIndex))
                save.selectedShipIndex = 0;
        }

        /// <summary>Chuyển dữ liệu PlayerPrefs của bản cũ sang save mới, đúng 1 lần.</summary>
        private bool MigrateLegacyPlayerPrefs()
        {
            if (PlayerPrefs.GetInt(PrefKey(LegacyMigrationKey), 0) == 1)
                return false;

            if (PlayerPrefs.HasKey(PrefKey("GalaxyStrike_Coins")))
                data.coins = Mathf.Max(data.coins, PlayerPrefs.GetInt(PrefKey("GalaxyStrike_Coins"), 0));

            for (int i = 0; i < 8; i++)
            {
                if (PlayerPrefs.GetInt(PrefKey($"Ship_Unlocked_{i}"), 0) == 1)
                    data.UnlockShip(i);
            }

            data.selectedShipIndex = Mathf.Max(0, PlayerPrefs.GetInt(PrefKey("SelectedShipIndex"), 0));
            if (!data.HasShip(data.selectedShipIndex))
                data.selectedShipIndex = 0;

            PlayerPrefs.SetInt(PrefKey(LegacyMigrationKey), 1);
            Debug.Log("[SaveManager] Đã chuyển dữ liệu PlayerPrefs cũ sang SaveV2.");
            return true;
        }

        private void MigrateCampaignProgress(bool hasSignedSave)
        {
            int legacyStage = PlayerPrefs.GetInt(PrefKey(LevelManager.LegacyStageKey), 0);
            int cleared = data.highestLevel;
            bool legacyCampaign = !LevelManager.ChallengeSelected &&
                legacyStage >= 1 && legacyStage <= LevelManager.CampaignLength;
            bool matchesClearedStage = legacyStage >= cleared && legacyStage <= cleared + 1;

            // The old signed save used 1 both as the default and as a clear of stage 1.
            int nextVerifiedStage = cleared > 1 ? cleared + 1 : 1;
            data.campaignStage = legacyCampaign && (!hasSignedSave || matchesClearedStage)
                ? legacyStage
                : Mathf.Min(nextVerifiedStage, LevelManager.CampaignLength);

            if (cleared == 1 && data.campaignStage == 1)
                cleared = 0;
            data.highestLevel = Mathf.Max(cleared, data.campaignStage - 1);
        }

        /// <summary>Ghi xuống máy ngay lập tức (mã hóa + chữ ký).</summary>
        public void Flush()
        {
            TryFlush();
        }

        private bool TryFlush()
        {
            if (data == null)
                return false;

            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(PrefKey(SaveKey), SaveCrypto.Encrypt(json));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Lỗi khi lưu dữ liệu: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Phát sự kiện OnDataChanged để mọi UI đang đăng ký tự refresh
        /// (ví dụ số Coin trên HUD, danh sách tàu...).
        /// </summary>
        public void NotifyChanged()
        {
            Action handlers = OnDataChanged;
            if (handlers == null)
                return;

            // Gọi từng listener riêng: một UI lỗi (hoặc đã bị huỷ mà quên huỷ đăng ký)
            // không được làm gãy luồng gameplay đang gọi tới đây (cộng Coin, nhặt vật phẩm...).
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                if (handler.Target is UnityEngine.Object owner && owner == null)
                {
                    OnDataChanged -= (Action)handler;
                    continue;
                }

                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // =====================================================================
        // NĂNG LƯỢNG
        // =====================================================================
        public int Energy => Data.energy;
        public float EnergyProgress => data.maxEnergy > 0 ? (float)data.energy / data.maxEnergy : 0f;

        /// <summary>
        /// Hồi năng lượng theo thời gian thực đã trôi qua (kể cả khi thoát game).
        /// Đồng hồ chỉ chạy khi chưa đầy năng lượng.
        /// </summary>
        public void ApplyEnergyRefill()
        {
            EnsureLoaded();

            long now = UnixNow;

            if (data.energy >= data.maxEnergy)
            {
                data.energy = data.maxEnergy;
                data.lastEnergyRefillUnixTime = now;
                return;
            }

            if (data.lastEnergyRefillUnixTime <= 0)
            {
                data.lastEnergyRefillUnixTime = now;
                return;
            }

            long elapsed = now - data.lastEnergyRefillUnixTime;
            long interval = Mathf.Max(1, energyRefillMinutes) * 60L;
            if (elapsed < interval)
                return;

            int gained = (int)(elapsed / interval);
            data.energy = Mathf.Min(data.maxEnergy, data.energy + gained);

            // Giữ phần dư (giây lẻ) để nhịp hồi không bị trôi theo thời gian.
            data.lastEnergyRefillUnixTime = data.energy >= data.maxEnergy
                ? now
                : data.lastEnergyRefillUnixTime + gained * interval;

            Flush();
            NotifyChanged();
        }

        /// <summary>Thời gian còn lại (giây) tới lần hồi năng lượng kế tiếp.</summary>
        public int SecondsUntilNextEnergy()
        {
            EnsureLoaded();

            if (data.energy >= data.maxEnergy)
                return 0;

            long now = UnixNow;
            long interval = Mathf.Max(1, energyRefillMinutes) * 60L;
            long elapsed = now - data.lastEnergyRefillUnixTime;
            long remaining = interval - (elapsed % interval);
            return (int)Mathf.Max(0, remaining);
        }

        /// <summary>Kiểm tra và trừ năng lượng khi vào trận.</summary>
        public bool TryConsumeEnergy(int amount = -1)
        {
            EnsureLoaded();
            ApplyEnergyRefill();

            int cost = amount >= 0 ? amount : energyCostPerRun;
            if (data.energy < cost)
                return false;

            bool wasFull = data.energy >= data.maxEnergy;
            data.energy -= cost;

            // Vừa rời trạng thái đầy -> bắt đầu tính nhịp hồi từ bây giờ.
            if (wasFull)
                data.lastEnergyRefillUnixTime = UnixNow;

            Flush();
            NotifyChanged();
            return true;
        }

        public void AddEnergy(int amount)
        {
            if (amount <= 0)
                return;

            EnsureLoaded();
            int before = data.energy;
            data.energy = Mathf.Min(data.maxEnergy, data.energy + amount);

            if (before >= data.maxEnergy)
                data.lastEnergyRefillUnixTime = UnixNow;

            Flush();
            NotifyChanged();
        }

        // =====================================================================
        // TIỀN TỆ
        // =====================================================================
        public int Coins => Data.coins;
        public int Gems => Data.gems;

        public const int WelcomeGiftCoins = 100;
        // Quà ngày 1 của lịch 7 ngày (xem DailyLoginRewards).
        public const int DailyLoginCoins = 300;
        public const int DailyLoginGems = 5;

        public bool IsDailyLoginRewardClaimed => HasDailyLoginClaim(Data, UtcNow);

        /// <summary>
        /// Ngày (0–6) trong lịch 7 ngày ứng với hôm nay: ngày đang chờ nhận, hoặc ngày vừa nhận
        /// nếu hôm nay đã nhận. Nhận liên tục thì tiến lên; bỏ lỡ một ngày hoặc xong ngày 7 thì về 0.
        /// </summary>
        public int DailyLoginDayIndex => GetDailyLoginDayIndex(Data, UtcNow);

        /// <summary>Số ô trong lịch 7 ngày đã nhận (tính cả hôm nay nếu đã nhận).</summary>
        public int DailyLoginDaysClaimed
        {
            get
            {
                DateTime now = UtcNow;
                int index = GetDailyLoginDayIndex(Data, now);
                return HasDailyLoginClaim(Data, now) ? index + 1 : index;
            }
        }

        /// <summary>Phần thưởng đăng nhập của hôm nay theo lịch 7 ngày.</summary>
        public DailyLoginRewards.Day TodayDailyLoginReward => DailyLoginRewards.Days[DailyLoginDayIndex];

        public bool TryClaimDailyLoginReward()
        {
            SaveData save = Data;
            DateTime now = UtcNow;
            int index = GetDailyLoginDayIndex(save, now);
            DailyLoginRewards.Day reward = DailyLoginRewards.Days[index];
            if (HasDailyLoginClaim(save, now) || save.coins > int.MaxValue - reward.Coins ||
                save.gems > int.MaxValue - reward.Gems)
                return false;

            int previousDay = save.dailyLoginClaimDay;
            int previousStreak = save.dailyLoginStreak;
            save.dailyLoginClaimDay = GetDailyLoginDay(now);
            save.dailyLoginStreak = index + 1;
            save.coins += reward.Coins;
            save.gems += reward.Gems;
            if (!TryFlush())
            {
                save.dailyLoginClaimDay = previousDay;
                save.dailyLoginStreak = previousStreak;
                save.coins -= reward.Coins;
                save.gems -= reward.Gems;
                return false;
            }

            NotifyChanged();
            return true;
        }

        private static int GetDailyLoginDayIndex(SaveData save, DateTime utc)
        {
            int last = DailyLoginRewards.Count - 1;
            if (HasDailyLoginClaim(save, utc))
                return Mathf.Clamp(save.dailyLoginStreak - 1, 0, last);

            if (save.dailyLoginClaimDay != GetDailyLoginDay(utc.Date.AddDays(-1)))
                return 0;

            // Save cũ (trước lịch 7 ngày) không có streak nhưng đã nhận hôm qua -> coi là đã xong ngày 1.
            int streak = Mathf.Max(1, save.dailyLoginStreak);
            return streak <= last ? streak : 0;
        }

        private static bool HasDailyLoginClaim(SaveData save, DateTime utc)
        {
            if (save.dailyLoginClaimDay == GetDailyLoginDay(utc))
                return true;

            // The previous HomeScene stored Login today as the first tab's sixth row.
            string date = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return PlayerPrefs.GetInt(PrefKey("GalaxyStrike.TaskPopup.Claimed.v1." + date + ".0.5"), 0) == 1;
        }

        private static int GetDailyLoginDay(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

        public bool IsBattlePassTierClaimed(int index)
        {
            return index >= 0 && index < BattlePassRewards.Tiers.Count &&
                   (Data.battlePassClaimedTiers & (1 << index)) != 0;
        }

        public bool TryClaimBattlePassTier(int index)
        {
            if (index < 0 || index >= BattlePassRewards.Tiers.Count || IsBattlePassTierClaimed(index))
                return false;

            BattlePassRewards.Tier tier = BattlePassRewards.Tiers[index];
            SaveData save = Data;
            if (save.experience < tier.RequiredExperience || save.coins > int.MaxValue - tier.Coins ||
                save.gems > int.MaxValue - tier.Gems)
                return false;

            int claim = 1 << index;
            int previousEnergy = save.energy;
            long previousRefill = save.lastEnergyRefillUnixTime;
            save.battlePassClaimedTiers |= claim;
            save.coins += tier.Coins;
            save.gems += tier.Gems;
            if (tier.Energy > 0)
            {
                // Giống AddEnergy: không vượt quá năng lượng tối đa.
                if (save.energy >= save.maxEnergy)
                    save.lastEnergyRefillUnixTime = UnixNow;
                save.energy = Mathf.Min(save.maxEnergy, save.energy + tier.Energy);
            }

            if (!TryFlush())
            {
                save.battlePassClaimedTiers &= ~claim;
                save.coins -= tier.Coins;
                save.gems -= tier.Gems;
                save.energy = previousEnergy;
                save.lastEnergyRefillUnixTime = previousRefill;
                return false;
            }

            NotifyChanged();
            return true;
        }

        public bool TryClaimWelcomeGift()
        {
            SaveData save = Data;
            if (save.welcomeGiftClaimed || save.coins > int.MaxValue - WelcomeGiftCoins)
                return false;

            save.welcomeGiftClaimed = true;
            save.coins += WelcomeGiftCoins;
            if (!TryFlush())
            {
                save.welcomeGiftClaimed = false;
                save.coins -= WelcomeGiftCoins;
                return false;
            }

            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Cộng/trừ Coin và lưu ngay. Giá trị không bao giờ xuống dưới 0.
        /// Truyền số âm để trừ tiền (dùng khi chắc chắn đã kiểm tra đủ tiền).
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount == 0)
                return;

            EnsureLoaded();
            data.coins = Mathf.Max(0, data.coins + amount);
            Flush();
            NotifyChanged();
        }

        /// <summary>
        /// Cộng/trừ Gem và lưu ngay. Giá trị không bao giờ xuống dưới 0.
        /// </summary>
        public void AddGems(int amount)
        {
            if (amount == 0)
                return;

            EnsureLoaded();
            data.gems = Mathf.Max(0, data.gems + amount);
            Flush();
            NotifyChanged();
        }

        /// <summary>Số transaction IAP tối đa được nhớ (đủ phủ các đơn Store có thể gửi lại).</summary>
        private const int MaxRememberedIapTransactions = 200;

        /// <summary>Giao dịch IAP này đã được cộng thưởng chưa.</summary>
        public bool IsIapTransactionProcessed(string transactionId)
        {
            return !string.IsNullOrEmpty(transactionId) &&
                   Data.processedIapTransactions.Contains(transactionId);
        }

        /// <summary>
        /// Cộng thưởng của một giao dịch IAP và ghi nhận transaction trong CÙNG một lần lưu.
        /// Store có thể gửi lại cùng một đơn (app tắt trước khi xác nhận) -> lần sau trả false,
        /// không cộng thêm. Trả true khi vừa cộng thưởng.
        /// </summary>
        public bool TryApplyIapPurchase(string transactionId, int coins, int gems, int energy)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(transactionId) || data.processedIapTransactions.Contains(transactionId))
                return false;

            data.processedIapTransactions.Add(transactionId);
            int overflow = data.processedIapTransactions.Count - MaxRememberedIapTransactions;
            if (overflow > 0)
                data.processedIapTransactions.RemoveRange(0, overflow);

            if (coins > 0)
                data.coins += coins;
            if (gems > 0)
                data.gems += gems;
            if (energy > 0)
            {
                if (data.energy >= data.maxEnergy)
                    data.lastEnergyRefillUnixTime = UnixNow;
                data.energy = Mathf.Min(data.maxEnergy, data.energy + energy);
            }

            Flush();
            NotifyChanged();
            return true;
        }

        /// <summary>Trừ tiền nếu đủ. Trả false khi không đủ số dư (không trừ gì).</summary>
        public bool TrySpendCurrency(CurrencyType currency, int amount)
        {
            if (amount <= 0)
                return true;

            EnsureLoaded();

            if (currency == CurrencyType.Gem)
            {
                if (data.gems < amount)
                    return false;

                data.gems -= amount;
            }
            else
            {
                if (data.coins < amount)
                    return false;

                data.coins -= amount;
            }

            Flush();
            NotifyChanged();
            return true;
        }

        // =====================================================================
        // TÀU
        // =====================================================================
        public int SelectedShipIndex => Data.selectedShipIndex;

        /// <summary>
        /// Tàu index 0 luôn miễn phí/mặc định; các tàu khác phải có trong danh sách đã mở khóa.
        /// </summary>
        public bool IsShipUnlocked(int shipIndex)
        {
            return shipIndex == 0 || Data.HasShip(shipIndex);
        }

        /// <summary>
        /// Mở khóa tàu (không trừ tiền) rồi lưu ngay.
        /// Thường dùng khi nhận tàu từ IAP hoặc phần thưởng.
        /// </summary>
        public void UnlockShip(int shipIndex)
        {
            EnsureLoaded();
            data.UnlockShip(shipIndex);
            Flush();
            NotifyChanged();
        }

        /// <summary>
        /// Mua tàu: kiểm tra số dư -> trừ tiền -> mở khóa. Không làm gì nếu đã sở hữu.
        /// </summary>
        public bool TryBuyShip(int shipIndex, CurrencyType currency, int price)
        {
            EnsureLoaded();
            if (IsShipUnlocked(shipIndex))
                return false;

            if (!TrySpendCurrency(currency, price))
                return false;

            data.UnlockShip(shipIndex);
            data.selectedShipIndex = shipIndex;
            Flush();
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Chọn tàu để sử dụng. Bỏ qua nếu tàu chưa được mở khóa.
        /// </summary>
        public void SelectShip(int shipIndex)
        {
            EnsureLoaded();
            if (!IsShipUnlocked(shipIndex))
                return;

            data.selectedShipIndex = shipIndex;
            Flush();
            NotifyChanged();
        }

        // =====================================================================
        // NÂNG CẤP CHỈ SỐ TÀU
        // =====================================================================
        public int GetUpgradeLevel(ShipUpgradeType type) => Data.GetUpgradeLevel(type);

        /// <summary>
        /// Giá Coin của lần nâng cấp kế tiếp (tra bảng giá trong ShipUpgradeCatalog).
        /// </summary>
        public int GetUpgradeCost(ShipUpgradeType type)
        {
            return ShipUpgradeCatalog.GetCost(type, GetUpgradeLevel(type));
        }

        /// <summary>Chỉ số nâng cấp đã đạt cấp tối đa (MaxLevel) hay chưa.</summary>
        public bool IsUpgradeMaxed(ShipUpgradeType type)
        {
            return GetUpgradeLevel(type) >= ShipUpgradeCatalog.MaxLevel;
        }

        /// <summary>Nâng cấp 1 bậc bằng Coin. Trả false nếu đã tối đa hoặc thiếu Coin.</summary>
        public bool TryUpgrade(ShipUpgradeType type)
        {
            EnsureLoaded();

            if (IsUpgradeMaxed(type))
                return false;

            int cost = GetUpgradeCost(type);
            if (!TrySpendCurrency(CurrencyType.Gold, cost))
                return false;

            data.SetUpgradeLevel(type, GetUpgradeLevel(type) + 1);
            Flush();
            NotifyChanged();
            return true;
        }

        // =====================================================================
        // NHIỆM VỤ (Daily / Weekly / Achievements)
        // =====================================================================

        private static int[] ResizeStats(int[] stats)
        {
            if (stats != null && stats.Length == TaskCatalog.MetricCount)
                return stats;

            var resized = new int[TaskCatalog.MetricCount];
            if (stats != null)
                Array.Copy(stats, resized, Mathf.Min(stats.Length, resized.Length));
            return resized;
        }

        /// <summary>Ngày thứ Hai (UTC, yyyyMMdd) mở đầu tuần chứa <paramref name="utc"/>.</summary>
        private static int GetTaskWeek(DateTime utc)
        {
            int daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
            return GetDailyLoginDay(utc.Date.AddDays(-daysSinceMonday));
        }

        /// <summary>Sang ngày/tuần mới thì xoá bộ đếm và trạng thái đã nhận của chu kỳ cũ.</summary>
        private void RollTaskPeriods()
        {
            DateTime now = UtcNow;

            int day = GetDailyLoginDay(now);
            if (data.taskDay != day)
            {
                data.taskDay = day;
                Array.Clear(data.dailyTaskStats, 0, data.dailyTaskStats.Length);
                data.claimedDailyTasks.Clear();
            }

            int week = GetTaskWeek(now);
            if (data.taskWeek != week)
            {
                data.taskWeek = week;
                Array.Clear(data.weeklyTaskStats, 0, data.weeklyTaskStats.Length);
                data.claimedWeeklyTasks.Clear();
            }
        }

        private static void AddStat(int[] stats, TaskMetric metric, int amount)
        {
            int index = (int)metric;
            if (index >= 0 && index < stats.Length)
                stats[index] = (int)Math.Min(int.MaxValue, (long)stats[index] + amount);
        }

        /// <summary>
        /// Cộng tiến độ nhiệm vụ cho ngày, tuần và thành tựu. KHÔNG ghi đĩa ngay
        /// (gọi rất nhiều lần trong trận); GameManager gọi <see cref="FlushProgress"/> cuối trận.
        /// </summary>
        public void RecordTaskMetric(TaskMetric metric, int amount = 1)
        {
            if (amount <= 0 || metric == TaskMetric.HighestStage)
                return;

            EnsureLoaded();
            RollTaskPeriods();
            AddStat(data.dailyTaskStats, metric, amount);
            AddStat(data.weeklyTaskStats, metric, amount);
            AddStat(data.lifetimeTaskStats, metric, amount);
        }

        /// <summary>Tính một ngày đăng nhập nếu hôm nay (UTC) chưa được tính.</summary>
        public void RecordLoginIfNewDay()
        {
            EnsureLoaded();
            int today = GetDailyLoginDay(UtcNow);
            if (data.lastLoginDay == today)
                return;

            data.lastLoginDay = today;
            RecordTaskMetric(TaskMetric.LoginDays);
            Flush();
        }

        /// <summary>Tiến độ hiện tại của nhiệm vụ trong chu kỳ của nó.</summary>
        public int GetTaskProgress(TaskDefinition task)
        {
            if (task == null)
                return 0;

            EnsureLoaded();
            RollTaskPeriods();

            if (task.metric == TaskMetric.HighestStage)
                return data.highestLevel;

            int index = (int)task.metric;
            switch (task.period)
            {
                case TaskPeriod.Daily:
                    return data.dailyTaskStats[index];
                case TaskPeriod.Weekly:
                    return data.weeklyTaskStats[index];
                default:
                    // Tổng quái hạ đã được đếm riêng từ trước khi có nhiệm vụ.
                    return task.metric == TaskMetric.EnemiesDestroyed
                        ? Mathf.Max(data.totalKills, data.lifetimeTaskStats[index])
                        : data.lifetimeTaskStats[index];
            }
        }

        public bool IsTaskComplete(TaskDefinition task) => task != null && GetTaskProgress(task) >= task.target;

        public bool IsTaskClaimed(TaskDefinition task)
        {
            if (task == null)
                return false;
            if (task.isDailyLoginReward)
                return IsDailyLoginRewardClaimed;

            EnsureLoaded();
            RollTaskPeriods();
            return ClaimedListFor(task.period).Contains(task.id);
        }

        /// <summary>Nhận thưởng nhiệm vụ đã hoàn thành. Trả false nếu chưa xong hoặc đã nhận.</summary>
        public bool TryClaimTask(TaskDefinition task)
        {
            if (task == null)
                return false;
            if (task.isDailyLoginReward)
                return TryClaimDailyLoginReward();

            if (IsTaskClaimed(task) || !IsTaskComplete(task))
                return false;

            ClaimedListFor(task.period).Add(task.id);
            ApplyTaskReward(task.rewardOne);
            ApplyTaskReward(task.rewardTwo);
            Flush();
            NotifyChanged();
            return true;
        }

        /// <summary>Số nhiệm vụ đã xong nhưng chưa nhận (để hiện chấm đỏ trên nút TASK nếu cần).</summary>
        public int CountClaimableTasks()
        {
            int count = 0;
            for (int tab = 0; tab < 3; tab++)
            {
                foreach (TaskDefinition task in TaskCatalog.ForTab(tab))
                {
                    if (IsTaskComplete(task) && !IsTaskClaimed(task))
                        count++;
                }
            }
            return count;
        }

        private System.Collections.Generic.List<string> ClaimedListFor(TaskPeriod period)
        {
            switch (period)
            {
                case TaskPeriod.Daily: return data.claimedDailyTasks;
                case TaskPeriod.Weekly: return data.claimedWeeklyTasks;
                default: return data.claimedAchievements;
            }
        }

        private void ApplyTaskReward(TaskReward reward)
        {
            if (reward.amount <= 0)
                return;

            switch (reward.type)
            {
                case TaskRewardType.Coins:
                    data.coins = (int)Math.Min(int.MaxValue, (long)data.coins + reward.amount);
                    break;
                case TaskRewardType.Gems:
                    data.gems = (int)Math.Min(int.MaxValue, (long)data.gems + reward.amount);
                    break;
                case TaskRewardType.Energy:
                    if (data.energy >= data.maxEnergy)
                        data.lastEnergyRefillUnixTime = UnixNow;
                    data.energy = Mathf.Min(data.maxEnergy, data.energy + reward.amount);
                    break;
            }
        }

        // =====================================================================
        // TIẾN TRÌNH LEVEL
        // =====================================================================
        public int HighestLevel => Data.highestLevel;

        public int CampaignStage => Data.campaignStage;

        /// <summary>
        /// Ghi mốc màn chiến dịch đã mở. Chỉ tăng, không bao giờ giảm: chọn chơi lại
        /// màn cũ trên bản đồ không được làm mất tiến trình.
        /// </summary>
        public void SaveCampaignStage(int stage)
        {
            EnsureLoaded();
            stage = Mathf.Clamp(stage, 1, LevelManager.CampaignLength);
            if (stage <= data.campaignStage)
                return;

            data.campaignStage = stage;
            Flush();
        }

        // =====================================================================
        // THÀNH TÍCH CHO BẢNG XẾP HẠNG
        // =====================================================================
        public int TotalKills => Data.totalKills;
        public int BestScore => Data.bestScore;

        /// <summary>
        /// Cộng dồn số quái đã hạ (GameManager gọi mỗi khi quái chết).
        /// KHÔNG Flush mỗi lần gọi (quái chết liên tục sẽ ghi đĩa liên tục trên mobile);
        /// dữ liệu được ghi khi kết thúc màn qua <see cref="FlushProgress"/>.
        /// </summary>
        public void AddTotalKills(int amount)
        {
            if (amount <= 0)
                return;

            EnsureLoaded();
            data.totalKills += amount;
        }

        /// <summary>Ghi các thay đổi tích luỹ trong màn xuống đĩa (gọi cuối màn).</summary>
        public void FlushProgress()
        {
            Flush();
            NotifyChanged();
        }

        /// <summary>Cập nhật điểm cao nhất nếu vượt kỷ lục cũ.</summary>
        public void ReportScore(int score)
        {
            if (score <= 0)
                return;

            EnsureLoaded();
            if (score <= data.bestScore)
                return;

            data.bestScore = score;
            Flush();
            NotifyChanged();
        }

        /// <summary>
        /// Được GameManager gọi khi người chơi vượt qua một màn mới.
        /// Chỉ cập nhật nếu màn đó cao hơn kỷ lục hiện tại, rồi lưu ngay.
        /// </summary>
        public void ReportLevelCleared(int levelNumber)
        {
            EnsureLoaded();
            if (levelNumber <= data.highestLevel)
                return;

            data.highestLevel = levelNumber;
            Flush();
            NotifyChanged();
        }

        // =====================================================================
        // TRANG BỊ (Weapon / Booster)
        // =====================================================================
        public bool IsItemOwned(string itemId) => Data.HasItem(itemId);

        /// <summary>Mua vật phẩm: trừ tiền rồi ghi nhận sở hữu. Trả false nếu đã có hoặc thiếu tiền.</summary>
        public bool TryBuyItem(string itemId, CurrencyType currency, int price)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(itemId))
                return false;

            if (Data.HasItem(itemId))
                return false;

            if (!TrySpendCurrency(currency, price))
                return false;

            data.UnlockItem(itemId);
            Flush();
            NotifyChanged();
            return true;
        }

        // =====================================================================
        // KINH NGHIỆM & CẤP TÀI KHOẢN
        // =====================================================================

        /// <summary>Tổng XP đã tích luỹ của tài khoản.</summary>
        public int Experience => Data.experience;

        /// <summary>Cấp tài khoản hiện tại (luôn được đồng bộ lại theo XP trong Normalize).</summary>
        public int AccountLevel => Data.accountLevel;

        /// <summary>XP cần để lên cấp tiếp theo (tăng dần theo cấp hiện tại).</summary>
        public int XpForNextLevel => GetTotalXpForLevel(data.accountLevel + 1) - GetTotalXpForLevel(data.accountLevel);

        /// <summary>XP đã tích luỹ trong cấp hiện tại.</summary>
        public int XpInCurrentLevel
        {
            get
            {
                int floor = GetTotalXpForLevel(data.accountLevel);
                return Mathf.Max(0, data.experience - floor);
            }
        }

        /// <summary>Tiến độ 0..1 tới cấp kế tiếp (UI thanh XP).</summary>
        public float AccountLevelProgress
        {
            get
            {
                int need = XpForNextLevel;
                return need > 0 ? Mathf.Clamp01((float)XpInCurrentLevel / need) : 0f;
            }
        }

        /// <summary>Trần XP (ứng với Lv.100) để chống file save bị chỉnh và giới hạn vòng lặp.</summary>
        private const int MaxExperience = 495000;

        /// <summary>Tổng XP cần để đạt một cấp: 100, 250, 450, 700... (100 * n * (n+1) / 2).</summary>
        private static int GetTotalXpForLevel(int level)
        {
            int n = Mathf.Max(0, level - 1);
            return 100 * n * (n + 1) / 2;
        }

        /// <summary>Cộng XP và tự nâng cấp khi vượt ngưỡng. Trả về số cấp đã tăng.</summary>
        public int AddExperience(int amount)
        {
            if (amount <= 0)
                return 0;

            EnsureLoaded();

            int before = data.experience;
            data.experience = Mathf.Min(MaxExperience, data.experience + amount);

            int gained = 0;
            while (data.experience >= GetTotalXpForLevel(data.accountLevel + 1))
            {
                data.accountLevel++;
                gained++;

                // Thưởng lên cấp: thêm năng lượng.
                data.energy = Mathf.Min(data.maxEnergy, data.energy + 3);
            }

            // Không có gì thay đổi (đã chạm trần) -> bỏ qua Flush để tránh ghi đĩa vô ích.
            if (gained == 0 && data.experience == before)
                return 0;

            Flush();
            NotifyChanged();
            return gained;
        }
    }
}
