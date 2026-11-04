using UnityEngine;

/// <summary>
/// NHÓM I — Phương thức xuất hiện vật lý.
/// Quyết định quái hình thành từ đâu và bằng cách nào.
/// </summary>
public enum SpawnOrigin
{
    [Tooltip("Rơi từ đỉnh màn hình xuống (phổ biến nhất, ~60%)")]
    TopCascade = 0,

    [Tooltip("Tạt sườn từ mép trái/phải, bay cắt mặt màn hình")]
    SideFlank = 1,

    [Tooltip("Đánh lén từ đáy màn hình lên, có vạch cảnh báo đỏ")]
    BottomAmbush = 2,

    [Tooltip("Nhảy dù không gian tại chỗ qua cổng warp, 1-2s đầu là bóng mờ")]
    WarpIn = 3
}

/// <summary>
/// NHÓM II — Đội hình & quỹ đạo bay.
/// Quyết định cách quái di chuyển và bài trí trên màn hình.
/// </summary>
public enum FormationType
{
    [Tooltip("Snake Wave: hàng một rồng rắn, lượn chữ S")]
    Snake = 0,

    [Tooltip("Wall: dàn hàng ngang 4-6 con như bức tường tiến chậm")]
    Wall = 1,

    [Tooltip("V-Formation: mũi tên chữ V có con chỉ huy dẫn đầu")]
    VFormation = 2,

    [Tooltip("Pincer Cross: hai hàng từ 2 góc trên bay chéo cắt nhau tạo chữ X")]
    PincerCross = 3,

    [Tooltip("Drop & Anchor: trụ pháo bay xuống 1/3 màn hình rồi đứng yên xả đạn")]
    DropAnchor = 4,

    [Tooltip("Dive-Bomber: lượn vòng trên đỉnh, khóa mục tiêu rồi lao xuống")]
    Kamikaze = 5,

    [Tooltip("Boss: giữ vị trí trên cao, xả đạn nhiều hướng")]
    Boss = 6
}

/// <summary>
/// NHÓM III — Cơ chế sinh ra đặc biệt.
/// Quái sinh ra từ yếu tố bên trong ván đấu, không phải từ ngoài màn hình.
/// </summary>
public enum SpecialSpawnType
{
    [Tooltip("Không dùng cơ chế đặc biệt")]
    None = 0,

    [Tooltip("Carrier/Hive: tàu mẹ liên tục phóng tàu con")]
    CarrierHive = 1,

    [Tooltip("Splitting: quái lớn vỡ thành 2-4 quái nhỏ khi bị bắn nổ")]
    Splitting = 2,

    [Tooltip("Boss Summoning: Boss triệu hồi đàn hộ vệ ở ngưỡng 50%/25% máu")]
    BossSummon = 3,

    [Tooltip("Thiên thạch rơi tự do: chướng ngại vật không bắn, máu rất lớn")]
    FallingHazard = 4
}
