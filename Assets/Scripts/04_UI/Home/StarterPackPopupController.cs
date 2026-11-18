using GalaxyStrike.IAP;

namespace GalaxyStrike.UI.Home
{
    public sealed class StarterPackPopupController : ShopPacksPopupController
    {
        protected override IapProductDefinition SelectPack(IapProductDefinition[] packs)
        {
            IapProductDefinition smallest = null;
            if (packs != null)
            {
                foreach (IapProductDefinition pack in packs)
                {
                    if (pack != null && !string.IsNullOrEmpty(pack.productId) &&
                        (smallest == null || pack.rewardAmount < smallest.rewardAmount))
                        smallest = pack;
                }
            }

            return smallest;
        }
    }
}
