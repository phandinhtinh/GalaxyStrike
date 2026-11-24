using UnityEngine;

/// <summary>
/// Base class cho các manager dạng Singleton (PATTERN: Singleton).
///
/// Chỉ dùng cho service toàn cục thật sự (Save, Audio, Cấp cứu scene). Singleton
/// tạo ra phụ thuộc ẩn — hạn chế dùng cho gameplay object.
///
/// Hướng dẫn migrate: kế thừa class này và bỏ phần tự viết
/// <c>private static X instance / Instance / Awake / OnDestroy</c>:
/// <code>
/// public sealed class FooService : SingletonBehaviour&lt;FooService&gt; { ... }
/// </code>
/// Các chỗ gọi cũ <c>FooService.Instance</c> giữ nguyên, không phải sửa.
/// </summary>
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    private static T _instance;
    private static bool _isQuitting;

    /// <summary>
    /// Instance đang sống. KHÔNG tự tạo trong getter.
    /// - Service toàn cục tự sinh bởi <see cref="GameBootstrap"/> (thứ tự khởi tạo xác định).
    /// - Singleton gắn trong Scene getter chỉ tìm object có sẵn (trả null nếu Scene chưa có).
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance != null || _isQuitting)
                return _instance;

            // Fallback: tìm trong scene (trường hợp object được đặt tay trong scene).
            _instance = FindFirstObjectByType<T>();
            return _instance;
        }
    }

    /// <summary>True nếu singleton đã có instance (dùng để kiểm tra trước khi gọi Instance).</summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// Tạo instance nếu chưa có (dùng cho service toàn cục cần tồn tại ở mọi Scene).
    /// Khác với getter <see cref="Instance"/>: hàm này CHỦ ĐỘNG tạo object mới.
    /// </summary>
    public static T CreateIfMissing()
    {
        if (_instance != null)
            return _instance;

        T found = FindFirstObjectByType<T>();
        if (found != null)
            return found;

        return new GameObject($"[{typeof(T).Name}]").AddComponent<T>();
    }

    /// <summary>Bật để singleton sống qua các Scene (mặc định: bật cho service).</summary>
    protected virtual bool PersistAcrossScenes => true;

    /// <summary>
    /// Ghi cảnh báo khi huỷ bản trùng. Tắt ở lớp con mà bản trùng là chuyện bình thường
    /// (ví dụ AudioManager: bản trong Scene chỉ để mang cấu hình sang bản đang sống).
    /// </summary>
    protected virtual bool WarnOnDuplicate => true;

    /// <summary>
    /// Đăng ký instance, tự huỷ bản trùng, giữ object qua Scene (nếu PersistAcrossScenes)
    /// rồi gọi hook OnSingletonAwake cho lớp con. Lớp con KHÔNG cần override Awake,
    /// chỉ cần override OnSingletonAwake.
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (WarnOnDuplicate)
                Debug.LogWarning($"[Singleton] Đã có {typeof(T).Name}, huỷ bản trùng trên '{name}'.");
            Destroy(gameObject);
            return;
        }

        _instance = (T)this;

        if (PersistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        OnSingletonAwake();
    }

    /// <summary>
    /// Khi object bị hủy: nếu đây vẫn là instance hiện hành thì xóa tham chiếu
    /// và gọi hook OnSingletonDestroyed cho lớp con.
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            OnSingletonDestroyed();
        }
    }

    /// <summary>
    /// Unity gọi khi thoát Play Mode: bật cờ _isQuitting để Instance không
    /// cố tìm/tạo object trong lúc Editor đang đóng (tránh cảnh báo/leak).
    /// </summary>
    private void OnApplicationQuit()
    {
        // Tránh tự tạo lại instance trong lúc Unity đang đóng.
        _isQuitting = true;
    }

    /// <summary>Hook thay cho Awake của lớp con.</summary>
    protected virtual void OnSingletonAwake() { }

    /// <summary>Hook thay cho OnDestroy của lớp con.</summary>
    protected virtual void OnSingletonDestroyed() { }

    /// <summary>Xoá tham chiếu tĩnh khi vào Play Mode (gọi từ GameBootstrap).</summary>
    public static void ResetStaticInstance()
    {
        _instance = null;
        _isQuitting = false;
    }
}
