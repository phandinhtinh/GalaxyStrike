using UnityEngine;

/// <summary>
/// Tên lửa tự tìm mục tiêu (Homing Missile).
/// Tìm Enemy gần nhất khi phóng hoặc mất mục tiêu và bẻ lái mỗi khung hình.
/// Chỉ dùng khi người chơi đã mua "HOMING ROCKET" trong Shop.
/// </summary>
public class HomingProjectile : MonoBehaviour
{
    [Header("Homing")]
    [SerializeField] private float speed = 11f;
    [SerializeField] private float turnRate = 320f;
    [SerializeField] private float lifetime = 3.5f;
    [SerializeField] private float targetSearchRadius = 12f;

    private int damage = 1;
    private float age;
    private Enemy currentTarget;
    private float retargetCooldown;

    /// <summary>Gán sát thương từ trang bị của người chơi.</summary>
    public void Initialize(int projectileDamage)
    {
        damage = Mathf.Max(1, projectileDamage);
        AcquireTarget();
    }

    /// <summary>
    /// Mỗi frame: tự huỷ khi hết lifetime; nếu mất mục tiêu thì thử tìm lại mỗi 0.25s
    /// (tránh scan mỗi frame); có mục tiêu thì bẻ lái dần về phía nó rồi bay tới.
    /// </summary>
    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (currentTarget == null || currentTarget.IsDead || !currentTarget.isActiveAndEnabled)
        {
            currentTarget = null;
            // Không tìm lại mỗi frame khi màn hình trống (tiết kiệm CPU trên mobile).
            retargetCooldown -= dt;
            if (retargetCooldown <= 0f)
            {
                retargetCooldown = 0.25f;
                AcquireTarget();
            }
        }

        if (currentTarget != null)
        {
            Vector3 toTarget = currentTarget.transform.position - transform.position;
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnRate * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        transform.position += transform.up * (speed * dt);
    }

    /// <summary>
    /// Tìm quái (Enemy) gần tên lửa nhất còn sống trong bán kính targetSearchRadius
    /// và gán làm mục tiêu. Không tìm thấy thì để trống (Update sẽ thử lại sau).
    /// </summary>
    private void AcquireTarget()
    {
        // Chỉ gọi khi mất mục tiêu (target chết hoặc chưa có), không chạy mỗi frame.
        var enemies = Enemy.ActiveEnemies;
        Vector2 origin = transform.position;
        float bestDistance = targetSearchRadius * targetSearchRadius;
        Enemy best = null;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].IsDead)
                continue;

            float distance = ((Vector2)enemies[i].transform.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemies[i];
            }
        }

        currentTarget = best;
    }

    /// <summary>
    /// Va chạm quái: gây sát thương rồi tự huỷ tên lửa (tên lửa không xuyên thấu).
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Enemy"))
            return;

        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy == null)
            enemy = collision.GetComponentInParent<Enemy>();

        if (enemy != null)
            enemy.TakeDamage(damage, collision.ClosestPoint(transform.position));

        Destroy(gameObject);
    }
}
