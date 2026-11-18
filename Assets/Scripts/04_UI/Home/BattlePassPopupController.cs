using System;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    public sealed class BattlePassPopupController : MonoBehaviour
    {
        [Serializable]
        private sealed class TierView
        {
            public Button claimButton;
            public TMP_Text claimLabel;
            public TMP_Text milestoneLabel;
            public TMP_Text rewardLabel;
        }

        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image xpFill;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private Sprite inactiveClaimSprite;
        [SerializeField] private TierView[] tiers;

        private Sprite readyClaimSprite;

        private void Awake()
        {
            if (tiers.Length > 0)
                readyClaimSprite = tiers[0].claimButton.image.sprite;
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
            closeButton.Select();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            openButton.Select();
        }

        public void ClaimTier(int index)
        {
            if (!isActiveAndEnabled || index < 0 || index >= tiers.Length || !tiers[index].claimButton.interactable)
                return;

            bool granted = SaveManager.Instance.TryClaimBattlePassTier(index);
            Refresh();
            if (granted)
                closeButton.Select();
            else if (tiers[index].claimButton.interactable)
                tiers[index].claimLabel.text = "RETRY";
        }

        private void Refresh()
        {
            SaveManager save = SaveManager.Instance;
            int targetXp = BattlePassRewards.Tiers[BattlePassRewards.Tiers.Count - 1].RequiredExperience;
            int xp = Mathf.Min(save.Experience, targetXp);
            xpLabel.text = $"{xp:N0} / {targetXp:N0} XP";
            xpFill.fillAmount = (float)xp / targetXp;

            for (int i = 0; i < tiers.Length; i++)
            {
                BattlePassRewards.Tier tier = BattlePassRewards.Tiers[i];
                TierView view = tiers[i];
                bool claimed = save.IsBattlePassTierClaimed(i);
                bool unlocked = xp >= tier.RequiredExperience;
                view.milestoneLabel.text = $"{tier.RequiredExperience:N0} XP";
                view.rewardLabel.text = $"{tier.Coins:N0} COINS";
                view.claimButton.interactable = unlocked && !claimed;
                view.claimLabel.text = claimed ? "CLAIMED" : unlocked ? "CLAIM" : "LOCKED";
                if (inactiveClaimSprite != null)
                    view.claimButton.image.sprite = view.claimButton.interactable ? readyClaimSprite : inactiveClaimSprite;
            }
        }
    }
}
