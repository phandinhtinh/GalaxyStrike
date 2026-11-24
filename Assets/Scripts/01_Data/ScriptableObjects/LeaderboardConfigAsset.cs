using UnityEngine;

namespace GalaxyStrike.Leaderboard
{
    /// <summary>
    /// Asset bọc <see cref="PlayGamesConfig"/> (Leaderboard ID trên Play Console).
    /// LeaderboardService được tạo tự động lúc chạy nên không chỉnh được trong Inspector;
    /// nó nạp cấu hình từ Resources/Config/LeaderboardConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "LeaderboardConfig", menuName = "GalaxyStrike/Leaderboard Config")]
    public sealed class LeaderboardConfigAsset : ScriptableObject
    {
        public PlayGamesConfig config = new PlayGamesConfig();
    }
}
