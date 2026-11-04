using UnityEngine;

/// <summary>Visual coin reward dropped when an enemy is defeated.</summary>
public sealed class CoinDrop : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float fallSpeed = 0.45f;
    [SerializeField] private float swayAmplitude = 0f;
    [SerializeField] private float swayFrequency = 2.5f;
    [Tooltip("Tốc độ xoay quanh trục dọc Y của coin")]
    [SerializeField] private float rotateSpeed = 240f;
    [SerializeField] private float bottomDespawnY = -6.5f;

    private float spawnX;
    private bool isActive = true;
    private bool isBeingAttracted;
    private Transform attractionTarget;
    private float attractionSpeed;

    private void Start()
    {
        spawnX = transform.position.x;
    }

    private void Update()
    {
        if (!isActive)
            return;

        Vector3 position = transform.position;

        if (isBeingAttracted)
        {
            if (attractionTarget == null)
            {
                isBeingAttracted = false;
            }
            else
            {
                position = Vector3.MoveTowards(
                    position,
                    attractionTarget.position,
                    attractionSpeed * Time.deltaTime);
                transform.position = position;
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
                return;
            }
        }

        position += Vector3.down * fallSpeed * Time.deltaTime;
        position.x = spawnX + Mathf.Sin(Time.time * swayFrequency) * swayAmplitude;
        transform.position = position;
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        if (position.y <= bottomDespawnY)
            Destroy(gameObject);
    }

    public void SetBeingAttracted(Transform target, float pullSpeed)
    {
        attractionTarget = target;
        attractionSpeed = Mathf.Max(0f, pullSpeed);
        isBeingAttracted = target != null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive || !collision.CompareTag("Player"))
            return;

        isActive = false;
        if (AudioManager.Instance != null && AudioManager.Instance.coinCollectSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollectSFX);

        // Enemy coinValue is credited on defeat; this pickup only represents that reward visually.
        Destroy(gameObject);
    }
}
