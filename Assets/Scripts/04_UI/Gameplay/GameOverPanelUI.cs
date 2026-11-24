using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GalaxyStrike.UI.Loading;

public class GameOverPanelUI : MonoBehaviour
{
    public static GameOverPanelUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button replayButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private RevivePanelUI reviveOffer;

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

        if (replayButton != null)
        {
            replayButton.onClick.AddListener(OnClickReplay);
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnClickMainMenu);
        }
    }

    public bool TryShowReviveOffer(int coinCost, float shieldSeconds)
    {
        if (reviveOffer == null)
            return false;

        gameObject.SetActive(true);
        reviveOffer.ShowRevive(coinCost, shieldSeconds);
        return true;
    }

    /// <summary>
    /// Hiển thị bảng Game Over và dừng thời gian của game lại
    /// </summary>
    public void ShowGameOver()
    {
        if (reviveOffer != null)
            reviveOffer.gameObject.SetActive(false);

        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnClickReplay()
    {
        FinalizeReviveOffer();
        Time.timeScale = 1f;

        // Đi qua GameManager để trừ năng lượng và reset điểm như mọi lượt chơi mới.
        if (GameManager.Instance != null)
            GameManager.Instance.Replay();
        else
            LoadingSceneController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickMainMenu()
    {
        FinalizeReviveOffer();
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

    private void FinalizeReviveOffer()
    {
        if (reviveOffer != null && reviveOffer.gameObject.activeSelf)
            GameManager.Instance?.DeclineRevive();
    }
}
