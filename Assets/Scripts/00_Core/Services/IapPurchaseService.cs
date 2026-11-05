// Galaxy Strike - Unity IAP (Giai đoạn 4)
//
// File này chỉ biên dịch khi Unity đã resolve package "com.unity.purchasing".
// Lý do: project có thể được mở lần đầu bằng máy chưa tải package (offline / CI),
// khi đó toàn bộ hệ thống mua hàng tạm thời bị loại khỏi build thay vì làm hỏng compile.
//
// Unity Purchasing tự định nghĩa các symbol sau khi import:
//   UNITY_PURCHASING        - Assembly UnityEngine.Purchasing tồn tại
//   UNITY_PURCHASING_4_8_OR_NEWER - API IDetailedStoreListener / PurchaseFailureDescription
//
// Muốn test kỹ phần này: Window > Package Manager > cài "In-App Purchasing" 4.x,
// rồi xem Console có log "[IAP] Unity IAP sẵn sàng".

#if UNITY_PURCHASING && UNITY_PURCHASING_4_8_OR_NEWER
#define GALAXYSTRIKE_IAP_READY
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

#if GALAXYSTRIKE_IAP_READY
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace GalaxyStrike.IAP
{
    /// <summary>
    /// Cầu nối Unity IAP ↔ Galaxy Strike (Giai đoạn 4).
    ///
    /// Trách nhiệm:
    ///  - Khởi tạo UnityPurchasing, nạp giá bản địa hoá từ Play Store.
    ///  - Mua gói Consumable (Gem Pack / Coin Pack) và giao phần thưởng qua SaveManager.
    ///  - Xử lý an toàn: chỉ Complete khi đã cộng thưởng, tránh trừ tiền mà không có hàng.
    ///  - Nếu package IAP chưa cài: chạy chế độ "mô phỏng" để UI vẫn test được,
    ///    nhưng KHÔNG cộng thưởng thật và log rõ ràng.
    ///
    /// Script tự sinh (DontDestroyOnLoad) nên dùng được ở mọi Scene.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class IapPurchaseService : MonoBehaviour
#if GALAXYSTRIKE_IAP_READY
        , IDetailedStoreListener
#endif
    {
        private static IapPurchaseService instance;

        /// <summary>
        /// Singleton "lazy": lấy instance đang có, tìm trong Scene, hoặc tự tạo mới.
        /// Nhờ vậy UI Shop có thể gọi IAP từ bất kỳ Scene nào mà không cần gắn tay.
        /// </summary>
        public static IapPurchaseService Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindObjectOfType<IapPurchaseService>();
                if (instance == null)
                    instance = new GameObject("[IapPurchaseService]").AddComponent<IapPurchaseService>();

                return instance;
            }
        }

        [Header("--- Catalog ---")]
        [Tooltip("Asset chứa danh sách gói. Bootstrap tự nạp Resources/Config/IapCatalog khi không được gán trong Inspector.")]
        [SerializeField] private IapCatalogAsset catalogAsset;
        [SerializeField] private IapCatalog catalog = new IapCatalog();

        [Header("--- Behaviour ---")]
        [Tooltip("Cho phép bấm mua khi chưa có Store thật (Editor/test). KHÔNG cộng thưởng thật.")]
        [SerializeField] private bool allowSimulationOutsideStore = true;
        [Tooltip("Ghi log chi tiết quá trình mua")]
        [SerializeField] private bool verboseLogging = true;

        /// <summary>Phát khi quá trình mua kết thúc. (productId, success, message)</summary>
        public event Action<string, bool, string> OnPurchaseFinished;

        /// <summary>Phát khi Store đã nạp xong giá sản phẩm (UI cần vẽ lại giá bản địa hoá).</summary>
        public event Action OnStoreReady;

        /// <summary>Phát khi mua thành công và phần thưởng đã được cộng. (productId)</summary>
#pragma warning disable CS0067 // Event chỉ được phát trong nhánh GALAXYSTRIKE_IAP_READY
        public event Action<string> OnRewardGranted;
#pragma warning restore CS0067

