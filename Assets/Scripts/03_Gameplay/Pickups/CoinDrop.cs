using UnityEngine;

/// <summary>Coin dropped when an enemy is defeated. Reward is credited only when the player picks it up.</summary>
public sealed class CoinDrop : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float fallSpeed = 0.45f;
    [SerializeField] private float swayAmplitude = 0f;
    [SerializeField] private float swayFrequency = 2.5f;
    [Tooltip("Tốc độ xoay quanh trục dọc Y của coin")]
    [SerializeField] private float rotateSpeed = 240f;
    [SerializeField] private float bottomDespawnY = -6.5f;

    [Tooltip("Số Coin cộng vào khi Player nhặt được đồng xu này")]
    [SerializeField] private int coinValue = 1;

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
    /// Gán số Coin của đồng xu (do Enemy truyền vào lúc chết).
    /// </summary>
    public void SetValue(int value)
    {
        coinValue = Mathf.Max(0, value);
    }

    /// <summary>
    /// Va chạm player: cộng Coin thật vào GameManager rồi phát âm nhặt coin và xoá đồng xu.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive || !collision.CompareTag("Player"))
            return;

        isActive = false;

        if (coinValue > 0 && GameManager.Instance != null)
            GameManager.Instance.AddCoins(coinValue);

        if (AudioManager.Instance != null && AudioManager.Instance.coinCollectSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollectSFX);

        CameraShake.Play(0.05f);
        VfxSpawner.SpawnPickup(transform.position);
        BattleFeedbackHUD.Instance?.ShowCoinPickup(transform.position, coinValue);

        Destroy(gameObject);
    }
}
