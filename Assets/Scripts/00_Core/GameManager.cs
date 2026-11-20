using UnityEngine;
using UnityEngine.SceneManagement;
using GalaxyStrike.UI.Loading;
using TMPro;
using GalaxyStrike.Save;
using GalaxyStrike.Leaderboard;
using GalaxyStrike.Data;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("HUD Text")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI levelProgressText; // Tùy chọn: hiển thị ENEMIES: 5/16 trên HUD

    [Header("Game Stats & Level Progress")]
    public int score;
    public int coins;

    [Header("Last Level Reward (Victory Panel)")]
    [SerializeField] private int lastRewardCoins;
    [SerializeField] private int lastRewardGems;
    public int enemiesDefeated;
    [Tooltip("Tổng số quái cần tiêu diệt để hoàn thành màn chơi")]
    public int totalEnemiesInLevel = 16;
    [Tooltip("Tổng lượng máu của toàn bộ quái trong màn chơi")]
    public int totalStageHP = 16;
    public int currentStageHP = 16;

    private bool isLevelWon = false;
    private bool isGameOver = false;

    /// <summary>Trận đã kết thúc (thắng hoặc thua).</summary>
    public bool IsRoundOver => isLevelWon || isGameOver;
    public bool IsOverForBooster => IsRoundOver;

    [Header("Revive")]
    [Tooltip("Giá Coin lần hồi sinh đầu tiên trong màn")]
    [SerializeField] private int reviveBaseCost = 900;
    [Tooltip("Mỗi lần hồi sinh tiếp theo trong cùng màn cộng thêm chừng này")]
    [SerializeField] private int reviveCostStep = 500;
    [Tooltip("Số giây bất tử sau khi hồi sinh")]
    [SerializeField] private float reviveShieldSeconds = 3f;

    [Header("VFX (để trống thì dùng hiệu ứng sinh bằng code)")]
    [Tooltip("Prefab vụ nổ khi quái/Boss chết")]
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Prefab tia lửa khi đạn trúng quái / nhặt item")]
    [SerializeField] private GameObject hitSparkPrefab;

    private int reviveCount;

    /// <summary>
    /// Singleton của bộ điều khiển game trong GameScene.
    /// Awake đảm bảo chỉ có một GameManager; nếu đã tồn tại thì object mới tự hủy.
    /// Đồng thời tự gắn HUD_Panel nếu Scene thiếu.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Tự động gắn HUD_Panel nếu trong Scene chưa có
        if (FindObjectOfType<HUD_Panel>() == null)
        {
            gameObject.AddComponent<HUD_Panel>();
        }

        EnsureGameplayFxSystems();
    }

    /// <summary>Cấu hình VFX và tự gắn rung camera nếu Scene chưa đặt sẵn.</summary>
    private void EnsureGameplayFxSystems()
    {
        // Cho phép designer gán prefab VFX trong Inspector; để trống thì dùng hiệu ứng code.
        VfxSpawner.Configure(explosionPrefab, hitSparkPrefab);

        // CameraShake phải nằm trên camera để rung đúng.
        if (FindObjectOfType<CameraShake>() == null)
        {
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraShake>() == null)
                cam.gameObject.AddComponent<CameraShake>();
        }
    }

    /// <summary>
    /// Chạy đầu mỗi màn: bỏ dừng thời gian, nạp lại điểm từ PlayerPrefs,
    /// lấy số Coin thật từ SaveManager rồi cập nhật HUD.
    /// </summary>
    private void Start()
    {
        Time.timeScale = 1f;
        isLevelWon = false;
        isGameOver = false;
        reviveCount = 0;

        // Điểm/vàng lấy từ SaveManager (nguồn dữ liệu duy nhất từ Giai đoạn 2).
        score = PlayerPrefs.GetInt("GalaxyStrike_Score", 0);
        coins = SaveManager.Instance.Coins;

        UpdateUI();
    }

    // --- KHỞI TẠO MÀN CHƠI & THANH HP TỔNG ---

    /// <summary>
    /// Khởi tạo dữ liệu đầu màn: tổng HP, tổng số quái, reset quái đã hạ,
    /// đẩy thanh HP tổng lên HUD và làm mới text điểm.
    /// Tham số null/0 được kẹp tối thiểu là 1 để tránh chia cho 0.
    /// </summary>
    public void InitializeLevel(int totalHP, int totalEnemies)
    {
        totalStageHP = Mathf.Max(1, totalHP);
        currentStageHP = totalStageHP;
        totalEnemiesInLevel = Mathf.Max(1, totalEnemies);
        enemiesDefeated = 0;
        isLevelWon = false;
        isGameOver = false;

        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.SetStageHP(currentStageHP, totalStageHP);
        }

        UpdateUI();
    }

    // Trừ máu khi quái bị bắn trúng

    /// <summary>
    /// Trừ HP tổng của màn khi một con quái bị bắn trúng (không âm dưới 0)
    /// và đồng bộ lại thanh HP trên HUD. Bỏ qua nếu màn đã thắng.
    /// </summary>
    public void OnEnemyDamaged(int damage)
    {
        if (isLevelWon) return;

        currentStageHP = Mathf.Max(0, currentStageHP - damage);

        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.SetStageHP(currentStageHP, totalStageHP);
        }
    }

    // --- QUẢN LÝ ĐIỂM, COIN & QUÁI ---

    /// <summary>
    /// Cộng thêm điểm trong màn hiện tại rồi refresh text điểm trên HUD.
    /// </summary>
    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    /// <summary>
    /// Cộng Coin vào biến local VÀ ghi ngay vào SaveManager (ghi bền ngay lập tức),
    /// sau đó cập nhật HUD.
    /// </summary>
    public void AddCoins(int amount)
    {
        coins += amount;

        // Ghi ngay vào save để tiền kiếm được không mất khi thoát giữa chừng.
        SaveManager.Instance.AddCoins(amount);

        UpdateUI();
    }

    /// <summary>
    /// Đồng bộ số Coin hiển thị theo số dư thật trong SaveManager.
    /// Dùng sau khi Booster (hoặc chỗ khác) tự trừ tiền qua SaveManager.
    /// </summary>
    public void SyncCoinsFromSave()
    {
        coins = SaveManager.Instance.Coins;
        UpdateUI();
    }

    /// <summary>
    /// Gọi mỗi khi hạ được 1 quái: tăng bộ đếm màn, cộng 1 vào tổng kills tích luỹ,
    /// và cập nhật HUD.
    /// Tham số <paramref name="enemy"/> giữ lại để tương thích với chỗ gọi cũ.
    /// </summary>
    public void AddEnemyDefeated(Enemy enemy = null)
    {
        if (IsRoundOver) return;

        enemiesDefeated++;

        // Tổng số quái hạ (tích luỹ mọi màn) — dùng cho bảng xếp hạng.
        SaveManager.Instance.AddTotalKills(1);

        UpdateUI();
    }

    /// <summary>
    /// Ghi đè tổng số quái của màn (thường do LevelManager/Spawner truyền vào)
    /// và cập nhật lại text tiến độ trên HUD.
    /// </summary>
    public void SetTotalEnemiesInLevel(int total)
    {
        totalEnemiesInLevel = total;
        UpdateUI();
    }

    /// <summary>
    /// Trả về trạng thái thắng do EnemySpawner xác nhận sau wave cuối.
    /// </summary>
    public bool IsLevelCompleted()
    {
        return isLevelWon;
    }

    // --- KẾT THÚC LEVEL VÀ HIỂN THỊ VICTORY PANEL ---

    /// <summary>
    /// Chốt thắng màn (chỉ chạy 1 lần nhờ cờ isLevelWon):
    /// set HP tổng về 0, lưu điểm, ghi mốc level đã qua, cấp thưởng,
    /// phát âm thắng rồi mở Victory Panel.
    /// </summary>
    public void OnLevelCleared()
    {
        if (IsRoundOver) return;
        isLevelWon = true;

        currentStageHP = 0;
        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.SetStageHP(0, totalStageHP);
        }

        // Lưu điểm số và tiền tích lũy
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();

        // Ghi nhận mốc level đã chinh phục (SaveManager).
        // Challenge không tính vào tiến trình chiến dịch.
        int clearedLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 1;
        if (LevelManager.Instance == null || !LevelManager.Instance.IsChallengeMode)
        {
            SaveManager.Instance.ReportLevelCleared(clearedLevel);
        }

        // Cộng thưởng hoàn thành màn vào ví thật (trước đây chỉ hiển thị trên panel).
        GrantLevelReward(clearedLevel);

        // Âm thanh ăn mừng (nếu có)
        if (AudioManager.Instance != null && AudioManager.Instance.coinCollectSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollectSFX);
        }

        Victory();
    }

    /// <summary>
    /// Cộng Coin / Gem / XP thưởng của màn vào SaveManager.
    /// <see cref="coins"/> được tăng tại đây để HUD và Victory Panel hiển thị đúng tổng.
    /// </summary>
    private void GrantLevelReward(int levelNumber)
    {
        LevelManager.LevelData levelData = LevelManager.Instance != null
            ? LevelManager.Instance.GetCurrentLevelData()
            : null;

        int rewardCoins = levelData != null ? Mathf.Max(0, levelData.rewardCoins) : 50;
        int rewardGems = levelNumber % 10 == 0 ? 10 : 0;

        if (rewardCoins > 0)
        {
            // AddCoins đã ghi vào SaveManager -> không cộng trực tiếp lần nữa.
            AddCoins(rewardCoins);
        }

        if (rewardGems > 0)
            SaveManager.Instance.AddGems(rewardGems);

        SaveManager.Instance.AddExperience(levelNumber * 20);
        lastRewardCoins = rewardCoins;
        lastRewardGems = rewardGems;

        // Ghi thành tích tích luỹ + điểm cao, rồi gửi lên Play Games.
        SaveManager.Instance.ReportScore(score);
        SaveManager.Instance.FlushProgress();
        LeaderboardService.Instance.SubmitProgress();
    }


    /// <summary>
    /// Cập nhật text điểm / coin / tiến độ quái trên HUD.
    /// Bỏ qua field nào chưa được gán trong Inspector.
    /// </summary>
    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "SCORE: " + score;
        if (coinText != null) coinText.text = "COINS: " + coins;
        if (levelProgressText != null) levelProgressText.text = $"KILLS: {enemiesDefeated}";
    }

    // --- TRẠNG THÁI GAME & CHUYỂN GIAO DIỆN QUA PANELMANAGER ---

    /// <summary>
    /// Tạm dừng game bằng cách đặt Time.timeScale = 0 (mọi Update/animation theo deltaTime đều đứng).
    /// </summary>
    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Chạy lại game sau khi pause: đóng bảng Settings qua PanelManager
    /// (PanelManager tự set timeScale = 1), nếu không có PanelManager thì tự set.
    /// </summary>
    public void ResumeGame()
    {
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.CloseSettingsPanel();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Mở bảng Settings (thường gọi từ nút bấm); PanelManager sẽ tự pause game.
    /// </summary>
    public void OpenSettings()
    {
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenSettingsPanel();
        }
    }

    /// <summary>
    /// Giá Coin của lần hồi sinh kế tiếp (lũy tiến theo số lần đã hồi trong màn).
    /// </summary>
    public int NextReviveCost => reviveBaseCost + reviveCostStep * reviveCount;

    /// <summary>
    /// Còn lượt hồi sinh miễn phí / không? Hiện cho phép tối đa 1 lần mỗi màn
    /// (khớp thiết kế level: "Hồi sinh tại chỗ khi Game Over, tối đa 1 lần/màn").
    /// </summary>
    public bool CanRevive => reviveCount < 1 && !isLevelWon;

    /// <summary>
    /// Người chơi vừa cạn máu. Nếu còn lượt hồi sinh thì mời hồi sinh,
    /// ngược lại chạy luồng Game Over như cũ.
    /// </summary>
    public void OnPlayerDefeated()
    {
        if (isLevelWon || isGameOver) return;

        if (CanRevive && OpenReviveOffer())
            return;

        GameOver();
    }

    /// <summary>
    /// Mở panel hồi sinh. Trả về false nếu không dựng được panel -> caller hiện Game Over.
    /// </summary>
    private bool OpenReviveOffer()
    {
        if (PanelManager.Instance != null && PanelManager.Instance.TryShowReviveOffer(NextReviveCost, reviveShieldSeconds))
        {
            Time.timeScale = 0f;
            return true;
        }

        RevivePanelUI panel = RevivePanelUI.EnsureExists();
        if (panel == null)
            return false;

        if (PanelManager.Instance != null)
            PanelManager.Instance.HideHudForModal();

        // Hiện panel TRƯỚC rồi mới dừng thời gian: nếu panel không hiển thị được
        // thì tránh để game kẹt paused mà người chơi không bấm được gì.
        panel.ShowRevive(NextReviveCost, reviveShieldSeconds);
        Time.timeScale = 0f;
        return true;
    }

    /// <summary>
    /// Người chơi chọn hồi sinh bằng Coin. Trừ tiền nếu đủ, hồi đầy máu + bất tử tạm thời
    /// rồi tiếp tục ván. Trả về true nếu hồi sinh thành công.
    /// </summary>
    public bool TryReviveWithCoins()
    {
        if (!CanRevive) return false;

        // Chỉ hồi sinh khi tàu thực sự đang gục; tránh trừ tiền oan nếu gọi sai lúc.
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.IsDowned)
            return false;

        int cost = NextReviveCost;
        if (!SaveManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost))
            return false;

        // HUD coin hiển thị tổng ví thật sau khi trừ.
        SyncCoinsFromSave();

        return ApplyRevive();
    }

    /// <summary>
    /// Người chơi chọn hồi sinh bằng Rewarded Ad (miễn phí Coin).
    ///
    /// HIỆN CHƯA tích hợp SDK quảng cáo nên hàm TỪ CHỐI (trả false) để không biến
    /// nút quảng cáo thành nút hồi sinh miễn phí vô hạn. Khi tích hợp AdMob/Unity Ads,
    /// chỉ gọi <see cref="ApplyRevive"/> bên trong callback onUserEarnedReward.
    /// </summary>
    public bool TryReviveWithAd()
    {
        if (!CanRevive) return false;

        // TODO(Ads): thay bằng AdsService.ShowRewarded(onReward: () => ApplyRevive()).
        Debug.LogWarning("[Revive] Rewarded Ad chưa tích hợp — từ chối hồi sinh bằng quảng cáo.");
        return false;
    }

    /// <summary>
    /// Thực sự hồi sinh tàu và đóng panel, trả lại quyền điều khiển.
    /// </summary>
    private bool ApplyRevive()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.Revive(reviveShieldSeconds))
            return false;

        reviveCount++;

        if (PanelManager.Instance != null)
            PanelManager.Instance.ShowHUDOnly();

        Time.timeScale = 1f;
        return true;
    }

    /// <summary>
    /// Người chơi chọn bỏ qua hồi sinh: đóng panel rồi đi thẳng tới Game Over.
    /// Việc mở Game Over Panel do <see cref="GameOver"/> tự lo (tránh gọi 2 lần).
    /// </summary>
    public void DeclineRevive()
    {
        if (RevivePanelUI.Instance != null)
            RevivePanelUI.Instance.gameObject.SetActive(false);

        GameOver();
    }

    /// <summary>
    /// Xử lý khi người chơi thua: cộng XP theo số quái đã hạ, lưu điểm,
    /// gửi thành tích lên Leaderboard rồi mở màn hình Game Over.
    /// Chỉ chạy đúng MỘT lần nhờ cờ <see cref="isGameOver"/>.
    /// </summary>
    public void GameOver()
    {
        if (isLevelWon || isGameOver) return;
        isGameOver = true;

        if (PanelManager.Instance != null)
            PanelManager.Instance.ShowGameOverPanel();

        // Nếu đang chờ hồi sinh mà người chơi bỏ qua, tàu vẫn còn trong Scene
        // nhưng đã ở trạng thái gục — dọn nó luôn cho nhất quán với luồng cũ.
        if (PlayerController.Instance != null && PlayerController.Instance.IsDowned)
            Destroy(PlayerController.Instance.gameObject);

        // Coin/XP kiếm được trong màn vẫn thuộc về người chơi.
        // AddCoins đã flush vào save ngay khi giết quái, nhưng XP chỉ cộng khi thắng màn,
        // nên ở đây cấp XP theo số quái đã hạ để không mất công sức.
        if (enemiesDefeated > 0)
            SaveManager.Instance.AddExperience(enemiesDefeated * 2);

        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();

        // Thua vẫn ghi thành tích: quái đã hạ + điểm cao (nếu phá kỷ lục).
        SaveManager.Instance.ReportScore(score);
        SaveManager.Instance.FlushProgress();
        LeaderboardService.Instance?.SubmitProgress();
    }

    /// <summary>
    /// Hiển thị Victory Panel. PanelManager lo việc bật panel/dừng game,
    /// còn VictoryPanelUI được truyền số level, điểm và phần thưởng của màn vừa qua.
    /// </summary>
    public void Victory()
    {
        isLevelWon = true;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ShowVictoryPanel();
        }

        int levelNum = (LevelManager.Instance != null)
            ? LevelManager.Instance.CurrentLevel
            : LevelManager.SavedLevel;

        if (VictoryPanelUI.Instance != null)
        {
            // Truyền PHẦN THƯỞNG của màn, không phải tổng Coin đang có.
            // lastRewardCoins đã được GrantLevelReward() tính trước đó.
            VictoryPanelUI.Instance.ShowVictory(levelNum, score, lastRewardCoins, enemiesDefeated);
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    // --- CHUYỂN SCENE & TIẾN TRÌNH LEVEL ---

    /// <summary>
    /// Sang màn kế tiếp: lưu điểm, xóa cấp đạn (mỗi màn bắt đầu lại từ 1 tia),
    /// tăng CurrentLevel rồi load lại GameScene. Nếu đã hết chiến dịch hoặc đang
    /// ở chế độ Challenge thì quay về HomeScene thay vì chơi lại.
    /// </summary>
    public void NextLevel()
    {
        Time.timeScale = 1f;

        // Lưu điểm số. Cấp đạn KHÔNG lưu sang màn mới: mỗi màn luôn bắt đầu với 1 tia.
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.DeleteKey("GalaxyStrike_BulletLevel");

        LevelManager levels = LevelManager.Instance;
        if (levels.IsChallengeMode || levels.CurrentLevel >= levels.MaxLevels)
        {
            PlayerPrefs.Save();
            LoadingSceneController.LoadScene("HomeScene");
            return;
        }

        levels.AdvanceToNextLevel();
        PlayerPrefs.Save();
        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Chơi lại đúng màn hiện tại: lưu điểm rồi load lại Scene đang hoạt động.
    /// </summary>
    public void Replay()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();
        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Thoát về màn hình chính (HomeScene) sau khi đã lưu điểm.
    /// </summary>
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();
        LoadingSceneController.LoadScene("HomeScene");
    }
}