#if GALAXYSTRIKE_IAP_READY
        private IStoreController controller;
        private IExtensionProvider extensions;
        private readonly Dictionary<string, Product> productCache = new Dictionary<string, Product>(16);
#endif

        private readonly List<string> registeredProductIds = new List<string>(16);
        private bool isInitializing;
        private bool isInitialized;

        /// <summary>Store đã sẵn sàng để mua chưa.</summary>
        public bool IsReady => isInitialized && HasStoreController;

        /// <summary>Package Unity IAP có được cài trong project hay không.</summary>
        public static bool IsPurchasingPackageInstalled =>
#if GALAXYSTRIKE_IAP_READY
            true;
#else
            false;
#endif

        /// <summary>
        /// Danh mục sản phẩm để mua. Ưu tiên ScriptableObject (cấu hình trong Inspector);
        /// nếu không có thì dùng list mặc định trên component.
        /// </summary>
        public IapCatalog Catalog
        {
            get
            {
                if (catalogAsset != null && catalogAsset.catalog != null)
                    return catalogAsset.catalog;

                return catalog;
            }
        }

        /// <summary>Xoá tham chiếu tĩnh (gọi khi vào Play Mode để không giữ object của phiên trước).</summary>
        public static void ResetStaticInstance()
        {
            instance = null;
        }

        /// <summary>
        /// Kiểm tra có controller của Store hay không (chỉ đúng khi đã cài package IAP
        /// và khởi tạo thành công). Dùng nội bộ cho thuộc tính IsReady.
        /// </summary>
        private bool HasStoreController
        {
            get
            {
#if GALAXYSTRIKE_IAP_READY
                return controller != null;
#else
                return false;
#endif
            }
        }

        /// <summary>Giá bản địa hoá ("₫25,000" / "$1.99"). Empty nếu chưa nạp được.</summary>
        public string GetLocalizedPrice(string productId)
        {
#if GALAXYSTRIKE_IAP_READY
            if (!string.IsNullOrEmpty(productId) &&
                productCache.TryGetValue(productId, out Product product) &&
                product != null && product.metadata != null)
            {
                return product.metadata.localizedPriceString;
            }
#endif
            return string.Empty;
        }

        // =====================================================================
        // VÒNG ĐỜI
        // =====================================================================
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            // Không gán tay thì tự nạp danh mục gói mua từ Resources.
            if (catalogAsset == null)
                catalogAsset = Resources.Load<IapCatalogAsset>("Config/IapCatalog");
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Tự khởi động Unity IAP ngay khi service xuất hiện trong Scene.
        /// </summary>
        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Xóa tham chiếu static khi object bị hủy, để lần sau Instance tạo lại sạch.
        /// </summary>
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        // =====================================================================
        // KHỞI TẠO STORE
        // =====================================================================

        /// <summary>
        /// Khởi tạo Unity Purchasing: đăng ký toàn bộ gói trong catalog với Store rồi
        /// gọi UnityPurchasing.Initialize. Nếu chưa cài package IAP thì chuyển sang
        /// chế độ mô phỏng. Chống gọi 2 lần bằng cờ isInitialized/isInitializing.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized || isInitializing)
                return;

#if !GALAXYSTRIKE_IAP_READY
            isInitialized = true;
            Log("Chưa cài package 'com.unity.purchasing' -> hệ thống mua hàng ở chế độ mô phỏng.");
            OnStoreReady?.Invoke();
            return;
#else
            isInitializing = true;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            IapCatalog catalogData = Catalog;

            int count = 0;
            count += RegisterProducts(builder, catalogData.gemPacks, ProductType.Consumable);
            count += RegisterProducts(builder, catalogData.coinPacks, ProductType.Consumable);

            if (!string.IsNullOrEmpty(catalogData.removeAdsProductId))
            {
                builder.AddProduct(catalogData.removeAdsProductId, ProductType.NonConsumable);
                registeredProductIds.Add(catalogData.removeAdsProductId);
                count++;
            }

            if (!string.IsNullOrEmpty(catalogData.legendaryShipProductId))
            {
                builder.AddProduct(catalogData.legendaryShipProductId, ProductType.NonConsumable);
                registeredProductIds.Add(catalogData.legendaryShipProductId);
                count++;
            }

            if (count == 0)
            {
                Log("Catalog rỗng - chưa có Product ID nào để đăng ký với Store.");
                isInitializing = false;
                isInitialized = true;
                return;
            }

            Log($"Đang khởi tạo Unity IAP với {count} sản phẩm...");
            UnityPurchasing.Initialize(this, builder);
