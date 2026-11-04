using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Đạn của phi thuyền người chơi.
/// Di chuyển thẳng theo trục Y (hoặc lệch trục X cho đạn xoè quạt), tự huỷ khi hết vòng đời
/// hoặc khi trúng quái (Tag "Enemy").
///
/// PATTERN: Object Pool — implement <see cref="IPoolable"/>, KHÔNG Destroy mà trả về
/// <see cref="BulletPool"/> để tránh sinh rác GC mỗi phát bắn.
/// </summary>
public class Bullet : MonoBehaviour, IPoolable
{
    [Header("Movement")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifetime = 3f;

    [Tooltip("Tốc độ di chuyển theo trục X (đạn bắn xoè cánh quạt cần lệch trục X)")]
    [SerializeField] private float horizontalSpeed = 0f;

    [Header("Combat")]
    [Tooltip("Sát thương gây lên quái (Giai đoạn 2 sẽ nhân với chỉ số nâng cấp tàu)")]
    [SerializeField] private int damage = 1;

    [Header("Sự kiện tuỳ chọn (VFX/Juiciness)")]
    [Tooltip("Phát khi đạn trúng quái - Giai đoạn 3 dùng để bắn tia lửa va chạm")]
    [SerializeField] private UnityEvent onHitEnemy;

    [SerializeField] private int pierceRemaining;

    private float age;
    private bool isAlive = true;
    private Transform cachedTransform;

    /// <summary>Sát thương hiện tại - Giai đoạn 2 sẽ điều chỉnh theo nâng cấp Damage.</summary>
    public int Damage => Mathf.Max(1, damage);

    /// <summary>Cho phép hệ thống nâng cấp thay đổi sát thương mà không cần sửa Prefab.</summary>
    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
    }

    /// <summary>
    /// Bật chế độ xuyên thấu: đạn xuyên qua tối đa <paramref name="maxTargets"/> quái.
    /// Dùng cho trang bị Laser Gun.
    /// </summary>
    public void EnablePiercing(int maxTargets)
    {
        pierceRemaining = Mathf.Max(0, maxTargets);
    }

    private void Awake()
    {
        cachedTransform = transform;
    }

    // =====================================================================
    // POOL LIFECYCLE
    // =====================================================================
    /// <summary>Reset toàn bộ state khi được lấy từ pool.</summary>
    public void OnSpawned()
    {
        age = 0f;
        isAlive = true;
        pierceRemaining = 0;
        // damage giữ nguyên giá trị người gọi vừa SetDamage cho instance này.
    }

    public void OnDespawned()
    {
        isAlive = false;
        age = 0f;
        pierceRemaining = 0;
    }

    // =====================================================================
    // UPDATE
    // =====================================================================
    private void Update()
    {
        float deltaTime = Time.deltaTime;
        age += deltaTime;

        if (horizontalSpeed != 0f)
        {
            cachedTransform.Translate(horizontalSpeed * deltaTime, speed * deltaTime, 0f, Space.Self);
        }
        else
        {
            cachedTransform.Translate(0f, speed * deltaTime, 0f, Space.Self);
        }

        if (age >= lifetime)
            Recycle();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isAlive)
            return;

        if (!collision.CompareTag("Enemy"))
            return;

        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy == null)
            enemy = collision.GetComponentInParent<Enemy>();

        if (enemy != null)
            enemy.TakeDamage(Damage);

        onHitEnemy?.Invoke();

        // Xuyên thấu (Laser Gun): đạn bay tiếp xuyên qua nhiều quái,
        // chỉ mất khi hết lifetime. Đếm số lần xuyên để không vô hạn.
        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        Recycle();
    }

    /// <summary>Trả về pool (thay cho Destroy).</summary>
    private void Recycle()
    {
        if (!isAlive)
            return;

        isAlive = false;

        if (BulletPool.HasInstanceAvailable)
            BulletPool.Instance.Despawn(gameObject);
        else
            Destroy(gameObject);
    }
}
