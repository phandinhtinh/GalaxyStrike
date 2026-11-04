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

    private void Awake() { cachedTransform = transform; body = GetComponent<Rigidbody2D>(); }
    private void OnEnable() { OnSpawned(); }
    public void SetDirection(Vector3 targetDirection)
    {
        direction = targetDirection.sqrMagnitude > 0.001f ? targetDirection.normalized : Vector3.down;
        cachedTransform.up = direction;
    }
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
    public void OnDespawned() { alive = false; target = null; }
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
    private void Update()
    {
        if (alive && visual != null && spinSpeed != 0f) visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
    private void Recycle()
    {
        if (!alive) return;
        alive = false;
        if (BulletPool.HasInstanceAvailable) BulletPool.Instance.Despawn(gameObject);
        else Destroy(gameObject);
    }
}
