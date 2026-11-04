#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GalaxyStrike.UI.Home;

/// <summary>Builds the HomeScene bottom navigation from the existing UI sprite assets.</summary>
public static class GalaxyStrikeBottomNavigationSetup
{
    private const string ScenePath = "Assets/Scenes/HomeScene.unity";
    private const string UiPath = "Assets/Sprites/UI/";

    [MenuItem("Tools/Galaxy Strike/Build Bottom Navigation")]
    private static void BuildBottomNavigation()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject barObject = GameObject.Find("Bottom_Navigation_Bar");
        if (barObject == null)
        {
            Debug.LogError("[GalaxyStrike] Bottom_Navigation_Bar was not found in HomeScene.");
            return;
        }

        Sprite panelSprite = LoadSprite("Neon Blue Sci-Fi UI Panel.png");
        Sprite tabSprite = LoadSprite("Tab.png");
        Sprite[] icons =
        {
            LoadSprite("Neon Galactic Space Shop Icon.png"),
            LoadSprite("upgrade.png"),
            LoadSprite("home.png"),
            LoadSprite("map.png"),
            LoadSprite("leaderboard.png")
        };

        string[] names = { "Shop", "Upgrade", "Home", "Map", "Leaderboard" };
        string[] labels = { "SHOP", "UPGRADE", "HOME", "MAP", "RANKINGS" };
        Button[] buttons =
        {
            FindButton("Shop_NavBtn"),
            FindButton("Upgrade_NavBtn"),
            FindButton("Home_NavBtn"),
            FindButton("Map_NavBtn"),
            FindButton("Leader_NavBtn")
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || icons[i] == null || tabSprite == null)
            {
                Debug.LogError($"[GalaxyStrike] Missing navigation button or sprite for {names[i]}.");
                return;
            }
        }

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 280f);

        Image barImage = barObject.GetComponent<Image>();
        if (barImage == null)
            barImage = barObject.AddComponent<Image>();
        barImage.sprite = panelSprite;
        barImage.type = Image.Type.Simple;
        barImage.preserveAspect = false;
        barImage.color = new Color(0.82f, 0.90f, 1f, 0.86f);
        barImage.raycastTarget = false;

        HorizontalLayoutGroup layout = barObject.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            Object.DestroyImmediate(layout);

        for (int i = 0; i < buttons.Length; i++)
            ConfigureButton(buttons[i], icons[i], labels[i], tabSprite, i == 2, i);

        HomeSceneManager manager = GameObject.Find("HomeSceneManager").GetComponent<HomeSceneManager>();
        SerializedObject managerData = new SerializedObject(manager);
        SerializedProperty selectedScale = managerData.FindProperty("navSelectedScale");
        if (selectedScale != null)
            selectedScale.floatValue = 1.12f;
        managerData.ApplyModifiedPropertiesWithoutUndo();

        LayoutRebuilder.ForceRebuildLayoutImmediate(barRect);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GalaxyStrike] Bottom navigation built and saved in HomeScene.");
    }

    private static void ConfigureButton(Button button, Sprite iconSprite, string labelText, Sprite tabSprite, bool selected, int index)
    {
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        float centerX = (index + 0.5f) / 5f;
        buttonRect.anchorMin = new Vector2(centerX, 0f);
        buttonRect.anchorMax = new Vector2(centerX, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(216f, 280f);
        buttonRect.localScale = Vector3.one * (selected ? 1.12f : 1f);

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement != null)
            Object.DestroyImmediate(layoutElement);

        Image selectionCard = button.image;
        Sprite hitAreaSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (hitAreaSprite != null)
            selectionCard.sprite = hitAreaSprite;
        selectionCard.type = Image.Type.Sliced;
        selectionCard.preserveAspect = false;
        selectionCard.color = new Color(1f, 1f, 1f, 0f);
        selectionCard.raycastTarget = true;
        button.targetGraphic = selectionCard;
        button.transition = Selectable.Transition.None;

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        Transform tabBackgroundTransform = button.transform.Find("Tab_Background");
        if (tabBackgroundTransform == null)
        {
            GameObject backgroundObject = new GameObject("Tab_Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tabBackgroundTransform = backgroundObject.transform;
            tabBackgroundTransform.SetParent(button.transform, false);
        }

        RectTransform tabBackgroundRect = (RectTransform)tabBackgroundTransform;
        tabBackgroundRect.anchorMin = new Vector2(0.5f, 0f);
        tabBackgroundRect.anchorMax = new Vector2(0.5f, 0f);
        tabBackgroundRect.pivot = new Vector2(0.5f, 0f);
        tabBackgroundRect.anchoredPosition = Vector2.zero;
        tabBackgroundRect.sizeDelta = new Vector2(216f, 330f);

        Image tabBackground = tabBackgroundTransform.GetComponent<Image>();
        tabBackground.sprite = tabSprite;
        tabBackground.type = Image.Type.Simple;
        tabBackground.preserveAspect = false;
        tabBackground.color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
        tabBackground.raycastTarget = false;
        tabBackgroundTransform.SetAsFirstSibling();

        Transform iconTransform = button.transform.Find("Icon");
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconTransform = iconObject.transform;
            iconTransform.SetParent(button.transform, false);
        }

        RectTransform iconRect = (RectTransform)iconTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -56f);
        iconRect.sizeDelta = labelText == "SHOP" ? new Vector2(160f, 120f) : new Vector2(140f, 140f);

        Image icon = iconTransform.GetComponent<Image>();
        icon.sprite = iconSprite;
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.color = selected ? Color.white : new Color(0.82f, 0.90f, 1f, 0.88f);
        icon.raycastTarget = false;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 20f);
            labelRect.sizeDelta = new Vector2(-20f, 40f);

            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20f;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 20f;
            label.enableAutoSizing = true;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Truncate;
            label.fontStyle = FontStyles.Bold;
            label.color = selected ? Color.white : new Color(0.72f, 0.82f, 0.94f, 0f);
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
        }

        EditorUtility.SetDirty(button.gameObject);
    }

    private static Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private static Sprite LoadSprite(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + assetName);
    }
}
#endif
