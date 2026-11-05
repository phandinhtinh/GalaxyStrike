using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pool tuỳ chỉnh theo prefab (PATTERN: Object Pool).
///
/// Mục tiêu: đạn/tên lửa/effect bắn ra mỗi frame hiện đang
/// <c>Instantiate</c> + <c>Destroy</c> -> sinh rác GC và giật khung trên mobile.
/// Pool giữ sẵn một số lượng và tái sử dụng.
///
/// Đặc điểm:
///  - Không phụ thuộc Unity built-in pool (Unity 2021+ chỉ có <c>ObjectPool&lt;T&gt;</c> chung),
///    nên quản lý được "còn sống / đang rảnh" và tự scale khi thiếu.
///  - Tự dọn mỗi khi chuyển Scene để không giữ object chết giữa HomeScene / GameScene.
///
/// A singleton only per-scene: nó KHÔNG DontDestroyOnLoad.
/// </summary>
public sealed class BulletPool : MonoBehaviour
{
    private static BulletPool instance;

    /// <summary>Đã có pool trong Scene chưa (dùng để tránh tạo pool lúc scene đang đóng).</summary>
    public static bool HasInstanceAvailable
    {
        get
        {
            if (instance != null)
                return true;

            instance = FindObjectOfType<BulletPool>();
            return instance != null;
        }
    }

    /// <summary>Instance của Scene hiện tại. Gameplay gọi qua đây; không có thì tự Instantiate.</summary>
    public static BulletPool Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = FindObjectOfType<BulletPool>();
            if (instance == null)
                instance = new GameObject("[BulletPool]").AddComponent<BulletPool>();

