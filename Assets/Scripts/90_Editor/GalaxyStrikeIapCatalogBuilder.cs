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
        const string prefix = "com.defaultcompany.galaxystrike";

        var asset = ScriptableObject.CreateInstance<GalaxyStrike.IAP.IapCatalogAsset>();
        GalaxyStrike.IAP.IapCatalog catalog = asset.catalog;

        catalog.gemPacks = new[]
        {
            Pack($"{prefix}.gems_small",  "Small Gem Pack",  "GÃ³i Gem nhá»",  120,  GalaxyStrike.IAP.IapRewardType.Gems, null),
            Pack($"{prefix}.gems_medium", "Medium Gem Pack", "GÃ³i Gem vá»«a",  650,  GalaxyStrike.IAP.IapRewardType.Gems, "POPULAR"),
            Pack($"{prefix}.gems_large",  "Large Gem Pack",  "GÃ³i Gem lá»›n",  1400, GalaxyStrike.IAP.IapRewardType.Gems, "BEST VALUE"),
        };

        catalog.coinPacks = new[]
        {
            Pack($"{prefix}.coins_small",  "Small Coin Pack",  "GÃ³i Coin nhá»", 2500,  GalaxyStrike.IAP.IapRewardType.Coins, null),
            Pack($"{prefix}.coins_medium", "Medium Coin Pack", "GÃ³i Coin vá»«a", 14000, GalaxyStrike.IAP.IapRewardType.Coins, "POPULAR"),
            Pack($"{prefix}.coins_large",  "Large Coin Pack",  "GÃ³i Coin lá»›n", 32000, GalaxyStrike.IAP.IapRewardType.Coins, "BEST VALUE"),
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
            $"[GalaxyStrike] ÄÃ£ táº¡o '{path}' vá»›i 6 gÃ³i tiÃªu thá»¥ + 2 gÃ³i non-consumable.\n" +
            "BÆ°á»›c tiáº¿p theo (Giai Ä‘oáº¡n 4/5):\n" +
            "1. Äá»•i applicationIdentifier sang com.<studio>.galaxystrike.\n" +
            "2. Sá»­a Product ID trong asset cho khá»›p prefix má»›i.\n" +
            "3. Khai bÃ¡o Ä‘Ãºng cÃ¡c Product ID Ä‘Ã³ trong Play Console â†’ In-app products.");
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
