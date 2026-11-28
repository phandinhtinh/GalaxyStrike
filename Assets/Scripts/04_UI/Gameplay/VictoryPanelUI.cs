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
    private CoinFlyEffect coinFly;
    private VictoryStars stars;

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

        PlayStars();
        PlayCoinFly(coins);
    }

    /// <summary>Đánh giá 1–3 sao theo lượng giáp còn lại khi qua màn (hồi sinh -> 1 sao).</summary>
    private void PlayStars()
    {
        PlayerController player = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        int earned = player != null
            ? VictoryStars.Rate(player.CurrentHealth, player.MaxHealth, gm != null && gm.UsedRevive)
            : 1;

        if (stars == null)
            stars = BuildStars();
        if (stars != null)
            stars.Play(earned);
    }

    /// <summary>
    /// Dựng hàng sao giữa dòng "LEVEL .. CLEARED" và emblem; emblem được thu nhỏ/hạ xuống
    /// một chút để có chỗ, không chạm vào ba ô kết quả bên dưới.
    /// </summary>
    private VictoryStars BuildStars()
    {
        RectTransform content = subtitleText != null ? subtitleText.rectTransform.parent as RectTransform : null;
        if (content == null)
            return null;

        Transform emblem = content.Find("VictoryEmblem");
        if (emblem is RectTransform emblemRect)
        {
            emblemRect.sizeDelta = new Vector2(400f, 200f);
            emblemRect.anchoredPosition = new Vector2(emblemRect.anchoredPosition.x, 168f);
        }

        Vector2 subtitlePos = subtitleText.rectTransform.anchoredPosition;
        return VictoryStars.Create(content, new Vector2(subtitlePos.x, subtitlePos.y - 92f));
    }

    /// <summary>
    /// Coin thưởng văng ra từ ô COINS rồi bay về biểu tượng vàng trên HUD, số Coin tăng dần.
    /// Phần thưởng đã được cộng vào ví trước khi panel mở; đây chỉ là hiệu ứng hiển thị.
    /// </summary>
    private void PlayCoinFly(int rewardCoins)
    {
        GameManager gm = GameManager.Instance;
        RectTransform hudCoinBar = gm != null ? gm.HudCoinBar : null;
        if (rewardCoins <= 0 || hudCoinBar == null || coinText == null)
            return;

        if (coinFly == null && !TryGetComponent(out coinFly))
            coinFly = gameObject.AddComponent<CoinFlyEffect>();

        RectTransform source = coinText.transform.parent as RectTransform ?? coinText.rectTransform;
        int total = GalaxyStrike.Save.SaveManager.Instance != null
            ? GalaxyStrike.Save.SaveManager.Instance.Coins
            : gm.coins;
        coinFly.Play(hudCoinBar, source, (RectTransform)transform, total, rewardCoins);
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

        // Đi qua GameManager để trừ năng lượng và reset điểm như mọi lượt chơi mới.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Replay();
            return;
        }

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
