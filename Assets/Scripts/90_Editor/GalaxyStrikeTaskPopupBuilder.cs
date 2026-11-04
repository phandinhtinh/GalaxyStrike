#if UNITY_EDITOR
using System;
using System.Linq;
using GalaxyStrike.UI.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the HomeScene task popup from generated frame and icon assets.</summary>
public static class GalaxyStrikeTaskPopupBuilder
{
    private const string FramePath = "Assets/Sprites/UI/Task_Panel/TaskPopupFrame.png";
    private const string IconsPath = "Assets/Sprites/UI/Task_Panel/TaskIconsAtlas.png";
    private const string RowFramePath = "Assets/Sprites/UI/Upgrade_Panel/Sliced/UpgradeRowFrameCleanTight.png";
    private const string GoButtonPath = "Assets/Sprites/UI/Task_Panel/TaskButton_Go.png";
    private const string ClaimButtonPath = "Assets/Sprites/UI/Task_Panel/TaskButton_Claim.png";
    private const string PopupName = "Task_Popup";
    private static readonly string[] IconNames =
    {
        "TaskIcon_Battle", "TaskIcon_Enemies", "TaskIcon_Boss",
        "TaskIcon_PowerUp", "TaskIcon_Ad", "TaskIcon_Login"
    };

    [MenuItem("Tools/GalaxyStrike/Build Task Popup UI")]
    private static void BuildTaskPopup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "HomeScene")
        {
            Debug.LogError("[Task UI] Open HomeScene before building the task popup.");
            return;
        }

        Transform canvas = FindInScene(scene, "Canvas");
        Transform missionButton = FindInScene(scene, "Missons");
        if (missionButton == null)
            missionButton = FindInScene(scene, "Missions");
        if (canvas == null || missionButton == null)
        {
            Debug.LogError("[Task UI] Could not find Canvas or the Missions sidebar button in HomeScene.");
            return;
        }

        PrepareSprites();
        Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        Sprite rowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RowFramePath);
        Sprite goButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoButtonPath);
        Sprite claimButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ClaimButtonPath);
        Sprite[] taskIcons = LoadTaskIcons();
        if (frameSprite == null || rowSprite == null || goButtonSprite == null || claimButtonSprite == null
            || taskIcons.Any(sprite => sprite == null))
        {
            Debug.LogError("[Task UI] A required frame, action button, or task icon sprite is missing. Check the generated assets under Assets/Sprites/UI/Task_Panel.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Task Popup UI");
        Selection.activeGameObject = canvas.gameObject;
        Transform oldPopup = canvas.Find(PopupName);
        if (oldPopup != null)
            Undo.DestroyObjectImmediate(oldPopup.gameObject);

        GameObject popupRoot = CreateRect(canvas, PopupName, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        popupRoot.SetActive(false);

        Image dim = CreateImage(popupRoot.transform, "Dim_Background", BuiltinUiSprite(),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color32(0, 7, 27, 235), true);
        dim.raycastTarget = true;

        Image panel = CreateImage(popupRoot.transform, "TaskPopup_Frame", frameSprite,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1024f, 1536f), Color.white, false);
        panel.preserveAspect = false;

        TMP_Text title = CreateText(panel.transform, "Title", "TASK", 88f,
            TextAlignmentOptions.Center, Color.white);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title, new Color32(0, 24, 85, 255), 2.5f);
        SetRect(title.rectTransform, new Vector2(0.23f, 0.85f), new Vector2(0.77f, 0.98f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Button closeButton = CreateButton(panel.transform, "Close_Button", BuiltinUiSprite(),
            new Vector2(0.88f, 0.89f), new Vector2(0.98f, 0.97f),
            new Color32(214, 39, 54, 255), "X", 48f, new Color32(255, 255, 255, 255));
        AddOutline(closeButton.image, new Color32(0, 210, 255, 255), 4f);

        Button[] tabs = new Button[3];
        Image[] tabImages = new Image[3];
        TMP_Text[] tabLabels = new TMP_Text[3];
        string[] tabNames = { "Daily", "Weekly", "Achievements" };
        float[] tabX = { -0.305f, 0f, 0.305f };
        for (int i = 0; i < tabNames.Length; i++)
        {
            float centerX = 0.5f + tabX[i];
            Button tab = CreateButton(panel.transform, tabNames[i] + "_Tab", BuiltinUiSprite(),
                new Vector2(centerX - 0.14f, 0.73f), new Vector2(centerX + 0.14f, 0.805f),
                i == 0 ? new Color32(255, 179, 24, 255) : new Color32(7, 42, 91, 255),
                tabNames[i], i == 2 ? 28f : 31f, Color.white);
            AddOutline(tab.image, new Color32(0, 206, 255, 255), 3f);
            TMP_Text tabText = tab.GetComponentInChildren<TMP_Text>(true);
            tabText.enableAutoSizing = true;
            tabText.fontSizeMin = i == 2 ? 23f : 27f;
            tabText.fontSizeMax = i == 2 ? 28f : 31f;
            AddOutline(tabText, new Color32(0, 24, 85, 255), 1.5f);
            tabs[i] = tab;
            tabImages[i] = tab.image;
            tabLabels[i] = tab.GetComponentInChildren<TMP_Text>(true);
        }

        RectTransform rowsRoot = CreateRect(panel.transform, "Task_Rows",
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.71f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ScrollRect taskScrollRect = rowsRoot.gameObject.AddComponent<ScrollRect>();
        taskScrollRect.horizontal = false;
        taskScrollRect.vertical = true;
        taskScrollRect.movementType = ScrollRect.MovementType.Clamped;
        taskScrollRect.inertia = true;
        taskScrollRect.decelerationRate = 0.135f;
        taskScrollRect.scrollSensitivity = 42f;

        RectTransform viewport = CreateRect(rowsRoot, "Viewport",
            Vector2.zero, new Vector2(0.935f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.sprite = BuiltinUiSprite();
        viewportImage.color = new Color32(0, 0, 0, 0);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        taskScrollRect.viewport = viewport;

        RectTransform content = CreateRect(viewport, "Content",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero);
        VerticalLayoutGroup taskLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        taskLayout.padding = new RectOffset(4, 4, 8, 8);
        taskLayout.spacing = 20f;
        taskLayout.childAlignment = TextAnchor.UpperCenter;
        taskLayout.childControlWidth = true;
        taskLayout.childControlHeight = true;
        taskLayout.childForceExpandWidth = true;
        taskLayout.childForceExpandHeight = false;
        ContentSizeFitter contentSizer = content.gameObject.AddComponent<ContentSizeFitter>();
        contentSizer.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizer.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        taskScrollRect.content = content;

        Scrollbar verticalScrollbar = CreateVerticalScrollbar(rowsRoot);
        taskScrollRect.verticalScrollbar = verticalScrollbar;
        taskScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        taskScrollRect.verticalScrollbarSpacing = 3f;

        TaskPopupRowView[] rowViews = new TaskPopupRowView[6];
        for (int i = 0; i < rowViews.Length; i++)
            rowViews[i] = CreateTaskRow(content, rowSprite, goButtonSprite, i);

        Sprite coin = GetHudSprite("Canvas/SafeArea_Container/TopBar_Panel/Bar_Coin/Coin",
            "Assets/Sprites/coin.png", "coin_0");
        Sprite gem = GetHudSprite("Canvas/SafeArea_Container/TopBar_Panel/Bar_Gem/Gem",
            "Assets/Sprites/HomeScene.png", "a_clean_transparent_background_game_ui_sprite_shee_21");
        Sprite energy = GetHudSprite("Canvas/SafeArea_Container/TopBar_Panel/Bar_Energy/Energy",
            "Assets/Sprites/HomeScene.png", "Enegy_1");
        Sprite crate = FindSpriteByName("IconCrate", "Assets/Sprites/UI/Upgrade_Panel/Sliced");
        if (crate == null)
            crate = coin;

        TaskPopupCategory[] categories =
        {
            new TaskPopupCategory { entries = CreateEntries(taskIcons, coin, gem, energy, crate,
                new[] {
                    Entry(taskIcons[0], "Play 3 battles", 1f / 3f, "1/3", coin, "x500", gem, "x5", "GO"),
                    Entry(taskIcons[1], "Destroy 50 enemies", 12f / 50f, "12/50", coin, "x800", energy, "x10", "GO"),
                    Entry(taskIcons[2], "Defeat 1 Boss", 0f, "0/1", gem, "x10", coin, "x1000", "GO"),
                    Entry(taskIcons[3], "Use 3 Power-ups", 0f, "0/3", coin, "x600", energy, "x10", "GO"),
                    Entry(taskIcons[4], "Watch 1 Ad", 0f, "0/1", gem, "x5", coin, "x100", "GO"),
                    Entry(taskIcons[5], "Login today", 1f, "1/1", coin, "x300", gem, "x5", "CLAIM", true,
                        TaskPopupRewardType.Coins, TaskPopupRewardType.Gems)
                }) },
            new TaskPopupCategory { entries = CreateEntries(taskIcons, coin, gem, energy, crate,
                new[] {
                    Entry(taskIcons[0], "Play 20 battles", 0.35f, "7/20", coin, "x2500", gem, "x20", "GO"),
                    Entry(taskIcons[1], "Destroy 500 enemies", 0.22f, "110/500", coin, "x4000", energy, "x50", "GO"),
                    Entry(taskIcons[2], "Defeat 5 Bosses", 0f, "0/5", gem, "x60", crate, "x2", "GO"),
                    Entry(taskIcons[3], "Use 30 Power-ups", 0.3f, "9/30", coin, "x3000", energy, "x40", "GO"),
                    Entry(taskIcons[0], "Clear 10 stages", 0.4f, "4/10", gem, "x30", coin, "x1800", "GO"),
                    Entry(taskIcons[5], "Login 5 days", 0.4f, "2/5", coin, "x1500", gem, "x15", "GO")
                }) },
            new TaskPopupCategory { entries = CreateEntries(taskIcons, coin, gem, energy, crate,
                new[] {
                    Entry(taskIcons[0], "Complete 10 battles", 0.7f, "7/10", gem, "x25", coin, "x1500", "GO"),
                    Entry(taskIcons[1], "Destroy 1000 enemies", 0.12f, "120/1000", gem, "x40", energy, "x30", "GO"),
                    Entry(taskIcons[2], "Defeat 10 Bosses", 0f, "0/10", crate, "x3", gem, "x50", "GO"),
                    Entry(taskIcons[3], "Use 100 Power-ups", 0.09f, "9/100", coin, "x5000", energy, "x60", "GO"),
                    Entry(taskIcons[5], "Login for 7 days", 0.28f, "2/7", gem, "x35", coin, "x2000", "GO"),
                    Entry(taskIcons[0], "Earn 3 stars", 0.33f, "1/3", crate, "x1", gem, "x15", "GO")
                }) }
        };

        GameObject missionObject = missionButton.gameObject;
        missionObject.name = "Missions";
        Button openButton = missionObject.GetComponent<Button>();
        if (openButton == null)
            openButton = missionObject.gameObject.AddComponent<Button>();
        Image missionImage = missionObject.GetComponent<Image>();
        if (missionImage != null)
            openButton.targetGraphic = missionImage;

        TaskPopupController controller = canvas.GetComponent<TaskPopupController>();
        if (controller == null)
            controller = Undo.AddComponent<TaskPopupController>(canvas.gameObject);
        controller.Configure(popupRoot, openButton, closeButton, taskScrollRect, goButtonSprite, claimButtonSprite,
            tabs, tabImages, tabLabels, rowViews, categories);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(popupRoot);
        EditorUtility.SetDirty(missionObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[Task UI] Built the HomeScene popup with Daily, Weekly, and Achievements tabs. Task counters/rewards are sample UI data.");
    }

    private static void PrepareSprites()
    {
        ConfigureSingleSprite(FramePath);
        ConfigureSingleSprite(RowFramePath);
        ConfigureSingleSprite(GoButtonPath);
        ConfigureSingleSprite(ClaimButtonPath);

        AssetDatabase.ImportAsset(IconsPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(IconsPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;

            // The generated atlas is 1024x1536 with six 512x512 cells.
            SpriteRect[] metadata = new SpriteRect[IconNames.Length];
            for (int i = 0; i < metadata.Length; i++)
            {
                int column = i % 2;
                int rowFromTop = i / 2;
                metadata[i] = new SpriteRect
                {
                    name = IconNames[i],
                    rect = new Rect(column * 512, 1024 - rowFromTop * 512, 512, 512),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
            }

            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            dataProvider.SetSpriteRects(metadata);
            dataProvider.Apply();
            importer.SaveAndReimport();
        }
        AssetDatabase.Refresh();
    }

    private static void ConfigureSingleSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadTaskIcons()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(IconsPath);
        return IconNames.Select(name => assets.OfType<Sprite>().FirstOrDefault(sprite => sprite.name == name)).ToArray();
    }

    private static Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        RectTransform trackRect = CreateRect(parent, "Vertical_Scrollbar",
            new Vector2(0.955f, 0.025f), new Vector2(0.99f, 0.975f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image track = trackRect.gameObject.AddComponent<Image>();
        track.sprite = BuiltinUiSprite();
        track.type = Image.Type.Sliced;
        track.color = new Color32(1, 16, 46, 235);
        track.raycastTarget = true;

        RectTransform handleRect = CreateRect(trackRect, "Handle",
            new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.98f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.sprite = BuiltinUiSprite();
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color32(0, 205, 255, 255);
        handleImage.raycastTarget = true;
        AddOutline(handleImage, new Color32(0, 26, 84, 255), 2f);

        Scrollbar scrollbar = trackRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.size = 0.35f;
        scrollbar.value = 1f;
        return scrollbar;
    }

    private static TaskPopupRowView CreateTaskRow(Transform parent, Sprite rowSprite, Sprite goButtonSprite, int index)
    {
        const float rowHeight = 150f;
        RectTransform rowRect = CreateRect(parent, "Task_Row_" + (index + 1),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, rowHeight));
        LayoutElement layoutElement = rowRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = rowHeight;
        layoutElement.preferredHeight = rowHeight;
        layoutElement.flexibleHeight = 0f;

        Image rowImage = rowRect.gameObject.AddComponent<Image>();
        rowImage.sprite = rowSprite;
        rowImage.color = Color.white;
        rowImage.raycastTarget = false;
        rowImage.type = Image.Type.Simple;
        rowImage.preserveAspect = false;

        TaskPopupRowView rowView = Undo.AddComponent<TaskPopupRowView>(rowRect.gameObject);
        rowView.root = rowRect.gameObject;

        Image icon = CreateImage(rowRect, "Task_Icon", null,
            new Vector2(0.025f, 0.12f), new Vector2(0.165f, 0.88f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, Color.white, false);
        icon.preserveAspect = true;
        icon.color = new Color32(255, 255, 255, 0);

        TMP_Text title = CreateText(rowRect, "Task_Title", "", 36f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 28f;
        title.fontSizeMax = 36f;
        AddOutline(title, new Color32(0, 20, 68, 255), 1.5f);
        SetRect(title.rectTransform,
            new Vector2(0.18f, 0.54f), new Vector2(0.575f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        Image progressTrack = CreateImage(rowRect, "Progress_Track", BuiltinUiSprite(),
            new Vector2(0.18f, 0.14f), new Vector2(0.565f, 0.43f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color32(2, 16, 45, 245), false);
        AddOutline(progressTrack, new Color32(0, 134, 228, 255), 2f);

        Image progressFill = CreateImage(progressTrack.transform, "Progress_Fill", BuiltinUiSprite(),
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero,
            new Color32(0, 206, 255, 255), false);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        TMP_Text progressLabel = CreateText(progressTrack.transform, "Progress_Label", "", 25f,
            TextAlignmentOptions.Center, Color.white);
        progressLabel.fontStyle = FontStyles.Bold;
        AddOutline(progressLabel, new Color32(0, 20, 68, 255), 1f);

        Image rewardOneIcon = CreateImage(rowRect, "Reward_1_Icon", null,
            new Vector2(0.585f, 0.35f), new Vector2(0.67f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, Color.white, false);
        rewardOneIcon.preserveAspect = true;
        TMP_Text rewardOneAmount = CreateText(rowRect, "Reward_1_Amount", "", 23f,
            TextAlignmentOptions.Center, Color.white);
        rewardOneAmount.fontStyle = FontStyles.Bold;
        rewardOneAmount.enableAutoSizing = true;
        rewardOneAmount.fontSizeMin = 18f;
        rewardOneAmount.fontSizeMax = 23f;
        AddOutline(rewardOneAmount, new Color32(0, 20, 68, 255), 1f);
        SetRect(rewardOneAmount.rectTransform,
            new Vector2(0.575f, 0.015f), new Vector2(0.685f, 0.37f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        Image rewardTwoIcon = CreateImage(rowRect, "Reward_2_Icon", null,
            new Vector2(0.70f, 0.35f), new Vector2(0.785f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, Color.white, false);
        rewardTwoIcon.preserveAspect = true;
        TMP_Text rewardTwoAmount = CreateText(rowRect, "Reward_2_Amount", "", 23f,
            TextAlignmentOptions.Center, Color.white);
        rewardTwoAmount.fontStyle = FontStyles.Bold;
        rewardTwoAmount.enableAutoSizing = true;
        rewardTwoAmount.fontSizeMin = 18f;
        rewardTwoAmount.fontSizeMax = 23f;
        AddOutline(rewardTwoAmount, new Color32(0, 20, 68, 255), 1f);
        SetRect(rewardTwoAmount.rectTransform,
            new Vector2(0.69f, 0.015f), new Vector2(0.80f, 0.37f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        Button action = CreateButton(rowRect, "Task_Action", goButtonSprite,
            new Vector2(0.79f, 0.20f), new Vector2(0.99f, 0.80f),
            Color.white, "GO", 31f, Color.white);
        action.image.type = Image.Type.Simple;
        action.image.preserveAspect = false;
        TMP_Text actionText = action.GetComponentInChildren<TMP_Text>(true);
        actionText.fontStyle = FontStyles.Bold;
        actionText.enableAutoSizing = true;
        actionText.fontSizeMin = 18f;
        actionText.fontSizeMax = 31f;
        AddOutline(actionText, new Color32(0, 20, 68, 255), 1.5f);

        rowView.icon = icon;
        rowView.title = title;
        rowView.progressFill = progressFill;
        rowView.progressLabel = progressLabel;
        rowView.rewardOneIcon = rewardOneIcon;
        rowView.rewardOneAmount = rewardOneAmount;
        rowView.rewardTwoIcon = rewardTwoIcon;
        rowView.rewardTwoAmount = rewardTwoAmount;
        rowView.actionButton = action;
        rowView.actionFace = null;
        rowView.actionLabel = action.GetComponentInChildren<TMP_Text>(true);
        return rowView;
    }

    private static TaskPopupEntry[] CreateEntries(Sprite[] icons, Sprite coin, Sprite gem, Sprite energy,
        Sprite crate, TaskPopupEntry[] entries)
    {
        // Keep the data construction centralized for consistent display values.
        return entries;
    }

    private static TaskPopupEntry Entry(Sprite icon, string title, float progress, string progressLabel,
        Sprite rewardOne, string amountOne, Sprite rewardTwo, string amountTwo, string action, bool claim = false,
        TaskPopupRewardType rewardOneType = TaskPopupRewardType.None,
        TaskPopupRewardType rewardTwoType = TaskPopupRewardType.None)
    {
        return new TaskPopupEntry
        {
            icon = icon,
            title = title,
            progress = progress,
            progressLabel = progressLabel,
            rewardOneIcon = rewardOne,
            rewardOneAmount = amountOne,
            rewardOneType = rewardOneType,
            rewardTwoIcon = rewardTwo,
            rewardTwoAmount = amountTwo,
            rewardTwoType = rewardTwoType,
            actionLabel = action,
            claimAvailable = claim
        };
    }

    private static Sprite GetHudSprite(string objectPath, string assetPath, string spriteName)
    {
        GameObject hudObject = GameObject.Find(objectPath);
        Image image = hudObject != null ? hudObject.GetComponent<Image>() : null;
        if (image != null && image.sprite != null)
            return image.sprite;
        return FindSpriteByName(spriteName, System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/'))
               ?? AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static Sprite FindSpriteByName(string spriteName, string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Sprite match = assets.OfType<Sprite>().FirstOrDefault(sprite => sprite.name == spriteName);
            if (match != null)
                return match;
        }
        return null;
    }

    private static Button CreateButton(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Color background, string label, float fontSize, Color labelColor)
    {
        RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = background;
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        TMP_Text text = CreateText(rect, "Label", label, fontSize,
            TextAlignmentOptions.Center, labelColor);
        text.fontStyle = FontStyles.Bold;
        TMP_Text labelText = text;
        AddOutline(labelText, new Color32(0, 24, 85, 255), 1.5f);
        return button;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size,
        Color color, bool raycastTarget)
    {
        RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(parent, name, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = color;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        else
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        return text;
    }

    private static RectTransform CreateRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Task Popup UI Element");
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, anchorMin, anchorMax, pivot, position, size);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AddOutline(Graphic graphic, Color color, float distance)
    {
        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static Sprite BuiltinUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate.name == name && candidate.gameObject.scene == scene)
                return candidate;
        }
        return null;
    }
}
#endif
