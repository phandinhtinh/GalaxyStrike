using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalaxyStrike.Data;
using GalaxyStrike.Save;

/// <summary>
/// Một thẻ vật phẩm trong Shop (Giai đoạn 2).
///
/// Luồng trạng thái của thẻ TÀU:
///   LOCKED  -> hiện giá, bấm để MUA (trừ Coin/Gem thật)
///   UNLOCKED-> hiện "CHỌN", bấm để trang bị
///   SELECTED-> hiện "ĐANG DÙNG", nút bị khoá
///
/// Mọi thao tác tiền tệ đều đi qua SaveManager để đảm bảo dữ liệu được mã hóa và lưu ngay.
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    [Header("--- UI Elements ---")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private GalaxyStrike.UI.Shop.ShopCardPresentation presentation;

    [Header("--- State Colours ---")]
    [SerializeField] private Color affordableColor = new Color(0.15f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color unaffordableColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color ownedColor = new Color(0.2f, 0.7f, 1f, 1f);

    private ShipDataSO shipData;
    private ItemDataSO itemData;
    private int shipIndex = -1;

    private void OnEnable()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnDataChanged += RefreshState;

        RefreshState();
    }

    private void OnDisable()
    {
        // FindFirstObjectByType thay vì .Instance: getter sẽ tự tạo lại object khi scene đang đóng.
        SaveManager save = FindFirstObjectByType<SaveManager>();
        if (save != null)
            save.OnDataChanged -= RefreshState;
    }

    // =====================================================================
    // GÁN DỮ LIỆU
    // =====================================================================
    /// <summary>Gán thẻ cho một phi thuyền. <paramref name="index"/> là chỉ số trong mảng ShipData của Shop.</summary>
    public void SetData(ShipDataSO data, int index)
    {
        if (data == null)
            return;

        shipData = data;
        itemData = null;
        shipIndex = index;

        if (iconImage != null) iconImage.sprite = data.shipSprite;
        if (nameText != null) nameText.text = string.IsNullOrEmpty(data.shipName) ? $"SHIP {index + 1}" : data.shipName;
        if (presentation != null) presentation.Configure(data, index);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickShip);
        }

        RefreshState();
    }

    /// <summary>Gán thẻ cho vật phẩm / booster / vũ khí.</summary>
    public void SetData(ItemDataSO data)
    {
        if (data == null)
            return;

        itemData = data;
        shipData = null;
        shipIndex = -1;

        if (iconImage != null) iconImage.sprite = data.itemIcon;
        if (nameText != null) nameText.text = data.itemName;
        if (presentation != null) presentation.Configure(data);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickItem);
        }

        RefreshState();
    }

    // =====================================================================
    // TRẠNG THÁI HIỂN THỊ
    // =====================================================================
    /// <summary>Vẽ lại nút/giá theo dữ liệu save hiện tại. Gọi khi mở panel hoặc sau mỗi giao dịch.</summary>
    public void RefreshState()
    {
        if (buyButton == null)
            return;

        if (shipData != null)
        {
            RefreshShipState();
            return;
        }

        if (itemData != null)
            RefreshItemState();
    }

    private void RefreshShipState()
    {
        SaveManager save = SaveManager.Instance;
        bool owned = save.IsShipUnlocked(shipIndex);
        bool selected = save.SelectedShipIndex == shipIndex;

        // Prefab chỉ có 1 ô Text cho nút (Price_Text). Trạng thái và giá
        // phải gộp vào cùng một chuỗi, không tách 2 lần ghi đè.
        if (owned)
        {
            SetButtonText(presentation != null ? (selected ? "EQUIPPED" : "SELECT") : (selected ? "ĐANG DÙNG" : "CHỌN"));
            if (presentation != null) presentation.ShowCurrency(false);
            SetPriceColor(presentation != null ? new Color32(7, 23, 48, 255) : ownedColor);
            buyButton.interactable = !selected;
            return;
        }

        int price = Mathf.Max(0, shipData.price);
        bool affordable = HasEnough(shipData.currencyType, price);
        string currency = shipData.currencyType == CurrencyType.Gem ? "GEM" : "COIN";

        SetButtonText(presentation != null ? $"{price:N0}" : $"MUA {price:N0} {currency}");
        if (presentation != null) presentation.ShowCurrency(true);
        SetPriceColor(presentation != null ? new Color32(37, 23, 0, 255) : (affordable ? affordableColor : unaffordableColor));
        buyButton.interactable = affordable;
    }

    private void RefreshItemState()
    {
        SaveManager save = SaveManager.Instance;

        // Đã mua rồi: khoá nút, hiện "ĐÃ SỞ HỮU" (trang bị tự áp dụng trong trận).
        if (save.IsItemOwned(itemData.itemID))
        {
            SetButtonText(presentation != null ? "OWNED" : "ĐÃ SỞ HỮU");
            if (presentation != null) presentation.ShowCurrency(false);
            SetPriceColor(presentation != null ? new Color32(7, 23, 48, 255) : ownedColor);
            buyButton.interactable = false;
            return;
        }

        int price = Mathf.Max(0, itemData.price);
        bool affordable = HasEnough(itemData.currencyType, price);
        string currency = itemData.currencyType == CurrencyType.Gem ? "GEM" : "COIN";

        SetButtonText(presentation != null ? $"{price:N0}" : $"MUA {price:N0} {currency}");
        if (presentation != null) presentation.ShowCurrency(true);
        SetPriceColor(presentation != null ? new Color32(37, 23, 0, 255) : (affordable ? affordableColor : unaffordableColor));
        buyButton.interactable = affordable;
    }

    /// <summary>
    /// Ghi chữ lên ô Text của nút.
    /// Prefab chỉ có MỘT ô Text (Price_Text, nằm cạnh Buy_Button chứ không phải con),
    /// nên mọi trạng thái (MUA / CHỌN / ĐANG DÙNG / ĐÃ SỞ HỮU) đều đi qua hàm này.
    /// </summary>
    private void SetButtonText(string label)
    {
        if (buyButton == null)
            return;

        TextMeshProUGUI buttonLabel = buyButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonLabel == null || buttonLabel == nameText)
            buttonLabel = priceText;

        if (buttonLabel != null)
            buttonLabel.text = label;
    }

    private void SetPriceColor(Color color)
    {
        if (priceText != null)
            priceText.color = color;
    }

    private static bool HasEnough(CurrencyType currency, int amount)
    {
        if (amount <= 0)
            return true;

        SaveManager save = SaveManager.Instance;
        return currency == CurrencyType.Gem ? save.Gems >= amount : save.Coins >= amount;
    }

    // =====================================================================
    // XỬ LÝ BẤM
    // =====================================================================
    private void OnClickShip()
    {
        if (shipData == null)
            return;

        SaveManager save = SaveManager.Instance;

        if (save.IsShipUnlocked(shipIndex))
        {
            // Đã sở hữu -> chỉ cần trang bị.
            save.SelectShip(shipIndex);
            PlayClickSound();
            RefreshState();
            return;
        }

        int price = Mathf.Max(0, shipData.price);
        if (!save.TryBuyShip(shipIndex, shipData.currencyType, price))
        {
            Debug.LogWarning($"[Shop] Không đủ tiền mua '{shipData.shipName}' ({price}).");
            RefreshState();
            return;
        }

        Debug.Log($"[Shop] Đã mua '{shipData.shipName}' với {price} {shipData.currencyType}.");
        PlayClickSound();
        RefreshState();
    }

    private void OnClickItem()
    {
        if (itemData == null)
            return;

        SaveManager save = SaveManager.Instance;
        int price = Mathf.Max(0, itemData.price);

        // Mua và GHI NHẬN SỞ HỮU để PlayerEquipment áp dụng trong trận.
        if (!save.TryBuyItem(itemData.itemID, itemData.currencyType, price))
        {
            if (save.IsItemOwned(itemData.itemID))
                Debug.Log($"[Shop] Đã sở hữu '{itemData.itemName}'.");
            else
                Debug.LogWarning($"[Shop] Không đủ tiền mua '{itemData.itemName}' ({price}).");

            RefreshState();
            return;
        }

        Debug.Log($"[Shop] Đã mua '{itemData.itemName}' với {price} {itemData.currencyType}.");
        PlayClickSound();
        RefreshState();
    }

    private static void PlayClickSound()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);
    }
}
