using System;
using GalaxyStrike.IAP;

namespace GalaxyStrike.UI.Home
{
    public sealed class SpecialOfferPopupController : ShopPacksPopupController
    {
        protected override IapProductDefinition SelectPack(IapProductDefinition[] packs)
        {
            IapProductDefinition featured = null;
            if (packs == null)
                return null;

            foreach (IapProductDefinition pack in packs)
            {
                if (pack != null && !string.IsNullOrEmpty(pack.productId) &&
                    string.Equals(pack.badge?.Trim(), "BEST VALUE", StringComparison.OrdinalIgnoreCase) &&
                    (featured == null || pack.rewardAmount > featured.rewardAmount))
                    featured = pack;
            }

            return featured;
        }
    }
}
