// Galaxy Strike - Google Play Games Leaderboard
//
// File này biên dịch theo 2 chế độ:
//
//  (A) ĐÃ CÀI plugin Google Play Games (com.google.play.games)
//      -> dùng API thật: đăng nhập, submit score, đọc top người chơi.
//
//  (B) CHƯA CÀI plugin
//      -> tự chuyển sang chế độ OFFLINE: bảng chỉ hiện thành tích của chính người chơi.
//         Không crash, không lỗi compile, UI vẫn test được.
//
// Plugin tự định nghĩa symbol GOOGLE_PLAY_GAMES khi được cài.
// Cách cài (Package Manager > Add package from git URL):
//   https://github.com/playgameservices/play-games-plugin-for-unity.git#current-build
//
// Sau khi cài, mở: Window > Google Play Games > Setup > Android Setup
// và paste 3 Leaderboard ID + Web Client ID vào component LeaderboardService.

using System;
using System.Collections.Generic;
using UnityEngine;
using GalaxyStrike.Save;

#if GOOGLE_PLAY_GAMES
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace GalaxyStrike.Leaderboard
{
    /// <summary>
    /// Dịch vụ bảng xếp hạng.
    ///
    /// Tự sinh (DontDestroyOnLoad) nên dùng được ở mọi Scene.
    /// Tự submit điểm mỗi khi hoàn thành màn (GameManager gọi).
    /// </summary>
    [DefaultExecutionOrder(-130)]
    public sealed class LeaderboardService : MonoBehaviour
    {
        private static LeaderboardService instance;

        public static LeaderboardService Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindObjectOfType<LeaderboardService>();
                if (instance == null)
                    instance = new GameObject("[LeaderboardService]").AddComponent<LeaderboardService>();

                return instance;
            }
        }

        [Header("--- Google Play Games Config ---")]
        [SerializeField] private PlayGamesConfig config = new PlayGamesConfig();

        /// <summary>Phát khi trạng thái đăng nhập thay đổi (true = đã đăng nhập).</summary>
        public event Action<bool> OnSignInChanged;

        /// <summary>Phát khi tải bảng xếp hạng xong.</summary>
        public event Action<LeaderboardType, LeaderboardEntry[], bool> OnLeaderboardLoaded;

        /// <summary>Nội bộ: gọi cả callback lẫn event để UI không phải subscribe 2 lần.</summary>
        private void NotifyLoaded(LeaderboardType type, LeaderboardEntry[] entries, bool isOnline)
        {
            OnLeaderboardLoaded?.Invoke(type, entries, isOnline);
        }

        private bool isSignedIn;
        private bool isInitializing;
        private readonly List<LeaderboardEntry> buffer = new List<LeaderboardEntry>(50);

        /// <summary>Đang dùng Play Games thật hay đang ở chế độ offline.</summary>
        public bool IsOnlineMode =>
#if GOOGLE_PLAY_GAMES
            config != null && config.IsConfigured;
#else
            false;
#endif

        public bool IsSignedIn => isSignedIn;
        public PlayGamesConfig Config => config;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializePlayGames();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public static void ResetStaticInstance()
        {
            instance = null;
        }

        // =====================================================================
        // KHỞI TẠO
        // =====================================================================
        public void InitializePlayGames()
        {
            if (isInitializing)
                return;

            isInitializing = true;

#if !GOOGLE_PLAY_GAMES
            isInitializing = false;
            Debug.Log("[Leaderboard] Chưa cài plugin Google Play Games -> chạy chế độ OFFLINE " +
                      "(chỉ hiển thị thành tích cá nhân).");
            return;
#else
            if (config == null || !config.IsConfigured)
            {
                isInitializing = false;
                Debug.Log("[Leaderboard] Chưa điền Web Client ID / Leaderboard ID -> chế độ OFFLINE.");
                return;
            }

            PlayGamesPlatform.Activate();
            PlayGamesPlatform.DebugLogEnabled = false;

            if (config.autoSignIn)
                SignIn();
            else
                isInitializing = false;
#endif
        }

        // =====================================================================
        // ĐĂNG NHẬP
        // =====================================================================
        public void SignIn()
        {
#if !GOOGLE_PLAY_GAMES
            Debug.Log("[Leaderboard] SignIn: chưa cài plugin Play Games.");
#else
            if (PlayGamesPlatform.Instance == null)
                PlayGamesPlatform.Activate();

            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                isInitializing = false;
                bool ok = status == SignInStatus.Success;

                isSignedIn = ok;
                OnSignInChanged?.Invoke(ok);

                Debug.Log(ok
                    ? "[Leaderboard] Đăng nhập Google Play Games thành công."
                    : $"[Leaderboard] Đăng nhập thất bại: {status}");
            });
