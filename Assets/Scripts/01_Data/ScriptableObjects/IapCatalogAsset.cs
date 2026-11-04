using UnityEngine;
using GalaxyStrike.IAP;

namespace GalaxyStrike.IAP
{
    /// <summary>
    /// Asset bọc <see cref="IapCatalog"/> để lưu danh sách gói mua thành file .asset.
    /// IapCatalog là class thuần (serializable) nên cần wrapper ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "IapCatalog", menuName = "GalaxyStrike/IAP Catalog")]
    public sealed class IapCatalogAsset : ScriptableObject
    {
        public IapCatalog catalog = new IapCatalog();
    }
}
