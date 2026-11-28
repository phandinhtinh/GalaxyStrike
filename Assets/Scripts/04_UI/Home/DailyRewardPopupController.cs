using System;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    /// <summary>
    /// Popup quà đăng nhập theo lịch 7 ngày: dải 7 ô (đã nhận / hôm nay / sắp tới) và hai thẻ
    /// Coin + Gem hiện phần thưởng của hôm nay. Dải ô được dựng bằng code từ <see cref="DailyLoginRewards"/>.
    /// </summary>
    public sealed class DailyRewardPopupController : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private Sprite inactiveClaimSprite;

        private const float TileWidth = 84f;
        private const float TileHeight = 112f;
        private const float TileSpacing = 8f;
        private const float StripY = 243f;
        private const float IntroY = 338f;
        private const float CardsY = 15f;

        private static readonly Color TileFuture = new Color(0.05f, 0.11f, 0.24f, 0.92f);
        private static readonly Color TileToday = new Color(0.16f, 0.2f, 0.36f, 0.96f);
        private static readonly Color TileClaimed = new Color(0.04f, 0.16f, 0.14f, 0.92f);
        private static readonly Color OutlineFuture = new Color(0.2f, 0.55f, 0.95f, 0.55f);
        private static readonly Color OutlineToday = new Color(1f, 0.82f, 0.3f, 1f);
        private static readonly Color OutlineClaimed = new Color(0.3f, 0.95f, 0.55f, 0.8f);

        private sealed class DayTile
        {
            public RectTransform rect;
            public Image background;
            public Outline outline;
            public Image icon;
            public Image check;
            public TMP_Text dayLabel;
            public TMP_Text amount;
        }

        private Sprite readyClaimSprite;
        private DayTile[] tiles;
        private TMP_Text introText;
        private float refreshAt;
        private bool claimFailed;

        private void Awake()
        {
            readyClaimSprite = claimButton.image.sprite;
            BuildCalendar();
        }

        private void OnEnable()
        {
            claimFailed = false;
            Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
            else if (Time.unscaledTime >= refreshAt)
                Refresh();

            // Ô hôm nay (chưa nhận) nhấp nháy nhẹ cho dễ thấy.
            if (tiles != null && !SaveManager.Instance.IsDailyLoginRewardClaimed)
            {
                DayTile today = tiles[SaveManager.Instance.DailyLoginDayIndex];
                float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f);
                today.rect.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            (claimButton.interactable ? claimButton : closeButton).Select();
        }

        public void Close()
        {
            PopupTransition.Close(gameObject);
            openButton.Select();
        }

        public void Claim()
        {
            if (!isActiveAndEnabled || !claimButton.interactable)
                return;

            DailyLoginRewards.Day reward = SaveManager.Instance.TodayDailyLoginReward;
            int day = SaveManager.Instance.DailyLoginDayIndex + 1;
            claimFailed = !SaveManager.Instance.TryClaimDailyLoginReward();
            if (!claimFailed)
            {
                AudioManager.Play(SfxCue.Reward);
                Toast.Show($"Day {day} reward claimed!\n+{reward.Coins:N0} Coins   +{reward.Gems:N0} Gems",
                    ToastKind.Success);
            }

            Refresh();
            if (!claimButton.interactable)
                closeButton.Select();
        }

        private void Refresh()
        {
            refreshAt = Time.unscaledTime + 1f;
            SaveManager save = SaveManager.Instance;
            bool claimed = save.IsDailyLoginRewardClaimed;
            int todayIndex = save.DailyLoginDayIndex;
            int claimedCount = save.DailyLoginDaysClaimed;
            DailyLoginRewards.Day today = DailyLoginRewards.Days[todayIndex];

            coinsText.text = $"{today.Coins:N0} COINS";
            gemsText.text = $"{today.Gems:N0} GEMS";
            if (introText != null)
                introText.text = $"Day {todayIndex + 1} of {DailyLoginRewards.Count}";

            claimButton.interactable = !claimed;
            claimButton.image.sprite = claimed && inactiveClaimSprite != null ? inactiveClaimSprite : readyClaimSprite;
            claimLabel.text = claimed ? "CLAIMED" : "CLAIM";
            if (claimed)
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan remaining = now.Date.AddDays(1) - now;
                statusText.text = "NEXT REWARD IN " + remaining.ToString(@"hh\:mm\:ss");
            }
            else
            {
                statusText.text = claimFailed ? "Could not claim. Try again." : "Claim daily to keep your streak!";
            }

            RefreshTiles(todayIndex, claimedCount, claimed);
        }

        private void RefreshTiles(int todayIndex, int claimedCount, bool claimedToday)
        {
            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Length; i++)
            {
                DayTile tile = tiles[i];
                bool done = i < claimedCount;
                bool isToday = i == todayIndex && !claimedToday;
                tile.background.color = done ? TileClaimed : isToday ? TileToday : TileFuture;
                tile.outline.effectColor = done ? OutlineClaimed : isToday ? OutlineToday : OutlineFuture;
                tile.check.gameObject.SetActive(done);
                tile.icon.color = done ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                tile.dayLabel.color = isToday ? OutlineToday : done ? OutlineClaimed : Color.white;
                tile.amount.alpha = done ? 0.45f : 1f;
                if (!isToday)
                    tile.rect.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Dựng dải 7 ô ngày giữa tiêu đề và hai thẻ phần thưởng; dòng giới thiệu được đẩy lên
        /// một chút để có chỗ. Icon lấy từ hai thẻ Coin/Gem có sẵn trong popup.
        /// </summary>
        private void BuildCalendar()
        {
            RectTransform frame = coinsText.transform.parent != null
                ? coinsText.transform.parent.parent as RectTransform
                : null;
            if (frame == null)
                return;

            Transform intro = frame.Find("Daily_Intro");
            if (intro != null)
            {
                introText = intro.GetComponent<TMP_Text>();
                var introRect = (RectTransform)intro;
                introRect.anchoredPosition = new Vector2(introRect.anchoredPosition.x, IntroY);
                if (introText != null)
                    introText.fontSize = 40f;
            }

            // Hạ hai thẻ Coin/Gem xuống một chút để tách khỏi dải ngày.
            foreach (TMP_Text amount in new[] { coinsText, gemsText })
            {
                if (amount != null && amount.transform.parent is RectTransform card)
                    card.anchoredPosition = new Vector2(card.anchoredPosition.x, CardsY);
            }

            Sprite coin = FindIcon(coinsText, "Coin_Icon");
            Sprite gem = FindIcon(gemsText, "Gem_Icon");
            Sprite check = Resources.Load<Sprite>("UI/Toast/Icon_Success");

            var strip = new GameObject("Daily_Calendar", typeof(RectTransform));
            var stripRect = (RectTransform)strip.transform;
            stripRect.SetParent(frame, false);
            stripRect.anchoredPosition = new Vector2(0f, StripY);

            int count = DailyLoginRewards.Count;
            float total = count * TileWidth + (count - 1) * TileSpacing;
            stripRect.sizeDelta = new Vector2(total, TileHeight);
            tiles = new DayTile[count];
            for (int i = 0; i < count; i++)
            {
                bool bigDay = i == count - 1;
                float x = -total * 0.5f + TileWidth * 0.5f + i * (TileWidth + TileSpacing);
                tiles[i] = CreateTile(stripRect, i, x, bigDay ? gem : coin, check,
                    bigDay ? DailyLoginRewards.Days[i].Gems : DailyLoginRewards.Days[i].Coins);
            }
        }

        private DayTile CreateTile(RectTransform parent, int index, float x, Sprite iconSprite, Sprite checkSprite, int amount)
        {
            var tile = new DayTile();
            var go = new GameObject("Day_" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tile.rect = (RectTransform)go.transform;
            tile.rect.SetParent(parent, false);
            tile.rect.anchoredPosition = new Vector2(x, 0f);
            tile.rect.sizeDelta = new Vector2(TileWidth, TileHeight);
            tile.background = go.GetComponent<Image>();
            tile.background.raycastTarget = false;
            tile.outline = go.AddComponent<Outline>();
            tile.outline.effectDistance = new Vector2(2f, -2f);

            tile.dayLabel = CreateText(tile.rect, "Label", "DAY " + (index + 1), 19f, new Vector2(0f, 38f));
            tile.icon = CreateImage(tile.rect, "Icon", iconSprite, new Vector2(0f, 2f), 46f);
            tile.amount = CreateText(tile.rect, "Amount", amount.ToString("N0"), 19f, new Vector2(0f, -38f));
            tile.check = CreateImage(tile.rect, "Check", checkSprite, new Vector2(0f, 2f), 44f);
            return tile;
        }

        private TMP_Text CreateText(RectTransform parent, string name, string value, float size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(TileWidth - 4f, 28f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = coinsText.font;
            text.fontSharedMaterial = coinsText.fontSharedMaterial;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Vector2 position, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        private static Sprite FindIcon(TMP_Text amountText, string iconName)
        {
            Transform card = amountText != null ? amountText.transform.parent : null;
            Transform icon = card != null ? card.Find(iconName) : null;
            Image image = icon != null ? icon.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }
    }
}
