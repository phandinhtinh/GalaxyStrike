using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; 
using GalaxyStrike.UI.Loading;
using TMPro;

/// <summary>
/// Quản lý giao diện Pop-up Chiến Thắng (Victory Pop-up) khi hoàn thành màn chơi,
/// cung cấp nút "NEXT LEVEL" để chuyển sang màn kế tiếp, cùng các nút Chơi Lại và Menu.
/// </summary>
public class VictoryPanelUI : MonoBehaviour
{
    public static VictoryPanelUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform dialogCard;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI enemiesText;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupButtons();
    }

    private void SetupButtons()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(OnClickNextLevel);
        }

        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(OnClickReplay);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(OnClickMainMenu);
        }
    }

    /// <summary>
    /// Hiển thị Pop-up Chiến thắng cùng các thông số và nút Next Level
    /// </summary>
    public void ShowVictory(int levelNum, int score, int coins, int enemiesDefeated)
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // Tạm dừng game để người chơi xem kết quả

        LevelManager levels = LevelManager.Instance;
        bool isChallenge = levels.IsChallengeMode;
        bool campaignComplete = !isChallenge && levelNum >= levels.MaxLevels;

        if (titleText != null)
        {
            titleText.text = campaignComplete && !isChallenge ? "★ GALAXY LIBERATED! ★" : "★ VICTORY! ★";
        }

        if (subtitleText != null)
        {
            if (isChallenge)
                subtitleText.text = $"CHALLENGE CLEARED · LV {levelNum}!";
            else if (campaignComplete)
                subtitleText.text = "ALL 50 LEVELS CLEARED!";
            else
                subtitleText.text = $"LEVEL {levelNum} CLEARED!";
        }

        // Màn cuối: không còn màn kế tiếp -> ẩn nút Next Level.
        // Challenge: mỗi lượt là một màn khác -> luôn quay về Home nên cũng ẩn.
        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(!campaignComplete && !isChallenge);
        }

        if (scoreText != null)
        {
            SetSummaryText(scoreText, $"SCORE\n<color=#00E5FF>+{score:N0}</color>");
        }

        if (coinText != null)
        {
            SetSummaryText(coinText, $"COINS\n<color=#FFD700>+{coins:N0}</color>");
        }

        if (enemiesText != null)
        {
            SetSummaryText(enemiesText, $"ENEMIES\n<color=#38BDF8>{enemiesDefeated:N0}</color>");
        }

        // Hiệu ứng Pop-in nảy nhẹ khi xuất hiện
        if (dialogCard != null)
        {
            StartCoroutine(PopInAnimationRoutine());
        }
    }

private static void SetSummaryText(TextMeshProUGUI text, string value)
    {
        text.text = value;
        text.fontSize = 52f;
        text.fontSizeMin = 38f;
        text.fontSizeMax = 52f;
        text.enableAutoSizing = true;
        text.enableWordWrapping = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;

        RectTransform rectTransform = text.rectTransform;
        rectTransform.sizeDelta = new Vector2(260f, 150f);
    }

    private IEnumerator PopInAnimationRoutine()
    {
        dialogCard.localScale = Vector3.one * 0.75f;
        float elapsed = 0f;
        float duration = 0.25f;

        // Dùng unscaledDeltaTime vì Time.timeScale đang bằng 0
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(0.75f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.08f;
            dialogCard.localScale = Vector3.one * scale;
            yield return null;
        }

        dialogCard.localScale = Vector3.one;
    }

    public void OnClickNextLevel()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NextLevel();
            return;
        }

        Time.timeScale = 1f;
        LevelManager levels = LevelManager.Instance;
        if (levels.IsChallengeMode || levels.CurrentLevel >= levels.MaxLevels)
        {
            LoadingSceneController.LoadScene("HomeScene");
            return;
        }

        levels.AdvanceToNextLevel();
        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickReplay()
    {
        Time.timeScale = 1f;

        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickMainMenu()
    {
        Time.timeScale = 1f;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMainMenu();
        }
        else
        {
            LoadingSceneController.LoadScene("HomeScene");
        }
    }
}
