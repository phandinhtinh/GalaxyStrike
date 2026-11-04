#if UNITY_EDITOR
using System;
using GalaxyStrike.Save;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the scene-editable upgrade screen hierarchy from the supplied UI kit.</summary>
public static class GalaxyStrikeUpgradeLayoutBuilder
{
    private const string SpriteFolder = "Assets/Sprites/UI/Upgrade_Panel/Sliced";
    private const string PanelPath = "Canvas/SafeArea_Container/UI_Panels/Upgrade_Panel";

    private static readonly ShipUpgradeType[] UpgradeTypes =
    {
        ShipUpgradeType.Damage,
        ShipUpgradeType.FireRate,
        ShipUpgradeType.MaxHealth,
        ShipUpgradeType.MoveSpeed
    };

    [MenuItem("Tools/GalaxyStrike/Build Upgrade Layout")]
    private static void BuildUpgradeLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform panel = FindInScene(scene, "Upgrade_Panel");
        if (panel == null)
        {
            Debug.LogError("[Upgrade UI] Could not find " + PanelPath + " in the active scene.");
            return;
        }

        // Keep the Inspector on the persistent parent before rebuilding its children.
        Selection.activeGameObject = panel.gameObject;

        PrepareSprites();
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Build Upgrade Layout");

        RectTransform panelRect = panel as RectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;

        Image panelBackground = panel.GetComponent<Image>();
        if (panelBackground != null)
        {
            panelBackground.enabled = false;
            panelBackground.raycastTarget = false;
        }

        while (panel.childCount > 0)
            Undo.DestroyObjectImmediate(panel.GetChild(0).gameObject);

