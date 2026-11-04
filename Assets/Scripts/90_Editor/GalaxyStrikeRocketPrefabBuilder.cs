#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo prefab tên lửa tự tìm mục tiêu cho trang bị HOMING ROCKET.
///
/// Cách dùng: menu "Galaxy Strike/Giai đoạn 2 - Tạo Homing Rocket Prefab".
/// An toàn khi bấm nhiều lần (bỏ qua nếu prefab đã tồn tại).
/// </summary>
public static class GalaxyStrikeRocketPrefabBuilder
{
    private const string SourceBulletPath = "Assets/Prefabs/Nova_Bullet.prefab";
    private const string TargetPath = "Assets/Prefabs/Player_HomingRocket.prefab";

[MenuItem("Galaxy Strike/Phase 2 - Create Homing Rocket Prefab", priority = 4)]
    public static void CreateRocketPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) != null)
        {
            Debug.Log($"[GalaxyStrike] '{TargetPath}' đã tồn tại, bỏ qua.");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceBulletPath);
        if (source == null)
        {
            Debug.LogError($"[GalaxyStrike] Không tìm thấy '{SourceBulletPath}'.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

        // Bỏ script Bullet cũ, thay bằng HomingProjectile.
        var bullet = instance.GetComponent<Bullet>();
        if (bullet != null)
            Object.DestroyImmediate(bullet);

        var homing = instance.GetComponent<HomingProjectile>();
        if (homing == null)
            instance.AddComponent<HomingProjectile>();

        // Tên lửa nhỏ hơn đạn thường một chút.
        instance.transform.localScale = Vector3.one * 0.8f;

        PrefabUtility.SaveAsPrefabAsset(instance, TargetPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GalaxyStrike] Đã tạo '{TargetPath}'.\n" +
                  "Gán prefab này vào field 'Rocket Prefab' của PlayerEquipment trên Player trong GameScene.");
    }
}
#endif
