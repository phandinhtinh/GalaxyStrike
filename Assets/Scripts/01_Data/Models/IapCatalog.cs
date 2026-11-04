using System;
using UnityEngine;

namespace GalaxyStrike.IAP
{
    /// <summary>
    /// Loại phần thưởng của một gói IAP.
    /// </summary>
    public enum IapRewardType
    {
        Coins = 0,
        Gems = 1,
        Energy = 2
    }

    /// <summary>
    /// Một gói mua thật trên Google Play (Giai đoạn 4).
    ///
    /// <see cref="productId"/> PHẢI trùng chính xác với Product ID khai báo trong
    /// Play Console → Monetize → In-app products / Subscriptions.
    /// </summary>
    [Serializable]
    public sealed class IapProductDefinition
    {
        [Tooltip("Product ID trên Play Console, ví dụ com.yourstudio.galaxystrike.gems_small")]
        public string productId;

        [Tooltip("Tên hiển thị trong Store, ví dụ 'Small Gem Pack'")]
        public string displayName;

        [Tooltip("Mô tả ngắn dưới tên (tuỳ chọn)")]
        public string description;

        public IapRewardType rewardType = IapRewardType.Gems;
        [Min(1)] public int rewardAmount = 100;

        [Tooltip("Icon tuỳ chọn. Để trống sẽ dùng icon của thẻ mặc định")]
        public Sprite icon;

        [Tooltip("Hiển thị nhãn HOT / BEST VALUE (tuỳ chọn)")]
        public string badge;
    }

    /// <summary>
    /// Toàn bộ catalog gói mua. Gán trong Inspector của <see cref="IapPurchaseService"/>
    /// (hoặc tạo asset qua menu "Galaxy Strike/Giai đoạn 4 - Tạo IAP Catalog").
    /// </summary>
    [Serializable]
    public sealed class IapCatalog
    {
        [Header("--- Consumable: Gem Packs ---")]
        public IapProductDefinition[] gemPacks = Array.Empty<IapProductDefinition>();

        [Header("--- Consumable: Coin Packs ---")]
        public IapProductDefinition[] coinPacks = Array.Empty<IapProductDefinition>();

        [Header("--- Non-consumable / khác ---")]
        [Tooltip("Gói xoá quảng cáo (Giai đoạn 4). Để trống nếu chưa có")]
        public string removeAdsProductId = "";
        [Tooltip("Gói mở khoá phi thuyền huyền thoại. Để trống nếu chưa có")]
        public string legendaryShipProductId = "";

        /// <summary>Tìm định nghĩa theo Product ID (null nếu không có trong catalog).</summary>
        public IapProductDefinition Find(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return null;

            return FindIn(gemPacks, productId) ?? FindIn(coinPacks, productId);
        }

        private static IapProductDefinition FindIn(IapProductDefinition[] source, string productId)
        {
            if (source == null)
                return null;

            for (int i = 0; i < source.Length; i++)
            {
                IapProductDefinition item = source[i];
                if (item != null && item.productId == productId)
                    return item;
            }

            return null;
        }
    }
}
