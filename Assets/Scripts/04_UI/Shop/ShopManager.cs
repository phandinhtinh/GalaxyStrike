using UnityEngine;
using UnityEngine.UI;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using GalaxyStrike.IAP;
using GalaxyStrike.UI.Shop;

namespace GalaxyStrike.UI
{
    /// <summary>
    /// Dựng toàn bộ thẻ trong Shop từ ScriptableObject và dữ liệu save.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        [Header("--- Prefabs ---")]
        [SerializeField] private ShopItemUI itemCardPrefab;

        [Header("--- Grid Containers ---")]
        [SerializeField] private Transform shipGrid;
        [SerializeField] private Transform weaponGrid;
        [SerializeField] private Transform boosterGrid;

        [Header("--- Data Sources (ScriptableObjects) ---")]
        [SerializeField] private ShipDataSO[] shipDataArray;
        [SerializeField] private ItemDataSO[] weaponDataArray;
        [SerializeField] private ItemDataSO[] boosterDataArray;

        [Header("--- Layout ---")]
        [Tooltip("Chiều cao tối thiểu của một hàng thẻ để VerticalLayoutGroup không ép grid về 0")]
        [SerializeField] private float minGridHeight = 280f;
        [Tooltip("Khoảng cách dọc giữa Header và Grid")]
        [SerializeField] private float headerGap = 16f;
        [Tooltip("Chiều cao cố định của mỗi Header (Ships / Weapons / Boosters)")]
        [SerializeField] private float headerHeight = 55f;

        private RectTransform contentRoot;
        private Transform gemPackGrid;
        private Transform coinPackGrid;
        private readonly System.Collections.Generic.List<IapPackCardUI> iapCards = new System.Collections.Generic.List<IapPackCardUI>(8);

        // Tham chiếu tới Header của từng nhóm để Category_Sidebar cuộn tới.
        private RectTransform shipsHeader;
        private RectTransform weaponsHeader;
        private RectTransform boostersHeader;
        private RectTransform gemPacksHeader;
        private RectTransform coinPacksHeader;

        private void OnEnable()
        {
            // Ở OnEnable, service được tạo bởi GameBootstrap trước khi scene load,
            // nên FindObjectOfType luôn tìm thấy. Không dùng .Instance để tránh tạo trùng.
            SaveManager save = FindObjectOfType<SaveManager>();
            if (save != null)
                save.OnDataChanged += RefreshAllCards;

            IapPurchaseService iap = FindObjectOfType<IapPurchaseService>();
            if (iap != null)
                iap.OnStoreReady += RefreshIapCards;
            if (iap != null)
                iap.OnRewardGranted += HandleRewardGranted;
        }

        private void OnDisable()
        {
            // KHÔNG dùng SaveManager.Instance / IapPurchaseService.Instance ở đây:
            // getter tự sinh object mới nếu instance đã bị huỷ, và khi scene đang unload
            // Unity sẽ báo "Some objects were not cleaned up when closing the scene".
            // Dùng FindObjectOfType để chỉ huỷ đăng ký với service còn sống.
            SaveManager save = FindObjectOfType<SaveManager>();
            if (save != null)
                save.OnDataChanged -= RefreshAllCards;

            IapPurchaseService iap = FindObjectOfType<IapPurchaseService>();
            if (iap != null)
                iap.OnStoreReady -= RefreshIapCards;
            if (iap != null)
                iap.OnRewardGranted -= HandleRewardGranted;
        }

