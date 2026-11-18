using UnityEngine;

/// <summary>
/// Rung camera kiểu "trauma" (Squirrel Eiserloh): các sự kiện gameplay gọi
/// <see cref="Shake"/> để cộng dồn độ rung, rồi giảm dần theo thời gian.
///
/// Gắn lên Main Camera của GameScene. Không có instance thì mọi lời gọi là no-op.
///
/// Đặc điểm:
///  - Cộng dồn nhiều lần trúng đạn cùng lúc -> rung mạnh hơn (capped).
///  - Dùng unscaled time nên vẫn rung khi game pause thoáng qua.
///  - Ghi nhớ vị trí gốc và trả lại chính xác khi hết rung, không trôi camera.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Tooltip("Biên độ dịch tối đa (world unit) khi trauma = 1")]
    [SerializeField] private float maxOffset = 0.28f;

    [Tooltip("Biên độ xoay tối đa (độ) khi trauma = 1")]
    [SerializeField] private float maxRoll = 1.6f;

    [Tooltip("Tốc độ giảm trauma mỗi giây")]
    [SerializeField] private float traumaDecay = 1.6f;

    [Tooltip("Tần số dao động (càng cao càng rung giật)")]
    [SerializeField] private float frequency = 26f;

    private float trauma;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float seed;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            seed = Random.value * 100f;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        if (trauma <= 0f)
            return;

        // trauma^2 cho cảm giác giảm rung tự nhiên (mạnh lúc đầu, tắt êm).
        float amount = trauma * trauma;
        float time = Time.unscaledTime * frequency;

        float offsetX = (Mathf.PerlinNoise(seed, time) * 2f - 1f) * maxOffset * amount;
        float offsetY = (Mathf.PerlinNoise(seed + 17.3f, time) * 2f - 1f) * maxOffset * amount;
        float roll = (Mathf.PerlinNoise(seed + 41.7f, time) * 2f - 1f) * maxRoll * amount;

        transform.localPosition = basePosition + new Vector3(offsetX, offsetY, 0f);
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, roll);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);

        if (trauma <= 0f)
        {
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
        }
    }

    /// <summary>
    /// Cộng thêm độ rung (0..1). Có thể gọi liên tục; trauma bị kẹp ở 1.
    /// </summary>
    public void Shake(float amount = 0.35f)
    {
        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
    }

    /// <summary>Cộng độ rung mà không cần biết Instance có tồn tại (tiện cho call site).</summary>
    public static void Play(float amount = 0.35f)
    {
        if (Instance != null)
            Instance.Shake(amount);
    }
}
