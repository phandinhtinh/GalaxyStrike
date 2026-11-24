// Galaxy Strike - Google Play Games Leaderboard (plugin GPGS 2.x trong Assets/GooglePlayGames)
//
// Bản cũ bọc API thật trong #if GOOGLE_PLAY_GAMES, nhưng plugin KHÔNG tự định nghĩa symbol này,
// nên bảng xếp hạng luôn chạy OFFLINE. Giờ dùng thẳng API của plugin và chọn chế độ lúc chạy:
//
//  ONLINE  khi: chạy trên Android + đã chạy Window > Google Play Games > Setup > Android Setup
//               (PlayGamesSettings có App ID) + đã điền Leaderboard ID trong
//               Resources/Config/LeaderboardConfig.asset.
//  OFFLINE khi thiếu một trong các điều kiện trên: bảng chỉ hiện thành tích của chính người chơi.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using GalaxyStrike.Save;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

namespace GalaxyStrike.Leaderboard
{
    /// <summary>
    /// Dịch vụ bảng xếp hạng.
    ///
    /// Tự sinh (DontDestroyOnLoad) nên dùng được ở mọi Scene.
    /// Tự submit điểm mỗi khi kết thúc màn (GameManager gọi) và ngay sau khi đăng nhập.
    /// </summary>
    [DefaultExecutionOrder(-130)]
    public sealed class LeaderboardService : SingletonBehaviour<LeaderboardService>
    {
        private const string ConfigResourcePath = "Config/LeaderboardConfig";

        [Header("--- Google Play Games Config ---")]
        [Tooltip("Asset chứa Leaderboard ID. Để trống thì tự nạp Resources/Config/LeaderboardConfig.")]
        [SerializeField] private LeaderboardConfigAsset configAsset;
        [SerializeField] private PlayGamesConfig config = new PlayGamesConfig();

        /// <summary>Phát khi trạng thái đăng nhập thay đổi (true = đã đăng nhập).</summary>
        public event Action<bool> OnSignInChanged;

        /// <summary>Phát khi tải bảng xếp hạng xong.</summary>
        public event Action<LeaderboardType, LeaderboardEntry[], bool> OnLeaderboardLoaded;

        private bool isSignedIn;
        private bool isInitializing;
        private bool isActivated;
        private readonly List<LeaderboardEntry> buffer = new List<LeaderboardEntry>(50);

        /// <summary>Cấu hình đang dùng: ưu tiên asset trong Resources, rồi tới field trên component.</summary>
        public PlayGamesConfig Config =>
            configAsset != null && configAsset.config != null ? configAsset.config : config;

        /// <summary>
        /// Đủ điều kiện dùng Play Games thật: đang chạy trên Android, đã có App ID
        /// (Android Setup) và đã điền Leaderboard ID.
        /// </summary>
        public bool IsOnlineMode =>
            Application.platform == RuntimePlatform.Android &&
            HasPlayGamesAppId() &&
            Config != null && Config.IsConfigured;

        /// <summary>True khi đã đăng nhập Google Play Games và có thể dùng bảng online.</summary>
        public bool IsSignedIn => isSignedIn;

        protected override void OnSingletonAwake()
        {
            if (configAsset == null)
                configAsset = Resources.Load<LeaderboardConfigAsset>(ConfigResourcePath);
        }

        /// <summary>Tự kích hoạt Play Games khi service xuất hiện.</summary>
        private void Start()
        {
            InitializePlayGames();
        }

        private static bool HasPlayGamesAppId()
        {
            PlayGamesSettings settings = PlayGamesSettings.LoadInstance();
            return settings != null && !string.IsNullOrEmpty(settings.AppId);
        }

        // =====================================================================
        // KHỞI TẠO
        // =====================================================================

        /// <summary>
        /// Kích hoạt Play Games và (nếu bật autoSignIn) tự đăng nhập.
        /// Chưa đủ cấu hình thì chạy OFFLINE, game vẫn chạy bình thường.
        /// </summary>
        public void InitializePlayGames()
        {
            if (isInitializing || isSignedIn)
                return;

            if (!IsOnlineMode)
            {
                Debug.Log("[Leaderboard] Chế độ OFFLINE: " + DescribeOfflineReason());
                return;
            }

            ActivatePlatform();

            if (Config.autoSignIn)
                Authenticate(manual: false);
        }

