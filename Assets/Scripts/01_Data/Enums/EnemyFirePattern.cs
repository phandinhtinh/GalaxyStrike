using UnityEngine;

/// <summary>
/// Kiểu xả đạn của quái (theo thiết kế bullet hell).
/// </summary>
public enum EnemyFirePattern
{
    /// <summary>Một viên bắn thẳng xuống.</summary>
    Single = 0,

    /// <summary>Chùm 3 viên tỏa góc quạt.</summary>
    Spread = 1,

    /// <summary>Bắn đuổi theo vị trí người chơi (aimed stream).</summary>
    Aimed = 2,

    /// <summary>Bung hoa tròn 360° nhiều tia.</summary>
    RadialBloom = 3,

    /// <summary>Sóng xoắn ốc liên tục.</summary>
    Spiral = 4,

    /// <summary>Chuỗi ba viên bắn nhanh có nhịp nghỉ rõ để né.</summary>
    Burst = 5,

    /// <summary>Chuỗi đạn khóa hướng người chơi khi khai hỏa.</summary>
    AimedBurst = 6,

    /// <summary>Chuỗi đạn tỏa quạt lần lượt.</summary>
    FanBurst = 7
}
