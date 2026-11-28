using System.Collections.Generic;
using GalaxyStrike.Leaderboard;
using TMPro;
using UnityEngine;

/// <summary>
/// Binds the leaderboard data service to the scene-authored leaderboard layout.
/// The visual hierarchy is built in the editor so it is visible and editable in
/// Leaderboard_Panel even while the panel is inactive.
/// </summary>
public sealed class LeaderboardPanelUI : MonoBehaviour
{
    private const int VisibleRowCount = 7;
    private const string ProfileAvatarPath = "Canvas/SafeArea_Container/TopBar_Panel/PlayerProfile_Block/Avatar";

    [Tooltip("Avatar của người chơi hiện tại. Để trống thì lấy avatar trên thanh profile của Home.")]
    [SerializeField] private Sprite playerAvatar;

    /// <summary>
    /// Vị trí khung avatar vẽ sẵn trong từng sprite dòng (toạ độ chuẩn hoá, gốc dưới-trái).
    /// Avatar của người chơi được đặt đè đúng chỗ này để dòng của họ và ô "YOUR RANK"
    /// dùng chung một avatar thay vì avatar trang trí khác nhau của từng hạng.
    /// </summary>
    private static readonly Dictionary<string, Rect> AvatarSlots = new Dictionary<string, Rect>
    {
        { "YourRankPanel", Rect.MinMaxRect(0.333f, 0.210f, 0.429f, 0.790f) },
        { "RowTop1", Rect.MinMaxRect(0.191f, 0.167f, 0.300f, 0.833f) },
        { "RowTop2", Rect.MinMaxRect(0.196f, 0.177f, 0.311f, 0.813f) },
        { "RowTop3", Rect.MinMaxRect(0.196f, 0.173f, 0.311f, 0.837f) },
        { "RowRank4", Rect.MinMaxRect(0.202f, 0.108f, 0.312f, 0.880f) },
        { "RowRank5", Rect.MinMaxRect(0.202f, 0.108f, 0.312f, 0.880f) },
        { "RowRank6", Rect.MinMaxRect(0.202f, 0.108f, 0.312f, 0.880f) },
        { "RowRank7", Rect.MinMaxRect(0.202f, 0.108f, 0.312f, 0.880f) },
    };

    private readonly List<RowView> rows = new List<RowView>(VisibleRowCount);
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI yourRankName;
    private TextMeshProUGUI yourRankScore;
    private TextMeshProUGUI yourRankNumber;
    private GameObject yourRankAvatar;
    private bool isBound;
    private bool isLoading;

    private readonly struct RowView
    {
        public readonly GameObject Root;
        public readonly TextMeshProUGUI Name;
        public readonly TextMeshProUGUI Score;
        public readonly GameObject PlayerAvatar;

        public RowView(GameObject root, TextMeshProUGUI name, TextMeshProUGUI score, GameObject playerAvatar)
        {
            Root = root;
            Name = name;
            Score = score;
            PlayerAvatar = playerAvatar;
        }
    }

    private void OnEnable()
    {
        BindSceneLayout();

        LeaderboardService service = LeaderboardService.Instance;
        if (service != null)
        {
            service.OnSignInChanged += HandleSignInChanged;
            service.OnLeaderboardLoaded += HandleLeaderboardLoaded;
        }

        RequestRefresh();
    }

    private void OnDisable()
    {
        LeaderboardService service = FindFirstObjectByType<LeaderboardService>();
        if (service != null)
        {
            service.OnSignInChanged -= HandleSignInChanged;
            service.OnLeaderboardLoaded -= HandleLeaderboardLoaded;
        }
    }

    private void BindSceneLayout()
    {
        if (isBound)
            return;

        isBound = true;
        rows.Clear();

        Transform frame = transform.Find("Leaderboard_Frame");
        Transform content = frame != null
            ? frame.Find("Leaderboard_ScrollView/Viewport/Content")
            : null;

        if (content == null)
        {
            Debug.LogError("[LeaderboardPanelUI] The scene-authored leaderboard content is missing.", this);
            return;
        }

        for (int rank = 1; rank <= VisibleRowCount; rank++)
        {
            Transform row = content.Find($"Row_{rank}");
            if (row == null)
                continue;

            TextMeshProUGUI name = row.Find("DataMask/Player_Name")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI score = row.Find("DataMask/Score")?.GetComponent<TextMeshProUGUI>();
            rows.Add(new RowView(row.gameObject, name, score, CreateAvatarOverlay(row)));
        }

        statusText = frame.Find("Leaderboard_ScrollView/Viewport/Leaderboard_Status")
            ?.GetComponent<TextMeshProUGUI>();

        Transform yourRank = frame.Find("Your_Rank_Panel");
        yourRankName = yourRank != null
            ? yourRank.Find("DataMask/Player_Name")?.GetComponent<TextMeshProUGUI>()
            : null;
        yourRankScore = yourRank != null
            ? yourRank.Find("DataMask/Score")?.GetComponent<TextMeshProUGUI>()
            : null;
        // Huy hiệu hạng của ô "YOUR RANK" (sprite không còn vẽ sẵn số "2").
        yourRankNumber = yourRank != null
            ? yourRank.Find("Rank_Number")?.GetComponent<TextMeshProUGUI>()
            : null;
        if (yourRankNumber != null)
            yourRankNumber.text = "-";
        yourRankAvatar = yourRank != null ? CreateAvatarOverlay(yourRank) : null;
    }

