using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Shop
{
    /// <summary>
    /// Cuộn Main_ScrollView tới đúng nhóm hàng khi người chơi bấm nút ở Category_Sidebar.
    ///
    /// Cách hoạt động:
    ///  - Mỗi Tab_* được gán một Transform "điểm đến" (Header của nhóm đó).
    ///  - Khi bấm, script tính vị trí của điểm đến trong Content rồi set ScrollRect.verticalNormalizedPosition
    ///    kèm hiệu ứng cuộn mượt (dùng unscaledDeltaTime để vẫn chạy khi Time.timeScale = 0).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopCategoryScroller : MonoBehaviour
    {
        [Header("--- Scroll ---")]
        [Tooltip("ScrollRect của Main_ScrollView")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Transform 'Content' chứa các Header và Grid")]
        [SerializeField] private RectTransform content;

        [Tooltip("Thời gian cuộn mượt (giây). 0 = nhảy tức thì")]
        [SerializeField] private float scrollDuration = 0.35f;

        [Tooltip("Khoảng đệm phía trên điểm đến để Header không sát mép")]
        [SerializeField] private float topPadding = 8f;

        [Header("--- Category Buttons ---")]
        [SerializeField] private Button shipsButton;
        [SerializeField] private Button weaponsButton;
        [SerializeField] private Button itemsButton;
        [SerializeField] private Button gemsButton;
        [SerializeField] private Button coinsButton;

        [Header("--- Category Anchors (điểm cuộn tới) ---")]
        [SerializeField] private RectTransform shipsTarget;
        [SerializeField] private RectTransform weaponsTarget;
        [SerializeField] private RectTransform itemsTarget;
        [SerializeField] private RectTransform gemsTarget;
        [SerializeField] private RectTransform coinsTarget;

        private Coroutine scrollRoutine;

        /// <summary>
        /// Gán nút và điểm đến theo tên object trong Scene.
        /// Gọi bởi <see cref="ShopManager"/> sau khi dựng xong các section.
        /// </summary>
        public void Bind(
            ScrollRect scroll,
            RectTransform contentRoot,
            Button ships, RectTransform shipsDest,
            Button weapons, RectTransform weaponsDest,
            Button items, RectTransform itemsDest,
            Button gems, RectTransform gemsDest,
            Button coins, RectTransform coinsDest)
        {
            scrollRect = scroll != null ? scroll : scrollRect;
            content = contentRoot != null ? contentRoot : content;

            shipsButton = ships;
            weaponsButton = weapons;
            itemsButton = items;
            gemsButton = gems;
            coinsButton = coins;

            shipsTarget = shipsDest;
            weaponsTarget = weaponsDest;
            itemsTarget = itemsDest;
            gemsTarget = gemsDest;
            coinsTarget = coinsDest;

            if (scrollRect != null)
                scrollRect.content = content;

            Register(shipsButton, () => ScrollTo(shipsTarget));
            Register(weaponsButton, () => ScrollTo(weaponsTarget));
            Register(itemsButton, () => ScrollTo(itemsTarget));
            Register(gemsButton, () => ScrollTo(gemsTarget));
            Register(coinsButton, () => ScrollTo(coinsTarget));
        }

        /// <summary>Gán lại điểm đến sau khi Shop dựng lại section (gọi lại từ ShopManager).</summary>
        public void UpdateTargets(
            RectTransform shipsDest,
            RectTransform weaponsDest,
            RectTransform itemsDest,
            RectTransform gemsDest,
            RectTransform coinsDest)
        {
            shipsTarget = shipsDest;
            weaponsTarget = weaponsDest;
            itemsTarget = itemsDest;
            gemsTarget = gemsDest;
            coinsTarget = coinsDest;
        }

        private void Awake()
        {
            // AddComponent chạy Awake NGAY, trước khi ShopManager kịp gọi Bind.
            // Chỉ tự dò khi chưa được gán thủ công, và không ghi đè tham chiếu đã có.
            if (scrollRect == null)
            {
                scrollRect = GetComponentInParent<ScrollRect>();

                // Main_ScrollView là Viewport con của ScrollRect -> tìm ngược lên từ Content.
                if (scrollRect == null && content != null)
                    scrollRect = content.GetComponentInParent<ScrollRect>();
            }

            if (content == null && scrollRect != null)
                content = scrollRect.content;
        }

        private static void Register(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Cuộn tới một Transform bất kỳ nằm trong Content.</summary>
        public void ScrollTo(RectTransform target)
        {
            if (scrollRect == null || content == null || target == null)
                return;

            if (!target.IsChildOf(content))
            {
                Debug.LogWarning($"[ShopCategoryScroller] '{target.name}' không nằm trong Content của ScrollRect.");
                return;
            }

            // Layout (VerticalLayoutGroup + ContentSizeFitter) phải được cập nhật trước khi đo vị trí,
            // nếu không rect/height của target vẫn là giá trị cũ (0) trong frame vừa spawn thẻ.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float targetValue = CalculateNormalizedPosition(target);

            if (scrollRoutine != null)
                StopCoroutine(scrollRoutine);

            if (scrollDuration <= 0.01f || !isActiveAndEnabled)
            {
                SetScrollPosition(targetValue);
                return;
            }

            scrollRoutine = StartCoroutine(ScrollRoutine(targetValue));
        }

        /// <summary>Quy đổi vị trí của target thành verticalNormalizedPosition (0 = đáy, 1 = đỉnh).</summary>
        private float CalculateNormalizedPosition(RectTransform target)
        {
            float viewportHeight = scrollRect.viewport != null
                ? scrollRect.viewport.rect.height
                : ((RectTransform)scrollRect.transform).rect.height;

            float contentHeight = content.rect.height;
            float scrollableHeight = contentHeight - viewportHeight;

            // Content chưa đo lại layout (vừa spawn thẻ) -> không có gì để cuộn.
            if (scrollableHeight <= 0.01f)
                return 1f;

            // Đưa tâm target về không gian local của Content.
            Vector3 targetLocalCenter = content.InverseTransformPoint(target.TransformPoint(target.rect.center));

            // Đỉnh Content nằm ở rect.yMax; khoảng cách từ đỉnh xuống đỉnh target.
            float distanceFromContentTop = content.rect.yMax - (targetLocalCenter.y + target.rect.height * 0.5f);

            // Cộng thêm độ cao viewport (sprite bị đẩy xuống khi scroll = 0) rồi trừ padding.
            float scrollDistance = Mathf.Clamp(distanceFromContentTop - topPadding, 0f, scrollableHeight);

            return 1f - (scrollDistance / scrollableHeight);
        }

        private IEnumerator ScrollRoutine(float targetValue)
        {
            float startValue = scrollRect.verticalNormalizedPosition;
            float elapsed = 0f;

            while (elapsed < scrollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / scrollDuration);

                // Ease-out cubic: khởi động nhanh, dừng êm.
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(startValue, targetValue, eased);

                yield return null;
            }

            scrollRect.verticalNormalizedPosition = targetValue;
            scrollRoutine = null;
        }

        private void SetScrollPosition(float value)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(value);
            scrollRect.velocity = Vector2.zero;
        }
    }
}