        private static void HandleRewardGranted(string productId)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.coinCollectSFX != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollectSFX);
        }

        private void Start()
        {
            // Shop_Panel mặc định tắt trong HomeScene: tạo thẻ một lần ngay từ đầu
            // để lần mở Shop đầu tiên không bị nháy trắng.
            contentRoot = shipGrid != null ? shipGrid.parent as RectTransform : null;
            ConfigureHeaders();
            ConfigureGrids();
            EnsureContentChildrenSized();
            PopulateShop();
            SetupCategoryScroller();
        }

        /// <summary>
        /// Gắn Category_Sidebar (5 nút trái) với đúng Header trong Main_ScrollView.
        /// Tự tìm theo tên để không phải kéo thả 10 tham chiếu trong Inspector.
        /// </summary>
        private void SetupCategoryScroller()
        {
            if (contentRoot == null)
                return;

            // ScrollRect nằm trên CHA của Main_ScrollView (Main_ScrollView chính là Viewport),
            // nên phải tìm ngược lên, không dùng GetComponent trên chính ShopManager.
            ScrollRect scrollRect = contentRoot.GetComponentInParent<ScrollRect>();
            if (scrollRect == null)
            {
                Debug.LogWarning(
                    "[ShopManager] Không tìm thấy ScrollRect trong cha của Content " +
                    "-> nút Category_Sidebar không cuộn được.");
                return;
            }

            Button shipsBtn = FindTabButton("Tab_Ships");
            Button weaponsBtn = FindTabButton("Tab_Weapons");
            Button itemsBtn = FindTabButton("Tab_Items");
            Button gemsBtn = FindTabButton("Tab_Gems");
            Button coinsBtn = FindTabButton("Tab_Coins");

            var scroller = GetComponent<ShopCategoryScroller>();
            if (scroller == null)
                scroller = gameObject.AddComponent<ShopCategoryScroller>();

            scroller.Bind(
                scrollRect, contentRoot,
                shipsBtn, shipsHeader,
                weaponsBtn, weaponsHeader,
                itemsBtn, boostersHeader,
                gemsBtn, gemPacksHeader,
                coinsBtn, coinPacksHeader);
        }

        /// <summary>Tìm Button theo tên trong toàn bộ scene (kể cả object đang tắt).</summary>
        private static Button FindTabButton(string objectName)
        {
            Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || buttons[i].gameObject.name != objectName)
                    continue;

                // Loại object thuộc Prefab/asset (Resources.FindObjectsOfTypeAll trả về cả asset).
                if (!buttons[i].gameObject.scene.IsValid())
                    continue;

                return buttons[i];
            }

            return null;
        }

        /// <summary>
        /// Cache Header của 5 nhóm để Category_Sidebar cuộn tới.
        /// Header IAP được tạo bằng code nên phải lấy sau SpawnIapPacks().
        /// </summary>
        private void CacheSectionHeaders()
        {
            shipsHeader = FindHeader("Header_Ships");
            weaponsHeader = FindHeader("Header_Weapon");
            boostersHeader = FindHeader("Header_Booster");
            gemPacksHeader = FindHeader("Header_GemPacks");
            coinPacksHeader = FindHeader("Header_CoinPacks");

            // Header gói IAP được tạo bằng code sau mỗi lần populate -> cập nhật lại điểm đến.
            var scroller = GetComponent<ShopCategoryScroller>();
            if (scroller != null)
            {
                scroller.UpdateTargets(shipsHeader, weaponsHeader, boostersHeader, gemPacksHeader, coinPacksHeader);
            }
        }

        private RectTransform FindHeader(string objectName)
        {
            if (contentRoot == null)
                return null;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform child = contentRoot.GetChild(i);
                if (child != null && child.name == objectName)
                    return child as RectTransform;
            }

            return null;
        }

        /// <summary>
        /// Chuẩn hoá layout 3 grid để Content (VerticalLayoutGroup) xếp Header và Grid nối tiếp nhau
        /// thay vì chồng lên nhau.
        /// </summary>
        private void ConfigureGrids()
        {
            ConfigureGrid(shipGrid);
            ConfigureGrid(weaponGrid);
            ConfigureGrid(boosterGrid);
        }

        private void ConfigureGrid(Transform grid)
        {
            if (grid == null)
                return;

            var rect = (RectTransform)grid;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -headerGap);

            // Grid tự co giãn chiều cao theo số hàng thẻ.
            var fitter = grid.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = grid.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Bắt buộc có LayoutElement: Content dùng ChildControlHeight nên sẽ hỏi preferredHeight,
            // nếu thiếu thì grid bị ép về 0 và mất hẳn khỏi màn hình.
            var element = grid.GetComponent<LayoutElement>();
            if (element == null)
                element = grid.gameObject.AddComponent<LayoutElement>();

            element.minHeight = minGridHeight;
            element.preferredHeight = minGridHeight;

            // Đảm bảo grid layout có mặt (scene cũ có thể thiếu).
            var gridLayout = grid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
                gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
        }

        /// <summary>Cập nhật lại chiều cao grid theo số hàng thẻ thực tế.</summary>
        private void UpdateGridHeights()
        {
            UpdateGridHeight(shipGrid);
            UpdateGridHeight(weaponGrid);
            UpdateGridHeight(boosterGrid);
            UpdateGridHeight(gemPackGrid);
            UpdateGridHeight(coinPackGrid);
        }

        private void UpdateGridHeight(Transform grid)
        {
            if (grid == null)
                return;

            var gridLayout = grid.GetComponent<GridLayoutGroup>();
            var element = grid.GetComponent<LayoutElement>();
            if (gridLayout == null || element == null)
                return;

            int cardCount = grid.childCount;
            if (cardCount == 0)
            {
                element.minHeight = 0f;
                element.preferredHeight = 0f;
                return;
            }

            int columns = Mathf.Max(1, gridLayout.constraintCount);
            int rows = Mathf.CeilToInt(cardCount / (float)columns);
            float height = gridLayout.padding.vertical
                           + rows * gridLayout.cellSize.y
                           + Mathf.Max(0, rows - 1) * gridLayout.spacing.y;

            element.minHeight = height;
            element.preferredHeight = height;
        }

        /// <summary>Gọi mỗi lần Shop được mở lại để số dư/ví mới nhất hiển thị đúng.</summary>
        public void RefreshAllCards()
        {
            if (!HasCards())
            {
                PopulateShop();
                return;
            }

            RefreshGrid(shipGrid);
            RefreshGrid(weaponGrid);
            RefreshGrid(boosterGrid);
            RefreshIapCards();
        }

        private bool HasCards()
        {
            return (shipGrid != null && shipGrid.childCount > 0) ||
                   (weaponGrid != null && weaponGrid.childCount > 0) ||
                   (boosterGrid != null && boosterGrid.childCount > 0);
        }

        public void PopulateShop()
        {
            ClearAllGrids();
            ClearIapSections();
            SpawnShips();
            SpawnWeapons();
            SpawnBoosters();
            SpawnIapPacks();
            UpdateGridHeights();
            CacheSectionHeaders();
        }

        private void ClearAllGrids()
        {
            ClearGrid(shipGrid);
            ClearGrid(weaponGrid);
            ClearGrid(boosterGrid);
        }

        /// <summary>Xoá các section IAP đã tạo bằng code trước khi dựng lại.</summary>
        private void ClearIapSections()
        {
            if (contentRoot == null)
                return;

            iapCards.Clear();

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == null)
                    continue;

                string childName = child.name;
                if (childName == "Header_GemPacks" || childName == "Header_CoinPacks" ||
                    childName == "Grid_GemPacks" || childName == "Grid_CoinPacks")
                {
                    // SetActive(false) NGAY để section cũ không còn chiếm chỗ trong layout
                    // trong frame này (Destroy chỉ có hiệu lực cuối frame).
                    child.gameObject.SetActive(false);

                    // Đổi tên để lần populate sau không nhặt lại (phòng trường hợp Destroy bị hoãn).
                    child.name = childName + "_Old";

                    Destroy(child.gameObject);
                }
            }

            gemPackGrid = null;
            coinPackGrid = null;
        }

        private static void ClearGrid(Transform grid)
        {
            if (grid == null)
                return;

            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                Transform child = grid.GetChild(i);

                // Tách khỏi grid NGAY để thẻ cũ không còn trong layout frame này.
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Content dùng ChildControlHeight nên mỗi Header phải khai báo chiều cao,
        /// nếu không header bị co về 0 hoặc đè lên grid bên dưới.
        /// </summary>
        private void ConfigureHeaders()
        {
            if (contentRoot == null)
                return;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("Header_"))
                    continue;

                // Chỉ header thuần (không chứa nội dung con) mới được set chiều cao cứng.
                if (child.childCount > 0)
                    continue;

                var element = child.GetComponent<LayoutElement>();
                if (element == null)
                    element = child.gameObject.AddComponent<LayoutElement>();

                element.minHeight = headerHeight;
                element.preferredHeight = headerHeight;
                element.flexibleHeight = 0f;
            }
        }

        /// <summary>
        /// Khi Content dùng ChildControlHeight, MỌI phần tử con đều phải khai báo chiều cao,
        /// nếu không sẽ bị ép về 0 (mất khỏi màn hình). Giữ nguyên chiều cao gốc đang có.
        /// </summary>
        private void EnsureContentChildrenSized()
        {
            if (contentRoot == null)
                return;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == null)
                    continue;

                var element = child.GetComponent<LayoutElement>();
                if (element == null)
                    element = child.gameObject.AddComponent<LayoutElement>();

                if (element.preferredHeight > 0f)
                    continue;

                float height = ((RectTransform)child).sizeDelta.y;
                if (height <= 0f)
                    continue;

                element.minHeight = height;
                element.preferredHeight = height;
            }
        }

        private void SpawnShips()
        {
            if (shipDataArray == null || shipGrid == null || itemCardPrefab == null)
                return;

            for (int i = 0; i < shipDataArray.Length; i++)
            {
                ShipDataSO shipSO = shipDataArray[i];
                if (shipSO == null)
                    continue;

                ShopItemUI card = Instantiate(itemCardPrefab, shipGrid);
                card.SetData(shipSO, i);
            }
        }

        private void SpawnWeapons()
        {
            SpawnItems(weaponDataArray, weaponGrid);
        }

        private void SpawnBoosters()
        {
            SpawnItems(boosterDataArray, boosterGrid);
        }

        private void SpawnItems(ItemDataSO[] source, Transform grid)
        {
            if (source == null || grid == null || itemCardPrefab == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                ItemDataSO itemSO = source[i];
                if (itemSO == null)
                    continue;

                ShopItemUI card = Instantiate(itemCardPrefab, grid);
                card.SetData(itemSO);
            }
        }

        private static void RefreshGrid(Transform grid)
        {
            if (grid == null)
                return;

            for (int i = 0; i < grid.childCount; i++)
            {
                ShopItemUI card = grid.GetChild(i).GetComponent<ShopItemUI>();
                if (card != null)
                    card.RefreshState();
            }
        }

        // =====================================================================
        // GÓI MUA THẬT (IAP - Giai đoạn 4)
        // =====================================================================
        /// <summary>
        /// Tạo Header + Grid cho 2 nhóm gói IAP ngay trong Content.
        /// Dùng code thay vì dựng tay trong Editor để không phải sửa prefab UI.
        /// </summary>
        private void SpawnIapPacks()
        {
            if (contentRoot == null)
                return;

            IapPurchaseService service = IapPurchaseService.Instance;
            IapCatalog catalog = service != null ? service.Catalog : null;
            if (catalog == null)
                return;

            gemPackGrid = BuildIapSection("Header_GemPacks", "GÓI GEM", catalog.gemPacks, new Color(0.55f, 0.3f, 1f));
            coinPackGrid = BuildIapSection("Header_CoinPacks", "GÓI COIN", catalog.coinPacks, new Color(1f, 0.84f, 0.2f));
        }

        private Transform BuildIapSection(string headerName, string headerLabel, IapProductDefinition[] packs, Color accent)
        {
            bool hasPacks = packs != null && packs.Length > 0;

            // Header vẫn tạo để người chơi biết vị trí, nhưng hiện "SẮP RA MẮT" khi catalog rỗng.
            CreateSectionHeader(headerName, hasPacks ? headerLabel : $"{headerLabel} (SẮP RA MẮT)");

            var gridObj = new GameObject(headerName.Replace("Header_", "Grid_"), typeof(RectTransform));
            var gridRect = gridObj.GetComponent<RectTransform>();
            gridRect.SetParent(contentRoot, false);

            var gridLayout = gridObj.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.cellSize = new Vector2(200f, 280f);
            gridLayout.spacing = new Vector2(15f, 20f);

            var fitter = gridObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var element = gridObj.AddComponent<LayoutElement>();
            element.minHeight = minGridHeight;
            element.preferredHeight = minGridHeight;

            if (!hasPacks)
                return gridObj.transform;

            for (int i = 0; i < packs.Length; i++)
            {
                IapProductDefinition pack = packs[i];
                if (pack == null || string.IsNullOrEmpty(pack.productId))
                    continue;

                IapPackCardUI card = IapPackCardUI.Create(pack, accent);
                card.transform.SetParent(gridObj.transform, false);
                card.transform.localScale = Vector3.one;
                iapCards.Add(card);
            }

            return gridObj.transform;
        }

        private void CreateSectionHeader(string objectName, string label)
        {
            if (contentRoot == null)
                return;

            var headerObj = new GameObject(objectName, typeof(RectTransform));
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.SetParent(contentRoot, false);
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(-60f, headerHeight);

            var element = headerObj.AddComponent<LayoutElement>();
            element.minHeight = headerHeight;
            element.preferredHeight = headerHeight;
            element.flexibleHeight = 0f;

            var textObj = new GameObject("Label", typeof(RectTransform));
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.SetParent(headerRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28f;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        /// <summary>Vẽ lại giá bản địa hoá của các gói khi Store khởi tạo xong.</summary>
        public void RefreshIapCards()
        {
            for (int i = 0; i < iapCards.Count; i++)
            {
                if (iapCards[i] != null)
                    iapCards[i].Refresh();
            }
        }
    }
}
