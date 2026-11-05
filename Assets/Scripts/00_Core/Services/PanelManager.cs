using UnityEngine;
using UnityEngine.UI; 

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    /// <summary>
    /// Khởi tạo Singleton cho PanelManager và tắt raycastTarget của ảnh nền HUD
    /// để nền HUD không "nuốt" thao tác kéo/click của người chơi.
    /// </summary>
    private void Awake()
    {
        // Khởi tạo Singleton để các script khác có thể gọi PanelManager dễ dàng
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
            return;
        }

        // Đảm bảo HUD_Panel không bao giờ chặn chuột/cảm ứng kéo tàu
        if (hudPanel != null)
        {
            Image img = hudPanel.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }
    }

    /// <summary>
    /// Khi Scene vừa vào: chỉ bật HUD, ẩn Settings/GameOver/Victory.
    /// </summary>
    private void Start()
    {
        // Mới vào game: Chỉ hiện HUD, ẩn tất cả panel khác
        ShowHUDOnly();
    }

    /// <summary>
    /// Đóng tất cả các bảng và chỉ hiển thị HUD (thanh máu, điểm số) trong lúc chơi.
    /// </summary>
    public void ShowHUDOnly()
    {
        if (hudPanel != null) hudPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    /// <summary>
    /// Mở bảng Settings và tạm dừng game.
    /// </summary>
    public void OpenSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        Time.timeScale = 0f; // Đóng băng game
    }

    /// <summary>
    /// Đóng bảng Settings và tiếp tục game.
    /// </summary>
    public void CloseSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f; // Chạy lại game
    }

    /// <summary>
    /// Hiển thị màn hình Game Over khi Player bị tiêu diệt.
    /// </summary>
    public void ShowGameOverPanel()
    {
        if (hudPanel != null) hudPanel.SetActive(false); // Ẩn HUD đi cho gọn
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f; // Đóng băng game
    }

    /// <summary>
    /// Hiển thị màn hình Chiến thắng khi hoàn thành Level.
    /// </summary>
    public void ShowVictoryPanel()
    {
        if (hudPanel != null) hudPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(true);
        // Lưu ý: Việc dừng thời gian (Time.timeScale = 0f) có thể được xử lý trực tiếp bên trong VictoryPanelUI để đồng bộ với hiệu ứng Animation nếu có.
        Time.timeScale = 0f; 
    }
}