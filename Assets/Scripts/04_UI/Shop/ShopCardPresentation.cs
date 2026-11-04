using GalaxyStrike.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Shop
{
    public sealed class ShopCardPresentation : MonoBehaviour
    {
        public TextMeshProUGUI designation;
        public TextMeshProUGUI health;
        public TextMeshProUGUI fireRate;
        public Image currencyIcon;
        public Sprite coinSprite;
        public Sprite gemSprite;
        public ShopPanelGraphic frame;
        public ShopPanelGraphic badge;
        public TextMeshProUGUI priceLabel;

        public void Configure(ShipDataSO data, int index)
        {
            if (designation) designation.text = "MK. " + (index + 1).ToString("00");
            if (health) health.text = "<color=#9BBADB>HP</color>   <b>" + data.maxHealth + "</b>";
            if (fireRate) fireRate.text = "<color=#9BBADB>FIRE/s</color>  <b>" + (1f / Mathf.Max(0.01f, data.fireRate)).ToString("0.00") + "</b>";
            ConfigureCurrency(data.currencyType);
            Color accent = data.currencyType == CurrencyType.Gem ? new Color32(191, 81, 255, 255) : new Color32(0, 201, 255, 255);
            if (index == 2 || index == 6) accent = new Color32(255, 197, 44, 255);
            if (index == 4) accent = new Color32(190, 81, 255, 255);
            if (frame) { frame.borderColor = accent; frame.SetVerticesDirty(); }
            if (badge) { badge.borderColor = accent; badge.topColor = Color.Lerp(new Color32(9,30,69,255),accent,0.45f); badge.SetVerticesDirty(); }
        }

        public void Configure(ItemDataSO data)
        {
            if (designation) designation.text = data.itemType == ItemType.Weapon ? "WEAPON" : "BOOSTER";
            if (health) health.text = data.duration > 0 ? "DURATION  <b>" + data.duration.ToString("0") + "s</b>" : "PERMANENT";
            if (fireRate) fireRate.text = "POWER  <b>" + data.value.ToString("0.##") + "</b>";
            ConfigureCurrency(data.currencyType);
        }

        public void ConfigureCurrency(CurrencyType currency)
        {
            if (currencyIcon) { currencyIcon.sprite = currency == CurrencyType.Gem ? gemSprite : coinSprite; currencyIcon.gameObject.SetActive(true); }
        }

        public void ShowCurrency(bool show)
        {
            if (currencyIcon) currencyIcon.gameObject.SetActive(show);
            if (priceLabel)
            {
                priceLabel.rectTransform.anchoredPosition = new Vector2(show ? 86 : 14, -4);
                priceLabel.rectTransform.sizeDelta = new Vector2(show ? 197 : 276, 51);
            }
        }
    }
}