        private string DescribeOfflineReason()
        {
            if (Application.platform != RuntimePlatform.Android)
                return "không chạy trên Android.";
            if (!HasPlayGamesAppId())
                return "chưa có App ID (Window > Google Play Games > Setup > Android Setup).";
            return "chưa điền Leaderboard ID trong Resources/Config/LeaderboardConfig.";
        }

        private void ActivatePlatform()
        {
            if (isActivated)
                return;

            PlayGamesPlatform.DebugLogEnabled = Debug.isDebugBuild;
            PlayGamesPlatform.Activate();
            isActivated = true;
        }

        // =====================================================================
        // ĐĂNG NHẬP
        // =====================================================================

        /// <summary>
        /// Đăng nhập thủ công (người chơi bấm nút). Hiện hộp thoại chọn tài khoản của Google.
        /// </summary>
        public void SignIn()
        {
            if (!IsOnlineMode)
            {
                Debug.Log("[Leaderboard] SignIn bị bỏ qua - " + DescribeOfflineReason());
                return;
            }

            ActivatePlatform();
            Authenticate(manual: true);
        }

        private void Authenticate(bool manual)
        {
            if (isInitializing)
                return;

            isInitializing = true;
            Action<SignInStatus> onDone = HandleAuthenticated;
            if (manual)
                PlayGamesPlatform.Instance.ManuallyAuthenticate(onDone);
            else
                PlayGamesPlatform.Instance.Authenticate(onDone);
        }

        private void HandleAuthenticated(SignInStatus status)
        {
            isInitializing = false;
            bool ok = status == SignInStatus.Success;
            isSignedIn = ok;
            OnSignInChanged?.Invoke(ok);

            Debug.Log(ok
                ? "[Leaderboard] Đăng nhập Google Play Games thành công."
                : $"[Leaderboard] Đăng nhập thất bại: {status}");

            // Đồng bộ thành tích đã đạt khi còn offline.
            if (ok)
                SubmitProgress();
        }

        /// <summary>
        /// Ngừng dùng bảng online trong phiên này. Plugin GPGS 2.x không còn API đăng xuất:
        /// người chơi đăng xuất trong ứng dụng Play Games.
        /// </summary>
        public void SignOut()
        {
            isSignedIn = false;
            OnSignInChanged?.Invoke(false);
        }

        /// <summary>
        /// Lấy tên hiển thị: ưu tiên tên tài khoản Google (khi đã đăng nhập),
        /// lỗi hoặc offline thì dùng tên lưu trong save (mặc định "You").
        /// </summary>
        public string GetPlayerName()
        {
            if (isSignedIn && PlayGamesPlatform.Instance != null)
            {
                try
                {
                    string name = PlayGamesPlatform.Instance.GetUserDisplayName();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Leaderboard] Không lấy được tên người chơi: {exception.Message}");
                }
            }

            return SaveManager.Instance != null ? SaveManager.Instance.Data.playerName : "You";
        }

        // =====================================================================
        // GỬI ĐIỂM
        // =====================================================================

        /// <summary>Gửi điểm cục bộ hiện tại của một bảng.</summary>
        public void SubmitAll(LeaderboardType type)
        {
            Submit(type, GetLocalScore(type));
        }

        /// <summary>
        /// Gửi điểm lên một bảng cụ thể (HighScore / HighestLevel / TotalKills).
        /// Bỏ qua nếu điểm ≤ 0, chưa đăng nhập, hoặc thiếu Leaderboard ID.
        /// </summary>
        public void Submit(LeaderboardType type, long score)
        {
            if (score <= 0 || !isSignedIn || PlayGamesPlatform.Instance == null)
                return;

            string id = Config.GetLeaderboardId(type);
            if (string.IsNullOrEmpty(id))
                return;

            PlayGamesPlatform.Instance.ReportScore(score, id, success =>
            {
                if (!success)
                    Debug.LogWarning($"[Leaderboard] Gửi điểm thất bại ({type}: {score}).");
            });
        }

