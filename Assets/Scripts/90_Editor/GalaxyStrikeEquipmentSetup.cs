#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gắn PlayerEquipment lên Player trong GameScene và gán prefab Rocket nếu có.
///
/// Cách dùng: menu "Galaxy Strike/Giai đoạn 2 - Gắn PlayerEquipment".
/// An toàn khi bấm nhiều lần.
/// </summary>
public static class GalaxyStrikeEquipmentSetup
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string RocketPrefabPath = "Assets/Prefabs/Player_HomingRocket.prefab";

[MenuItem("Galaxy Strike/Phase 2 - Attach PlayerEquipment", priority = 5)]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Galaxy Strike", "Hãy thoát Play Mode trước khi thiết lập.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        GameObject player = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].CompareTag("Player"))
            {
                player = roots[i];
                break;
            }
        }

        if (player == null)
        {
            Debug.LogError("[GalaxyStrike] Không tìm thấy GameObject có tag 'Player' trong GameScene.");
            return;
        }

        bool added = false;
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        if (equipment == null)
        {
            equipment = Undo.AddComponent<PlayerEquipment>(player);
            added = true;
        }

        GameObject rocket = AssetDatabase.LoadAssetAtPath<GameObject>(RocketPrefabPath);
        if (rocket != null)
        {
            var serialized = new SerializedObject(equipment);
            SerializedProperty prop = serialized.FindProperty("rocketPrefab");
            if (prop != null)
            {
                prop.objectReferenceValue = rocket;
                serialized.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GalaxyStrike] PlayerEquipment {(added ? "đã thêm" : "đã có sẵn")} trên Player.\n" +
            $"- Rocket prefab: {(rocket != null ? "đã gán" : "CHƯA CÓ (chạy menu Tạo Homing Rocket Prefab trước)")}");
    }
}
#endif
