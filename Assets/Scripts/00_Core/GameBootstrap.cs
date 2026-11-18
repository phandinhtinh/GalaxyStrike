using UnityEngine;
using GalaxyStrike.Save;
using GalaxyStrike.IAP;
using GalaxyStrike.Leaderboard;

/// <summary>
/// Bootstrap chung cho toàn game.
///
/// Đảm bảo các service dùng chung tồn tại TRƯỚC mọi UI/Gameplay ở bất kỳ Scene nào,
/// nên không cần gắn tay vào HomeScene hay GameScene.
///
/// Mọi service kế thừa <see cref="SingletonBehaviour{T}"/> nên việc tự tạo + DontDestroyOnLoad
/// nằm trong base class; ở đây chỉ cần "chạm" vào Instance theo thứ tự xác định.
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
        SingletonBehaviour<SaveManager>.ResetStaticInstance();
        SingletonBehaviour<IapPurchaseService>.ResetStaticInstance();
        SingletonBehaviour<LeaderboardService>.ResetStaticInstance();
        SingletonBehaviour<AudioManager>.ResetStaticInstance();
        SingletonBehaviour<PanelManager>.ResetStaticInstance();

        // LevelManager tự quản singleton riêng (vì cần transform/DontDestroyOnLoad).
        LevelManager.ResetStaticInstance();
    }

    /// <summary>
    /// Unity tự gọi một lần trước khi Scene đầu tiên được load.
    /// Tại đây CHỦ ĐỘNG tạo service toàn cục (chúng không nằm trong Scene nào),
    /// nhờ vậy mọi Scene đều có sẵn SaveManager / IAP / Leaderboard / Audio.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // CreateIfMissing: getter Instance KHÔNG tự tạo, nên phải tạo tường minh ở đây.
        // Tạo SaveManager trước để save sẵn sàng khi service khác cần đọc.
        SaveManager.CreateIfMissing();

        // Dịch vụ mua hàng: tự khởi tạo Unity IAP, chạy song song với save.
        IapPurchaseService.CreateIfMissing();

        // Bảng xếp hạng: tự đăng nhập Play Games (nếu đã cấu hình), nếu không thì chạy offline.
        LeaderboardService.CreateIfMissing();

        // Âm thanh: giữ BGM xuyên Scene.
        AudioManager.CreateIfMissing();

        // PanelManager gắn trong GameScene (cần reference tới panel) nên KHÔNG tự tạo ở đây.

        // Khung hình ổn định cho Android và chặn màn hình tự tắt khi chơi.
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
