using UnityEngine;

/// <summary>
/// Rung phản hồi (haptic) gộp một chỗ cho mobile.
///
/// - Android : <see cref="Handheld.Vibrate"/> (mọi máy đều có).
/// - iOS     : tạm gọi cùng API; khi tích hợp plugin rung riêng thì thay phần
///             inside <see cref="Vibrate"/> (điểm nối duy nhất).
/// - Editor  : không rung, chỉ log khi bật <see cref="debugLog"/>.
///
/// Thiết kế "fire-and-forget": không cấp phát, không chặn frame; giới hạn tần suất
/// để chùm va chạm liên tiếp không làm rung liên tục.
/// </summary>
public static class Haptics
{
    private const float MinIntervalSeconds = 0.06f;
    private static float lastVibrateTime = float.NegativeInfinity;

    /// <summary>Có bật rung không (do cài đặt người dùng). Mặc định bật.</summary>
    public static bool Enabled { get; set; } = true;

    [Tooltip("Bật log ra Console khi không rung được (Editor).")]
    public static bool debugLog;

    /// <summary>Rung một nhịp nhẹ dùng cho va chạm/đạn trúng.</summary>
    public static void Light() => Vibrate(0.6f);

    /// <summary>Rung mạnh hơn dùng cho nổ/nổ boss.</summary>
    public static void Heavy() => Vibrate(1f);

    /// <summary>
    /// Rung theo cường độ 0..1. Hiện Unity không cho điều khiển cường độ trên
    /// Android bằng API mặc định nên chỉ rung khi intensity vượt ngưỡng nhẹ.
    /// </summary>
    public static void Vibrate(float intensity = 1f)
    {
        if (!Enabled || intensity <= 0.05f)
            return;

        // Chống rung dồn dập khi nhiều va chạm cùng frame/liền nhau.
        if (Time.unscaledTime - lastVibrateTime < MinIntervalSeconds)
            return;

        lastVibrateTime = Time.unscaledTime;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#else
        if (debugLog)
            Debug.Log($"[Haptics] Vibrate({intensity:0.00}) (nền tảng này không rung)");
#endif
    }
}
