using UnityEngine;
using UnityEngine.SceneManagement;
using GalaxyStrike.UI.Loading;
using TMPro;
using GalaxyStrike.Save;
using GalaxyStrike.Leaderboard;

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
    }

    /// <summary>
    /// Chạy đầu mỗi màn: bỏ dừng thời gian, nạp lại điểm từ PlayerPrefs,
    /// lấy số Coin thật từ SaveManager rồi cập nhật HUD.
    /// </summary>
    private void Start()
    {
        Time.timeScale = 1f;
        isLevelWon = false;

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
    /// Gọi mỗi khi hạ được 1 quái: tăng bộ đếm màn, cộng 1 vào tổng kills tích luỹ,
    /// cập nhật HUD và kích hoạt thắng màn nếu đã hạ đủ số quái.
    /// Tham số <paramref name="enemy"/> giữ lại để tương thích với chỗ gọi cũ.
    /// </summary>
    public void AddEnemyDefeated(Enemy enemy = null)
    {
        if (isLevelWon) return;

        enemiesDefeated++;

        // Tổng số quái hạ (tích luỹ mọi màn) — dùng cho bảng xếp hạng.
        SaveManager.Instance.AddTotalKills(1);

        UpdateUI();

        if (enemiesDefeated >= totalEnemiesInLevel)
        {
            OnLevelCleared();
        }
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
    /// Kiểm tra màn đã hoàn thành chưa: đã thắng hoặc đã hạ đủ số quái yêu cầu.
    /// </summary>
    public bool IsLevelCompleted()
    {
        return isLevelWon || enemiesDefeated >= totalEnemiesInLevel;
    }

    // --- KẾT THÚC LEVEL VÀ HIỂN THỊ VICTORY PANEL ---

    /// <summary>
    /// Chốt thắng màn (chỉ chạy 1 lần nhờ cờ isLevelWon):
    /// set HP tổng về 0, lưu điểm, ghi mốc level đã qua, cấp thưởng,
    /// phát âm thắng rồi mở Victory Panel.
    /// </summary>
    public void OnLevelCleared()
    {
        if (isLevelWon) return;
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
        if (levelProgressText != null) levelProgressText.text = $"ENEMIES: {enemiesDefeated}/{totalEnemiesInLevel}";
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
    /// Xử lý khi người chơi thua: cộng XP theo số quái đã hạ, lưu điểm,
    /// gửi thành tích lên Leaderboard rồi mở màn hình Game Over.
    /// </summary>
    public void GameOver()
    {
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
        LeaderboardService.Instance.SubmitProgress();

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ShowGameOverPanel();
        }
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
            : PlayerPrefs.GetInt("GalaxyStrike_CurrentLevel", 1);

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

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.AdvanceToNextLevel();
        }
        else
        {
            int current = PlayerPrefs.GetInt("GalaxyStrike_CurrentLevel", 1);
            PlayerPrefs.SetInt("GalaxyStrike_CurrentLevel", current + 1);
        }
        PlayerPrefs.Save();

        // Đã hoàn thành màn cuối của chiến dịch -> về Home, không chơi lại màn 50.
        // Challenge: mỗi lượt là một màn khác -> quay về Home để chọn lượt mới.
        if (LevelManager.Instance != null &&
            (LevelManager.Instance.IsChallengeMode || LevelManager.Instance.IsCampaignComplete()))
        {
            LoadingSceneController.LoadScene("HomeScene");
            return;
        }

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
