using UnityEngine;
using GalaxyStrike.Save;
using GalaxyStrike.IAP;
using GalaxyStrike.Leaderboard;

/// <summary>
/// Bootstrap chung cho toàn game.
///
/// Đảm bảo các service dùng chung tồn tại TRƯỚC mọi UI/Gameplay ở bất kỳ Scene nào,
/// nên không cần gắn tay vào HomeScene hay GameScene.
/// </summary>
[DefaultExecutionOrder(-200)]
public static class GameBootstrap
{
    /// <summary>
    /// Được Unity tự gọi lại mỗi khi bật Play Mode (trước khi tạo object nào).
    /// Mục đích: xóa các biến static (Instance) còn sót của phiên chạy trước,
    /// tránh Singleton trỏ tới object "đã chết" khi tắt Domain Reload.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Reset static instance khi vào Play Mode để không giữ tham chiếu object của phiên trước.
        SaveManager.ResetStaticInstance();
        IapPurchaseService.ResetStaticInstance();
        LeaderboardService.ResetStaticInstance();
    }

    /// <summary>
    /// Unity tự gọi một lần trước khi Scene đầu tiên được load.
    /// Tại đây "chạm" vào Instance của 3 service để chúng tự tạo DontDestroyOnLoad,
    /// nhờ vậy mọi Scene đều có sẵn SaveManager / IAP / Leaderboard.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Tạo (hoặc dùng lại) SaveManager bền vững giữa các Scene.
        _ = SaveManager.Instance;

        // Dịch vụ mua hàng: tự khởi tạo Unity IAP, chạy song song với save.
        _ = IapPurchaseService.Instance;

        // Bảng xếp hạng: tự đăng nhập Play Games (nếu đã cấu hình), nếu không thì chạy offline.
        _ = LeaderboardService.Instance;

        // Khung hình ổn định cho Android và chặn màn hình tự tắt khi chơi.
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
