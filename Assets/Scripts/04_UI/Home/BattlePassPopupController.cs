using System;
using System.Collections.Generic;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    /// <summary>
    /// Popup Battle Pass: danh sách cuộn các mốc XP trong <see cref="BattlePassRewards"/>.
    /// Scene chỉ có sẵn vài dòng mốc; khi mở lần đầu, các dòng đó được chuyển vào một ScrollRect
    /// và nhân bản dòng đầu cho đủ số mốc. Thanh XP hiện tiến độ tới mốc kế tiếp.
    /// </summary>
    public sealed class BattlePassPopupController : MonoBehaviour
    {
        [Serializable]
        private sealed class TierView
        {
            public Button claimButton;
            public TMP_Text claimLabel;
            public TMP_Text milestoneLabel;
            public TMP_Text rewardLabel;
            [NonSerialized] public Image icon;
            [NonSerialized] public RectTransform root;
        }

        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image xpFill;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private Sprite inactiveClaimSprite;
        [SerializeField] private TierView[] tiers;
        [SerializeField] private Sprite gemIcon;
        [SerializeField] private Sprite energyIcon;

        private const float RowSpacing = 16f;
        private const float ListTop = 212f;     // ngay dưới thanh XP
        private const float ListBottom = -348f; // ngay trên dòng gợi ý

        private readonly List<TierView> views = new List<TierView>();
        private Sprite readyClaimSprite;
        private Sprite coinIcon;
        private ScrollRect scroll;

        private void Awake()
        {
            if (tiers.Length > 0)
            {
                readyClaimSprite = tiers[0].claimButton.image.sprite;
                Transform icon = tiers[0].claimButton.transform.parent.Find("Coin_Icon");
                coinIcon = icon != null ? icon.GetComponent<Image>().sprite : null;
            }
            BuildList();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
            ScrollToCurrentTier();
            closeButton.Select();
        }

        public void Close()
        {
            PopupTransition.Close(gameObject);
            openButton.Select();
        }

        public void ClaimTier(int index)
        {
            if (!isActiveAndEnabled || index < 0 || index >= views.Count || !views[index].claimButton.interactable)
                return;

            bool granted = SaveManager.Instance.TryClaimBattlePassTier(index);
            if (granted)
            {
                AudioManager.Play(SfxCue.Reward);
                Toast.Show($"Tier {index + 1} reward claimed!\n+{DescribeReward(BattlePassRewards.Tiers[index], "   +")}",
                    ToastKind.Success);
            }

            Refresh();
            if (granted)
                closeButton.Select();
            else if (views[index].claimButton.interactable)
                views[index].claimLabel.text = "RETRY";
        }

        private void Refresh()
        {
            SaveManager save = SaveManager.Instance;
            IReadOnlyList<BattlePassRewards.Tier> all = BattlePassRewards.Tiers;
            int xp = save.Experience;

            // Tiến độ tới mốc kế tiếp chưa đạt (đạt hết thì hiện đầy).
            int next = all.Count - 1;
            for (int i = 0; i < all.Count; i++)
            {
                if (xp < all[i].RequiredExperience)
                {
                    next = i;
                    break;
                }
            }
            int target = all[next].RequiredExperience;
            int from = next > 0 && xp < target ? all[next - 1].RequiredExperience : 0;
            int shown = Mathf.Min(xp, target);
            xpLabel.text = xp >= all[all.Count - 1].RequiredExperience
                ? $"{xp:N0} XP  ·  MAX"
                : $"{shown:N0} / {target:N0} XP";
            xpFill.fillAmount = target > from ? Mathf.Clamp01((float)(shown - from) / (target - from)) : 1f;

            for (int i = 0; i < views.Count && i < all.Count; i++)
            {
                BattlePassRewards.Tier tier = all[i];
                TierView view = views[i];
                bool claimed = save.IsBattlePassTierClaimed(i);
                bool unlocked = xp >= tier.RequiredExperience;
                view.milestoneLabel.text = $"TIER {i + 1}  ·  {tier.RequiredExperience:N0} XP";
                view.rewardLabel.text = DescribeReward(tier, " + ").ToUpperInvariant();
                view.claimButton.interactable = unlocked && !claimed;
                view.claimLabel.text = claimed ? "CLAIMED" : unlocked ? "CLAIM" : "LOCKED";
                if (inactiveClaimSprite != null)
                    view.claimButton.image.sprite = view.claimButton.interactable ? readyClaimSprite : inactiveClaimSprite;
                if (view.icon != null)
                {
                    Sprite icon = IconFor(tier);
                    if (icon != null)
                        view.icon.sprite = icon;
                    view.icon.color = claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                }
            }
        }

        private Sprite IconFor(BattlePassRewards.Tier tier)
        {
            if (tier.Coins <= 0 && tier.Gems > 0 && gemIcon != null)
                return gemIcon;
            if (tier.Coins <= 0 && tier.Energy > 0 && energyIcon != null)
                return energyIcon;
            return coinIcon;
        }

        private static string DescribeReward(BattlePassRewards.Tier tier, string separator)
        {
            var parts = new List<string>(3);
            if (tier.Coins > 0) parts.Add($"{tier.Coins:N0} Coins");
            if (tier.Gems > 0) parts.Add($"{tier.Gems:N0} Gems");
            if (tier.Energy > 0) parts.Add($"{tier.Energy:N0} Energy");
            return string.Join(separator, parts);
        }

        /// <summary>Cuộn tới mốc có thể nhận đầu tiên (hoặc mốc chưa mở đầu tiên).</summary>
        private void ScrollToCurrentTier()
        {
            if (scroll == null || views.Count == 0)
                return;

            SaveManager save = SaveManager.Instance;
            int focus = -1;
            for (int i = 0; i < views.Count && focus < 0; i++)
            {
                if (!save.IsBattlePassTierClaimed(i))
                    focus = i;
            }
            if (focus < 0)
                focus = views.Count - 1;

            Canvas.ForceUpdateCanvases();
            RectTransform content = scroll.content;
            float viewport = ((RectTransform)scroll.viewport).rect.height;
            float max = Mathf.Max(0f, content.rect.height - viewport);
            float rowY = -views[focus].root.anchoredPosition.y - views[focus].root.rect.height * 0.5f;
            float y = Mathf.Clamp(rowY - RowSpacing, 0f, max);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            scroll.velocity = Vector2.zero;
        }

        private void BuildList()
        {
            if (tiers == null || tiers.Length == 0)
                return;

            var template = (RectTransform)tiers[0].claimButton.transform.parent;
            var frame = (RectTransform)template.parent;

            // Vùng cuộn
            var viewportGo = new GameObject("Tier_Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(frame, false);
            viewportRect.SetSiblingIndex(template.GetSiblingIndex());
            float width = template.rect.width + 24f;
            viewportRect.anchoredPosition = new Vector2(0f, (ListTop + ListBottom) * 0.5f);
            viewportRect.sizeDelta = new Vector2(width, ListTop - ListBottom);
            var hitArea = viewportGo.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f); // chỉ để bắt thao tác kéo

            var contentGo = new GameObject("Tier_Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(viewportRect, false);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            // Các dòng có sẵn trong scene + nhân bản dòng đầu cho đủ số mốc.
            var rows = new List<RectTransform>();
            foreach (TierView existing in tiers)
                rows.Add((RectTransform)existing.claimButton.transform.parent);
            for (int i = rows.Count; i < BattlePassRewards.Tiers.Count; i++)
            {
                RectTransform clone = Instantiate(template, content);
                clone.name = "Tier_" + (i + 1);
                rows.Add(clone);
            }

            float rowHeight = template.rect.height;
            float y = -RowSpacing * 0.5f;
            for (int i = 0; i < rows.Count; i++)
            {
                RectTransform row = rows[i];
                bool used = i < BattlePassRewards.Tiers.Count;
                row.gameObject.SetActive(used);
                if (!used)
                    continue;

                row.SetParent(content, false);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, y);
                y -= rowHeight + RowSpacing;

                views.Add(CreateView(row, i));
            }
            content.sizeDelta = new Vector2(width, -y - RowSpacing * 0.5f);
        }

        private TierView CreateView(RectTransform row, int index)
        {
            var view = new TierView
            {
                root = row,
                claimButton = row.Find("Claim_Button").GetComponent<Button>(),
                milestoneLabel = row.Find("Milestone").GetComponent<TMP_Text>(),
                rewardLabel = row.Find("Reward").GetComponent<TMP_Text>()
            };
            view.claimLabel = view.claimButton.transform.Find("Label").GetComponent<TMP_Text>();
            Transform icon = row.Find("Coin_Icon");
            view.icon = icon != null ? icon.GetComponent<Image>() : null;

            // Chữ mốc/phần thưởng có thể dài ("TIER 13 · 20,000 XP") -> tự thu nhỏ cho vừa.
            foreach (TMP_Text label in new[] { view.milestoneLabel, view.rewardLabel })
            {
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.enableAutoSizing = true;
            }
            view.milestoneLabel.fontSizeMin = 20f;
            view.milestoneLabel.fontSizeMax = 30f;
            view.rewardLabel.fontSizeMin = 18f;
            view.rewardLabel.fontSizeMax = 30f;

            // Tắt callback cũ gắn trong scene (theo chỉ số cố định) rồi gắn lại theo chỉ số thật.
            Button.ButtonClickedEvent onClick = view.claimButton.onClick;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
                onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            onClick.RemoveAllListeners();
            int captured = index;
            onClick.AddListener(() => ClaimTier(captured));
            return view;
        }
    }
}
