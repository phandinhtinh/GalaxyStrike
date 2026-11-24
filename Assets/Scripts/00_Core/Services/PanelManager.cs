using UnityEngine;
using UnityEngine.UI; 

public sealed class PanelManager : SingletonBehaviour<PanelManager>
{
    /// <summary>
    /// Panel là object của Scene hiện tại (hudPanel/settingsPanel/gameOverPanel/victoryPanel
    /// đều nằm trong GameScene). KHÔNG giữ qua Scene, nếu không sau khi load lại Scene
    /// các reference sẽ trở thành null và mọi panel ngừng hoạt động.
    /// </summary>
    protected override bool PersistAcrossScenes => false;

    [Header("UI Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    /// <summary>
    /// Khởi tạo Singleton cho PanelManager và tắt raycastTarget của ảnh nền HUD
    /// để nền HUD không "nuốt" thao tác kéo/click của người chơi.
    /// Việc giữ object qua Scene do <see cref="SingletonBehaviour{T}"/> lo.
    /// </summary>
    protected override void OnSingletonAwake()
    {
        // Đảm bảo HUD_Panel không bao giờ chặn chuột/cảm ứng kéo tàu
        if (hudPanel != null)
        {
            Image img = hudPanel.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }
    }

    /// <summary>
    /// Khi Scene vừa vào: chỉ bật HUD, ẩn Settings/GameOver/Victory/Revive.
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

        if (RevivePanelUI.Instance != null)
            RevivePanelUI.Instance.gameObject.SetActive(false);
    }

    /// <summary>Bảng Settings (tạm dừng) đang mở hay không.</summary>
    public bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeSelf;

    /// <summary>Scene có gán bảng Settings không (thiếu thì không được tự pause, kẻo kẹt game).</summary>
    public bool HasSettingsPanel => settingsPanel != null;

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
        HideHudForModal();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (RevivePanelUI.Instance != null) RevivePanelUI.Instance.gameObject.SetActive(false);
        if (gameOverPanel != null)
        {
            if (gameOverPanel.TryGetComponent(out GameOverPanelUI gameOver))
                gameOver.ShowGameOver();
            else
                gameOverPanel.SetActive(true);
        }
        Time.timeScale = 0f; // Đóng băng game
    }

    public bool TryShowReviveOffer(int coinCost, float shieldSeconds)
    {
        if (gameOverPanel == null || !gameOverPanel.TryGetComponent(out GameOverPanelUI gameOver)
            || !gameOver.TryShowReviveOffer(coinCost, shieldSeconds))
            return false;

        HideHudForModal();
        return true;
    }

    /// <summary>
    /// Ẩn HUD khi mở một panel modal (Revive) để tránh chồng lên nhau.
    /// </summary>
    public void HideHudForModal()
    {
        if (hudPanel != null) hudPanel.SetActive(false);
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
