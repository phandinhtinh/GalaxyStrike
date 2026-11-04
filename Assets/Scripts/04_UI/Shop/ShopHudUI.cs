using GalaxyStrike.Save;
using TMPro;
using UnityEngine;

namespace GalaxyStrike.UI.Shop
{
    public sealed class ShopHudUI : MonoBehaviour
    {
        public TextMeshProUGUI energyText;
        public TextMeshProUGUI coinText;
        public TextMeshProUGUI gemText;
        public TextMeshProUGUI playerName;
        public TextMeshProUGUI levelText;
        public GameObject sharedTopBar;
        public GameObject sharedSettingsButton;
        public ShopPanelGraphic[] experienceSegments;
        private SaveManager save;
        private float timer;

        private void OnEnable()
        {
            if (sharedTopBar) sharedTopBar.SetActive(false);
            if (sharedSettingsButton) sharedSettingsButton.SetActive(false);
            save = SaveManager.Instance;
            if (save != null) save.OnDataChanged += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (save != null) save.OnDataChanged -= Refresh;
            save = null;
            if (sharedTopBar) sharedTopBar.SetActive(true);
            if (sharedSettingsButton) sharedSettingsButton.SetActive(true);
        }
        private void Update() { timer += Time.unscaledDeltaTime; if (timer >= 1) { timer=0; Refresh(); } }
        private void Refresh()
        {
            if (save == null) return;
            if (energyText) energyText.text = save.Energy + "/" + save.MaxEnergy;
            if (coinText) coinText.text = save.Coins.ToString("N0");
            if (gemText) gemText.text = save.Gems.ToString("N0");
            if (playerName) playerName.text = save.Data.playerName;
            if (levelText) levelText.text = "Lv. " + save.Data.accountLevel;
            if (experienceSegments != null)
            {
                float progress = save.AccountLevelProgress;
                for (int i=0; i<experienceSegments.Length; i++)
                    if (experienceSegments[i]) experienceSegments[i].color = progress > (float)i / experienceSegments.Length ? Color.white : new Color(.25f,.4f,.6f,1);
            }
        }
    }
}