#endif
        }

#if GALAXYSTRIKE_IAP_READY
        /// <summary>
        /// Đăng ký một mảng gói sản phẩm vào ConfigurationBuilder.
        /// Bỏ qua phần tử null hoặc thiếu productId; trả về số sản phẩm đã thêm
        /// và ghi ID vào danh sách registeredProductIds để tra giá sau này.
        /// </summary>
        private int RegisterProducts(ConfigurationBuilder builder, IapProductDefinition[] source, ProductType type)
        {
            if (source == null)
                return 0;

            int added = 0;
            for (int i = 0; i < source.Length; i++)
            {
                IapProductDefinition definition = source[i];
                if (definition == null || string.IsNullOrEmpty(definition.productId))
                    continue;

                builder.AddProduct(definition.productId, type);
                registeredProductIds.Add(definition.productId);
                added++;
            }

            return added;
        }

        // =====================================================================
        // CALLBACK KHỞI TẠO
        // =====================================================================

        /// <summary>
        /// Callback của Unity IAP khi khởi tạo THÀNH CÔNG: giữ controller/extensions,
        /// cache các Product (để lấy giá bản địa hoá), rồi báo UI qua OnStoreReady.
        /// </summary>
        public void OnInitialized(IStoreController storeController, IExtensionProvider storeExtensions)
        {
            controller = storeController;
            extensions = storeExtensions;
            isInitializing = false;
            isInitialized = true;

            productCache.Clear();
            for (int i = 0; i < registeredProductIds.Count; i++)
            {
                string id = registeredProductIds[i];
                Product product = controller.products.WithID(id);
                if (product != null)
                    productCache[id] = product;
            }

            Log($"Unity IAP sẵn sàng ({productCache.Count} sản phẩm).");
            OnStoreReady?.Invoke();
        }

        /// <summary>
        /// Callback khi khởi tạo THẤT BẠI (không kèm message).
        /// Chỉ chuyển tiếp sang overload có message để log chung một chỗ.
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, string.Empty);
        }

        /// <summary>
        /// Callback khởi tạo thất bại có message chi tiết: reset trạng thái về "chưa sẵn sàng"
        /// và log cảnh báo.
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            isInitializing = false;
            isInitialized = false;
            Debug.LogWarning($"[IAP] Khởi tạo thất bại: {error}. {message}");
        }

        // =====================================================================
        // MUA HÀNG
        // =====================================================================

        /// <summary>
        /// Điểm vào để mua một gói. Nếu Store chưa sẵn sàng thì chạy chế độ mô phỏng;
        /// nếu sản phẩm không có/không mua được thì báo lỗi; ngược lại gọi InitiatePurchase.
        /// Kết quả trả về qua event OnPurchaseFinished.
        /// </summary>
        public void Purchase(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                NotifyFailure(productId, "Thiếu Product ID.");
                return;
            }

            if (!IsReady)
            {
                SimulatePurchase(productId);
                return;
            }

            Product product = controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                NotifyFailure(productId, "Sản phẩm hiện không khả dụng.");
                return;
            }

            Log($"Bắt đầu mua: {productId}");
            controller.InitiatePurchase(product);
        }

        /// <summary>
        /// Callback của Store khi một giao dịch MUA THÀNH CÔNG:
        /// tra gói trong catalog, cộng thưởng vào save, phát event, rồi trả Complete
        /// để Google Play/App Store xác nhận đã giao hàng (tránh trừ tiền mà không có hàng).
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            Product product = args.purchasedProduct;
            string productId = product != null ? product.definition.id : null;

            IapProductDefinition definition = Catalog.Find(productId);
            if (definition == null)
            {
                Debug.LogError($"[IAP] Mua sản phẩm không có trong catalog: {productId}. Không cấp thưởng.");
                return PurchaseProcessingResult.Complete;
            }

            GrantReward(definition);

            Log($"Mua thành công: {productId} (+{definition.rewardAmount} {definition.rewardType})");
            OnPurchaseFinished?.Invoke(productId, true, "Mua thành công.");
            OnRewardGranted?.Invoke(productId);

            // Complete -> Google Play xác nhận đã giao hàng.
            return PurchaseProcessingResult.Complete;
        }

        /// <summary>
        /// Callback mua thất bại (API cũ, chỉ có reason): đóng gói thành
        /// PurchaseFailureDescription rồi gọi overload mới để xử lý chung.
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            OnPurchaseFailed(product, new PurchaseFailureDescription(
                product != null ? product.definition.id : string.Empty,
                failureReason,
                failureReason.ToString()));
        }

        /// <summary>
        /// Callback mua thất bại: log lý do và báo UI qua OnPurchaseFinished(success = false).
        /// Không cộng thưởng trong trường hợp này.
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            string productId = failureDescription != null ? failureDescription.productId : null;
            string reason = failureDescription != null ? failureDescription.message : "Unknown";

            Debug.LogWarning($"[IAP] Mua thất bại: {productId} - {reason}");
            OnPurchaseFinished?.Invoke(productId, false, reason);
        }
