using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện Thanh HP Tổng ở phía trên màn hình (Stage / Fleet Total HP Bar),
/// hiển thị thanh máu tổng của toàn bộ quái trong màn chơi và chỉ số máu của người chơi.
/// </summary>
public class HUD_Panel : MonoBehaviour
{
    public static HUD_Panel Instance { get; private set; }

    [Header("UI References (Tự động khởi tạo nếu để trống)")]
    [SerializeField] private RectTransform barFillRect;
    [SerializeField] private Image barFillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private List<Image> hpSegments = new List<Image>();

    [Header("Settings & Visuals")]
    [SerializeField] private float barMaxWidth = 470f;
    [SerializeField] private float smoothSpeed = 7f;
    [SerializeField] private Color activeColor = new Color(0f, 217f / 255f, 1f);
    [SerializeField] private Color inactiveColor = new Color(30f / 255f, 42f / 255f, 56f / 255f);

    private float currentFillAmount = 1f;
    private float targetFillAmount = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        // Tự động tìm hoặc sinh giao diện nếu chưa được kéo thả trong Inspector
        EnsureUIExists();
    }

    private void Start()
    {
        UpdateLevelDisplay();
        Transform nameTransform = GameObject.Find("HUD_Panel")?.transform.Find("PlayerProfile_Block/PlayerName");
        if (nameTransform != null && GalaxyStrike.Save.SaveManager.Instance != null)
        {
            var nameLabel = nameTransform.GetComponent<TextMeshProUGUI>();
            if (nameLabel != null)
            {
                string pilotName = GalaxyStrike.Save.SaveManager.Instance.Data.playerName;
                nameLabel.text = string.IsNullOrWhiteSpace(pilotName) ? "ACE PILOT" : pilotName;
                nameLabel.enableAutoSizing = true;
                nameLabel.fontSizeMin = 24f;
                nameLabel.fontSizeMax = 43f;
                nameLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60f);
                nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }

    private void Update()
    {
        if (barFillRect != null)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * smoothSpeed);
            float currentWidth = Mathf.Max(0f, barMaxWidth * currentFillAmount);
            barFillRect.sizeDelta = new Vector2(currentWidth, barFillRect.sizeDelta.y);

            if (barFillImage != null)
            {
                if (currentFillAmount > 0.5f)
                    barFillImage.color = Color.Lerp(new Color(1f, 0.55f, 0f), new Color(1f, 0.25f, 0.15f), (currentFillAmount - 0.5f) * 2f);
                else
                    barFillImage.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.55f, 0f), currentFillAmount * 2f);
            }
        }
    }

    /// <summary>
    /// Cập nhật tỷ lệ thanh HP tổng (từ 0.0 đến 1.0) và chuỗi text hiển thị
    /// </summary>
    public void SetStageHP(int currentHp, int maxHp)
    {
        if (maxHp <= 0) maxHp = 1;
        targetFillAmount = Mathf.Clamp01((float)currentHp / maxHp);

        if (hpText != null)
        {
            int percent = Mathf.CeilToInt(targetFillAmount * 100f);
            hpText.text = $"STAGE HP: {percent}% ({Mathf.Max(0, currentHp)}/{maxHp})";
        }
    }

    public void UpdatePlayerHPBar(float currentHp, float maxHp)
    {
        if (hpSegments == null) return;

        float hpRatio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        int activeSegmentCount = Mathf.CeilToInt(hpRatio * hpSegments.Count);

        for (int i = 0; i < hpSegments.Count; i++)
        {
            if (hpSegments[i] != null)
            {
                hpSegments[i].color = i < activeSegmentCount ? activeColor : inactiveColor;
            }
        }
    }

    public void UpdateLevelDisplay()
    {
        if (levelText == null) return;

        int level = LevelManager.Instance != null
            ? LevelManager.Instance.CurrentLevel
            : LevelManager.SavedLevel;
        levelText.text = $"Lv. {level}";
    }

    // --- TỰ ĐỘNG SINH GIAO DIỆN NẾU TRONG SCENE CHƯA CÓ SẴN ---
    private void EnsureUIExists()
    {
        // Tìm Canvas và HUD_Panel
        Transform hudPanel = null;
        GameObject hudObj = GameObject.Find("HUD_Panel");
        if (hudObj != null)
        {
            hudPanel = hudObj.transform;
        }
        else
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) hudPanel = canvas.transform;
        }

        if (hudPanel == null) return;

        if (hpSegments == null || hpSegments.Count == 0)
        {
            Transform hpBarContainer = hudPanel.Find("PlayerProfile_Block/Bar/HP_Bar_Container");
            if (hpBarContainer != null)
            {
                hpSegments = new List<Image>(hpBarContainer.GetComponentsInChildren<Image>(true));
            }
        }

        Transform levelTransform = hudPanel.Find("PlayerProfile_Block/Level");
        if (levelTransform != null)
        {
            levelText = levelTransform.GetComponent<TextMeshProUGUI>();
        }

        // WaveProgressHUD now displays stage progress; retire the old red fleet HP bar.
        Transform existingContainer = hudPanel.Find("StageHP_Container");
        if (existingContainer != null) existingContainer.gameObject.SetActive(false);
        barFillRect = null;
        barFillImage = null;
        hpText = null;

        // Hide legacy level-clear UI in scenes that already contain it.
        Transform existingBanner = hudPanel.Find("Victory_Banner");
        if (existingBanner != null)
        {
            existingBanner.gameObject.SetActive(false);
        }
    }
}
