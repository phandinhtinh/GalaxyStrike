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

    /// <summary>
    /// Hiển thị bảng Game Over và dừng thời gian của game lại
    /// </summary>
    public void ShowGameOver()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;
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