#endif
        }

        public void SignOut()
        {
#if GOOGLE_PLAY_GAMES
            if (PlayGamesPlatform.Instance != null)
                PlayGamesPlatform.Instance.SignOut();
#endif
            isSignedIn = false;
            OnSignInChanged?.Invoke(false);
        }

        public string GetPlayerName()
        {
#if GOOGLE_PLAY_GAMES
            if (isSignedIn && PlayGamesPlatform.Instance != null)
            {
                try
                {
                    return PlayGamesPlatform.Instance.GetUserDisplayName();
                }
                catch
                {
                    // rơi xuống fallback
                }
            }
#endif
            return SaveManager.Instance != null ? SaveManager.Instance.Data.playerName : "You";
        }

        // =====================================================================
        // GỬI ĐIỂM
        // =====================================================================
        /// <summary>
        /// Gửi thành tích lên Play Games. Gọi mỗi khi hoàn thành màn.
        /// Bỏ qua an toàn nếu chưa đăng nhập.
        /// </summary>
        public void SubmitAll(LeaderboardType type)
        {
            Submit(type, GetLocalScore(type));
        }

        public void Submit(LeaderboardType type, long score)
        {
            if (score <= 0)
                return;

#if !GOOGLE_PLAY_GAMES
            _ = type;
            _ = score;
            return;
#else
            if (!isSignedIn || PlayGamesPlatform.Instance == null)
                return;

            string id = config.GetLeaderboardId(type);
            if (string.IsNullOrEmpty(id))
                return;

            PlayGamesPlatform.Instance.ReportScore(score, id, success =>
            {
                if (!success)
                    Debug.LogWarning($"[Leaderboard] Gửi điểm thất bại ({type}: {score}).");
            });
#endif
        }

        /// <summary>Gửi cả 3 bảng cùng lúc (gọi sau khi thắng màn).</summary>
        public void SubmitProgress()
        {
            SubmitAll(LeaderboardType.HighScore);
            SubmitAll(LeaderboardType.HighestLevel);
            SubmitAll(LeaderboardType.TotalKills);
        }

        /// <summary>Điểm cục bộ theo loại bảng (dùng cả khi offline).</summary>
        public long GetLocalScore(LeaderboardType type)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
                return 0;

            switch (type)
            {
                case LeaderboardType.HighScore:
                    return save.BestScore;
                case LeaderboardType.HighestLevel:
                    return save.HighestLevel;
                case LeaderboardType.TotalKills:
                    return save.TotalKills;
                default:
                    return 0;
            }
        }

        // =====================================================================
        // TẢI BẢNG
        // =====================================================================
        /// <summary>
        /// Tải top N người chơi. Ở chế độ offline sẽ trả về 1 dòng duy nhất là chính người chơi.
        /// Callback cuối cùng: (entries, isOnline).
        /// </summary>
        public void Fetch(LeaderboardType type, Action<LeaderboardEntry[], bool> onDone)
        {
#if !GOOGLE_PLAY_GAMES
            onDone?.Invoke(BuildOfflineEntries(type), false);
            return;
#else
            if (!isSignedIn || PlayGamesPlatform.Instance == null || !config.IsConfigured)
            {
                onDone?.Invoke(BuildOfflineEntries(type), false);
                return;
            }

            string id = config.GetLeaderboardId(type);
            if (string.IsNullOrEmpty(id))
            {
                onDone?.Invoke(BuildOfflineEntries(type), false);
                return;
            }

            int count = Mathf.Clamp(config.maxEntries, 1, 50);

            PlayGamesPlatform.Instance.LoadScores(
                id,
                LeaderboardStart.TopScores,
                count,
                LeaderboardCollection.Public,
                LeaderboardTimeSpan.AllTime,
                data =>
                {
                    buffer.Clear();

                    if (data != null && data.Valid)
                    {
                        var scores = data.Scores;
                        for (int i = 0; i < scores.Length; i++)
                        {
                            var s = scores[i];
                            string name = string.IsNullOrEmpty(s.UserName) ? "Player" : s.UserName;

                            buffer.Add(new LeaderboardEntry(
                                s.Rank,
                                name,
                                s.Value,
                                s.UserID == PlayGamesPlatform.Instance.GetUserId()));
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Leaderboard] Không tải được bảng '{type}'.");
                    }

                    onDone?.Invoke(buffer.ToArray(), true);
                    OnLeaderboardLoaded?.Invoke(type, buffer.ToArray(), true);
                });
#endif
        }

        /// <summary>Bảng offline: chỉ có dòng của người chơi hiện tại.</summary>
        private LeaderboardEntry[] BuildOfflineEntries(LeaderboardType type)
        {
            string name = GetPlayerName();
            long score = GetLocalScore(type);

            return new[]
            {
                new LeaderboardEntry(1, name, score, true)
            };
        }
    }
}
