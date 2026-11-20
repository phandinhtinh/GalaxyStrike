using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
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

    [Header("Celebration")]
    [SerializeField] private VictoryCelebration celebration;
    private Coroutine popInRoutine;
    private bool transitionStarted;

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
        BindButton(nextLevelButton, OnClickNextLevel);
        BindButton(replayButton, OnClickReplay);
        BindButton(mainMenuButton, OnClickMainMenu);
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        // Inspector callbacks survive RemoveAllListeners. Reuse an existing active binding.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == (action.Target as Object) &&
                button.onClick.GetPersistentMethodName(i) == action.Method.Name &&
                button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)
                return;
        }
        button.onClick.AddListener(action);
    }

    private bool TryBeginTransition()
    {
        if (transitionStarted) return false;
        transitionStarted = true;
        if (nextLevelButton != null) nextLevelButton.interactable = false;
        if (replayButton != null) replayButton.interactable = false;
        if (mainMenuButton != null) mainMenuButton.interactable = false;
        return true;
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
            titleText.text = campaignComplete ? "GALAXY LIBERATED" : "VICTORY";
        }

        if (subtitleText != null)
        {
            if (isChallenge)
                subtitleText.text = $"CHALLENGE · LEVEL {levelNum:00} CLEARED";
            else if (campaignComplete)
                subtitleText.text = "ALL 50 LEVELS CLEARED!";
            else
                subtitleText.text = $"LEVEL {levelNum:00} CLEARED";
        }

        // Màn cuối: không còn màn kế tiếp -> ẩn nút Next Level.
        // Challenge: mỗi lượt là một màn khác -> luôn quay về Home nên cũng ẩn.
        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(!campaignComplete && !isChallenge);
        }

        if (scoreText != null)
        {
            scoreText.text = $"<size=24>SCORE</size>\n<color=#A9EDFF>{score:N0}</color>";
        }

        if (coinText != null)
        {
            coinText.text = $"<size=24>COINS</size>\n<color=#FFD77A>+{coins:N0}</color>";
        }

        if (enemiesText != null)
        {
            enemiesText.text = $"<size=24>KILLS</size>\n<color=#A9EDFF>{enemiesDefeated:N0}</color>";
        }

        if (celebration != null) celebration.Play();
        if (dialogCard != null)
        {
            if (popInRoutine != null) StopCoroutine(popInRoutine);
            popInRoutine = StartCoroutine(PopInAnimationRoutine());
        }
    }

    private void OnDisable()
    {
        if (popInRoutine != null) StopCoroutine(popInRoutine);
        popInRoutine = null;
    }

    private IEnumerator PopInAnimationRoutine()
    {
        dialogCard.localScale = Vector3.one * 0.94f;
        float elapsed = 0f;
        float duration = 0.25f;

        // Dùng unscaledDeltaTime vì Time.timeScale đang bằng 0
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(0.94f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.025f;
            dialogCard.localScale = Vector3.one * scale;
            yield return null;
        }

        dialogCard.localScale = Vector3.one;
        popInRoutine = null;
    }

    public void OnClickNextLevel()
    {
        if (!TryBeginTransition()) return;

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
        if (!TryBeginTransition()) return;

        Time.timeScale = 1f;

        LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickMainMenu()
    {
        if (!TryBeginTransition()) return;

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
