using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Tasks
{
    public enum TaskPopupRewardType
    {
        None,
        Coins,
        Gems,
        Energy
    }

    /// <summary>
    /// Dữ liệu hiển thị của một dòng do editor builder sinh ra. Từ khi có <see cref="TaskCatalog"/>,
    /// popup chỉ còn dùng các Sprite ở đây (icon dòng, icon phần thưởng); tiêu đề, mục tiêu,
    /// tiến độ và phần thưởng lấy từ catalog + save.
    /// </summary>
    [Serializable]
    public sealed class TaskPopupEntry
    {
        public Sprite icon;
        public string title;
        [Range(0f, 1f)] public float progress;
        public string progressLabel;
        public Sprite rewardOneIcon;
        public string rewardOneAmount;
        public TaskPopupRewardType rewardOneType;
        public Sprite rewardTwoIcon;
        public string rewardTwoAmount;
        public TaskPopupRewardType rewardTwoType;
        public string actionLabel = "CLAIM";
        public bool claimAvailable;
        public bool isDailyLoginReward;
    }

    [Serializable]
    public sealed class TaskPopupCategory
    {
        public TaskPopupEntry[] entries;
    }

    /// <summary>
    /// Popup TASK ở HomeScene với 3 tab Daily / Weekly / Achievements.
    /// Nhiệm vụ lấy từ <see cref="TaskCatalog"/>, tiến độ và trạng thái đã nhận lấy từ
    /// <see cref="SaveManager"/> (đếm thật trong trận, tự reset theo ngày/tuần UTC).
    /// </summary>
    public sealed class TaskPopupController : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private Image[] tabBackgrounds;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private TaskPopupRowView[] rows;
        [SerializeField] private TaskPopupCategory[] categories;

        [SerializeField] private Sprite goButtonSprite;
        [SerializeField] private Sprite claimButtonSprite;

        [Header("Reward icons (để trống thì lấy từ layout builder)")]
        [SerializeField] private Sprite coinRewardIcon;
        [SerializeField] private Sprite gemRewardIcon;
        [SerializeField] private Sprite energyRewardIcon;

        private UnityAction[] tabCallbacks;
        private readonly Dictionary<Button, UnityAction> actionCallbacks = new Dictionary<Button, UnityAction>();
        private int selectedCategoryIndex;
        private float refreshAt;

        private const string ClaimLabel = "CLAIM";
        private const string ClaimedLabel = "CLAIMED";
        private const string GoLabel = "GO";

        private static readonly Color32 SelectedTabColor = new Color32(255, 179, 24, 255);
        private static readonly Color32 IdleTabColor = new Color32(7, 42, 91, 255);
        private static readonly Color32 DisabledActionColor = new Color32(158, 185, 210, 255);

        /// <summary>Called by the HomeScene editor builder to connect generated UI objects.</summary>
        public void Configure(
            GameObject root,
            Button open,
            Button close,
            ScrollRect listScrollRect,
            Sprite goSprite,
            Sprite claimSprite,
            Button[] tabs,
            Image[] tabImages,
            TMP_Text[] tabTitles,
            TaskPopupRowView[] rowViews,
            TaskPopupCategory[] taskCategories)
        {
            UnbindButtons();
            popupRoot = root;
            openButton = open;
            closeButton = close;
            scrollRect = listScrollRect;
            goButtonSprite = goSprite;
            claimButtonSprite = claimSprite;
            tabButtons = tabs;
            tabBackgrounds = tabImages;
            tabLabels = tabTitles;
            rows = rowViews;
            categories = taskCategories;
        }

        private void Awake()
        {
            ResolveRewardIcons();
            BindButtons();
            if (popupRoot != null)
                popupRoot.SetActive(false);
            SelectTab(0);
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void Update()
        {
            // Popup mở qua 00:00 UTC thì tự reset; cũng cập nhật khi Daily Reward được nhận ở popup khác.
            if (popupRoot == null || !popupRoot.activeInHierarchy || Time.unscaledTime < refreshAt)
                return;

            refreshAt = Time.unscaledTime + 1f;
            RenderRows();
        }

        public void OpenPopup()
        {
            if (popupRoot == null)
                return;

            popupRoot.transform.SetAsLastSibling();
            popupRoot.SetActive(true);
            SelectTab(0);
        }

        public void ClosePopup()
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
        }

        /// <summary>
        /// Builder cũ không gắn loại phần thưởng cho icon; suy ra từ layout đã biết:
        /// dòng "Login today" có Coin + Gem, dòng "Destroy 50 enemies" có Energy ở ô thứ hai.
        /// </summary>
        private void ResolveRewardIcons()
        {
            TaskPopupEntry[] daily = categories != null && categories.Length > 0 && categories[0] != null
                ? categories[0].entries
                : null;
            if (daily == null)
                return;

            foreach (TaskPopupEntry entry in daily)
            {
                if (entry == null || !entry.isDailyLoginReward)
                    continue;
                if (coinRewardIcon == null) coinRewardIcon = entry.rewardOneIcon;
                if (gemRewardIcon == null) gemRewardIcon = entry.rewardTwoIcon;
            }

            if (energyRewardIcon == null && daily.Length > 1 && daily[1] != null)
                energyRewardIcon = daily[1].rewardTwoIcon;
        }

        private void BindButtons()
        {
            if (openButton != null)
                openButton.onClick.AddListener(OpenPopup);
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePopup);

            if (tabButtons != null)
            {
                tabCallbacks = new UnityAction[tabButtons.Length];
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int tabIndex = i;
                    tabCallbacks[i] = () => SelectTab(tabIndex);
                    if (tabButtons[i] != null)
                        tabButtons[i].onClick.AddListener(tabCallbacks[i]);
                }
            }

            BindActionButtons();
        }

        private void UnbindButtons()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(OpenPopup);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(ClosePopup);

            if (tabButtons != null && tabCallbacks != null)
            {
                int count = Mathf.Min(tabButtons.Length, tabCallbacks.Length);
                for (int i = 0; i < count; i++)
                {
                    if (tabButtons[i] != null && tabCallbacks[i] != null)
                        tabButtons[i].onClick.RemoveListener(tabCallbacks[i]);
                }
            }
            tabCallbacks = null;

            foreach (KeyValuePair<Button, UnityAction> binding in actionCallbacks)
            {
                if (binding.Key != null)
                    binding.Key.onClick.RemoveListener(binding.Value);
            }
            actionCallbacks.Clear();
        }

        private void SelectTab(int index)
        {
            int tabCount = tabButtons != null ? tabButtons.Length : 3;
            index = Mathf.Clamp(index, 0, Mathf.Max(0, tabCount - 1));
            selectedCategoryIndex = index;

            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                bool selected = i == index;
                if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
                    tabBackgrounds[i].color = selected ? SelectedTabColor : IdleTabColor;
                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                    tabLabels[i].color = selected ? Color.white : new Color32(183, 218, 255, 255);
            }

            EnsureRowCapacity(TaskCatalog.ForTab(index).Count);
            BindActionButtons();
            RenderRows();
            ResetScrollPosition();
        }

        /// <summary>Vẽ lại toàn bộ dòng của tab đang chọn theo tiến độ thật.</summary>
        private void RenderRows()
        {
            if (rows == null)
                return;

            IReadOnlyList<TaskDefinition> tasks = TaskCatalog.ForTab(selectedCategoryIndex);
            SaveManager save = SaveManager.Instance;

            for (int i = 0; i < rows.Length; i++)
            {
                TaskPopupRowView row = rows[i];
                if (row == null || row.root == null)
                    continue;

                TaskDefinition task = i < tasks.Count ? tasks[i] : null;
                row.root.SetActive(task != null);
                if (task == null)
                    continue;

                int progress = save != null ? Mathf.Min(save.GetTaskProgress(task), task.target) : 0;
                bool claimed = save != null && save.IsTaskClaimed(task);
                bool complete = progress >= task.target;

                if (row.icon != null)
                {
                    Sprite icon = GetLayoutEntry(selectedCategoryIndex, i)?.icon;
                    if (icon != null)
                        row.icon.sprite = icon;
                }
                if (row.title != null)
                    row.title.text = task.title;

                if (row.progressFill != null)
                {
                    row.progressFill.fillAmount = claimed ? 1f : (float)progress / task.target;
                    Transform track = row.progressFill.transform.parent;
                    if (track != null && track.name == "Progress_Track")
                        track.gameObject.SetActive(true);
                }
                if (row.progressLabel != null)
                    row.progressLabel.text = string.Format(CultureInfo.InvariantCulture, "{0:N0}/{1:N0}",
                        claimed ? task.target : progress, task.target);

                SetReward(row.rewardOneIcon, row.rewardOneAmount, task.rewardOne);
                SetReward(row.rewardTwoIcon, row.rewardTwoAmount, task.rewardTwo);

                RenderActionButton(row, complete, claimed);
            }
        }

        private TaskPopupEntry GetLayoutEntry(int categoryIndex, int rowIndex)
        {
            if (categories == null || categoryIndex < 0 || categoryIndex >= categories.Length)
                return null;

            TaskPopupEntry[] entries = categories[categoryIndex]?.entries;
            return entries != null && rowIndex >= 0 && rowIndex < entries.Length ? entries[rowIndex] : null;
        }

        private void BindActionButtons()
        {
            if (rows == null)
                return;

            foreach (TaskPopupRowView row in rows)
            {
                if (row == null || row.actionButton == null || actionCallbacks.ContainsKey(row.actionButton))
                    continue;

                TaskPopupRowView capturedRow = row;
                UnityAction callback = () => HandleActionClick(capturedRow);
                actionCallbacks.Add(row.actionButton, callback);
                row.actionButton.onClick.AddListener(callback);
            }
        }

        /// <summary>
        /// Nút của dòng: đã xong -> nhận thưởng; chưa xong -> "GO" đóng popup để người chơi vào trận.
        /// </summary>
        private void HandleActionClick(TaskPopupRowView row)
        {
            int rowIndex = rows != null ? Array.IndexOf(rows, row) : -1;
            IReadOnlyList<TaskDefinition> tasks = TaskCatalog.ForTab(selectedCategoryIndex);
            if (rowIndex < 0 || rowIndex >= tasks.Count)
                return;

            TaskDefinition task = tasks[rowIndex];
            SaveManager save = SaveManager.Instance;
            if (save == null || save.IsTaskClaimed(task))
                return;

            if (!save.IsTaskComplete(task))
            {
                ClosePopup();
                return;
            }

            if (save.TryClaimTask(task))
                AudioManager.Play(SfxCue.Reward);

            RenderRows();
        }

        private void RenderActionButton(TaskPopupRowView row, bool complete, bool claimed)
        {
            bool canClaim = complete && !claimed;
            if (row.actionLabel != null)
                row.actionLabel.text = claimed ? ClaimedLabel : canClaim ? ClaimLabel : GoLabel;

            if (row.actionButton != null)
            {
                row.actionButton.interactable = !claimed;
                if (row.actionButton.image != null)
                {
                    if (goButtonSprite != null)
                        row.actionButton.image.sprite = canClaim && claimButtonSprite != null ? claimButtonSprite : goButtonSprite;
                    row.actionButton.image.type = Image.Type.Simple;
                    row.actionButton.image.preserveAspect = false;
                    row.actionButton.image.color = claimed ? (Color)DisabledActionColor : Color.white;
                }
            }

            if (row.actionFace != null)
                row.actionFace.enabled = false;
        }

        private void SetReward(Image icon, TMP_Text amount, TaskReward reward)
        {
            Sprite sprite = null;
            switch (reward.type)
            {
                case TaskRewardType.Coins: sprite = coinRewardIcon; break;
                case TaskRewardType.Gems: sprite = gemRewardIcon; break;
                case TaskRewardType.Energy: sprite = energyRewardIcon; break;
            }

            bool visible = reward.type != TaskRewardType.None && reward.amount > 0;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.gameObject.SetActive(visible && sprite != null);
            }

            if (amount != null)
            {
                amount.text = visible ? "x" + reward.amount.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
                amount.gameObject.SetActive(visible);
            }
        }

        private void EnsureRowCapacity(int requiredCount)
        {
            if (rows == null || rows.Length == 0 || rows[0] == null || requiredCount <= rows.Length)
                return;

            List<TaskPopupRowView> expandedRows = new List<TaskPopupRowView>(rows);
            TaskPopupRowView template = rows[0];
            Transform parent = scrollRect != null && scrollRect.content != null
                ? scrollRect.content
                : template.transform.parent;

            while (expandedRows.Count < requiredCount)
            {
                GameObject clone = Instantiate(template.gameObject, parent, false);
                clone.name = "Task_Row_" + (expandedRows.Count + 1);
                TaskPopupRowView row = clone.GetComponent<TaskPopupRowView>();
                if (row == null)
                {
                    Destroy(clone);
                    break;
                }

                row.root = clone;
                expandedRows.Add(row);
            }

            rows = expandedRows.ToArray();
        }

        private void ResetScrollPosition()
        {
            if (scrollRect == null)
                return;

            scrollRect.StopMovement();
            Canvas.ForceUpdateCanvases();
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
