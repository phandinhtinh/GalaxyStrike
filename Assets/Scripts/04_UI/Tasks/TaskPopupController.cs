using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
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
        public string actionLabel = "GO";
        public bool claimAvailable;
    }

    [Serializable]
    public sealed class TaskPopupCategory
    {
        public TaskPopupEntry[] entries;
    }

    /// <summary>Controls the HomeScene task popup and its Daily, Weekly, and Achievements tabs.</summary>
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

        private UnityAction[] tabCallbacks;
        private readonly Dictionary<Button, UnityAction> actionCallbacks = new Dictionary<Button, UnityAction>();
        private int selectedCategoryIndex;

        private const string ClaimPrefsPrefix = "GalaxyStrike.TaskPopup.Claimed.v1";
        private const string ClaimedLabel = "CLAIMED";

        [SerializeField] private Sprite goButtonSprite;
        [SerializeField] private Sprite claimButtonSprite;

        private static readonly Color32 SelectedTabColor = new Color32(255, 179, 24, 255);
        private static readonly Color32 IdleTabColor = new Color32(7, 42, 91, 255);

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
            BindButtons();
            if (popupRoot != null)
                popupRoot.SetActive(false);
            SelectTab(0);
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        public void OpenPopup()
        {
            if (popupRoot == null)
                return;

            popupRoot.SetActive(true);
            SelectTab(0);
        }

        public void ClosePopup()
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
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

            if (tabButtons == null || tabCallbacks == null)
            {
                tabCallbacks = null;
            }
            else
            {
                int count = Mathf.Min(tabButtons.Length, tabCallbacks.Length);
                for (int i = 0; i < count; i++)
                {
                    if (tabButtons[i] != null && tabCallbacks[i] != null)
                        tabButtons[i].onClick.RemoveListener(tabCallbacks[i]);
                }

                tabCallbacks = null;
            }

            foreach (KeyValuePair<Button, UnityAction> binding in actionCallbacks)
            {
                if (binding.Key != null)
                    binding.Key.onClick.RemoveListener(binding.Value);
            }
            actionCallbacks.Clear();
        }

        private void SelectTab(int index)
        {
            if (categories == null || categories.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, categories.Length - 1);
            selectedCategoryIndex = index;
            int tabCount = tabButtons != null ? tabButtons.Length : 0;
            for (int i = 0; i < tabCount; i++)
            {
                bool selected = i == index;
                if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
                    tabBackgrounds[i].color = selected ? SelectedTabColor : IdleTabColor;
                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                    tabLabels[i].color = selected ? Color.white : new Color32(183, 218, 255, 255);
            }

            TaskPopupEntry[] entries = categories[index] != null ? categories[index].entries : null;
            EnsureRowCapacity(entries != null ? entries.Length : 0);
            BindActionButtons();

            if (rows != null)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    TaskPopupRowView row = rows[i];
                    if (row == null || row.root == null)
                        continue;

                    TaskPopupEntry entry = entries != null && i < entries.Length ? entries[i] : null;
                    row.root.SetActive(entry != null);
                    if (entry == null)
                        continue;

                    if (entry.claimAvailable && HasClaimed(index, i))
                    {
                        entry.claimAvailable = false;
                        entry.actionLabel = ClaimedLabel;
                    }

                    if (row.icon != null)
                        row.icon.sprite = entry.icon;
                    if (row.title != null)
                        row.title.text = entry.title;
                    if (row.progressFill != null)
                        row.progressFill.fillAmount = Mathf.Clamp01(entry.progress);
                    if (row.progressLabel != null)
                        row.progressLabel.text = entry.progressLabel;
                    SetReward(row.rewardOneIcon, row.rewardOneAmount, entry.rewardOneIcon, entry.rewardOneAmount);
                    SetReward(row.rewardTwoIcon, row.rewardTwoAmount, entry.rewardTwoIcon, entry.rewardTwoAmount);

                    RenderActionButton(row, entry);
                }
            }

            ResetScrollPosition();
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

        private void HandleActionClick(TaskPopupRowView row)
        {
            int rowIndex = rows != null ? Array.IndexOf(rows, row) : -1;
            if (rowIndex < 0 || categories == null || selectedCategoryIndex >= categories.Length)
                return;

            TaskPopupEntry[] entries = categories[selectedCategoryIndex] != null
                ? categories[selectedCategoryIndex].entries
                : null;
            if (entries == null || rowIndex >= entries.Length)
                return;

            TaskPopupEntry entry = entries[rowIndex];
            if (entry == null || !entry.claimAvailable)
                return;

            if (HasClaimed(selectedCategoryIndex, rowIndex))
            {
                entry.claimAvailable = false;
                entry.actionLabel = ClaimedLabel;
                RenderActionButton(row, entry);
                return;
            }

            if (!GrantReward(entry.rewardOneType, entry.rewardOneAmount)
                || !GrantReward(entry.rewardTwoType, entry.rewardTwoAmount))
                return;

            entry.claimAvailable = false;
            entry.actionLabel = ClaimedLabel;
            PlayerPrefs.SetInt(GetClaimPrefsKey(selectedCategoryIndex, rowIndex), 1);
            PlayerPrefs.Save();
            RenderActionButton(row, entry);
        }

        private void RenderActionButton(TaskPopupRowView row, TaskPopupEntry entry)
        {
            if (row.actionLabel != null)
                row.actionLabel.text = entry.actionLabel;

            bool isClaim = entry.claimAvailable || string.Equals(entry.actionLabel, ClaimedLabel, StringComparison.Ordinal);
            if (row.actionButton != null)
            {
                row.actionButton.interactable = !string.Equals(entry.actionLabel, ClaimedLabel, StringComparison.Ordinal);
                if (row.actionButton.image != null)
                {
                    if (goButtonSprite != null)
                        row.actionButton.image.sprite = isClaim && claimButtonSprite != null ? claimButtonSprite : goButtonSprite;
                    row.actionButton.image.type = Image.Type.Simple;
                    row.actionButton.image.preserveAspect = false;
                    row.actionButton.image.color = Color.white;
                }
            }

            if (row.actionFace != null)
                row.actionFace.enabled = false;
        }

        private static bool GrantReward(TaskPopupRewardType rewardType, string amountText)
        {
            if (rewardType == TaskPopupRewardType.None)
                return true;

            int amount = ParseRewardAmount(amountText);
            if (amount <= 0)
                return false;

            SaveManager save = SaveManager.Instance;
            switch (rewardType)
            {
                case TaskPopupRewardType.Coins:
                    save.AddCoins(amount);
                    return true;
                case TaskPopupRewardType.Gems:
                    save.AddGems(amount);
                    return true;
                case TaskPopupRewardType.Energy:
                    save.AddEnergy(amount);
                    return true;
                default:
                    return false;
            }
        }

        private static int ParseRewardAmount(string amountText)
        {
            if (string.IsNullOrEmpty(amountText))
                return 0;

            StringBuilder digits = new StringBuilder(amountText.Length);
            foreach (char character in amountText)
            {
                if (char.IsDigit(character))
                    digits.Append(character);
            }

            return int.TryParse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                ? amount
                : 0;
        }

        private static bool HasClaimed(int categoryIndex, int rowIndex)
        {
            return PlayerPrefs.GetInt(GetClaimPrefsKey(categoryIndex, rowIndex), 0) == 1;
        }

        private static string GetClaimPrefsKey(int categoryIndex, int rowIndex)
        {
            string day = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return ClaimPrefsPrefix + "." + day + "." + categoryIndex + "." + rowIndex;
        }

        private void EnsureRowCapacity(int requiredCount)
        {
            if (requiredCount <= (rows != null ? rows.Length : 0) || rows == null || rows.Length == 0 || rows[0] == null)
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

        private static void SetReward(Image icon, TMP_Text amount, Sprite sprite, string text)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.gameObject.SetActive(sprite != null);
            }

            if (amount != null)
            {
                amount.text = text ?? string.Empty;
                amount.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }
    }
}
