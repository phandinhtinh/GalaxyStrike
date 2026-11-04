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
    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public void AddCoins(int amount)
    {
        coins += amount;

        // Ghi ngay vào save để tiền kiếm được không mất khi thoát giữa chừng.
        SaveManager.Instance.AddCoins(amount);

        UpdateUI();
    }

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

    public void SetTotalEnemiesInLevel(int total)
    {
        totalEnemiesInLevel = total;
        UpdateUI();
    }

    public bool IsLevelCompleted()
    {
        return isLevelWon || enemiesDefeated >= totalEnemiesInLevel;
    }

    // --- KẾT THÚC LEVEL VÀ HIỂN THỊ VICTORY PANEL ---
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


    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "SCORE: " + score;
        if (coinText != null) coinText.text = "COINS: " + coins;
        if (levelProgressText != null) levelProgressText.text = $"ENEMIES: {enemiesDefeated}/{totalEnemiesInLevel}";
    }

    // --- TRẠNG THÁI GAME & CHUYỂN GIAO DIỆN QUA PANELMANAGER ---
    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

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

    public void OpenSettings()
    {
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenSettingsPanel();
        }
    }

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

    public void Replay()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();
        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt("GalaxyStrike_Score", score);
        PlayerPrefs.Save();
        LoadingSceneController.LoadScene("HomeScene");
    }
}
