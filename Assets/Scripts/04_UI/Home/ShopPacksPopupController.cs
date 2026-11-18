using GalaxyStrike.IAP;
using GalaxyStrike.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    public abstract class ShopPacksPopupController : MonoBehaviour
    {
        private const int GemsCategory = 3;
        private const int CoinsCategory = 4;

        [SerializeField] private HomeSceneManager home;
        [SerializeField] private ShopCategoryScroller shop;
        [SerializeField] private IapCatalogAsset catalog;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text coinPackName;
        [SerializeField] private TMP_Text coinAmount;
        [SerializeField] private TMP_Text gemPackName;
        [SerializeField] private TMP_Text gemAmount;

        protected void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            ShowPack(SelectPack(catalog.catalog.coinPacks), "COINS", "Coin Packs", coinPackName, coinAmount);
            ShowPack(SelectPack(catalog.catalog.gemPacks), "GEMS", "Gem Packs", gemPackName, gemAmount);
            gameObject.SetActive(true);
            closeButton.Select();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            openButton.Select();
        }

        public void ViewCoins() => ViewCategory(CoinsCategory);

        public void ViewGems() => ViewCategory(GemsCategory);

        protected abstract IapProductDefinition SelectPack(IapProductDefinition[] packs);

        private void ViewCategory(int category)
        {
            gameObject.SetActive(false);
            home.OnTabSelected("Shop");
            shop.SelectCategory(category);
        }

        private static void ShowPack(
            IapProductDefinition pack, string unit, string sectionName, TMP_Text name, TMP_Text amount)
        {
            name.text = pack != null ? pack.displayName : sectionName;
            amount.text = pack != null ? $"{pack.rewardAmount:N0} {unit}" : "VIEW IN SHOP";
        }
    }
}