            return instance;
        }
    }

    [Header("--- Cấu hình ---")]
    [Tooltip("Số object giữ sẵn cho mỗi prefab khi mở pool (luôn >= số này).")]
    [SerializeField, Min(0)] private int prewarmCount = 24;

    [Tooltip("Số object tối đa được phép tồn tại mỗi prefab. 0 = không giới hạn.")]
    [SerializeField, Min(0)] private int maxSizePerPrefab = 256;

    [Tooltip("Giới hạn cứng tổng số object trong pool (chống tràn bộ nhớ nếu prefab bị gọi sai).")]
    [SerializeField, Min(16)] private int hardCapTotalObjects = 1024;

    private sealed class PrefabPool
    {
        /// <summary>Prefab gốc dùng để Instantiate thêm object cho pool này.</summary>
        public GameObject prefab;

        /// <summary>Ngăn xếp các object đang rảnh, sẵn sàng được tái sử dụng.</summary>
        public readonly Stack<GameObject> idle = new Stack<GameObject>(32);

        /// <summary>Tổng số object đã tạo cho prefab này (đã tạo - chưa huỷ).</summary>
        public int totalCreated;
    }

    /// <summary>Danh sách pool theo từng prefab (mỗi prefab có một stack object rảnh riêng).</summary>
    private readonly Dictionary<GameObject, PrefabPool> pools = new Dictionary<GameObject, PrefabPool>(8);

    /// <summary>Object cha chứa mọi object trong pool, để Hierarchy gọn và dễ Disable khi cần.</summary>
    private Transform poolRoot;

    /// <summary>Tổng số object đang tồn tại trong TẤT CẢ các pool (để chặn trần cứng).</summary>
    private int totalObjects;

    /// <summary>
    /// Khởi tạo Singleton của pool và tạo object cha "Pooled" để chứa các object được tái sử dụng.
    /// Bản trùng lặp (mỗi Scene có thể có 1 pool) bị huỷ component.
    /// </summary>
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        poolRoot = new GameObject("Pooled").transform;
        poolRoot.SetParent(transform, false);
    }

    /// <summary>
    /// Đăng ký sự kiện sceneUnloaded để dọn dữ liệu pool khi chuyển Scene.
    /// </summary>
    private void Start()
    {
        // Object trong pool gắn với Scene tạo ra pool. Khi pool là object được đặt tay trong
        // GameScene thì không cần dọn; khi pool tự sinh ở scene phụ thì sẽ tự huỷ cùng scene.
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    /// <summary>
    /// Callback khi một Scene bị gỡ: xoá các prefab key đã "chết" (Unity đã huỷ object)
    /// khỏi từ điển, tránh giữ tham chiếu null làm rác bộ nhớ.
    /// </summary>
    private void HandleSceneUnloaded(Scene scene)
    {
        // Xoá bản ghi prefab nếu prefab gốc thuộc Scene vừa unload (tránh giữ key null).
        if (pools.Count == 0)
            return;

        var deadKeys = new List<GameObject>(pools.Count);
        foreach (var pair in pools)
        {
            if (pair.Key == null)
                deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
            pools.Remove(deadKeys[i]);
    }

    /// <summary>
    /// Dọn Singleton + huỷ đăng ký sự kiện sceneUnloaded khi pool bị huỷ.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    // =====================================================================
    // API
    // =====================================================================
    /// <summary>Lấy một object từ pool (tự Instantiate nếu pool chưa có / đang cạn).</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        PrefabPool pool = GetOrCreatePool(prefab);

        GameObject go = null;
        while (pool.idle.Count > 0 && go == null)
        {
            // Object có thể đã bị huỷ từ ngoài -> bỏ qua bản ghi rác.
            go = pool.idle.Pop();
        }

        if (go == null)
        {
            if (maxSizePerPrefab > 0 && pool.totalCreated >= maxSizePerPrefab)
            {
                // Chạm trần: tái sử dụng bằng cách tự huỷ bộ đếm thay vì nổ bộ nhớ.
                Debug.LogWarning($"[BulletPool] '{prefab.name}' chạm maxSize={maxSizePerPrefab}; bỏ qua spawn.");
                return null;
            }

            if (totalObjects >= hardCapTotalObjects)
            {
                Debug.LogWarning($"[BulletPool] Chạm trần tổng {hardCapTotalObjects} object; bỏ qua spawn.");
                return null;
            }

            go = Instantiate(prefab, position, rotation, poolRoot);
            go.name = prefab.name;
            PooledHandle handle = go.GetComponent<PooledHandle>();
            if (handle == null)
                handle = go.AddComponent<PooledHandle>();
            handle.Owner = pool;
            pool.totalCreated++;
            totalObjects++;
        }

        Transform t = go.transform;
        t.SetParent(poolRoot, false);
        t.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        if (go.TryGetComponent(out IPoolable p))
            p.OnSpawned();

        return go;
    }

    /// <summary>Trả object về pool. Gọi thay cho <c>Destroy</c>.</summary>
    public void Despawn(GameObject go)
    {
        if (go == null || !go.activeSelf)
            return;

        if (go.TryGetComponent(out IPoolable p))
            p.OnDespawned();

        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);

        if (go.TryGetComponent(out PooledHandle handle) && handle.Owner != null)
            handle.Owner.idle.Push(go);
        else
            Destroy(go); // Không truy được nguồn gốc -> tránh rò rỉ.
    }

    // =====================================================================
    // NỘI BỘ
    // =====================================================================

    /// <summary>
    /// Lấy pool của prefab; chưa có thì tạo mới và nạp sẵn (prewarm) vài object rảnh.
    /// Đạn của quái (EnemyBullet) được giữ lượng dự trữ nhỏ hơn để tiết kiệm bộ nhớ
    /// vì có nhiều biến thể đạn khác nhau, sau đó tự tăng khi cần.
    /// </summary>
    private PrefabPool GetOrCreatePool(GameObject prefab)
    {
        // A large arsenal needs a small initial reserve per hostile variant.
        // Grow on demand instead of reserving 24 objects for every artwork variant.
        int reserve = prefab.GetComponent<EnemyBullet>() != null ? Mathf.Min(prewarmCount, 8) : prewarmCount;
        if (pools.TryGetValue(prefab, out PrefabPool pool))
        {
            if (pool.idle.Count == 0 && pool.totalCreated < reserve)
                Grow(pool, reserve - pool.totalCreated);
            return pool;
        }

        pool = new PrefabPool { prefab = prefab };
        pools[prefab] = pool;
        Grow(pool, reserve);
        return pool;
    }

    /// <summary>
    /// Tạo thêm tối đa <paramref name="amount"/> object cho pool (đang tắt sẵn),
    /// dừng lại nếu chạm trần mỗi prefab hoặc trần tổng.
    /// </summary>
    private void Grow(PrefabPool pool, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (maxSizePerPrefab > 0 && pool.totalCreated >= maxSizePerPrefab)
                return;
            if (totalObjects >= hardCapTotalObjects)
                return;

            GameObject go = Instantiate(pool.prefab, poolRoot);
            go.name = pool.prefab.name;
            go.SetActive(false);

            PooledHandle handle = go.GetComponent<PooledHandle>();
            if (handle == null)
                handle = go.AddComponent<PooledHandle>();
            handle.Owner = pool;

            pool.idle.Push(go);
            pool.totalCreated++;
            totalObjects++;
        }
    }

    /// <summary>Backlink object -> pool gốc, để Despawn biết trả về đâu.</summary>
    private sealed class PooledHandle : MonoBehaviour
    {
        public PrefabPool Owner;
    }
}
