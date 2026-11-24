using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalaxyStrike.IAP;

namespace GalaxyStrike.UI.Shop
{
    /// <summary>
    /// Thẻ hiển thị một gói mua thật (Gem Pack / Coin Pack) trong Store.
    ///
    /// Không tự tạo Prefab: dựng bằng code để chạy được ngay với Grid có sẵn,
    /// và để Gameplay/UI không phải phụ thuộc vào asset UI mới.
    /// </summary>
    public sealed class IapPackCardUI : MonoBehaviour
    {
        private IapProductDefinition definition;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI packAmount;
        [SerializeField] private TextMeshProUGUI packName;
        [SerializeField] private TextMeshProUGUI packBadge;
        private bool subscribed;
        private IapPurchaseService subscribedService;

        /// <summary>Dựng thẻ và gán gói cần hiển thị.</summary>
        public static IapPackCardUI Create(IapProductDefinition definition, Color accent, IapPackCardUI template = null)
        {
            if (template != null)
            {
                IapPackCardUI result = Instantiate(template);
                result.definition = definition;
                result.name = "IapPack_" + definition.productId;
                if (result.iconImage) result.iconImage.sprite = definition.icon;
                if (result.packAmount) result.packAmount.text = definition.rewardAmount.ToString("N0");
                if (result.packName) result.packName.text = definition.displayName;
                if (result.packBadge) result.packBadge.text = string.IsNullOrEmpty(definition.badge) ? "SUPPLY PACK" : definition.badge;
                if (result.buyButton) result.buyButton.onClick.AddListener(result.OnClickBuy);
                result.Refresh();
                return result;
            }
            var root = new GameObject($"IapPack_{definition.productId}", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 280f);

            var background = root.AddComponent<Image>();
            background.color = new Color(0.09f, 0.12f, 0.22f, 0.95f);

            var card = root.AddComponent<IapPackCardUI>();
            card.definition = definition;
            card.BuildLayout(accent);
            return card;
        }

        private void BuildLayout(Color accent)
        {
            // Khung icon
            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(1f, 1f, 1f, 0.9f);
            iconImage.raycastTarget = false;
            if (definition.icon != null)
                iconImage.sprite = definition.icon;

            RectTransform iconRect = iconImage.rectTransform;
            iconRect.SetParent(transform, false);
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -20f);
            iconRect.sizeDelta = new Vector2(110f, 110f);

            // Số lượng
            TextMeshProUGUI amountText = CreateText(transform, "Amount", $"x{definition.rewardAmount:N0}", 34f, TextAlignmentOptions.Center);
            amountText.fontStyle = FontStyles.Bold;
            amountText.color = accent;
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = new Vector2(0f, 1f);
            amountRect.anchorMax = new Vector2(1f, 1f);
            amountRect.pivot = new Vector2(0.5f, 1f);
            amountRect.anchoredPosition = new Vector2(0f, -140f);
            amountRect.sizeDelta = new Vector2(-20f, 44f);

            // Tên gói
            TextMeshProUGUI nameLabel = CreateText(transform, "Name", definition.displayName, 22f, TextAlignmentOptions.Center);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -188f);
            nameRect.sizeDelta = new Vector2(-20f, 32f);

            // Badge (tuỳ chọn)
            if (!string.IsNullOrEmpty(definition.badge))
            {
                TextMeshProUGUI badge = CreateText(transform, "Badge", definition.badge, 18f, TextAlignmentOptions.Center);
                badge.fontStyle = FontStyles.Bold;
                badge.color = new Color(1f, 0.9f, 0.3f);
                RectTransform badgeRect = badge.rectTransform;
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 1f);
                badgeRect.anchoredPosition = new Vector2(0f, -220f);
                badgeRect.sizeDelta = new Vector2(-20f, 26f);
            }

            // Nút mua + giá
            var buttonObj = new GameObject("BuyButton", typeof(RectTransform));
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.SetParent(transform, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 16f);
            buttonRect.sizeDelta = new Vector2(-24f, 56f);

            var buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = new Color(0.15f, 0.55f, 0.95f);

            buyButton = buttonObj.AddComponent<Button>();
            buyButton.targetGraphic = buttonBg;

            priceText = CreateText(buttonRect, "Price", "...", 24f, TextAlignmentOptions.Center);
            priceText.fontStyle = FontStyles.Bold;

            buyButton.onClick.AddListener(OnClickBuy);
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            // Cache lại service đã đăng ký để OnDisable huỷ đúng instance
            // (không dùng .Instance trong OnDisable vì getter có thể tạo object mới khi scene đóng).
            subscribedService = FindFirstObjectByType<IapPurchaseService>();
            if (subscribedService == null)
                return;

            subscribedService.OnPurchaseFinished += HandlePurchaseFinished;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (subscribedService != null)
                subscribedService.OnPurchaseFinished -= HandlePurchaseFinished;

            subscribedService = null;
            subscribed = false;
        }

        private void HandlePurchaseFinished(string productId, bool success, string message)
        {
            if (definition == null || productId != definition.productId)
                return;

            if (!success)
                Debug.LogWarning($"[IAP] Gói {definition.displayName} thất bại: {message}");

            Refresh();
        }

        /// <summary>Cập nhật giá bản địa hoá; gọi lại khi Store khởi tạo xong.</summary>
        public void Refresh()
        {
            if (definition == null) return;
            IapPurchaseService service = IapPurchaseService.Instance;

            if (service == null)
            {
                if (priceText != null) priceText.text = "N/A";
                if (buyButton != null) buyButton.interactable = false;
                return;
            }

            string localized = service.GetLocalizedPrice(definition.productId);
            bool ready = service.IsReady;

            if (priceText != null)
                priceText.text = !string.IsNullOrEmpty(localized) ? localized
                    : !IapPurchaseService.IsPurchasingPackageInstalled ? (Application.isEditor ? "PREVIEW" : "UNAVAILABLE")
                    : (ready ? "BUY" : "CONNECTING");

            if (buyButton != null)
                buyButton.interactable = ready || Application.isEditor;
        }

        private void OnClickBuy()
        {
            IapPurchaseService service = IapPurchaseService.Instance;
            if (service == null)
            {
                Debug.LogError("[IAP] IapPurchaseService chưa tồn tại.");
                return;
            }

            service.Purchase(definition.productId);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }
    }
}
