using System.Globalization;
using GalaxyStrike.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel mời hồi sinh khi phi thuyền cạn máu.
///
/// Có 2 cách dùng:
///  1. Author trong Scene (gán 3 Button + 2 Text) rồi bật panel như bình thường.
///  2. Không author gì: <see cref="EnsureExists"/> tự dựng panel bằng code
///     (Canvas riêng, nút Coin/Ad/Bỏ qua) để tính năng Revive vẫn chạy được.
///
/// Chỉ hoạt động khi <see cref="GameManager.CanRevive"/> còn lượt.
/// </summary>
public sealed class RevivePanelUI : MonoBehaviour
{
    public static RevivePanelUI Instance { get; private set; }

    [Header("UI References (để trống thì tự dựng)")]
    [SerializeField] private Button reviveCoinButton;
    [SerializeField] private Button reviveAdButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI shieldText;

    private bool hasOffer;

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

        if (reviveCoinButton != null)
            reviveCoinButton.onClick.AddListener(OnClickReviveWithCoins);

        if (reviveAdButton != null)
            reviveAdButton.onClick.AddListener(OnClickReviveWithAd);

        if (declineButton != null)
            declineButton.onClick.AddListener(OnClickDecline);

        if (!hasOffer)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Bảo đảm có một panel hồi sinh dùng được: đã author thì dùng luôn,
    /// chưa có thì dựng bằng code trên Canvas hiện có.
    /// </summary>
    public static RevivePanelUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        return BuildRuntime(canvas);
    }

    /// <summary>
    /// Chuẩn bị panel với giá của lượt hồi sinh kế tiếp. GameManager gọi ngay trước khi hiện.
    /// </summary>
    public void ShowRevive(int coinCost, float shieldSeconds)
    {
        hasOffer = true;

        if (costText != null)
            costText.text = Mathf.Max(0, coinCost).ToString("N0", CultureInfo.InvariantCulture) + " COIN";

        if (shieldText != null)
            shieldText.text = $"Khiên {Mathf.RoundToInt(Mathf.Max(0f, shieldSeconds))} giây";

        // Ẩn nút quảng cáo khi chưa tích hợp SDK (GameManager từ chối hồi sinh bằng Ad)
        // để người chơi không bấm nhầm vào nút không có tác dụng.
        if (reviveAdButton != null)
            reviveAdButton.gameObject.SetActive(AdsAvailable);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// True khi Rewarded Ad đã sẵn sàng. Hiện luôn false vì chưa tích hợp SDK;
    /// đổi thành kiểm tra service khi tích hợp AdMob/Unity Ads.
    /// </summary>
    private const bool AdsAvailable = false;

    private void OnClickReviveWithCoins()
    {
        if (GameManager.Instance != null && GameManager.Instance.TryReviveWithCoins())
        {
            gameObject.SetActive(false);
            return;
        }

        // Không đủ Coin -> giữ panel mở để người chơi chọn quảng cáo hoặc bỏ qua.
        PlayInsufficientFeedback();
    }

    private void OnClickReviveWithAd()
    {
        if (GameManager.Instance != null && GameManager.Instance.TryReviveWithAd())
            gameObject.SetActive(false);
    }

    private void OnClickDecline()
    {
        gameObject.SetActive(false);
        GameManager.Instance?.DeclineRevive();
    }

    /// <summary>Phản hồi nhẹ khi không đủ Coin.</summary>
    private void PlayInsufficientFeedback()
    {
        Haptics.Light();
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX, 0.4f);
    }

    // =====================================================================
    // DỰNG PANEL BẰNG CODE (dùng khi Scene chưa author)
    // =====================================================================
    private static RevivePanelUI BuildRuntime(Canvas baseCanvas)
    {
        // Canvas riêng với sortingOrder cao để panel luôn nằm trên HUD/Booster,
        // tránh trường hợp canvas gốc bị che và người chơi không bấm được.
        var canvasGo = new GameObject("Revive_Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas overlay = canvasGo.GetComponent<Canvas>();
        overlay.renderMode = baseCanvas != null ? baseCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 500;   // trên HUD/Booster/Settings
        if (overlay.renderMode == RenderMode.ScreenSpaceCamera && baseCanvas != null)
            overlay.worldCamera = baseCanvas.worldCamera;

        var rootGo = new GameObject("Revive_Panel_Runtime", typeof(RectTransform));
        rootGo.SetActive(false);
        RectTransform rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.SetParent(canvasGo.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dimmer = rootGo.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform box = CreateRect(rootRect, "Box", new Vector2(0.5f, 0.5f), new Vector2(640f, 560f));
        box.gameObject.AddComponent<PopupFrameFitter>();
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(0.06f, 0.10f, 0.18f, 0.96f);

        CreateLabel(box, "Tiêu đề", "SHIP DESTROYED", 44f, new Vector2(0f, 170f), new Vector2(600f, 60f), Color.white);
        CreateLabel(box, "Mô tả", "Tiếp tục chiến đấu ngay tại Wave hiện tại?", 24f, new Vector2(0f, 110f), new Vector2(600f, 40f), new Color(0.75f, 0.82f, 0.9f, 1f));

        TextMeshProUGUI costText = CreateLabel(box, "Giá", "900", 40f, new Vector2(0f, 40f), new Vector2(600f, 56f), new Color(1f, 0.82f, 0.3f, 1f));
        TextMeshProUGUI shieldText = CreateLabel(box, "Khiên", "3", 22f, new Vector2(0f, -6f), new Vector2(600f, 34f), new Color(0.55f, 0.85f, 1f, 1f));

        Button coinBtn = CreateButton(box, "Nút_Coin", "HỒI SINH (COIN)", new Vector2(0f, -80f), new Color(0.10f, 0.55f, 0.95f, 1f));
        Button adBtn = CreateButton(box, "Nút_Ad", "XEM QUẢNG CÁO", new Vector2(0f, -160f), new Color(0.15f, 0.65f, 0.35f, 1f));
        Button declineBtn = CreateButton(box, "Nút_BỏQua", "BỎ QUA", new Vector2(0f, AdsAvailable ? -240f : -160f), new Color(0.3f, 0.32f, 0.36f, 1f));

        RevivePanelUI panel = rootGo.AddComponent<RevivePanelUI>();
        panel.reviveCoinButton = coinBtn;
        panel.reviveAdButton = adBtn;
        panel.declineButton = declineBtn;
        panel.costText = costText;
        panel.shieldText = shieldText;
        return panel;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = pivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text,
        float fontSize, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), size);
        rect.anchoredPosition = pos;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(RectTransform parent, string name, string text, Vector2 pos, Color color)
    {
        RectTransform rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(460f, 64f));
        rect.anchoredPosition = pos;

        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = color;

        Button button = rect.gameObject.AddComponent<Button>();

        TextMeshProUGUI label = CreateLabel(rect, "Label", text, 26f, Vector2.zero, new Vector2(440f, 60f), Color.white);
        label.raycastTarget = false;

        return button;
    }
}
