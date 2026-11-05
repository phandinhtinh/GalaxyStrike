using UnityEngine;

/// <summary>Pooled hostile projectile with independent visuals and forgiving hitbox.</summary>
public class EnemyBullet : MonoBehaviour, IPoolable
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float speed = 5f;
    [SerializeField, Min(0.1f)] private float lifetime = 6f;
    [SerializeField] private float acceleration;
    [Header("Limited missile tracking")]
    [SerializeField, Min(0f)] private float homingDuration;
    [SerializeField, Min(0f)] private float turnSpeed = 65f;
    [Header("Presentation")]
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed;
    private Vector3 direction = Vector3.down;
    private float age;
    private float currentSpeed;
    private bool alive;
    private Transform cachedTransform;
    private Transform target;
    private Camera gameplayCamera;
    private Rigidbody2D body;

    /// <summary>Cache Transform + Rigidbody2D để dùng lại trong FixedUpdate (không GetComponent mỗi frame).</summary>
    private void Awake() { cachedTransform = transform; body = GetComponent<Rigidbody2D>(); }

    /// <summary>
    /// Unity gọi mỗi khi object được bật lên (kể cả khi pool tái sử dụng object),
    /// nên gọi OnSpawned() để reset toàn bộ trạng thái cho lần bắn mới.
    /// </summary>
    private void OnEnable() { OnSpawned(); }

    /// <summary>
    /// Gán hướng bay cho viên đạn (đã chuẩn hoá), đồng thời xoay trục "up" của sprite
    /// theo hướng bay. Hướng không hợp lệ thì mặc định bắn xuống.
    /// </summary>
    public void SetDirection(Vector3 targetDirection)
    {
        direction = targetDirection.sqrMagnitude > 0.001f ? targetDirection.normalized : Vector3.down;
        cachedTransform.up = direction;
    }

    /// <summary>
    /// Reset khi lấy đạn từ pool: tuổi = 0, tốc độ về gốc, hướng mặc định xuống dưới,
    /// tìm player (nếu đạn có khả năng bám đuổi) và trả visual về góc xoay gốc.
    /// </summary>
    public void OnSpawned()
    {
        age = 0f; currentSpeed = speed; direction = Vector3.down; alive = true;
        gameplayCamera = Camera.main;
        target = null;
        if (homingDuration > 0f)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
        if (visual != null) visual.localRotation = Quaternion.identity;
    }
    /// <summary>Khi trả về pool: đánh dấu đã chết và xoá mục tiêu để không bay tiếp.</summary>
    public void OnDespawned() { alive = false; target = null; }

    /// <summary>
    /// Vật lý chạy theo FixedUpdate (đồng bộ với Rigidbody2D):
    /// trong homingDuration đầu thì bẻ lái dần về phía player, tăng tốc theo acceleration
    /// (giới hạn 0.5..12), di chuyển đạn; hết lifetime hoặc bay ra ngoài màn thì thu hồi về pool.
    /// </summary>
    private void FixedUpdate()
    {
        if (!alive) return;
        float dt = Time.fixedDeltaTime;
        age += dt;
        if (target != null && age < homingDuration)
        {
            Vector3 desired = (target.position - cachedTransform.position).normalized;
            direction = Vector3.RotateTowards(direction, desired, turnSpeed * Mathf.Deg2Rad * dt, 0f);
            cachedTransform.up = direction;
        }
        currentSpeed = Mathf.Clamp(currentSpeed + acceleration * dt, 0.5f, 12f);
        Vector3 next = cachedTransform.position + direction * (currentSpeed * dt);
        if (body != null) body.MovePosition(next);
        else cachedTransform.position = next;
        if (age >= lifetime) { Recycle(); return; }
        if (age > 0.5f && gameplayCamera != null)
        {
            Vector3 p = gameplayCamera.WorldToViewportPoint(next);
            if (p.x < -0.18f || p.x > 1.18f || p.y < -0.18f || p.y > 1.18f) Recycle();
        }
    }
    /// <summary>
    /// Hiệu ứng trang trí mỗi frame: xoay phần visual của viên đạn (không đổi
    /// hướng bay/chỉnh va chạm).
    /// </summary>
    private void Update()
    {
        if (alive && visual != null && spinSpeed != 0f) visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Trả đạn về BulletPool (không Destroy) để tái sử dụng; pool không tồn tại thì huỷ hẳn.
    /// </summary>
    private void Recycle()
    {
        if (!alive) return;
        alive = false;
        if (BulletPool.HasInstanceAvailable) BulletPool.Instance.Despawn(gameObject);
        else Destroy(gameObject);
    }
}
