#if UNITY_EDITOR
using System.Linq;
using GalaxyStrike.UI.Home;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GalaxyStrikeMapLayoutBuilder
{
    private const string ArtFolder = "Assets/Sprites/UI/Map_Panel/Campaign/";
    private const string PlanetsPath = "Assets/Sprites/UI/Map_Panel/Source/Colorful Cartoon Planet Sprite Sheet.png";
    private const string BackgroundPath = "Assets/Sprites/UI/ShopPanel/ShopSpaceBackground.png";
    private const string FontPath = "Assets/Art/UI/Fonts/BarlowCondensed-Bold SDF.asset";
    private static readonly Color MutedText = new Color32(172, 199, 228, 255);
    private static readonly Color CyanText = new Color32(123, 225, 255, 255);

    [MenuItem("Tools/GalaxyStrike/Build Map Layout")]
    private static void BuildMapLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        HomeSceneManager home = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<HomeSceneManager>(true))
            .FirstOrDefault();
        var map = home != null
            ? new SerializedObject(home).FindProperty("mapPanel").objectReferenceValue as GameObject
            : null;

        if (map == null)
        {
            Debug.LogError("[Map UI] Open HomeScene to build its Map panel.");
            return;
        }

        Sprite[] planets = LoadPlanets();
        Sprite card = LoadSprite("MapCard", 26f);
        Sprite badge = LoadSprite("MapBadge", 27f);
        Sprite action = LoadSprite("MapAction", 27f);
        Sprite glow = LoadSprite("RouteGlow");
        Sprite focus = LoadSprite("StageFocus");
        Sprite background = AssetDatabase.LoadAllAssetsAtPath(BackgroundPath).OfType<Sprite>().FirstOrDefault();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (planets == null || !card || !badge || !action || !glow || !focus || !background || !font)
        {
            Debug.LogError("[Map UI] Campaign Map assets are incomplete.");
            return;
        }

        Undo.SetCurrentGroupName("Build Campaign Map");
        Undo.RegisterFullObjectHierarchyUndo(map, "Build Campaign Map");
        while (map.transform.childCount > 0)
            Undo.DestroyObjectImmediate(map.transform.GetChild(0).gameObject);
        if (map.TryGetComponent(out CampaignMapPanel oldController))
            Undo.DestroyObjectImmediate(oldController);

        if (map.TryGetComponent(out Image originalBackground))
            originalBackground.enabled = false;

        var controller = Undo.AddComponent<CampaignMapPanel>(map);
        var data = new SerializedObject(controller);
        Bind(data, "home", home);
        Bind(data, "mainActionSprite", action);
        Bind(data, "secondaryActionSprite", badge);
        CreateBackground(map.transform, background);
        CreateRoute(map.transform, data, planets, badge, glow, focus, font);
        CreateHeader(map.transform, data, card, badge, font);
        CreateMissionCard(map.transform, data, card, action, font);
        data.ApplyModifiedPropertiesWithoutUndo();
        controller.PreviewLayout();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = map;
        Debug.Log("[Map UI] Built the Campaign Map in HomeScene.");
    }

    private static void CreateBackground(Transform map, Sprite sprite)
    {
        Image background = DrawImage(Full(map, "Campaign_Background"), sprite, Color.white);
        var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = sprite.rect.width / sprite.rect.height;

        DrawImage(Full(map, "Campaign_Background_Tint"), null, new Color32(3, 11, 33, 100));
    }

    private static void CreateHeader(Transform map, SerializedObject data, Sprite card, Sprite badge, TMP_FontAsset font)
    {
        RectTransform header = Box(map, "Map_Header", Vector2.up, Vector2.one,
            new Vector2(36f, -338f), new Vector2(-36f, -120f));
        DrawImage(header, card, Color.white);

        Label(At(header, "Screen_Title", Vector2.up, new Vector2(248f, -42f), new Vector2(428f, 45f)),
            font, "CAMPAIGN MAP", 34f, CyanText, TextAlignmentOptions.Left).characterSpacing = 4f;
        Bind(data, "zoneNumber", Label(At(header, "Zone_Number", Vector2.one, new Vector2(-160f, -42f), new Vector2(252f, 45f)),
            font, "", 33f, MutedText, TextAlignmentOptions.Right));

        RectTransform title = Box(header, "Zone_Title", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(34f, -32f), new Vector2(-227f, 43f));
        Bind(data, "zoneTitle", Label(title, font, "", 62f, Color.white, TextAlignmentOptions.Left));

        Bind(data, "previousZone", CreateArrow(header, "Previous_Zone", -166f, "<", badge, font));
        Bind(data, "nextZone", CreateArrow(header, "Next_Zone", -73f, ">", badge, font));

        RectTransform track = Box(header, "Zone_Progress", Vector2.zero, Vector2.right,
            new Vector2(35f, 31f), new Vector2(-242f, 39f));
        DrawImage(track, null, new Color32(72, 115, 163, 151));
        RectTransform fill = Full(track, "Fill");
        DrawImage(fill, null, CyanText);
        Bind(data, "progressFill", fill);

        Bind(data, "progressLabel", Label(At(header, "Progress_Label", Vector2.right,
            new Vector2(-130f, 36f), new Vector2(192f, 36f)), font, "", 27f, MutedText, TextAlignmentOptions.Right));
    }

    private static Button CreateArrow(Transform header, string name, float x, string character, Sprite sprite, TMP_FontAsset font)
    {
        RectTransform rect = At(header, name, new Vector2(1f, 0.5f), new Vector2(x, 5f), new Vector2(76f, 72f));
        Image image = DrawImage(rect, sprite, Color.white);
        image.raycastTarget = true;
        Label(Full(rect, "Chevron"), font, character, 50f, Color.white);
        return CreateButton(rect, image);
    }

    private static void CreateRoute(Transform map, SerializedObject data, Sprite[] planets,
        Sprite badge, Sprite glow, Sprite focus, TMP_FontAsset font)
    {
        RectTransform viewport = Box(map, "Campaign_Route", Vector2.zero, Vector2.one,
            new Vector2(28f, 476f), new Vector2(-28f, -354f));
        DrawImage(viewport, null, Color.clear).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 24);

        const float padding = 124f;
        const float spacing = CampaignMapPanel.StageSpacing;
        Vector2 lowerCenter = new Vector2(0.5f, 0f);
        float height = padding * 2f + (planets.Length - 1) * spacing;
        RectTransform content = Box(viewport, "Route_Content", Vector2.zero, Vector2.right,
            Vector2.zero, new Vector2(0f, height));
        content.pivot = lowerCenter;
        content.anchoredPosition = Vector2.zero;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 74f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Bind(data, "route", scroll);

        SerializedProperty lines = data.FindProperty("connections");
        lines.arraySize = planets.Length - 1;
        for (int i = 0; i < lines.arraySize; i++)
        {
            Image line = DrawImage(At(content, $"Route_Segment_{i + 1:00}", lowerCenter,
                new Vector2(0f, padding + (i + 0.5f) * spacing), new Vector2(50f, spacing)),
                glow, Color.white);
            lines.GetArrayElementAtIndex(i).objectReferenceValue = line;
        }

        SerializedProperty nodes = data.FindProperty("nodes");
        nodes.arraySize = planets.Length;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            int stage = i + 1;
            RectTransform node = At(content, $"Stage_Node_{stage:00}", lowerCenter,
                new Vector2(0f, padding + i * spacing), new Vector2(246f, 218f));
            Image hitbox = DrawImage(node, null, Color.clear);
            hitbox.raycastTarget = true;

            Image halo = DrawImage(At(node, "Selection_Ring", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(244f, 218f)),
                focus, Color.white);
            halo.preserveAspect = true;
            Vector2 planetSize = stage % CampaignMapPanel.StagesPerZone == 0
                ? new Vector2(240f, 187f)
                : new Vector2(220f, 169f);
            Image planet = DrawImage(At(node, "Planet", new Vector2(0.5f, 0.5f), Vector2.zero, planetSize),
                planets[i], Color.white);
            planet.preserveAspect = true;

            RectTransform numberRect = At(node, "Stage_Badge", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -72f), new Vector2(84f, 48f));
            Image pill = DrawImage(numberRect, badge, Color.white);
            TMP_Text number = Label(Full(numberRect, "Stage_Number"), font, "", 34f, Color.white);

            SerializedProperty nodeData = nodes.GetArrayElementAtIndex(i);
            Bind(nodeData, "button", CreateButton(node, planet));
            Bind(nodeData, "planet", planet);
            Bind(nodeData, "focus", halo);
            Bind(nodeData, "badge", pill);
            Bind(nodeData, "number", number);
        }
    }

    private static void CreateMissionCard(Transform map, SerializedObject data, Sprite card, Sprite action, TMP_FontAsset font)
    {
        RectTransform panel = Box(map, "Stage_Details", Vector2.zero, Vector2.right,
            new Vector2(36f, 315f), new Vector2(-36f, 471f));
        DrawImage(panel, card, Color.white);

        Bind(data, "missionTitle", Label(At(panel, "Stage_Title", Vector2.zero,
            new Vector2(274f, 102f), new Vector2(480f, 62f)), font, "", 51f, Color.white, TextAlignmentOptions.Left));
        Bind(data, "missionStatus", Label(At(panel, "Stage_Status", Vector2.zero,
            new Vector2(274f, 52f), new Vector2(480f, 42f)), font, "", 29f, CyanText, TextAlignmentOptions.Left));

        RectTransform button = At(panel, "Stage_Action", new Vector2(1f, 0.5f),
            new Vector2(-221f, 0f), new Vector2(374f, 84f));
        Image buttonImage = DrawImage(button, action, Color.white);
        buttonImage.raycastTarget = true;
        Bind(data, "missionButton", CreateButton(button, buttonImage));
        Bind(data, "missionAction", Label(Full(button, "Action_Label"), font, "", 37f, Color.white));
    }

    private static Button CreateButton(RectTransform rect, Graphic target)
    {
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        var colors = button.colors;
        colors.highlightedColor = new Color32(200, 232, 255, 255);
        colors.pressedColor = new Color32(153, 200, 240, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(103, 128, 161, 178);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    private static TMP_Text Label(RectTransform rect, TMP_FontAsset font, string content, float size,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Image DrawImage(RectTransform rect, Sprite sprite, Color tint)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        if (sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Full(Transform parent, string name)
    {
        return Box(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        return Box(parent, name, anchor, anchor, position - size * 0.5f, position + size * 0.5f);
    }

    private static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Vector2 start, Vector2 end)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        Undo.RegisterCreatedObjectUndo(go, "Create Campaign Map Element");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = start;
        rect.offsetMax = end;
        return rect;
    }

    private static void Bind(SerializedObject owner, string key, Object value)
    {
        owner.FindProperty(key).objectReferenceValue = value;
    }

    private static void Bind(SerializedProperty owner, string key, Object value)
    {
        owner.FindPropertyRelative(key).objectReferenceValue = value;
    }

    private static Sprite LoadSprite(string filename, float border = 0f)
    {
        string path = ArtFolder + filename + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single || importer.spriteBorder != Vector4.one * border))
        {
            ConfigureImporter(importer);
            importer.spriteBorder = Vector4.one * border;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] LoadPlanets()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(PlanetsPath).OfType<Sprite>()
            .ToDictionary(sprite => sprite.name);
        int count = CampaignMapPanel.ZoneCount * CampaignMapPanel.StagesPerZone;
        var planets = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            if (!sprites.TryGetValue($"CampaignPlanet_{i + 1:00}", out planets[i]))
                return null;
        }
        return planets;
    }

    private static void ConfigureImporter(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100f;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
#endif
