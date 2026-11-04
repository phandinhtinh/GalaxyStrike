#if UNITY_EDITOR
using TMPro;
using GalaxyStrike.UI.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the standalone loading screen hierarchy from project sprites.</summary>
public static class GalaxyStrikeLoadingSceneBuilder
{
    private const string SpriteFolder = "Assets/Sprites/UI/Loading_Screen";
    private const string BackgroundSprite = "LoadingSpaceBackground";
    private const string LogoSprite = "GalaxyStrikeLogo";
    private const string ProgressSprite = "LoadingProgressFrameCropped";
    private const string TipFrameSprite = "LoadingTipFrameCropped";
    private const string TipIconSprite = "LoadingTipIcon";

    [MenuItem("Tools/GalaxyStrike/Build Loading Scene UI")]
    private static void BuildLoadingSceneUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LoadingScene")
        {
            Debug.LogError("[Loading UI] Open LoadingScene before building its UI.");
            return;
        }

        AssetDatabase.Refresh();
        PrepareSprites();

        GameObject existingCanvas = FindRoot(scene, "LoadingCanvas");
        if (existingCanvas != null)
            Undo.DestroyObjectImmediate(existingCanvas);

        GameObject canvasObject = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Loading Canvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        Image background = CreateImage(canvasRect, "Background_Image", BackgroundSprite,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        background.preserveAspect = false;

        Image logo = CreateImage(canvasRect, "GalaxyStrike_Logo", LogoSprite,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 475f), new Vector2(960f, 440f));
        logo.preserveAspect = true;

        TextMeshProUGUI loadingLabel = CreateText(canvasRect, "Loading_Label", "LOADING…", 46f,
            TextAlignmentOptions.Center, Color.white);
        loadingLabel.fontStyle = FontStyles.Bold;
        loadingLabel.characterSpacing = 2f;
        AddTextGlow(loadingLabel, new Color32(28, 129, 255, 200));
        SetRect(loadingLabel.rectTransform,
            new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -540f), new Vector2(0f, 72f));

        CreateProgressBar(canvasRect, out Image progressFill, out TextMeshProUGUI percentageLabel);
        CreateTipPanel(canvasRect);

        LoadingSceneController loadingController = canvasObject.AddComponent<LoadingSceneController>();
        loadingController.Configure(progressFill, percentageLabel, loadingLabel);
        EditorUtility.SetDirty(loadingController);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("[Loading UI] Built LoadingCanvas and its independent loading screen elements.");
    }

    private static void PrepareSprites()
    {
        string[] names = { BackgroundSprite, LogoSprite, ProgressSprite, TipFrameSprite, TipIconSprite };
        for (int i = 0; i < names.Length; i++)
        {
            string path = SpriteFolder + "/" + names[i] + ".png";
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
            importer.SaveAndReimport();
        }
    }

    private static void CreateProgressBar(Transform parent, out Image progressFill,
        out TextMeshProUGUI percentageLabel)
    {
        RectTransform bar = CreateRectTransform(parent, "Progress_Bar",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -630f), new Vector2(760f, 108f));

        Image frame = CreateImage(bar, "Progress_Frame", ProgressSprite,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        frame.preserveAspect = false;

        progressFill = CreateImage(bar, "Progress_Fill", null,
            new Vector2(0.07f, 0.2f), new Vector2(0.93f, 0.8f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        progressFill.sprite = BuiltinUiSprite();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.color = new Color32(0, 219, 255, 245);

        percentageLabel = CreateText(bar, "Progress_Percentage", "0%", 28f,
            TextAlignmentOptions.Center, Color.white);
        percentageLabel.fontStyle = FontStyles.Bold;
        AddTextGlow(percentageLabel, new Color32(28, 129, 255, 180));
        SetRect(percentageLabel.rectTransform,
            new Vector2(0.79f, 0.15f), new Vector2(0.95f, 0.85f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    private static void CreateTipPanel(Transform parent)
    {
        RectTransform panel = CreateRectTransform(parent, "Tip_Panel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -770f), new Vector2(755f, 110f));

        Image frame = CreateImage(panel, "Tip_Frame", TipFrameSprite,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        frame.preserveAspect = false;

        Image icon = CreateImage(panel, "Tip_Icon", TipIconSprite,
            new Vector2(0.12f, 0.2f), new Vector2(0.245f, 0.8f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        icon.preserveAspect = true;

        TextMeshProUGUI tipLabel = CreateText(panel, "Tip_Label", "TIP:", 22f,
            TextAlignmentOptions.MidlineLeft, new Color32(0, 237, 255, 255));
        tipLabel.fontStyle = FontStyles.Bold;
        AddTextGlow(tipLabel, new Color32(0, 186, 255, 120));
        SetRect(tipLabel.rectTransform,
            new Vector2(0.25f, 0.58f), new Vector2(0.9f, 0.91f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI body = CreateText(panel, "Tip_Text",
            "Nâng cấp phi thuyền để mạnh hơn\nvà vượt qua những thử thách khó khăn hơn!", 20f,
            TextAlignmentOptions.MidlineLeft, new Color32(226, 245, 255, 255));
        body.enableWordWrapping = true;
        body.fontStyle = FontStyles.Bold;
        SetRect(body.rectTransform,
            new Vector2(0.25f, 0.12f), new Vector2(0.95f, 0.66f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    private static Image CreateImage(Transform parent, string name, string spriteName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRectTransform(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = string.IsNullOrEmpty(spriteName) ? null : LoadSprite(spriteName);
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRectTransform(parent, name, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = color;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void AddTextGlow(TextMeshProUGUI label, Color color)
    {
        Shadow shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
    }

    private static RectTransform CreateRectTransform(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Loading UI Element");
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
        string path = SpriteFolder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError("[Loading UI] Missing sprite: " + path);
        return sprite;
    }

    private static Sprite BuiltinUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i];
        }
        return null;
    }
}
#endif
