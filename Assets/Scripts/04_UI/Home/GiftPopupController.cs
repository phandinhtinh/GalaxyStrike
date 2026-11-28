using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    public sealed class GiftPopupController : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private Sprite inactiveClaimSprite;

        private Sprite readyClaimSprite;

        private void Awake()
        {
            readyClaimSprite = claimButton.image.sprite;
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

            bool granted = SaveManager.Instance.TryClaimWelcomeGift();
            Refresh();
            if (granted)
                closeButton.Select();
            else if (claimButton.interactable)
                statusText.text = "Could not claim. Try again.";
        }

        private void Refresh()
        {
            bool claimed = SaveManager.Instance.Data.welcomeGiftClaimed;
            rewardText.text = $"{SaveManager.WelcomeGiftCoins:N0} COINS";
            statusText.text = claimed ? "Added to your coin balance." : "Available once per profile.";
            claimLabel.text = claimed ? "CLAIMED" : "CLAIM";
            claimButton.interactable = !claimed;
            if (inactiveClaimSprite != null)
                claimButton.image.sprite = claimed ? inactiveClaimSprite : readyClaimSprite;
        }
    }
}