#else
        /// <summary>Chưa cài package IAP: không thể mua thật.</summary>
        public void Purchase(string productId)
        {
            SimulatePurchase(productId);
        }
#endif

        // =====================================================================
        // MÔ PHỎNG (khi chưa có Store thật)
        // =====================================================================

        /// <summary>
        /// "Mua giả" khi chưa có Store (Editor/CI): chỉ log và báo lỗi cho UI,
        /// KHÔNG cộng Coin/Gem thật. Nếu đã tắt allowSimulationOutsideStore thì
        /// trả về thông báo "cửa hàng chưa sẵn sàng".
        /// </summary>
        private void SimulatePurchase(string productId)
        {
            if (!allowSimulationOutsideStore)
            {
                NotifyFailure(productId, "Cửa hàng chưa sẵn sàng, vui lòng thử lại.");
                return;
            }

            Debug.LogWarning(
                $"[IAP] (Mô phỏng) Bấm mua '{productId}'. " +
                "Không có giao dịch thật, KHÔNG cộng Coin/Gem. " +
                "Cài package com.unity.purchasing và cấu hình Play Console để mua thật.");

            OnPurchaseFinished?.Invoke(productId, false, "Chưa cấu hình thanh toán thật (chế độ mô phỏng).");
        }

        // =====================================================================
        // CẤP THƯỞNG
        // =====================================================================

        /// <summary>
        /// Cộng phần thưởng của gói vừa mua vào save (Coin / Gem / Energy)
        /// thông qua SaveManager, tuỳ theo rewardType của gói.
        /// </summary>
        private static void GrantReward(IapProductDefinition definition)
        {
            var save = GalaxyStrike.Save.SaveManager.Instance;
            if (save == null)
            {
                Debug.LogError("[IAP] Không tìm thấy SaveManager -> không cấp thưởng được.");
                return;
            }

            switch (definition.rewardType)
            {
                case IapRewardType.Coins:
                    save.AddCoins(definition.rewardAmount);
                    break;

                case IapRewardType.Gems:
                    save.AddGems(definition.rewardAmount);
                    break;

                case IapRewardType.Energy:
                    save.AddEnergy(definition.rewardAmount);
                    break;
            }
        }

        /// <summary>
        /// Log cảnh báo lỗi mua và phát event thất bại cho UI (success = false).
        /// </summary>
        private void NotifyFailure(string productId, string message)
        {
            Debug.LogWarning($"[IAP] {message} ({productId})");
            OnPurchaseFinished?.Invoke(productId, false, message);
        }

        /// <summary>
        /// Log có tiền tố [IAP], bị bỏ qua khi verboseLogging đang tắt.
        /// </summary>
        private void Log(string message)
        {
            if (!verboseLogging)
                return;

            Debug.Log($"[IAP] {message}");
        }
    }
}
