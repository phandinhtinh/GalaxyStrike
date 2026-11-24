// Galaxy Strike - Unity IAP 5 (StoreController API)
//
// Package "com.unity.purchasing" 5.x đã có trong Packages/manifest.json nên file này
// dùng thẳng API thật. Bản cũ bọc trong #if UNITY_PURCHASING_4_8_OR_NEWER — symbol đó
// chỉ tồn tại bên trong assembly của package, nên code mua thật KHÔNG BAO GIỜ được biên dịch.
//
// Luồng IAP 5:
//   Connect() -> FetchProducts() -> (OnProductsFetched) -> FetchPurchases()
//   PurchaseProduct() -> OnPurchasePending -> cộng thưởng -> ConfirmPurchase()
//
// Chống cộng thưởng 2 lần: Store gửi lại đơn chưa xác nhận (app bị tắt giữa chừng), nên
// transaction ID được ghi vào save cùng lúc với phần thưởng (SaveManager.TryApplyIapPurchase).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GalaxyStrike.IAP
{
    /// <summary>
    /// Cầu nối Unity IAP ↔ Galaxy Strike.
    ///
    /// Trách nhiệm:
    ///  - Kết nối Store, nạp giá bản địa hoá từ Play Store.
    ///  - Mua gói Consumable (Gem Pack / Coin Pack) và giao phần thưởng qua SaveManager.
    ///  - Chỉ xác nhận (ConfirmPurchase) SAU KHI đã cộng thưởng; đơn lạ thì không xác nhận
    ///    để Google Play tự hoàn tiền thay vì trừ tiền mà không giao hàng.
    ///  - Store không kết nối được: chạy chế độ "mô phỏng", KHÔNG cộng thưởng thật.
    ///
    /// Script tự sinh (DontDestroyOnLoad) nên dùng được ở mọi Scene.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class IapPurchaseService : SingletonBehaviour<IapPurchaseService>
    {
        [Header("--- Catalog ---")]
        [Tooltip("Asset chứa danh sách gói. Bootstrap tự nạp Resources/Config/IapCatalog khi không được gán trong Inspector.")]
        [SerializeField] private IapCatalogAsset catalogAsset;
        [SerializeField] private IapCatalog catalog = new IapCatalog();

        [Header("--- Behaviour ---")]
        [Tooltip("Cho phép bấm mua khi Store chưa kết nối (Editor/test). KHÔNG cộng thưởng thật.")]
        [SerializeField] private bool allowSimulationOutsideStore = true;
        [Tooltip("Ghi log chi tiết quá trình mua")]
        [SerializeField] private bool verboseLogging = true;

        /// <summary>Phát khi quá trình mua kết thúc. (productId, success, message)</summary>
        public event Action<string, bool, string> OnPurchaseFinished;

        /// <summary>Phát khi Store đã nạp xong giá sản phẩm (UI cần vẽ lại giá bản địa hoá).</summary>
        public event Action OnStoreReady;

        /// <summary>Phát khi mua thành công và phần thưởng đã được cộng. (productId)</summary>
        public event Action<string> OnRewardGranted;

        private StoreController store;
        private readonly Dictionary<string, Product> productCache = new Dictionary<string, Product>(16);
        private bool isInitializing;
        private bool isInitialized;

        /// <summary>Store đã kết nối và nạp xong sản phẩm, sẵn sàng để mua.</summary>
        public bool IsReady => isInitialized && store != null;

        /// <summary>Package Unity IAP có trong project (luôn đúng từ IAP 5).</summary>
        public static bool IsPurchasingPackageInstalled => true;

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

        /// <summary>Giá bản địa hoá ("₫25,000" / "$1.99"). Empty nếu chưa nạp được.</summary>
        public string GetLocalizedPrice(string productId)
        {
            if (!string.IsNullOrEmpty(productId) &&
                productCache.TryGetValue(productId, out Product product) &&
                product != null && product.metadata != null)
            {
                return product.metadata.localizedPriceString;
            }

            return string.Empty;
        }

        // =====================================================================
        // VÒNG ĐỜI
        // =====================================================================

        /// <summary>
        /// Callback của <see cref="SingletonBehaviour{T}"/>: không gán tay thì tự nạp
        /// danh mục gói mua từ Resources. Việc giữ object qua Scene đã do base class lo.
        /// </summary>
        protected override void OnSingletonAwake()
        {
            if (catalogAsset == null)
                catalogAsset = Resources.Load<IapCatalogAsset>("Config/IapCatalog");
        }

        /// <summary>Tự kết nối Store ngay khi service xuất hiện.</summary>
        private void Start()
        {
            Initialize();
        }

        protected override void OnSingletonDestroyed()
        {
            UnsubscribeStore();
        }

        // =====================================================================
        // KẾT NỐI STORE
        // =====================================================================

        /// <summary>
        /// Kết nối Store rồi nạp toàn bộ gói trong catalog. Chống gọi trùng bằng
        /// cờ isInitialized/isInitializing. Gọi lại được sau khi kết nối thất bại.
        /// </summary>
        public async void Initialize()
        {
            if (isInitialized || isInitializing)
                return;

            List<ProductDefinition> definitions = BuildProductDefinitions();
            if (definitions.Count == 0)
            {
                Log("Catalog rỗng - chưa có Product ID nào để đăng ký với Store.");
                return;
            }

            isInitializing = true;

            try
            {
                if (store == null)
                {
                    store = UnityIAPServices.StoreController();
                    SubscribeStore();
                }

                Log($"Đang kết nối Store với {definitions.Count} sản phẩm...");
                await store.Connect();

                // Đơn còn treo từ phiên trước (app bị tắt khi chưa xác nhận) sẽ đi qua
                // OnPurchasePending sau FetchPurchases để được giao hàng.
                store.ProcessPendingOrdersOnPurchasesFetched(true);
                store.FetchProducts(definitions);
            }
            catch (Exception exception)
            {
                isInitializing = false;
                Debug.LogWarning($"[IAP] Kết nối Store thất bại: {exception.Message}");
            }
        }

        /// <summary>
        /// Chuyển catalog thành danh sách ProductDefinition (Consumable).
        ///
        /// Gói Non-consumable (remove_ads / legendary_ship) CHƯA được đăng ký: game chưa có
        /// hệ thống quyền sở hữu để giao chúng, đăng ký lúc này chỉ tạo đơn không giao được hàng.
        /// </summary>
        private List<ProductDefinition> BuildProductDefinitions()
        {
            var definitions = new List<ProductDefinition>(16);
            var seen = new HashSet<string>();
            IapCatalog catalogData = Catalog;

            AddConsumables(definitions, seen, catalogData.gemPacks);
            AddConsumables(definitions, seen, catalogData.coinPacks);

            return definitions;
        }

        private static void AddConsumables(List<ProductDefinition> target, HashSet<string> seen, IapProductDefinition[] source)
        {
            if (source == null)
                return;

            foreach (IapProductDefinition definition in source)
            {
                if (definition == null || string.IsNullOrEmpty(definition.productId) || !seen.Add(definition.productId))
                    continue;

                target.Add(new ProductDefinition(definition.productId, ProductType.Consumable));
            }
        }

        private void SubscribeStore()
        {
            store.OnProductsFetched += HandleProductsFetched;
            store.OnProductsFetchFailed += HandleProductsFetchFailed;
            store.OnPurchasePending += HandlePurchasePending;
            store.OnPurchaseFailed += HandlePurchaseFailed;
            store.OnPurchaseDeferred += HandlePurchaseDeferred;
            store.OnStoreDisconnected += HandleStoreDisconnected;
        }

        private void UnsubscribeStore()
        {
            if (store == null)
                return;

            store.OnProductsFetched -= HandleProductsFetched;
            store.OnProductsFetchFailed -= HandleProductsFetchFailed;
            store.OnPurchasePending -= HandlePurchasePending;
            store.OnPurchaseFailed -= HandlePurchaseFailed;
            store.OnPurchaseDeferred -= HandlePurchaseDeferred;
            store.OnStoreDisconnected -= HandleStoreDisconnected;
        }

        // =====================================================================
        // CALLBACK STORE
        // =====================================================================

        /// <summary>Nạp sản phẩm xong: cache giá, đánh dấu sẵn sàng, rồi lấy đơn còn treo.</summary>
        private void HandleProductsFetched(List<Product> products)
        {
            productCache.Clear();
            if (products != null)
            {
                foreach (Product product in products)
                {
                    if (product != null && product.definition != null)
                        productCache[product.definition.id] = product;
                }
            }

            isInitializing = false;
            isInitialized = true;
            Log($"Unity IAP sẵn sàng ({productCache.Count} sản phẩm).");
            OnStoreReady?.Invoke();

            store.FetchPurchases();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            isInitializing = false;
            int count = failure?.FailedFetchProducts?.Count ?? 0;
            Debug.LogWarning($"[IAP] Không nạp được {count} sản phẩm: {failure?.FailureReason}. " +
                             "Kiểm tra Product ID có khớp Play Console không.");
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription description)
        {
            isInitialized = false;
            isInitializing = false;
            Debug.LogWarning($"[IAP] Mất kết nối Store: {description?.Message}");
        }

        // =====================================================================
        // MUA HÀNG
        // =====================================================================

        /// <summary>
        /// Điểm vào để mua một gói. Store chưa sẵn sàng thì chạy chế độ mô phỏng;
        /// sản phẩm không có/không mua được thì báo lỗi; ngược lại gọi PurchaseProduct.
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
                // Thử kết nối lại cho lần bấm sau.
                Initialize();
                return;
            }

            if (!productCache.TryGetValue(productId, out Product product) || product == null || !product.availableToPurchase)
            {
                NotifyFailure(productId, "Sản phẩm hiện không khả dụng.");
                return;
            }

            Log($"Bắt đầu mua: {productId}");
            store.PurchaseProduct(product);
        }

        /// <summary>
        /// Store báo đơn đã thanh toán và chờ giao hàng: cộng thưởng (đúng 1 lần theo
        /// transaction ID) rồi mới ConfirmPurchase. Đơn không có trong catalog thì KHÔNG
        /// xác nhận, để Google Play tự hoàn tiền thay vì giữ tiền mà không giao hàng.
        /// </summary>
        private void HandlePurchasePending(PendingOrder order)
        {
            string productId = GetProductId(order);
            IapProductDefinition definition = Catalog.Find(productId);
            if (definition == null)
            {
                Debug.LogError($"[IAP] Đơn cho sản phẩm không có trong catalog: {productId}. " +
                               "Không cộng thưởng và không xác nhận đơn.");
                NotifyFailure(productId, "Sản phẩm không hợp lệ.");
                return;
            }

            if (GalaxyStrike.Save.SaveManager.Instance == null)
            {
                // Chưa ghi được thưởng thì chưa xác nhận: Store sẽ gửi lại đơn ở lần FetchPurchases sau.
                Debug.LogError("[IAP] Không tìm thấy SaveManager -> chưa cấp thưởng, giữ đơn ở trạng thái chờ.");
                return;
            }

            string transactionId = GetTransactionKey(order, productId);
            bool granted = GrantReward(transactionId, definition);

            store.ConfirmPurchase(order);

            if (granted)
            {
                Log($"Mua thành công: {productId} (+{definition.rewardAmount} {definition.rewardType})");
                OnPurchaseFinished?.Invoke(productId, true, "Mua thành công.");
                OnRewardGranted?.Invoke(productId);
            }
            else
            {
                Log($"Đơn {transactionId} đã được giao trước đó -> chỉ xác nhận lại với Store.");
            }
        }

        /// <summary>Mua thất bại / người chơi huỷ: báo UI, không cộng thưởng.</summary>
        private void HandlePurchaseFailed(FailedOrder order)
        {
            string productId = GetProductId(order);
            string reason = order != null && order.FailureReason == PurchaseFailureReason.UserCancelled
                ? "Đã huỷ giao dịch."
                : order != null ? $"{order.FailureReason}: {order.Details}" : "Unknown";

            Debug.LogWarning($"[IAP] Mua thất bại: {productId} - {reason}");
            OnPurchaseFinished?.Invoke(productId, false, reason);
        }

        /// <summary>
        /// Đơn bị hoãn (thanh toán chậm, chờ phụ huynh duyệt...). Thưởng sẽ được cộng khi
        /// Store chuyển đơn sang trạng thái pending.
        /// </summary>
        private void HandlePurchaseDeferred(DeferredOrder order)
        {
            string productId = GetProductId(order);
            Log($"Đơn đang chờ xử lý: {productId}");
            OnPurchaseFinished?.Invoke(productId, false, "Giao dịch đang chờ xử lý, phần thưởng sẽ được cộng khi hoàn tất.");
        }

        private static string GetProductId(Order order)
        {
            IReadOnlyList<CartItem> items = order?.CartOrdered?.Items();
            if (items == null || items.Count == 0 || items[0]?.Product?.definition == null)
                return null;

            return items[0].Product.definition.id;
        }

        /// <summary>
        /// Khoá chống trùng của đơn: transaction ID của Store, nếu thiếu thì băm receipt.
        /// </summary>
        private static string GetTransactionKey(Order order, string productId)
        {
            string transactionId = order?.Info?.TransactionID;
            if (!string.IsNullOrEmpty(transactionId))
                return transactionId;

            string receipt = order?.Info?.Receipt;
            if (!string.IsNullOrEmpty(receipt))
                return productId + ":" + Hash128.Compute(receipt);

            // Không có gì để nhận diện đơn: vẫn giao hàng, nhưng không chống trùng được.
            return productId + ":" + Guid.NewGuid().ToString("N");
        }

        // =====================================================================
        // MÔ PHỎNG (khi chưa có Store thật)
        // =====================================================================

        /// <summary>
        /// "Mua giả" khi Store chưa kết nối (Editor/CI): chỉ log và báo lỗi cho UI,
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
                $"[IAP] (Mô phỏng) Bấm mua '{productId}' khi Store chưa kết nối. " +
                "Không có giao dịch thật, KHÔNG cộng Coin/Gem.");

            OnPurchaseFinished?.Invoke(productId, false, "Cửa hàng chưa kết nối (chế độ mô phỏng).");
        }

        // =====================================================================
        // CẤP THƯỞNG
        // =====================================================================

        /// <summary>
        /// Cộng phần thưởng của gói vào save, ghi kèm transaction ID trong cùng một lần lưu.
        /// Trả false nếu đơn này đã được cộng trước đó.
        /// </summary>
        private static bool GrantReward(string transactionId, IapProductDefinition definition)
        {
            var save = GalaxyStrike.Save.SaveManager.Instance;
            if (save == null)
            {
                Debug.LogError("[IAP] Không tìm thấy SaveManager -> không cấp thưởng được.");
                return false;
            }

            int amount = Mathf.Max(0, definition.rewardAmount);
            return save.TryApplyIapPurchase(
                transactionId,
                definition.rewardType == IapRewardType.Coins ? amount : 0,
                definition.rewardType == IapRewardType.Gems ? amount : 0,
                definition.rewardType == IapRewardType.Energy ? amount : 0);
        }

        /// <summary>Log cảnh báo lỗi mua và phát event thất bại cho UI (success = false).</summary>
        private void NotifyFailure(string productId, string message)
        {
            Debug.LogWarning($"[IAP] {message} ({productId})");
            OnPurchaseFinished?.Invoke(productId, false, message);
        }

        /// <summary>Log có tiền tố [IAP], bị bỏ qua khi verboseLogging đang tắt.</summary>
        private void Log(string message)
        {
            if (!verboseLogging)
                return;

            Debug.Log($"[IAP] {message}");
        }
    }
}