        /// <summary>Gửi cả 3 bảng cùng lúc (gọi sau khi kết thúc màn và khi vừa đăng nhập).</summary>
        public void SubmitProgress()
        {
            SubmitAll(LeaderboardType.HighScore);
            SubmitAll(LeaderboardType.HighestLevel);
            SubmitAll(LeaderboardType.TotalKills);
        }

        /// <summary>Điểm cục bộ theo loại bảng (dùng cả khi offline).</summary>
        public long GetLocalScore(LeaderboardType type)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
                return 0;

            switch (type)
            {
                case LeaderboardType.HighScore:
                    return save.BestScore;
                case LeaderboardType.HighestLevel:
                    return save.HighestLevel;
                case LeaderboardType.TotalKills:
                    return save.TotalKills;
                default:
                    return 0;
            }
        }

        // =====================================================================
        // TẢI BẢNG
        // =====================================================================

        /// <summary>
        /// Tải top N người chơi. Offline (hoặc tải lỗi) trả về 1 dòng là chính người chơi.
        /// Callback cuối cùng: (entries, isOnline).
        /// </summary>
        public void Fetch(LeaderboardType type, Action<LeaderboardEntry[], bool> onDone)
        {
            string id = Config != null ? Config.GetLeaderboardId(type) : null;
            if (!isSignedIn || PlayGamesPlatform.Instance == null || string.IsNullOrEmpty(id))
            {
                onDone?.Invoke(BuildOfflineEntries(type), false);
                return;
            }

            int count = Mathf.Clamp(Config.maxEntries, 1, 50);

            PlayGamesPlatform.Instance.LoadScores(
                id,
                LeaderboardStart.TopScores,
                count,
                LeaderboardCollection.Public,
                LeaderboardTimeSpan.AllTime,
                data =>
                {
                    if (data == null || !data.Valid)
                    {
                        Debug.LogWarning($"[Leaderboard] Không tải được bảng '{type}' -> hiện thành tích cá nhân.");
                        onDone?.Invoke(BuildOfflineEntries(type), false);
                        return;
                    }

                    IScore[] scores = data.Scores ?? Array.Empty<IScore>();
                    var userIds = new string[scores.Length];
                    for (int i = 0; i < scores.Length; i++)
                        userIds[i] = scores[i].userID;

                    // Điểm chỉ có userID: tải thêm hồ sơ để lấy tên hiển thị.
                    PlayGamesPlatform.Instance.LoadUsers(userIds, profiles =>
                    {
                        var names = new Dictionary<string, string>(scores.Length);
                        if (profiles != null)
                        {
                            foreach (IUserProfile profile in profiles)
                            {
                                if (profile != null && !string.IsNullOrEmpty(profile.id))
                                    names[profile.id] = profile.userName;
                            }
                        }

                        buffer.Clear();
                        string myId = PlayGamesPlatform.Instance.GetUserId();
                        foreach (IScore s in scores)
                        {
                            bool isMe = s.userID == myId;
                            string name = names.TryGetValue(s.userID ?? string.Empty, out string found) && !string.IsNullOrEmpty(found)
                                ? found
                                : isMe ? GetPlayerName() : "Player";
                            buffer.Add(new LeaderboardEntry(s.rank, name, s.value, isMe));
                        }

                        LeaderboardEntry[] entries = buffer.ToArray();
                        onDone?.Invoke(entries, true);
                        OnLeaderboardLoaded?.Invoke(type, entries, true);
                    });
                });
        }

        /// <summary>Bảng offline: chỉ có dòng của người chơi hiện tại.</summary>
        private LeaderboardEntry[] BuildOfflineEntries(LeaderboardType type)
        {
            return new[]
            {
                new LeaderboardEntry(1, GetPlayerName(), GetLocalScore(type), true)
            };
        }
    }
}
