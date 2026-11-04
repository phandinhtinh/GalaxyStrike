using System;
using System.Collections.Generic;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    /// <summary>
    /// Builds the Backpack overlay at runtime and presents values from the player's save data.
    /// The icon atlas is loaded from Resources/BackpackIconsAtlas.
    /// </summary>
    public sealed class BackpackUIController : MonoBehaviour
    {
        private enum Category
        {
            All,
            Items,
            Materials,
            Others
        }

        private struct InventoryEntry
        {
            public string title;
            public string description;
            public string quantity;
            public int iconIndex;
        }

        private static readonly string[] CategoryNames = { "All", "Items", "Materials", "Others" };
        private static readonly Category[] Categories = { Category.All, Category.Items, Category.Materials, Category.Others };

        private readonly List<Button> categoryButtons = new List<Button>(4);
        private readonly List<Sprite> iconSprites = new List<Sprite>(16);
        private readonly List<Sprite> controlSprites = new List<Sprite>(4);

        private RectTransform overlay;
        private RectTransform content;
        private RectTransform emptyState;
        private TextMeshProUGUI detailsText;
        private Button shopButton;
        private Image selectedCardImage;
        private Category activeCategory = Category.All;
        private UnityAction openShopAction;
        private Texture2D iconAtlas;
        private Texture2D frameAtlas;
        private Sprite frameSprite;
        private Sprite activeTabSprite;
        private Sprite inactiveTabSprite;
        private Sprite itemSlotSprite;
        private Sprite closeButtonSprite;
        private bool initialized;

        private static readonly Color PanelColor = new Color(0.012f, 0.034f, 0.105f, 0.985f);
        private static readonly Color CardColor = new Color(0.018f, 0.065f, 0.17f, 0.97f);
        private static readonly Color Cyan = new Color(0.04f, 0.78f, 1f, 1f);
        private static readonly Color Gold = new Color(1f, 0.64f, 0.035f, 1f);
        private static readonly Color MutedText = new Color(0.66f, 0.82f, 0.98f, 1f);

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (overlay != null && overlay.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                CloseBackpack();
        }

        public void BindShopAction(UnityAction action)
        {
            openShopAction = action;
        }

        public void OpenBackpack()
        {
            EnsureInitialized();
            if (overlay == null)
                return;

            RefreshCurrencySummary();
            RefreshGrid();
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        public void CloseBackpack()
        {
            if (overlay != null)
                overlay.gameObject.SetActive(false);
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Backpack] Không tìm thấy Canvas trong HomeScene.");
                return;
            }

            iconAtlas = Resources.Load<Texture2D>("BackpackIconsAtlas");
            frameAtlas = Resources.Load<Texture2D>("BackpackPanelFrame");
            BuildIconSprites();
            BuildFrameSprite();
            BuildUiSprites();
            if (!BuildOverlayFromPrefab(canvas.transform))
                BuildOverlay(canvas.transform);
            initialized = overlay != null;
        }

        private bool BuildOverlayFromPrefab(Transform canvasTransform)
        {
            Transform sceneOverlay = canvasTransform.Find("SafeArea_Container/UI_Panels/BackpackPanel");
            GameObject instance;
            if (sceneOverlay != null)
            {
                instance = sceneOverlay.gameObject;
            }
            else
            {
                GameObject prefab = Resources.Load<GameObject>("BackpackOverlay");
                if (prefab == null)
                    return false;

                instance = Instantiate(prefab, canvasTransform, false);
                instance.name = "Backpack_Overlay";
            }

            overlay = instance.GetComponent<RectTransform>();
            Transform panel = overlay != null ? overlay.Find("Backpack_Panel") : null;
            Transform header = panel != null ? panel.Find("Header_Controls") : null;
            Transform tabs = panel != null ? panel.Find("Category_Tabs") : null;
            Transform summary = panel != null ? panel.Find("Currency_Summary") : null;
            Transform scrollContent = panel != null ? panel.Find("Item_Scroll_Area/Viewport/Item_Grid") : null;
            Transform empty = panel != null ? panel.Find("Empty_State") : null;
            Transform details = panel != null ? panel.Find("Item_Details_Bar/Item_Details") : null;
            Button closeButton = header != null ? header.Find("Close_Button")?.GetComponent<Button>() : null;
            Button openShop = empty != null ? empty.Find("Open_Shop_Button")?.GetComponent<Button>() : null;

            if (overlay == null || panel == null || tabs == null || summary == null || scrollContent == null || empty == null || details == null || closeButton == null || openShop == null)
            {
                Destroy(instance);
                overlay = null;
                return false;
            }

            content = scrollContent.GetComponent<RectTransform>();
            emptyState = empty.GetComponent<RectTransform>();
            detailsText = details.GetComponent<TextMeshProUGUI>();
            shopButton = openShop;
            if (content == null || emptyState == null || detailsText == null)
            {
                Destroy(instance);
                overlay = null;
                content = null;
                emptyState = null;
                detailsText = null;
                shopButton = null;
                return false;
            }

            Image frameImage = panel.Find("Frame_Art")?.GetComponent<Image>();
            if (frameImage != null)
            {
                frameImage.sprite = frameSprite;
                frameImage.preserveAspect = false;
                frameImage.raycastTarget = false;
            }

            Image closeImage = closeButton.GetComponent<Image>();
            if (closeImage != null && closeButtonSprite != null)
            {
                closeImage.sprite = closeButtonSprite;
                closeImage.type = Image.Type.Simple;
                closeImage.color = Color.white;
            }
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseBackpack);

            string[] currencyNames = { "COINS", "GEMS", "ENERGY" };
            for (int i = 0; i < currencyNames.Length; i++)
            {
                Transform pill = summary.Find("Currency_" + currencyNames[i]);
                Image pillImage = pill != null ? pill.GetComponent<Image>() : null;
                if (pillImage != null && inactiveTabSprite != null)
                {
                    pillImage.sprite = inactiveTabSprite;
                    pillImage.type = Image.Type.Sliced;
                    pillImage.color = Color.white;
                }

                Image icon = pill != null ? pill.Find("Icon")?.GetComponent<Image>() : null;
                if (icon != null)
                    icon.sprite = GetIconSprite(i);
            }

            Image detailsBackground = panel.Find("Item_Details_Bar")?.GetComponent<Image>();
            if (detailsBackground != null && inactiveTabSprite != null)
            {
                detailsBackground.sprite = inactiveTabSprite;
                detailsBackground.type = Image.Type.Sliced;
                detailsBackground.color = Color.white;
            }

            categoryButtons.Clear();
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                Transform tabTransform = tabs.Find("Tab_" + CategoryNames[i]);
                Button tab = tabTransform != null ? tabTransform.GetComponent<Button>() : null;
                if (tab == null)
                {
                    Destroy(instance);
                    overlay = null;
                    content = null;
                    emptyState = null;
                    detailsText = null;
                    shopButton = null;
                    categoryButtons.Clear();
                    return false;
                }

                categoryButtons.Add(tab);
                tab.onClick.RemoveAllListeners();
                Category selectedCategory = Categories[i];
                tab.onClick.AddListener(() => SelectCategory(selectedCategory));
            }

            Image shopImage = shopButton.GetComponent<Image>();
            if (shopImage != null && activeTabSprite != null)
            {
                shopImage.sprite = activeTabSprite;
                shopImage.type = Image.Type.Sliced;
                shopImage.color = Color.white;
            }
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(() =>
            {
                CloseBackpack();
                if (openShopAction != null)
                    openShopAction.Invoke();
            });

            Image emptyBadge = empty.Find("Empty_Icon_Badge")?.GetComponent<Image>();
            if (emptyBadge != null)
            {
                emptyBadge.sprite = GetIconSprite(15);
                emptyBadge.preserveAspect = true;
            }

            overlay.gameObject.SetActive(false);
            SelectCategory(activeCategory);
            RefreshCurrencySummary();
            return true;
        }

        private void BuildFrameSprite()
        {
            if (frameAtlas == null)
            {
                Debug.LogWarning("[Backpack] Không tìm thấy Resources/BackpackPanelFrame.png. Dùng khung màu dự phòng.");
                return;
            }

            Rect rect = new Rect(0f, 0f, frameAtlas.width, frameAtlas.height);
            frameSprite = Sprite.Create(frameAtlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            frameSprite.name = "BackpackPanelFrame_Runtime";
        }

        private void BuildUiSprites()
        {
            activeTabSprite = LoadUiSprite("BackpackTabActive", 0.12f, 0.22f, 10f);
            inactiveTabSprite = LoadUiSprite("BackpackTabInactive", 0.12f, 0.22f, 10f);
            itemSlotSprite = LoadUiSprite("BackpackItemSlot", 0.15f, 0.15f, 10f);
            closeButtonSprite = LoadUiSprite("BackpackCloseButton", 0f, 0f, 100f);
        }

        private Sprite LoadUiSprite(string resourceName, float horizontalBorder, float verticalBorder, float pixelsPerUnit)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourceName);
            if (texture == null)
            {
                Debug.LogWarning("[Backpack] Không tìm thấy Resources/" + resourceName + ".png.");
                return null;
            }

            Vector4 border = new Vector4(
                texture.width * horizontalBorder,
                texture.height * verticalBorder,
                texture.width * horizontalBorder,
                texture.height * verticalBorder);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = resourceName + "_Runtime";
            controlSprites.Add(sprite);
            return sprite;
        }

        private void BuildIconSprites()
        {
            if (iconAtlas == null)
            {
                Debug.LogWarning("[Backpack] Không tìm thấy Resources/BackpackIconsAtlas.png. Icon sẽ dùng ô màu mặc định.");
                return;
            }

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    int xMin = Mathf.RoundToInt(iconAtlas.width * column / 4f);
                    int xMax = Mathf.RoundToInt(iconAtlas.width * (column + 1) / 4f);
                    int yMin = iconAtlas.height - Mathf.RoundToInt(iconAtlas.height * (row + 1) / 4f);
                    int yMax = iconAtlas.height - Mathf.RoundToInt(iconAtlas.height * row / 4f);
                    Rect rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                    Sprite sprite = Sprite.Create(iconAtlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = "BackpackIcon_" + (row * 4 + column + 1).ToString("00");
                    iconSprites.Add(sprite);
                }
            }
        }

        private void BuildOverlay(Transform canvasTransform)
        {
            overlay = CreateRect(canvasTransform, "Backpack_Overlay");
            Stretch(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = overlay.gameObject.AddComponent<Image>();
            dimmer.color = new Color(0.002f, 0.008f, 0.03f, 0.82f);
            dimmer.raycastTarget = true;

            RectTransform panel = CreateRect(overlay, "Backpack_Panel");
            // The Backpack is a centered modal popup, not a full-screen panel.
            Stretch(panel, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.75f), Vector2.zero, Vector2.zero);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            if (frameSprite != null)
            {
                Image frameImage = AddImage(panel, "Frame_Art", Color.white);
                frameImage.sprite = frameSprite;
                frameImage.preserveAspect = false;
                frameImage.raycastTarget = false;
                Stretch(frameImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            else
            {
                Outline panelOutline = panel.gameObject.AddComponent<Outline>();
                panelOutline.effectColor = new Color(0.02f, 0.68f, 1f, 0.9f);
                panelOutline.effectDistance = new Vector2(4f, -4f);
                panelOutline.useGraphicAlpha = true;
            }

            BuildHeader(panel);
            BuildCurrencySummary(panel);
            BuildCategoryTabs(panel);
            BuildDetails(panel);
            BuildItemGrid(panel);
            BuildEmptyState(panel);
            SelectCategory(activeCategory);
            RefreshCurrencySummary();

            overlay.gameObject.SetActive(false);
        }

        private void BuildHeader(RectTransform panel)
        {
            Image header = AddImage(panel, "Header_Controls", new Color(0.018f, 0.10f, 0.28f, 0f));
            // The generated popup frame has a transparent top margin and its title plate sits below it.
            Stretch(header.rectTransform, new Vector2(0.018f, 0.82f), new Vector2(0.982f, 0.92f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI title = AddText(header.rectTransform, "Title", "BACKPACK", 60f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            Stretch(title.rectTransform, new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.95f), Vector2.zero, Vector2.zero);
            title.outlineWidth = 0.18f;
            title.outlineColor = new Color(0.01f, 0.34f, 0.95f, 1f);

            Button close = AddButton(header.rectTransform, "Close_Button", closeButtonSprite != null ? Color.white : new Color(0.72f, 0.055f, 0.12f, 1f));
            Stretch(close.GetComponent<RectTransform>(), new Vector2(0.88f, 0.05f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);
            Image closeImage = close.GetComponent<Image>();
            if (closeButtonSprite != null)
            {
                closeImage.sprite = closeButtonSprite;
                closeImage.type = Image.Type.Simple;
            }
            else
            {
                Outline closeOutline = close.gameObject.AddComponent<Outline>();
                closeOutline.effectColor = new Color(1f, 0.28f, 0.3f, 1f);
                closeOutline.effectDistance = new Vector2(2f, -2f);
                TextMeshProUGUI closeLabel = AddText(close.transform, "Close_Label", "×", 52f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
                Stretch(closeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            close.onClick.AddListener(CloseBackpack);
        }

        private void BuildCurrencySummary(RectTransform panel)
        {
            RectTransform summary = CreateRect(panel, "Currency_Summary");
            Stretch(summary, new Vector2(0.13f, 0.725f), new Vector2(0.87f, 0.805f), Vector2.zero, Vector2.zero);

            string[] labels = { "COINS", "GEMS", "ENERGY" };
            for (int i = 0; i < labels.Length; i++)
            {
                float left = i / 3f;
                float right = (i + 1) / 3f;
                Image pill = AddImage(summary, "Currency_" + labels[i], inactiveTabSprite != null ? Color.white : new Color(0.018f, 0.075f, 0.20f, 0.96f));
                if (inactiveTabSprite != null)
                {
                    pill.sprite = inactiveTabSprite;
                    pill.type = Image.Type.Sliced;
                }
                Stretch(pill.rectTransform, new Vector2(left + 0.012f, 0.08f), new Vector2(right - 0.012f, 0.92f), Vector2.zero, Vector2.zero);

                Image icon = AddImage(pill.rectTransform, "Icon", Color.white);
                icon.sprite = GetIconSprite(i);
                icon.preserveAspect = true;
                Stretch(icon.rectTransform, new Vector2(0.03f, 0.1f), new Vector2(0.30f, 0.9f), Vector2.zero, Vector2.zero);
                icon.raycastTarget = false;

                TextMeshProUGUI label = AddText(pill.rectTransform, "Value", labels[i] + " 0", 27f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
                Stretch(label.rectTransform, new Vector2(0.28f, 0.08f), new Vector2(0.98f, 0.92f), Vector2.zero, Vector2.zero);
            }
        }

        private void BuildCategoryTabs(RectTransform panel)
        {
            RectTransform tabs = CreateRect(panel, "Category_Tabs");
            Stretch(tabs, new Vector2(0.13f, 0.625f), new Vector2(0.87f, 0.715f), Vector2.zero, Vector2.zero);

            for (int i = 0; i < CategoryNames.Length; i++)
            {
                float left = i / 4f;
                float right = (i + 1) / 4f;
                Button tab = AddButton(tabs, "Tab_" + CategoryNames[i], inactiveTabSprite != null ? Color.white : CardColor);
                Stretch(tab.GetComponent<RectTransform>(), new Vector2(left + 0.008f, 0.04f), new Vector2(right - 0.008f, 0.96f), Vector2.zero, Vector2.zero);
                Image tabImage = tab.GetComponent<Image>();
                if (inactiveTabSprite != null)
                {
                    tabImage.sprite = inactiveTabSprite;
                    tabImage.type = Image.Type.Sliced;
                }
                else
                {
                    Outline outline = tab.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.02f, 0.45f, 0.9f, 0.9f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }

                TextMeshProUGUI label = AddText(tab.transform, "Label", CategoryNames[i], 32f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                Category category = Categories[i];
                tab.onClick.AddListener(() => SelectCategory(category));
                categoryButtons.Add(tab);
            }
        }

        private void BuildDetails(RectTransform panel)
        {
            RectTransform bar = CreateRect(panel, "Item_Details_Bar");
            Stretch(bar, new Vector2(0.13f, 0.565f), new Vector2(0.87f, 0.615f), Vector2.zero, Vector2.zero);
            Image background = bar.gameObject.AddComponent<Image>();
            background.color = inactiveTabSprite != null ? Color.white : new Color(0.018f, 0.065f, 0.17f, 0.8f);
            if (inactiveTabSprite != null)
            {
                background.sprite = inactiveTabSprite;
                background.type = Image.Type.Sliced;
            }
            background.raycastTarget = false;

            detailsText = AddText(bar, "Item_Details", "Select an item to view its details.", 22f, FontStyles.Normal, TextAlignmentOptions.Left, MutedText);
            Stretch(detailsText.rectTransform, new Vector2(0.025f, 0.04f), new Vector2(0.975f, 0.96f), Vector2.zero, Vector2.zero);
            detailsText.enableAutoSizing = true;
            detailsText.fontSizeMin = 16f;
            detailsText.fontSizeMax = 22f;
        }

        private void BuildItemGrid(RectTransform panel)
        {
            RectTransform scrollRoot = CreateRect(panel, "Item_Scroll_Area");
            Stretch(scrollRoot, new Vector2(0.13f, 0.10f), new Vector2(0.87f, 0.55f), Vector2.zero, Vector2.zero);

            RectTransform viewport = CreateRect(scrollRoot, "Viewport");
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect(viewport, "Item_Grid");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(8, 8, 12, 12);
            grid.cellSize = new Vector2(160f, 176f);
            grid.spacing = new Vector2(12f, 12f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
        }

        private void BuildEmptyState(RectTransform panel)
        {
            emptyState = CreateRect(panel, "Empty_State");
            Stretch(emptyState, new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.55f), Vector2.zero, Vector2.zero);

            Image badge = AddImage(emptyState, "Empty_Icon_Badge", itemSlotSprite != null ? Color.white : new Color(0.02f, 0.12f, 0.29f, 0.98f));
            if (itemSlotSprite != null)
            {
                badge.sprite = itemSlotSprite;
                badge.type = Image.Type.Sliced;
            }
            RectTransform badgeRect = badge.rectTransform;
            badgeRect.anchorMin = new Vector2(0.40f, 0.48f);
            badgeRect.anchorMax = new Vector2(0.60f, 0.98f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            badge.preserveAspect = true;
            badge.sprite = GetIconSprite(15);

            TextMeshProUGUI message = AddText(emptyState, "Empty_Message", "No equipment owned yet.\nVisit the Shop to get started.", 25f, FontStyles.Bold, TextAlignmentOptions.Center, MutedText);
            Stretch(message.rectTransform, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero);
            message.enableAutoSizing = true;
            message.fontSizeMin = 18f;
            message.fontSizeMax = 25f;

            shopButton = AddButton(emptyState, "Open_Shop_Button", activeTabSprite != null ? Color.white : Gold);
            if (activeTabSprite != null)
            {
                Image shopImage = shopButton.GetComponent<Image>();
                shopImage.sprite = activeTabSprite;
                shopImage.type = Image.Type.Sliced;
            }
            RectTransform shopRect = shopButton.GetComponent<RectTransform>();
            shopRect.anchorMin = new Vector2(0.31f, 0.015f);
            shopRect.anchorMax = new Vector2(0.69f, 0.15f);
            shopRect.offsetMin = Vector2.zero;
            shopRect.offsetMax = Vector2.zero;
            TextMeshProUGUI shopLabel = AddText(shopButton.transform, "Label", "OPEN SHOP", 20f, FontStyles.Bold, TextAlignmentOptions.Center, activeTabSprite != null ? Color.white : new Color(0.05f, 0.12f, 0.25f, 1f));
            Stretch(shopLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            shopButton.onClick.AddListener(() =>
            {
                CloseBackpack();
                if (openShopAction != null)
                    openShopAction.Invoke();
            });
            emptyState.gameObject.SetActive(false);
        }

        private void SelectCategory(Category category)
        {
            activeCategory = category;
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                bool selected = Categories[i] == category;
                Image image = categoryButtons[i].GetComponent<Image>();
                if (activeTabSprite != null && inactiveTabSprite != null)
                {
                    image.sprite = selected ? activeTabSprite : inactiveTabSprite;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
                else
                {
                    image.color = selected ? Gold : CardColor;
                }

                TextMeshProUGUI label = categoryButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.color = activeTabSprite != null ? Color.white : selected ? new Color(0.035f, 0.10f, 0.24f, 1f) : Color.white;
            }

            selectedCardImage = null;
            detailsText.text = "Select an item to view its details.";
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (content == null)
                return;

            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            List<InventoryEntry> entries = GetInventoryEntries();

            for (int i = 0; i < entries.Count; i++)
                CreateItemCard(entries[i]);

            bool showEmpty = entries.Count == 0;
            if (emptyState != null)
            {
                emptyState.gameObject.SetActive(showEmpty);
                Transform messageTransform = emptyState.Find("Empty_Message");
                TextMeshProUGUI message = messageTransform != null ? messageTransform.GetComponent<TextMeshProUGUI>() : null;
                if (message != null)
                {
                    message.text = activeCategory == Category.Items
                        ? "No equipment owned yet.\nVisit the Shop to get started."
                        : activeCategory == Category.Others
                            ? "No spacecraft unlocked yet."
                            : "Your backpack is empty.";
                }

                if (shopButton != null)
                    shopButton.gameObject.SetActive(activeCategory == Category.Items);
            }

            Canvas.ForceUpdateCanvases();
            if (content != null)
                content.anchoredPosition = Vector2.zero;
        }

        private List<InventoryEntry> GetInventoryEntries()
        {
            List<InventoryEntry> entries = new List<InventoryEntry>();
            SaveManager save = SaveManager.Instance;
            if (save == null)
                return entries;

            if (activeCategory == Category.All || activeCategory == Category.Materials)
                AddCurrencies(entries, save);

            if (activeCategory == Category.All || activeCategory == Category.Items)
                AddOwnedItems(entries, save);

            if (activeCategory == Category.All || activeCategory == Category.Others)
                AddOwnedShips(entries, save);

            return entries;
        }

        private static void AddCurrencies(List<InventoryEntry> entries, SaveManager save)
        {
            entries.Add(new InventoryEntry
            {
                title = "Coins",
                description = "Used to buy equipment and upgrade your spacecraft.",
                quantity = save.Coins.ToString("N0"),
                iconIndex = 0
            });
            entries.Add(new InventoryEntry
            {
                title = "Gems",
                description = "Premium currency for rare equipment.",
                quantity = save.Gems.ToString("N0"),
                iconIndex = 1
            });
            entries.Add(new InventoryEntry
            {
                title = "Energy",
                description = "Campaign energy. It recovers over time.",
                quantity = save.Energy + "/" + save.MaxEnergy,
                iconIndex = 2
            });
        }

        private static void AddOwnedItems(List<InventoryEntry> entries, SaveManager save)
        {
            List<string> ownedItemIds = save.Data.ownedItemIds;
            if (ownedItemIds == null)
                return;

            for (int i = 0; i < ownedItemIds.Count; i++)
            {
                string itemId = ownedItemIds[i];
                if (string.IsNullOrEmpty(itemId))
                    continue;

                string normalized = itemId.ToLowerInvariant();
                InventoryEntry entry = new InventoryEntry
                {
                    quantity = "x1",
                    iconIndex = 15
                };

                switch (normalized)
                {
                    case "booster_shield":
                    case "booster-shield":
                        entry.title = "Energy Shield";
                        entry.description = "Absorbs incoming damage for a short time.";
                        entry.iconIndex = 4;
                        break;
                    case "booster_magnet":
                    case "booster-magnet":
                        entry.title = "Magnet Booster";
                        entry.description = "Pulls nearby drops toward your spacecraft.";
                        entry.iconIndex = 7;
                        break;
                    case "weapon_rocket":
                    case "weapon-rocket":
                        entry.title = "Homing Rocket";
                        entry.description = "A homing missile that tracks enemy targets.";
                        entry.iconIndex = 5;
                        break;
                    case "weapon_laser":
                    case "weapon-laser":
                        entry.title = "Laser Weapon";
                        entry.description = "A focused energy weapon for combat.";
                        entry.iconIndex = 6;
                        break;
                    default:
                        entry.title = itemId.Replace('_', ' ').Replace('-', ' ');
                        entry.description = "Owned equipment.";
                        break;
                }

                entries.Add(entry);
            }
        }

        private static void AddOwnedShips(List<InventoryEntry> entries, SaveManager save)
        {
            List<int> unlockedShips = save.Data.unlockedShipIndices;
            if (unlockedShips == null)
                return;

            for (int i = 0; i < unlockedShips.Count; i++)
            {
                int shipIndex = unlockedShips[i];
                if (shipIndex < 0)
                    continue;

                entries.Add(new InventoryEntry
                {
                    title = "Spacecraft " + (shipIndex + 1).ToString("00"),
                    description = shipIndex == save.SelectedShipIndex ? "Your currently selected spacecraft." : "Unlocked spacecraft in your fleet.",
                    quantity = "OWNED",
                    iconIndex = 5
                });
            }
        }

        private void CreateItemCard(InventoryEntry entry)
        {
            Button card = AddButton(content, "Item_" + entry.title.Replace(' ', '_'), itemSlotSprite != null ? Color.white : CardColor);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(160f, 176f);

            Image cardImage = card.GetComponent<Image>();
            if (itemSlotSprite != null)
            {
                cardImage.sprite = itemSlotSprite;
                cardImage.type = Image.Type.Sliced;
            }
            else
            {
                Outline outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.02f, 0.5f, 0.95f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            Image icon = AddImage(card.transform, "Icon", Color.white);
            icon.sprite = GetIconSprite(entry.iconIndex);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Stretch(icon.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.95f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI name = AddText(card.transform, "Name", entry.title, 21f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            Stretch(name.rectTransform, new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.34f), Vector2.zero, Vector2.zero);
            name.enableAutoSizing = true;
            name.fontSizeMin = 15f;
            name.fontSizeMax = 21f;

            TextMeshProUGUI count = AddText(card.transform, "Count", entry.quantity, 20f, FontStyles.Bold, TextAlignmentOptions.Right, new Color(0.92f, 0.97f, 1f, 1f));
            Stretch(count.rectTransform, new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.16f), Vector2.zero, Vector2.zero);
            count.enableAutoSizing = true;
            count.fontSizeMin = 14f;
            count.fontSizeMax = 20f;

            InventoryEntry selectedEntry = entry;
            card.onClick.AddListener(() => ShowItemDetails(selectedEntry, cardImage));
        }

        private void ShowItemDetails(InventoryEntry entry, Image cardImage)
        {
            if (selectedCardImage != null)
                selectedCardImage.color = itemSlotSprite != null ? Color.white : CardColor;

            selectedCardImage = cardImage;
            selectedCardImage.color = itemSlotSprite != null
                ? new Color(0.76f, 0.9f, 1f, 1f)
                : new Color(0.035f, 0.19f, 0.40f, 1f);
            if (detailsText != null)
                detailsText.text = entry.title + "  •  " + entry.quantity + "    |    " + entry.description;
        }

        private void RefreshCurrencySummary()
        {
            if (overlay == null)
                return;

            SaveManager save = SaveManager.Instance;
            if (save == null)
                return;

            SetSummaryValue("COINS", "COINS " + save.Coins.ToString("N0"));
            SetSummaryValue("GEMS", "GEMS " + save.Gems.ToString("N0"));
            SetSummaryValue("ENERGY", "ENERGY " + save.Energy + "/" + save.MaxEnergy);
        }

        private void SetSummaryValue(string token, string value)
        {
            Transform summary = overlay.Find("Backpack_Panel/Currency_Summary/Currency_" + token + "/Value");
            TextMeshProUGUI text = summary != null ? summary.GetComponent<TextMeshProUGUI>() : null;
            if (text != null)
                text.text = value;
        }

        private Sprite GetIconSprite(int index)
        {
            return index >= 0 && index < iconSprites.Count ? iconSprites[index] : null;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(Transform parent, string name, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button AddButton(Transform parent, string name, Color color)
        {
            Image image = AddImage(parent, name, color);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.84f, 0.94f, 1f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            image.raycastTarget = true;
            return button;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < iconSprites.Count; i++)
            {
                if (iconSprites[i] != null)
                    Destroy(iconSprites[i]);
            }
            iconSprites.Clear();
            for (int i = 0; i < controlSprites.Count; i++)
            {
                if (controlSprites[i] != null)
                    Destroy(controlSprites[i]);
            }
            controlSprites.Clear();
            if (frameSprite != null)
                Destroy(frameSprite);
        }
    }
}
