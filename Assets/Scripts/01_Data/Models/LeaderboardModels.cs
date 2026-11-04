using System;
using UnityEngine;

namespace GalaxyStrike.Leaderboard
{
    /// <summary>Loại bảng xếp hạng được hỗ trợ.</summary>
    public enum LeaderboardType
    {
        [Tooltip("Điểm cao nhất trong một màn")]
        HighScore = 0,

        [Tooltip("Level cao nhất đã chinh phục")]
        HighestLevel = 1,

        [Tooltip("Tổng số quái đã hạ")]
        TotalKills = 2
    }

    /// <summary>Một dòng trong bảng xếp hạng.</summary>
    public struct LeaderboardEntry
    {
        public int Rank;
        public string PlayerName;
        public long Score;
        public string PlayerId;
        public bool IsCurrentPlayer;
        public Sprite Avatar;

        public LeaderboardEntry(int rank, string playerName, long score, bool isCurrentPlayer = false)
        {
            Rank = rank;
            PlayerName = playerName;
            Score = score;
            PlayerId = null;
            IsCurrentPlayer = isCurrentPlayer;
            Avatar = null;
        }
    }

    /// <summary>
    /// Cấu hình Google Play Games.
    ///
    /// CÁC GIÁ TRỊ NÀY LẤY TỪ PLAY CONSOLE:
    ///  1. Play Console → Play Games Services → Configuration
    ///     → "Web client ID" (OAuth 2.0 Client ID loại Web)
    ///  2. Play Games Services → Leaderboards → Create
    ///     → Mỗi leaderboard có "ID" dạng CgkIxxxxxxxxEAIQAQ
    ///
    /// Để trống hết thì hệ thống tự chuyển sang chế độ offline (chỉ hiện thành tích cá nhân),
    /// nên game không bao giờ crash vì thiếu cấu hình.
    /// </summary>
    [Serializable]
    public sealed class PlayGamesConfig
    {
        [Header("--- OAuth (Play Console) ---")]
        [Tooltip("Web Client ID từ Google Cloud Console, dạng 123456789-abc.apps.googleusercontent.com")]
        public string webClientId = "";

        [Header("--- Leaderboard IDs (Play Console) ---")]
        [Tooltip("ID bảng điểm cao nhất, dạng CgkIxxxxxxxxEAIQAQ")]
        public string highScoreLeaderboardId = "";

        [Tooltip("ID bảng Level cao nhất")]
        public string highestLevelLeaderboardId = "";

        [Tooltip("ID bảng tổng số quái đã hạ")]
        public string totalKillsLeaderboardId = "";

        [Header("--- Runtime ---")]
        [Tooltip("Tự động đăng nhập khi mở game")]
        public bool autoSignIn = true;

        [Tooltip("Số dòng hiển thị trên bảng xếp hạng")]
        [Range(5, 50)] public int maxEntries = 20;

        /// <summary>Cấu hình đã đủ để gọi Play Games thật hay chưa.</summary>
        public bool IsConfigured =>
            !string.IsNullOrEmpty(webClientId) &&
            !string.IsNullOrEmpty(highScoreLeaderboardId);

        /// <summary>Lấy ID theo loại bảng.</summary>
        public string GetLeaderboardId(LeaderboardType type)
        {
            switch (type)
            {
                case LeaderboardType.HighScore: return highScoreLeaderboardId;
                case LeaderboardType.HighestLevel: return highestLevelLeaderboardId;
                case LeaderboardType.TotalKills: return totalKillsLeaderboardId;
                default: return highScoreLeaderboardId;
            }
        }
    }
}
