using UnityEngine;

/// <summary>
/// Vật phẩm tăng số lượng đạn (+1 Bullet Upgrade) rơi ra khi tiêu diệt quái.
/// Trôi xuống, đung đưa và tự huỷ khi ra khỏi màn hình hoặc khi được nhặt.
/// </summary>
public class BulletUpgradeItem : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float fallSpeed = 2.5f;
    [SerializeField] private float swayAmplitude = 0.6f;
    [SerializeField] private float swayFrequency = 3f; 
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private float bottomDespawnY = -6.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSFX;

    private float spawnX;
    private bool isActive = true;

    // Magnet: khi true, đường bay mặc định bị tắt để PlayerEquipment điều khiển vị trí.
    private bool isBeingAttracted;

    /// <summary>
    /// Bật/tắt chế độ bị hút. Khi bị hút, vật phẩm ngừng trôi xuống
    /// để bán kính nam châm kéo thẳng về Player.
    /// </summary>
    public void SetBeingAttracted(bool attracted)
    {
        isBeingAttracted = attracted;
    }

    private void Start()
    {
        spawnX = transform.position.x;
    }

    private void Update()
    {
        if (!isActive || isBeingAttracted)
            return;

        // 1. Trôi dần xuống phía dưới màn hình
        Vector3 position = transform.position;
        position += Vector3.down * fallSpeed * Time.deltaTime;

        // 2. Đung đưa nhẹ theo hình sin để tạo hiệu ứng vật phẩm bay lơ lửng đẹp mắt
        position.x = spawnX + Mathf.Sin(Time.time * swayFrequency) * swayAmplitude;
        transform.position = position;

        // 3. Tự xoay nhẹ quanh trục
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 4. Tự hủy khi trôi hết màn hình phía dưới để giải phóng bộ nhớ
        if (position.y <= bottomDespawnY)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive)
            return;

        if (collision.CompareTag("Player"))
        {
            isActive = false;

            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                player.UpgradeBulletCount(1);
            }

            // Phát âm thanh nhặt vật phẩm
            AudioClip sfxToPlay = pickupSFX;
            if (sfxToPlay == null && AudioManager.Instance != null)
            {
                sfxToPlay = AudioManager.Instance.coinCollectSFX;
            }

            if (AudioManager.Instance != null && sfxToPlay != null)
            {
                AudioManager.Instance.PlaySFX(sfxToPlay);
            }

            Destroy(gameObject);
        }
    }
}
