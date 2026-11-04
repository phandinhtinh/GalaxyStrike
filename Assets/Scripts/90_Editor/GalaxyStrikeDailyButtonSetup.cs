#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Assigns the Galaxy Strike themed Daily icon to the HomeScene sidebar button.</summary>
public static class GalaxyStrikeDailyButtonSetup
{
    private const string IconPath = "Assets/Sprites/UI/HomePanel/DailyButton.png";

    [MenuItem("Tools/GalaxyStrike/Setup Daily Home Button")]
    private static void SetupDailyButton()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "HomeScene")
        {
            Debug.LogError("[Daily UI] Open HomeScene before setting up the Daily button.");
            return;
        }

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[Daily UI] Could not import Daily icon at: " + IconPath);
            return;
        }

        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !importer.alphaIsTransparency)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        GameObject buttonObject = GameObject.Find("7-DayTopRace");
        if (buttonObject == null)
            buttonObject = GameObject.Find("Daily");

        if (buttonObject == null)
        {
            Debug.LogError("[Daily UI] Could not find the 7-DayTopRace button in HomeScene.");
            return;
        }

        Sprite dailySprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        Image image = buttonObject.GetComponent<Image>();
        if (dailySprite == null || image == null)
        {
            Debug.LogError("[Daily UI] Daily sprite or button Image component is missing.");
            return;
        }

        buttonObject.name = "Daily";
        image.sprite = dailySprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "Daily";
            label.gameObject.SetActive(false);
            EditorUtility.SetDirty(label);
        }

        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(buttonObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Daily UI] Replaced the 7-DayTopRace icon with the Daily button in HomeScene.");
    }
}
#endif
