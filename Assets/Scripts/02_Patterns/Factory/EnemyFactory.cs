using UnityEngine;

/// <summary>
/// Nơi duy nhất tạo ra quái (PATTERN: Factory Method).
///
/// Trước đây: <c>EnemySpawner</c> tự <c>Instantiate(prefab)</c> + <c>GetComponent&lt;Enemy&gt;()</c>
/// và tự cấu hình 8 tham số. Giờ Factory đóng gói toàn bộ việc đó:
///  - Tra prefab theo <see cref="EnemyKind"/> qua <see cref="EnemyRegistry"/>.
///  - Kiểm tra prefab có Enemy component hay không trước khi trả về.
///  - Cung cấp điểm mở rộng duy nhất cho pool quái / biến thể quái về sau.
///
/// Spawner chỉ cần gọi <see cref="Create"/>.
/// </summary>
public static class EnemyFactory
{
    /// <summary>Registry dùng chung. Lazy nạp từ Resources lần đầu truy cập.</summary>
    public static EnemyRegistry Registry { get; } = new EnemyRegistry();

    private static bool _registryInitialized;

    /// <summary>Nạp registry từ Resources/&lt;resourcesFolder&gt; đúng 1 lần (hoặc ép nạp lại).</summary>
    public static void EnsureRegistry(string resourcesFolder = "Prefabs")
    {
        if (_registryInitialized)
            return;

        int loaded = Registry.LoadDefaultsFromResources(resourcesFolder);
        _registryInitialized = true;

        if (loaded == 0)
        {
            Debug.LogWarning($"[EnemyFactory] Không nạp được quái nào từ Resources/{resourcesFolder}. " +
                             "Kiểm tra prefab có trong Assets/Resources/Prefabs.");
        }
    }


    /// <summary>
    /// Xóa toàn bộ registry và đặt lại cờ để lần gọi EnsureRegistry tiếp theo
    /// sẽ nạp lại từ Resources (dùng cho test hoặc khi đổi bộ prefab).
    /// </summary>
    public static void ResetRegistry()
    {
        Registry.Clear();
        _registryInitialized = false;
    }

    /// <summary>
    /// Tạo (chưa cấu hình) một con quái tại vị trí cho trước.
    /// Trả null nếu prefab không hợp lệ — caller phải kiểm tra.
    /// </summary>
    public static Enemy Create(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (prefab.GetComponent<Enemy>() == null)
        {
            Debug.LogError($"[EnemyFactory] Prefab '{prefab.name}' thiếu component Enemy.");
            return null;
        }

        // PATTERN: Object Pool — quái được tái sử dụng thay vì Instantiate/Destroy mỗi lần.
        // Không dựng sẵn (reserve 0) vì có nhiều biến thể quái; pool lớn dần theo nhu cầu.
        // Pool đầy thì vẫn Instantiate để wave không bao giờ thiếu quái.
        GameObject obj = BulletPool.Instance.Spawn(prefab, position, rotation, 0);
        if (obj == null)
            obj = Object.Instantiate(prefab, position, rotation);

        Enemy enemy = obj.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogError($"[EnemyFactory] Prefab '{prefab.name}' thiếu component Enemy. Đã huỷ object.");
            Object.Destroy(obj);
            return null;
        }

        return enemy;
    }

    /// <summary>
    /// Overload tiện dụng: tạo quái với rotation mặc định (không xoay).
    /// </summary>
    public static Enemy Create(GameObject prefab, Vector3 position)
    {
        return Create(prefab, position, Quaternion.identity);
    }

    /// <summary>Tạo theo loại quái đã đăng ký.</summary>
    public static Enemy Create(EnemyKind kind, Vector3 position)
    {
        EnsureRegistry();

        if (!Registry.TryGet(kind, out GameObject prefab))
        {
            Debug.LogWarning($"[EnemyFactory] Chưa đăng ký prefab cho {kind}.");
            return null;
        }

        return Create(prefab, position);
    }
}
