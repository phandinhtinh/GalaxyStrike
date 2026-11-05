using UnityEngine;

public class BackgroundScroll : MonoBehaviour
{
    [Header("Scroll Movement")]
    [Tooltip("Tốc độ cuộn của background")]
    [SerializeField] private float scrollSpeed = 2f;

    [Header("Seamless Looping Settings")]
    [Tooltip("Tự động đo chiều cao của Sprite từ SpriteRenderer để tính toán chuẩn xác")]
    [SerializeField] private bool autoCalculateHeight = true;

    [Tooltip("Chiều cao của 1 tấm Background trong không gian World (nếu autoCalculateHeight = false)")]
    [SerializeField] private float backgroundHeight = 10f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (autoCalculateHeight && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // bounds.size.y là chiều cao thực tế theo đơn vị World Space (đã tự nhân theo scale Y của Transform)
            backgroundHeight = spriteRenderer.bounds.size.y;
        }
    }

    /// <summary>
    /// Mỗi frame: cuộn nền xuống dưới; khi tấm nền đã trượt qua đáy thì "dịch" nó lên
    /// đúng 2 lần chiều cao để nối vòng liền mạch (seamless loop) mà không hở mép.
    /// </summary>
    private void Update()
    {
        // Trôi xuống theo scrollSpeed
        transform.Translate(Vector3.down * scrollSpeed * Time.deltaTime);

        // Khi tấm ảnh trôi qua khỏi đáy màn hình (thấp hơn -backgroundHeight)
        if (transform.position.y <= -backgroundHeight)
        {
            // CỘNG THÊM 2 lần chiều cao thay vì gán cứng tọa độ.
            // Phép toán vector này giữ nguyên phần dư deltaTime (overshoot),
            // triệt tiêu 100% hiện tượng hở mép nối / giật hình theo thời gian!
            transform.position += Vector3.up * (2f * backgroundHeight);
        }
    }

    // Hàm public cho phép script khác điều chỉnh tốc độ cuộn khi tàu tăng tốc / hyper-drive
    public void SetScrollSpeed(float newSpeed)
    {
        scrollSpeed = newSpeed;
    }
}