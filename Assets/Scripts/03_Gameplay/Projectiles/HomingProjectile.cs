using UnityEngine;

/// <summary>
/// Tên lửa tự tìm mục tiêu (Homing Missile).
/// Tìm Enemy gần nhất mỗi khung hình và bẻ lái về phía đó.
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
    private Transform currentTarget;
    private float retargetCooldown;

    /// <summary>Gán sát thương từ trang bị của người chơi.</summary>
    public void Initialize(int projectileDamage)
    {
        damage = Mathf.Max(1, projectileDamage);
        AcquireTarget();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (currentTarget == null)
        {
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
            Vector3 toTarget = currentTarget.position - transform.position;
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnRate * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        transform.position += transform.up * (speed * dt);
    }

    private void AcquireTarget()
    {
        // Chỉ gọi khi mất mục tiêu (target chết hoặc chưa có), không chạy mỗi frame.
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        float bestDistance = targetSearchRadius;
        Transform best = null;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || enemies[i].IsDead)
                continue;

            float distance = Vector2.Distance(transform.position, enemies[i].transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemies[i].transform;
            }
        }

        currentTarget = best;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Enemy"))
            return;

        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy == null)
            enemy = collision.GetComponentInParent<Enemy>();

        if (enemy != null)
            enemy.TakeDamage(damage);

        Destroy(gameObject);
    }
}
