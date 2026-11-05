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

    /// <summary>
    /// Ghi lại vị trí X lúc sinh ra, để hiệu ứng lắc ngang dao động quanh cột gốc.
    /// </summary>
    private void Start()
    {
        spawnX = transform.position.x;
    }

    /// <summary>
    /// Mỗi frame: đang bị hút thì bay về player, không thì trôi xuống theo cột
    /// + lắc ngang nhẹ, đồng thời xoay tròn. Rơi quá đáy thì tự huỷ.
    /// </summary>
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

    /// <summary>
    /// Cho đồng xu bay về phía <paramref name="target"/> (player) với tốc độ
    /// <paramref name="pullSpeed"/> — dùng cho trang bị Magnet / hút mặc định.
    /// </summary>
    public void SetBeingAttracted(Transform target, float pullSpeed)
    {
        attractionTarget = target;
        attractionSpeed = Mathf.Max(0f, pullSpeed);
        isBeingAttracted = target != null;
    }

    /// <summary>
    /// Va chạm player: tắt xu, phát âm nhặt coin. Coin THẬT đã được cộng lúc quái chết,
    /// nên ở đây chỉ xoá object hiển thị.
    /// </summary>
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
