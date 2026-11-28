using System;
using System.Collections.Generic;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    /// <summary>
    /// Dấu báo đỏ "!" trên các nút ở Home, bật/tắt theo dữ liệu thật:
    ///  - Missions: còn nhiệm vụ đã xong chưa nhận thưởng.
    ///  - Daily: quà đăng nhập hôm nay chưa nhận.
    ///  - Gift: quà chào mừng chưa nhận.
    ///  - Battle Pass: có mốc đã đủ XP nhưng chưa nhận.
    /// Trước đây dấu "!" của Missions được vẽ cố định trong sprite nên luôn hiện.
    /// </summary>
    public sealed class HomeNotificationBadges : MonoBehaviour
    {
        private const string BadgeResource = "UI/NotificationBadge";
        // Tìm từ Canvas bằng Transform.Find để vẫn thấy nút khi Center_Panel đang ẩn (Home mở thẳng tab khác).
        private const string SidebarRoot = "SafeArea_Container/UI_Panels/Center_Panel/";

        private sealed class Badge
        {
            public RectTransform Rect;
            public Func<SaveManager, bool> IsActive;
        }

        private readonly List<Badge> badges = new List<Badge>(4);
        private float refreshAt;

        private void Start()
        {
            Sprite sprite = Resources.Load<Sprite>(BadgeResource);
            if (sprite == null)
            {
                Debug.LogWarning("[Badges] Missing Resources/" + BadgeResource);
                enabled = false;
                return;
            }

            // Missions: đặt đúng chỗ dấu "!" từng vẽ trong sprite nút (góc trên phải của khung icon).
            Add(SidebarRoot + "Right_Sidebar/Missions", sprite, new Vector2(0.608f, 0.648f), new Vector2(0.988f, 1.012f),
                save => save.CountClaimableTasks() > 0);
            Add(SidebarRoot + "Right_Sidebar/Daily", sprite, new Vector2(0.64f, 0.64f), new Vector2(1.0f, 1.0f),
                save => !save.IsDailyLoginRewardClaimed);
            Add(SidebarRoot + "Left_Sidebar/Gift_Popup", sprite, new Vector2(0.64f, 0.64f), new Vector2(1.0f, 1.0f),
                save => !save.Data.welcomeGiftClaimed);
            Add(SidebarRoot + "Left_Sidebar/BattlePass", sprite, new Vector2(0.64f, 0.64f), new Vector2(1.0f, 1.0f),
                HasClaimableBattlePassTier);

            SaveManager.Instance.OnDataChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save != null)
                save.OnDataChanged -= Refresh;
        }

        private void Update()
        {
            // Sang ngày mới (00:00 UTC) quà/nhiệm vụ ngày reset -> kiểm tra lại mỗi giây.
            if (Time.unscaledTime >= refreshAt)
                Refresh();

            // Nhịp đập nhẹ cho dấu báo đang hiện.
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (Badge badge in badges)
            {
                if (badge.Rect.gameObject.activeSelf)
                    badge.Rect.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        private void Add(string buttonPath, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Func<SaveManager, bool> isActive)
        {
            GameObject canvas = GameObject.Find("Canvas");
            Transform button = canvas != null ? canvas.transform.Find(buttonPath) : null;
            if (button == null)
                return;

            var go = new GameObject("Notification_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(button, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            badges.Add(new Badge { Rect = rect, IsActive = isActive });
        }

        private void Refresh()
        {
            refreshAt = Time.unscaledTime + 1f;
            SaveManager save = SaveManager.Instance;
            if (save == null)
                return;

            foreach (Badge badge in badges)
            {
                bool active = badge.IsActive(save);
                if (badge.Rect.gameObject.activeSelf != active)
                    badge.Rect.gameObject.SetActive(active);
            }
        }

        private static bool HasClaimableBattlePassTier(SaveManager save)
        {
            for (int i = 0; i < BattlePassRewards.Tiers.Count; i++)
            {
                if (!save.IsBattlePassTierClaimed(i) && save.Experience >= BattlePassRewards.Tiers[i].RequiredExperience)
                    return true;
            }
            return false;
        }
    }
}
