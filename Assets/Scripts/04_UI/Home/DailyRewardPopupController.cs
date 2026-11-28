using System;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
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

        private Sprite readyClaimSprite;
        private float refreshAt;
        private bool claimFailed;

        private void Awake()
        {
            readyClaimSprite = claimButton.image.sprite;
            coinsText.text = $"{SaveManager.DailyLoginCoins:N0} COINS";
            gemsText.text = $"{SaveManager.DailyLoginGems:N0} GEMS";
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

            claimFailed = !SaveManager.Instance.TryClaimDailyLoginReward();
            Refresh();
            if (!claimButton.interactable)
                closeButton.Select();
        }

        private void Refresh()
        {
            refreshAt = Time.unscaledTime + 1f;
            bool claimed = SaveManager.Instance.IsDailyLoginRewardClaimed;
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
                statusText.text = claimFailed ? "Could not claim. Try again." : "Resets daily at 00:00 UTC";
            }
        }
    }
}