    /// <summary>
    /// Tạo (một lần) Image avatar của người chơi đè lên khung avatar vẽ sẵn trong sprite dòng.
    /// Trả null nếu không biết vị trí khung hoặc không có avatar để hiển thị.
    /// </summary>
    private GameObject CreateAvatarOverlay(Transform row)
    {
        Transform existing = row.Find("Player_Avatar");
        if (existing != null)
            return existing.gameObject;

        UnityEngine.UI.Image rowImage = row.GetComponent<UnityEngine.UI.Image>();
        Sprite avatar = ResolvePlayerAvatar();
        if (rowImage == null || rowImage.sprite == null || avatar == null ||
            !AvatarSlots.TryGetValue(rowImage.sprite.name, out Rect slot))
            return null;

        var go = new GameObject("Player_Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(row, false);
        rect.anchorMin = slot.min;
        rect.anchorMax = slot.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // Phủ kín viền khung cũ.
        rect.localScale = new Vector3(1.06f, 1.06f, 1f);

        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = avatar;
        image.preserveAspect = true;
        image.raycastTarget = false;
        go.SetActive(false);
        return go;
    }

    private Sprite ResolvePlayerAvatar()
    {
        if (playerAvatar == null)
        {
            GameObject profile = GameObject.Find(ProfileAvatarPath);
            if (profile != null && profile.TryGetComponent(out UnityEngine.UI.Image image))
                playerAvatar = image.sprite;
        }
        return playerAvatar;
    }

    private void RequestRefresh()
    {
        if (isLoading)
            return;

        LeaderboardService service = LeaderboardService.Instance;
        if (service == null)
            return;

        isLoading = true;
        // Font LiberationSans không có dấu tiếng Việt nên thông báo dùng tiếng Anh như phần còn lại của UI.
        SetStatus("LOADING RANKINGS...");

        service.Fetch(LeaderboardType.HighScore, (entries, isOnline) =>
        {
            isLoading = false;
            RenderEntries(entries, isOnline);
        });
    }

    private void RenderEntries(LeaderboardEntry[] entries, bool isOnline)
    {
        if (rows.Count == 0)
            BindSceneLayout();

        if (entries == null || entries.Length == 0)
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Root.SetActive(false);

            SetStatus(isOnline ? "NO SCORES YET\nPlay a battle to set the first record." : OfflineMessage);
            return;
        }

        int count = Mathf.Min(entries.Length, rows.Count);
        LeaderboardEntry? currentPlayer = null;

        for (int i = 0; i < rows.Count; i++)
        {
            RowView row = rows[i];
            bool hasEntry = i < count;
            row.Root.SetActive(hasEntry);
            if (!hasEntry)
                continue;

            LeaderboardEntry entry = entries[i];
            if (row.Name != null)
                row.Name.text = entry.PlayerName;
            if (row.Score != null)
                row.Score.text = entry.Score.ToString("N0");
            if (row.PlayerAvatar != null)
                row.PlayerAvatar.SetActive(entry.IsCurrentPlayer);

            if (entry.IsCurrentPlayer)
                currentPlayer = entry;
        }

        if (yourRankAvatar != null)
            yourRankAvatar.SetActive(true);

        if (currentPlayer.HasValue)
        {
            if (yourRankName != null)
                yourRankName.text = currentPlayer.Value.PlayerName;
            if (yourRankScore != null)
                yourRankScore.text = currentPlayer.Value.Score.ToString("N0");
            if (yourRankNumber != null)
                yourRankNumber.text = currentPlayer.Value.Rank > 0 ? currentPlayer.Value.Rank.ToString() : "-";
        }

        // Offline chỉ có thành tích của chính người chơi: giải thích vì sao bảng trống phía dưới.
        if (!isOnline)
            SetStatus(OfflineMessage);
        else if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private const string OfflineMessage =
        "OFFLINE MODE\nSign in to Google Play Games to compete with pilots worldwide.";

    private void HandleSignInChanged(bool signedIn)
    {
        RequestRefresh();
    }

    private void HandleLeaderboardLoaded(LeaderboardType type, LeaderboardEntry[] entries, bool isOnline)
    {
        if (type == LeaderboardType.HighScore)
            RenderEntries(entries, isOnline);
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 20f;
        statusText.fontSizeMax = 34f;
        statusText.gameObject.SetActive(true);
    }
}
