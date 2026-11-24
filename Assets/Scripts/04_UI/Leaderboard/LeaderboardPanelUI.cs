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

    private readonly List<RowView> rows = new List<RowView>(VisibleRowCount);
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI yourRankName;
    private TextMeshProUGUI yourRankScore;
    private bool isBound;
    private bool isLoading;

    private readonly struct RowView
    {
        public readonly GameObject Root;
        public readonly TextMeshProUGUI Name;
        public readonly TextMeshProUGUI Score;

        public RowView(GameObject root, TextMeshProUGUI name, TextMeshProUGUI score)
        {
            Root = root;
            Name = name;
            Score = score;
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
            rows.Add(new RowView(row.gameObject, name, score));
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
    }

    private void RequestRefresh()
    {
        if (isLoading)
            return;

        LeaderboardService service = LeaderboardService.Instance;
        if (service == null)
            return;

        isLoading = true;
        SetStatus("Đang tải bảng xếp hạng…");

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

            SetStatus(isOnline ? "Chưa có dữ liệu." : "Chưa có kết nối Play Games.");
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

            if (entry.IsCurrentPlayer)
                currentPlayer = entry;
        }

        if (currentPlayer.HasValue)
        {
            if (yourRankName != null)
                yourRankName.text = currentPlayer.Value.PlayerName;
            if (yourRankScore != null)
                yourRankScore.text = currentPlayer.Value.Score.ToString("N0");
        }

        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

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
        statusText.gameObject.SetActive(true);
    }
}
