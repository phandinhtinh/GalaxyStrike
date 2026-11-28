#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Táº¡o asset catalog IAP báº±ng menu, Ä‘á»ƒ Product ID khá»›p chÃ­nh xÃ¡c vá»›i Play Console
/// mÃ  khÃ´ng pháº£i nháº­p tay 6 láº§n trong Inspector.
///
/// CÃ¡ch dÃ¹ng: menu "Galaxy Strike/Giai Ä‘oáº¡n 4 - Táº¡o IAP Catalog".
/// Sau khi táº¡o: Ä‘á»•i applicationIdentifier trong ProjectSettings (Giai Ä‘oáº¡n 5)
/// rá»“i sá»­a Product ID cho khá»›p package name tháº­t.
/// </summary>
public static class GalaxyStrikeIapCatalogBuilder
{
[UnityEditor.MenuItem("Galaxy Strike/Phase 4 - Create IAP Catalog", priority = 2)]
    public static void CreateCatalogAsset()
    {
        // Bundle ID hiá»‡n táº¡i váº«n lÃ  DefaultCompany -> dÃ¹ng prefix táº¡m, sáº½ Ä‘á»•i á»Ÿ Giai Ä‘oáº¡n 5.
        const string prefix = "com.solostudio.galaxystrike";

        var asset = ScriptableObject.CreateInstance<GalaxyStrike.IAP.IapCatalogAsset>();
        GalaxyStrike.IAP.IapCatalog catalog = asset.catalog;

        catalog.gemPacks = new[]
        {
            Pack($"{prefix}.gems_small",  "Small Gem Pack",  "Small gem pack",  120,  GalaxyStrike.IAP.IapRewardType.Gems, null),
            Pack($"{prefix}.gems_medium", "Medium Gem Pack", "Medium gem pack",  650,  GalaxyStrike.IAP.IapRewardType.Gems, "POPULAR"),
            Pack($"{prefix}.gems_large",  "Large Gem Pack",  "Large gem pack",  1400, GalaxyStrike.IAP.IapRewardType.Gems, "BEST VALUE"),
        };

        catalog.coinPacks = new[]
        {
            Pack($"{prefix}.coins_small",  "Small Coin Pack",  "Small coin pack", 2500,  GalaxyStrike.IAP.IapRewardType.Coins, null),
            Pack($"{prefix}.coins_medium", "Medium Coin Pack", "Medium coin pack", 14000, GalaxyStrike.IAP.IapRewardType.Coins, "POPULAR"),
            Pack($"{prefix}.coins_large",  "Large Coin Pack",  "Large coin pack", 32000, GalaxyStrike.IAP.IapRewardType.Coins, "BEST VALUE"),
        };

        catalog.removeAdsProductId = $"{prefix}.remove_ads";
        catalog.legendaryShipProductId = $"{prefix}.legendary_ship";

        const string folder = "Assets/Resources/Config";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Config");

        const string path = folder + "/IapCatalog.asset";
        UnityEditor.AssetDatabase.CreateAsset(asset, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        Debug.Log(
            $"[GalaxyStrike] Created '{path}' with 6 consumable packs + 2 non-consumables.\n" +
            "Next steps (phase 4/5):\n" +
            "1. Change applicationIdentifier to com.<studio>.galaxystrike.\n" +
            "2. Update the Product IDs in the asset to match the new prefix.\n" +
            "3. Declare the same Product IDs in Play Console -> In-app products.");
    }

    private static GalaxyStrike.IAP.IapProductDefinition Pack(
        string id,
        string name,
        string desc,
        int amount,
        GalaxyStrike.IAP.IapRewardType type,
        string badge)
    {
        return new GalaxyStrike.IAP.IapProductDefinition
        {
            productId = id,
            displayName = name,
            description = desc,
            rewardType = type,
            rewardAmount = amount,
            badge = badge
        };
    }
}
#endif
