#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalaxyStrike.UI.Home;
using GalaxyStrike.Data;

/// <summary>
/// Thiết lập HomeScene cho Giai đoạn 2 (Shop + Save).
///
/// Vì sao cần tool: script mới chưa có .meta trước lần import đầu, và gán tay GUID
/// trong file .unity rất dễ hỏng. Tool này để Unity tự serialize nên GUID luôn khớp.
///
/// Cách dùng: menu "Galaxy Strike/Giai đoạn 2 - Thiết lập HomeScene".
/// An toàn khi bấm nhiều lần.
/// </summary>
public static class GalaxyStrikePhase2Setup
{
    private const string HomeScenePath = "Assets/Scenes/HomeScene.unity";
    private const string UpgradePanelObjectName = "Upgrade_Panel";
    private const string ShipDataFolder = "Assets/ScriptableObject/ShipData";

[MenuItem("Galaxy Strike/Phase 2 - Setup HomeScene", priority = 1)]
    public static void SetupHomeScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Galaxy Strike", "Hãy thoát Play Mode trước khi thiết lập.", "OK");
            return;
        }

        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path != HomeScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            active = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        GameObject upgradePanel = FindObjectInScene(active, UpgradePanelObjectName);
        if (upgradePanel == null)
        {
            Debug.LogError($"[GalaxyStrike] Không tìm thấy '{UpgradePanelObjectName}' trong {HomeScenePath}.");
            return;
        }

        bool added = false;
        UpgradePanelUI panel = upgradePanel.GetComponent<UpgradePanelUI>();
        if (panel == null)
        {
            panel = Undo.AddComponent<UpgradePanelUI>(upgradePanel);
            added = true;
        }

        // Gán danh sách 8 ShipDataSO để panel hiển thị đúng chỉ số gốc.
        ShipDataSO[] ships = LoadShipData();
        if (ships != null && ships.Length > 0)
        {
            var serialized = new SerializedObject(panel);
            SerializedProperty arrayProperty = serialized.FindProperty("allShipsData");
            if (arrayProperty != null)
            {
                arrayProperty.arraySize = ships.Length;
                for (int i = 0; i < ships.Length; i++)
                    arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = ships[i];

                serialized.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GalaxyStrike] Giai đoạn 2: UpgradePanelUI {(added ? "đã thêm" : "đã có sẵn")} trên '{UpgradePanelObjectName}', " +
            $"{ships?.Length ?? 0} ShipDataSO đã gán.");
    }

    private static ShipDataSO[] LoadShipData()
    {
        string[] guids = AssetDatabase.FindAssets("t:ShipDataSO", new[] { ShipDataFolder });
        if (guids == null || guids.Length == 0)
            return null;

        var ships = new ShipDataSO[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ships[i] = AssetDatabase.LoadAssetAtPath<ShipDataSO>(path);
        }

        return ships;
    }

    private static GameObject FindObjectInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                if (children[j].gameObject.name == objectName)
                    return children[j].gameObject;
            }
        }

        return null;
    }
}
#endif
