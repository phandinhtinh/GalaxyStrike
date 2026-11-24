#if UNITY_EDITOR
using System;
using GalaxyStrike.Leaderboard;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the scene-editable leaderboard hierarchy from the supplied UI kit.</summary>
public static class GalaxyStrikeLeaderboardLayoutBuilder
{
    private const string SpriteFolder = "Assets/Sprites/UI/LeaderBoard_Panel/Sliced";
    private const string PanelPath = "Canvas/SafeArea_Container/UI_Panels/Leaderboard_Panel";

    [MenuItem("Tools/GalaxyStrike/Build Leaderboard Layout")]
    private static void BuildLeaderboardLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform panel = FindInScene(scene, "Leaderboard_Panel");
        if (panel == null)
        {
            Debug.LogError($"[Leaderboard UI] Could not find {PanelPath} in the active scene.");
            return;
        }

        PrepareSprites();
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Build Leaderboard Layout");

        while (panel.childCount > 0)
            Undo.DestroyObjectImmediate(panel.GetChild(0).gameObject);

        CreateImage(panel, "Title_Logo", LoadSprite("LeaderboardTitle"),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -132f), new Vector2(930f, 188f));

        CreateTabs(panel);
        Transform leaderboardFrame = CreateLeaderboardFrame(panel);
        CreateYourRankPanel(leaderboardFrame);

        EditorUtility.SetDirty(panel.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[Leaderboard UI] Built the leaderboard layout in Leaderboard_Panel.");
    }

    [MenuItem("Tools/GalaxyStrike/Move Rank Panel Up")]
    private static void MoveRankPanelUp()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform panel = FindInScene(scene, "Leaderboard_Panel");
        RectTransform frame = panel != null
            ? panel.Find("Leaderboard_Frame") as RectTransform
            : null;
        RectTransform rank = frame != null
            ? frame.Find("Your_Rank_Panel") as RectTransform
            : null;
        RectTransform scroll = frame != null
            ? frame.Find("Leaderboard_ScrollView") as RectTransform
            : null;

        if (frame == null || rank == null || scroll == null)
        {
            Debug.LogError("[Leaderboard UI] Could not find the frame and rank panel in the active scene.");
            return;
        }

        Undo.RecordObjects(new UnityEngine.Object[] { frame, rank, scroll }, "Fit Rank Panel Inside Frame");
        frame.sizeDelta = new Vector2(frame.sizeDelta.x, 1300f);
        scroll.offsetMin = new Vector2(46f, 210f);
        rank.anchorMin = new Vector2(0.5f, 0f);
        rank.anchorMax = new Vector2(0.5f, 0f);
        rank.pivot = new Vector2(0.5f, 0f);
        rank.anchoredPosition = new Vector2(0f, 50f);
        EditorUtility.SetDirty(frame);
        EditorUtility.SetDirty(rank);
        EditorUtility.SetDirty(scroll);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
            Debug.LogError("[Leaderboard UI] The scene could not be saved after moving the rank panel.");
    }

    private static void PrepareSprites()
    {
        string[] names =
        {
            "LeaderboardTitle", "TabGlobal", "TabFriends", "TabWeekly", "MainPanelFrame",
            "RowTop1", "RowTop2", "RowTop3", "RowRank4", "RowRank5", "RowRank6", "RowRank7",
            "YourRankPanel", "InfoPanel", "AvatarFrame", "RankMedal1", "RankMedal2", "RankMedal3",
            "DecorativeStar", "DecorativeWings"
        };

        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{SpriteFolder}/{names[i]}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (names[i] == "MainPanelFrame")
                importer.spriteBorder = new Vector4(58f, 42f, 58f, 42f);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
    }

    private static void CreateTabs(Transform panel)
    {
        RectTransform tabs = CreateRectTransform(panel, "TabButtons",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -345f), new Vector2(1000f, 108f));

        CreateTab(tabs, "Tab_Global", "TabGlobal", new Vector2(-330f, 0f), true);
        CreateTab(tabs, "Tab_Friends", "TabFriends", Vector2.zero, false);
        CreateTab(tabs, "Tab_Weekly", "TabWeekly", new Vector2(330f, 0f), false);
    }

    private static void CreateTab(Transform parent, string name, string spriteName, Vector2 position, bool selected)
    {
        Image image = CreateImage(parent, name, LoadSprite(spriteName),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, new Vector2(342f, 108f));

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.interactable = selected;
    }

    private static Transform CreateLeaderboardFrame(Transform panel)
    {
        Image frameImage = CreateImage(panel, "Leaderboard_Frame", LoadSprite("MainPanelFrame"),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -466f), new Vector2(1040f, 1300f));
        frameImage.type = Image.Type.Sliced;
        frameImage.fillCenter = true;

        RectTransform scrollRectTransform = CreateRectTransform(frameImage.transform, "Leaderboard_ScrollView",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        scrollRectTransform.offsetMin = new Vector2(46f, 210f);
        scrollRectTransform.offsetMax = new Vector2(-46f, -46f);

        Image scrollHitArea = scrollRectTransform.gameObject.AddComponent<Image>();
        scrollHitArea.sprite = BuiltinUiSprite();
        scrollHitArea.color = new Color(1f, 1f, 1f, 0.01f);
        scrollHitArea.raycastTarget = true;

        ScrollRect scroll = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        RectTransform viewport = CreateRectTransform(scrollRectTransform, "Viewport",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.sprite = BuiltinUiSprite();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRectTransform(viewport, "Content",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 0f));

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateRow(content, 1, "RowTop1", 148f, "NovaRider", "128,450", new Color32(0, 24, 57, 255));
        CreateRow(content, 2, "RowTop2", 148f, "Pilot_327", "116,820", new Color32(0, 38, 85, 255));
        CreateRow(content, 3, "RowTop3", 148f, "StarWolf", "109,300", new Color32(0, 24, 50, 255));
        CreateRow(content, 4, "RowRank4", 132f, "LunaStrike", "98,740", new Color32(0, 31, 68, 255));
        CreateRow(content, 5, "RowRank5", 132f, "AceComet", "91,260", new Color32(0, 31, 68, 255));
        CreateRow(content, 6, "RowRank6", 132f, "OrionX", "84,900", new Color32(0, 31, 68, 255));
        CreateRow(content, 7, "RowRank7", 132f, "PixelFalcon", "78,650", new Color32(0, 31, 68, 255));

        TextMeshProUGUI status = CreateText(viewport, "Leaderboard_Status", "", 30f,
            TextAlignmentOptions.Center, Color.cyan);
        SetRect(status.rectTransform,
            new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.56f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        status.gameObject.SetActive(false);

        scroll.viewport = viewport;
        scroll.content = content;
        return frameImage.transform;
    }

    private static void CreateRow(Transform content, int rank, string spriteName, float height,
        string playerName, string score, Color32 maskColor)
    {
        Image rowImage = CreateImage(content, $"Row_{rank}", LoadSprite(spriteName),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rowImage.raycastTarget = false;

        LayoutElement layout = rowImage.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        layout.flexibleHeight = 0f;

        RectTransform maskRect = CreateRectTransform(rowImage.transform, "DataMask",
            new Vector2(0.315f, 0.12f), new Vector2(0.825f, 0.88f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image mask = maskRect.gameObject.AddComponent<Image>();
        mask.sprite = BuiltinUiSprite();
        mask.color = maskColor;
        mask.raycastTarget = false;

        Image divider = CreateImage(maskRect, "Column_Divider", BuiltinUiSprite(),
            new Vector2(0.575f, 0f), new Vector2(0.582f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        divider.color = new Color32(0, 146, 224, 190);
        divider.raycastTarget = false;

        TextMeshProUGUI name = CreateText(maskRect, "Player_Name", playerName, 31f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform,
            new Vector2(0.035f, 0.08f), new Vector2(0.55f, 0.92f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI scoreLabel = CreateText(maskRect, "Score_Label", "SCORE", 19f,
            TextAlignmentOptions.MidlineLeft, new Color32(0, 235, 255, 255));
        scoreLabel.fontStyle = FontStyles.Bold;
        SetRect(scoreLabel.rectTransform,
            new Vector2(0.625f, 0.49f), new Vector2(0.99f, 0.90f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI scoreText = CreateText(maskRect, "Score", score, 34f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        scoreText.fontStyle = FontStyles.Bold;
        SetRect(scoreText.rectTransform,
            new Vector2(0.625f, 0.04f), new Vector2(0.99f, 0.60f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    private static void CreateYourRankPanel(Transform frame)
    {
        Image rankImage = CreateImage(frame, "Your_Rank_Panel", LoadSprite("YourRankPanel"),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 50f), new Vector2(1000f, 146f));
        rankImage.raycastTarget = false;

        RectTransform maskRect = CreateRectTransform(rankImage.transform, "DataMask",
            new Vector2(0.455f, 0.12f), new Vector2(0.845f, 0.88f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image mask = maskRect.gameObject.AddComponent<Image>();
        mask.sprite = BuiltinUiSprite();
        mask.color = new Color32(0, 31, 66, 255);
        mask.raycastTarget = false;

        Image divider = CreateImage(maskRect, "Column_Divider", BuiltinUiSprite(),
            new Vector2(0.58f, 0f), new Vector2(0.588f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        divider.color = new Color32(0, 146, 224, 190);
        divider.raycastTarget = false;

        TextMeshProUGUI name = CreateText(maskRect, "Player_Name", "Pilot_327", 31f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform,
            new Vector2(0.035f, 0.08f), new Vector2(0.55f, 0.92f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI scoreLabel = CreateText(maskRect, "Score_Label", "SCORE", 19f,
            TextAlignmentOptions.MidlineLeft, new Color32(0, 235, 255, 255));
        scoreLabel.fontStyle = FontStyles.Bold;
        SetRect(scoreLabel.rectTransform,
            new Vector2(0.625f, 0.49f), new Vector2(0.99f, 0.90f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI score = CreateText(maskRect, "Score", "116,820", 34f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        score.fontStyle = FontStyles.Bold;
        SetRect(score.rectTransform,
            new Vector2(0.625f, 0.04f), new Vector2(0.99f, 0.60f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRectTransform(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRectTransform(parent, name, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static RectTransform CreateRectTransform(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Leaderboard UI Element");
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

    private static Sprite LoadSprite(string name)
    {
        string path = $"{SpriteFolder}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"[Leaderboard UI] Missing sprite: {path}");
        return sprite;
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
