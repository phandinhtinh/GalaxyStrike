#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Builds the map screen UI hierarchy in the HomeScene Map_Panel.</summary>
public static class GalaxyStrikeMapLayoutBuilder
{
    private const string MapFolder = "Assets/Sprites/UI/Map_Panel/Generated";
    private const string DecorativeStarPath = "Assets/Sprites/UI/LeaderBoard_Panel/Sliced/DecorativeStar.png";
    private const string PanelPath = "Canvas/SafeArea_Container/UI_Panels/Map_Panel";

    [MenuItem("Tools/GalaxyStrike/Build Map Layout")]
    private static void BuildMapLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform panel = FindInScene(scene, "Map_Panel");
        if (panel == null)
        {
            Debug.LogError("[Map UI] Could not find " + PanelPath + " in the active scene.");
            return;
        }

        Selection.activeGameObject = panel.gameObject;
        PrepareSprites();
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Build Map Layout");

        RectTransform panelRect = panel as RectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }

        while (panel.childCount > 0)
            Undo.DestroyObjectImmediate(panel.GetChild(0).gameObject);

        CreateBackground(panel);
        CreateRouteArtwork(panel);
        CreateLevelNodes(panel);
        CreateHeader(panel);
        CreateDescription(panel);

        EditorUtility.SetDirty(panel.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[Map UI] Built the map layout in Map_Panel.");
    }

    private static void PrepareSprites()
    {
        AssetDatabase.Refresh();
        string[] paths =
        {
            "Assets/Sprites/space_Background.png",
            MapFolder + "/MapRouteArtworkTight.png",
            MapFolder + "/MapZoneHeaderControlsTight.png",
            MapFolder + "/MapLevelBadge.png",
            MapFolder + "/MapRatingStars.png",
            DecorativeStarPath
        };

        foreach (string path in paths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static void CreateBackground(Transform panel)
    {
        Image background = CreateImage(panel, "Map_Background", "Assets/Sprites/space_Background.png",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1317f, 2340f));
        background.preserveAspect = true;
        background.raycastTarget = false;
    }

    private static void CreateRouteArtwork(Transform panel)
    {
        Image route = CreateImage(panel, "Map_Route_Artwork", MapFolder + "/MapRouteArtworkTight.png",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f), new Vector2(760f, 1474f));
        route.preserveAspect = true;
        route.raycastTarget = false;
    }

    private static void CreateLevelNodes(Transform panel)
    {
        RectTransform nodesRoot = CreateRectTransform(panel, "Map_Level_Nodes",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        CreateLevelNode(nodesRoot, 3, new Vector2(0f, 330f));
        CreateLevelNode(nodesRoot, 2, new Vector2(0f, -240f));
        CreateLevelNode(nodesRoot, 1, new Vector2(0f, -690f));
    }

    private static void CreateLevelNode(Transform parent, int level, Vector2 position)
    {
        Image badge = CreateImage(parent, "Level_Node_" + level, MapFolder + "/MapLevelBadge.png",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, new Vector2(250f, 147f));
        badge.preserveAspect = true;
        badge.raycastTarget = true;

        Button button = badge.gameObject.AddComponent<Button>();
        button.targetGraphic = badge;
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI number = CreateText(badge.transform, "Level_Number", level.ToString(), 54f,
            TextAlignmentOptions.Center, Color.white);
        number.fontStyle = FontStyles.Bold;
        SetRect(number.rectTransform,
            new Vector2(0.35f, 0.67f), new Vector2(0.65f, 0.94f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        Image stars = CreateImage(badge.transform, "Star_Rating", MapFolder + "/MapRatingStars.png",
            new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.34f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        stars.preserveAspect = true;
    }

    private static void CreateHeader(Transform panel)
    {
        Image header = CreateImage(panel, "Zone_Header_Controls",
            MapFolder + "/MapZoneHeaderControlsTight.png",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -200f), new Vector2(970f, 158f));
        header.preserveAspect = true;
        header.raycastTarget = false;

        Image emblem = CreateImage(header.transform, "Zone_Emblem", DecorativeStarPath,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 66f), new Vector2(78f, 78f));
        emblem.preserveAspect = true;

        TextMeshProUGUI zoneTitle = CreateText(header.transform, "Zone_Title", "ZONE 1", 64f,
            TextAlignmentOptions.Center, Color.white);
        zoneTitle.fontStyle = FontStyles.Bold;
        SetRect(zoneTitle.rectTransform,
            new Vector2(0.22f, 0.45f), new Vector2(0.78f, 0.91f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI zoneSubtitle = CreateText(header.transform, "Zone_Subtitle", "EARTH ORBIT", 28f,
            TextAlignmentOptions.Center, new Color32(64, 238, 255, 255));
        zoneSubtitle.fontStyle = FontStyles.Bold;
        SetRect(zoneSubtitle.rectTransform,
            new Vector2(0.24f, 0.16f), new Vector2(0.76f, 0.46f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        CreateHitTarget(header.transform, "Previous_Zone_Button",
            new Vector2(0.005f, 0.06f), new Vector2(0.14f, 0.94f));
        CreateHitTarget(header.transform, "Next_Zone_Button",
            new Vector2(0.86f, 0.06f), new Vector2(0.995f, 0.94f));
    }

    private static void CreateDescription(Transform panel)
    {
        TextMeshProUGUI description = CreateText(panel, "Map_Description",
            "Defeat enemies. Conquer the galaxy!", 28f,
            TextAlignmentOptions.Center, new Color32(73, 221, 255, 255));
        description.fontStyle = FontStyles.Bold;
        SetRect(description.rectTransform,
            new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -340f), new Vector2(0f, 50f));
    }

    private static void CreateHitTarget(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateRectTransform(parent, name, anchorMin, anchorMax,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
    }

    private static Image CreateImage(Transform parent, string name, string assetPath,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRectTransform(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        image.color = Color.white;
        image.type = Image.Type.Simple;
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
        tmp.enableWordWrapping = false;
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
        Undo.RegisterCreatedObjectUndo(go, "Create Map UI Element");
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

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChild(root.transform, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