        CreateImage(panel, "Title_Logo", "TitleUpgrade",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -245f), new Vector2(930f, 228f));

        TextMeshProUGUI subtitle = CreateText(panel, "Upgrade_Subtitle",
            "STRONGER SHIP   •   HIGHER POWER   •   VICTORY", 25f,
            TextAlignmentOptions.Center, new Color32(0, 232, 255, 255));
        SetRect(subtitle.rectTransform,
            new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -376f), new Vector2(0f, 46f));
        subtitle.fontStyle = FontStyles.Bold;
        subtitle.characterSpacing = 1.2f;

        CreateStatCard(panel, "Stat_Damage", ShipUpgradeType.Damage,
            new Vector2(-350f, -505f));
        CreateStatCard(panel, "Stat_FireRate", ShipUpgradeType.FireRate,
            new Vector2(-350f, -705f));
        CreateStatCard(panel, "Stat_MaxHealth", ShipUpgradeType.MaxHealth,
            new Vector2(350f, -505f));
        CreateStatCard(panel, "Stat_MoveSpeed", ShipUpgradeType.MoveSpeed,
            new Vector2(350f, -705f));

        CreateImage(panel, "Ship_Showcase", "ShipArt",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -610f), new Vector2(352f, 414f));

        Transform details = CreateDetailsPanel(panel);
        for (int i = 0; i < UpgradeTypes.Length; i++)
            CreateUpgradeRow(details, UpgradeTypes[i], i);

        EditorUtility.SetDirty(panel.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[Upgrade UI] Built the upgrade layout in Upgrade_Panel.");
    }

    private static void PrepareSprites()
    {
        string[] names =
        {
            "TitleUpgrade", "MenuButton", "ShipShowcase", "ShipArt", "StatFirepower", "StatHP",
            "StatBullets", "StatShield", "DetailRowDamage", "DetailRowFireRate", "DetailRowMaxHP",
            "DetailRowShield", "IconDamage", "IconFireRate", "IconMaxHP", "IconShield",
            "UpgradeButtonSmall", "UpgradeButtonBlank", "DetailsFrameOnly", "UpgradeRowFrameClean",
            "UpgradeRowFrameCleanTight", "UpgradeButtonFrameClean", "UpgradeStatCardFrameClean"
        };

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
            if (names[i] == "UpgradeRowFrameCleanTight")
                importer.spriteBorder = Vector4.zero;
            else if (names[i] == "UpgradeButtonFrameClean")
                importer.spriteBorder = Vector4.zero;
            else if (names[i] == "UpgradeStatCardFrameClean")
                importer.spriteBorder = Vector4.zero;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
    }

    private static void CreateStatCard(Transform panel, string objectName,
        ShipUpgradeType type, Vector2 position)
    {
        Image card = CreateImage(panel, objectName, "UpgradeStatCardFrameClean",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            position, new Vector2(326f, 166f));
        card.type = Image.Type.Simple;
        card.preserveAspect = true;
        RectTransform rect = card.rectTransform;

        Image icon = CreateImage(rect, "Stat_Icon", GetRowIcon(type),
            new Vector2(0.045f, 0.13f), new Vector2(0.35f, 0.87f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        icon.preserveAspect = true;

        TextMeshProUGUI title = CreateText(rect, "Title", GetTitle(type), 22f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform,
            new Vector2(0.4f, 0.64f), new Vector2(0.83f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI level = CreateText(rect, "Level", "Lv.0", 25f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        level.fontStyle = FontStyles.Bold;
        SetRect(level.rectTransform,
            new Vector2(0.4f, 0.34f), new Vector2(0.82f, 0.68f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI value = CreateText(rect, "Value", "", 18f,
            TextAlignmentOptions.MidlineLeft, new Color32(65, 226, 255, 255));
        value.fontStyle = FontStyles.Bold;
        SetRect(value.rectTransform,
            new Vector2(0.4f, 0.08f), new Vector2(0.82f, 0.39f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI arrow = CreateText(rect, "Arrow", "↑", 30f,
            TextAlignmentOptions.Center, new Color32(72, 255, 139, 255));
        arrow.fontStyle = FontStyles.Bold;
        SetRect(arrow.rectTransform,
            new Vector2(0.84f, 0.28f), new Vector2(0.98f, 0.77f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    private static Transform CreateDetailsPanel(Transform panel)
    {
        Image frame = CreateImage(panel, "Upgrade_Details_Panel", "DetailsFrameOnly",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -910f), new Vector2(1030f, 740f));
        frame.type = Image.Type.Simple;
        frame.preserveAspect = false;

        RectTransform rowsRoot = CreateRectTransform(frame.transform, "Upgrade_Rows",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rowsRoot.SetAsLastSibling();
        return rowsRoot;
    }

    private static void CreateUpgradeRow(Transform rowsRoot, ShipUpgradeType type, int index)
    {
        Image row = CreateImage(rowsRoot, "UpgradeRow_" + type, "UpgradeRowFrameCleanTight",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -190f - index * 135f), new Vector2(940f, 130f));
        row.type = Image.Type.Simple;
        row.preserveAspect = false;

        Image icon = CreateImage(row.transform, "Stat_Icon", GetRowIcon(type),
            new Vector2(0.045f, 0.13f), new Vector2(0.145f, 0.87f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        icon.preserveAspect = true;

        RectTransform dividerRect = CreateRectTransform(row.transform, "Values_Divider",
            new Vector2(0.51f, 0.14f), new Vector2(0.515f, 0.86f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image divider = dividerRect.gameObject.AddComponent<Image>();
        divider.sprite = BuiltinUiSprite();
        divider.color = new Color32(0, 148, 226, 210);
        divider.raycastTarget = false;

        TextMeshProUGUI name = CreateText(row.transform, "Name", GetTitle(type), 24f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform,
            new Vector2(0.17f, 0.53f), new Vector2(0.49f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI description = CreateText(row.transform, "Description", GetDescription(type), 17f,
            TextAlignmentOptions.MidlineLeft, new Color32(203, 226, 255, 255));
        SetRect(description.rectTransform,
            new Vector2(0.17f, 0.1f), new Vector2(0.49f, 0.52f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI currentLevel = CreateText(row.transform, "CurrentLevel", "Lv.0", 21f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        currentLevel.fontStyle = FontStyles.Bold;
        SetRect(currentLevel.rectTransform,
            new Vector2(0.53f, 0.55f), new Vector2(0.65f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI arrow = CreateText(row.transform, "NextArrow", "›", 26f,
            TextAlignmentOptions.Center, Color.white);
        SetRect(arrow.rectTransform,
            new Vector2(0.647f, 0.55f), new Vector2(0.68f, 0.94f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI nextLevel = CreateText(row.transform, "NextLevel", "Lv.1", 21f,
            TextAlignmentOptions.MidlineLeft, new Color32(255, 218, 85, 255));
        nextLevel.fontStyle = FontStyles.Bold;
        SetRect(nextLevel.rectTransform,
            new Vector2(0.68f, 0.55f), new Vector2(0.77f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI currentValue = CreateText(row.transform, "CurrentValue", "", 21f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        SetRect(currentValue.rectTransform,
            new Vector2(0.53f, 0.08f), new Vector2(0.65f, 0.51f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI nextValue = CreateText(row.transform, "NextValue", "", 21f,
            TextAlignmentOptions.MidlineLeft, new Color32(0, 255, 176, 255));
        nextValue.fontStyle = FontStyles.Bold;
        SetRect(nextValue.rectTransform,
            new Vector2(0.68f, 0.08f), new Vector2(0.77f, 0.51f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        RectTransform buttonRect = CreateRectTransform(row.transform, "UpgradeButton",
            new Vector2(0.78f, 0.26f), new Vector2(0.99f, 0.74f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.sprite = LoadSprite("UpgradeButtonFrameClean");
        buttonImage.type = Image.Type.Simple;
        buttonImage.fillCenter = true;
        buttonImage.preserveAspect = true;
        buttonImage.raycastTarget = true;
        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;

        Image coinIcon = CreateImage(buttonRect, "Coin_Icon", "Assets/Sprites/coin.png",
            new Vector2(0.13f, 0.56f), new Vector2(0.34f, 0.94f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        coinIcon.preserveAspect = true;

        TextMeshProUGUI cost = CreateText(buttonRect, "Cost", "0", 22f,
            TextAlignmentOptions.MidlineLeft, new Color32(45, 22, 0, 255));
        cost.fontStyle = FontStyles.Bold;
        SetRect(cost.rectTransform,
            new Vector2(0.35f, 0.56f), new Vector2(0.88f, 0.94f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);

        TextMeshProUGUI buttonLabel = CreateText(buttonRect, "Button_Label", "UPGRADE", 19f,
            TextAlignmentOptions.Center, Color.white);
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.outlineWidth = 0.18f;
        buttonLabel.outlineColor = new Color32(112, 23, 5, 255);
        SetRect(buttonLabel.rectTransform,
            new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.49f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        ShipUpgradeType capturedType = type;
        button.onClick.AddListener(() =>
        {
            UpgradePanelUI controller = rowsRoot.GetComponentInParent<UpgradePanelUI>();
            if (controller != null)
                controller.OnUpgradeButtonClicked(capturedType);
        });
    }

    private static Image CreateImage(Transform parent, string name, string spriteName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRectTransform(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(spriteName);
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
        Undo.RegisterCreatedObjectUndo(go, "Create Upgrade UI Element");
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
        string path = name.StartsWith("Assets/", StringComparison.Ordinal)
            ? name
            : SpriteFolder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError("[Upgrade UI] Missing sprite: " + path);
        return sprite;
    }

    private static Sprite BuiltinUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static string GetRowSprite(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "DetailRowDamage";
            case ShipUpgradeType.FireRate: return "DetailRowFireRate";
            case ShipUpgradeType.MaxHealth: return "DetailRowMaxHP";
            default: return "DetailRowShield";
        }
    }

    private static string GetRowIcon(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "IconDamage";
            case ShipUpgradeType.FireRate: return "IconFireRate";
            case ShipUpgradeType.MaxHealth: return "IconMaxHP";
            default: return "IconFireRate";
        }
    }

    private static string GetTitle(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "FIREPOWER";
            case ShipUpgradeType.FireRate: return "FIRE RATE";
            case ShipUpgradeType.MaxHealth: return "MAX HP";
            default: return "MOVE SPEED";
        }
    }

    private static string GetDescription(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "Increase bullet damage";
            case ShipUpgradeType.FireRate: return "Reduce time between shots";
            case ShipUpgradeType.MaxHealth: return "Increase maximum health";
            default: return "Increase ship movement speed";
        }
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
