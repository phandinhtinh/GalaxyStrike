using UnityEngine;
using GalaxyStrike.Data;
using GalaxyStrike.Save;

/// <summary>
/// Áp dụng trang bị đã mua (Weapon / Booster) vào gameplay.
///
///  - LASER GUN    : đạn xuyên thấu, sát thương x2.
///  - HOMING ROCKET: bắn thêm tên lửa tự tìm mục tiêu.
///  - MAGNET       : hút PowerUp trong bán kính rộng.
///  - ENERGY SHIELD: miễn nhiễm sát thương trong 5 giây đầu màn.
///
/// Script đặt trên Player. Không có trang bị nào được mua thì hoạt động y như cũ.
/// </summary>
[DisallowMultipleComponent]
public class PlayerEquipment : MonoBehaviour
{
    [Header("--- Owned Item IDs (khớp ItemDataSO.itemID) ---")]
    [SerializeField] private string laserGunItemId = "weapon_laser";
    [SerializeField] private string homingRocketItemId = "weapon_rocket";
    [SerializeField] private string magnetItemId = "booster_magnet";
    [SerializeField] private string shieldItemId = "booster_shield";

    [Header("--- Laser Gun ---")]
    [Tooltip("Hệ số sát thương khi trang bị Laser")]
    [SerializeField] private int laserDamageMultiplier = 2;

    [Header("--- Homing Rocket ---")]
    [SerializeField] private GameObject rocketPrefab;
    [Tooltip("Số giây giữa hai lần phóng tên lửa")]
    [SerializeField] private float rocketInterval = 2.5f;

    [Header("--- Magnet ---")]
    [Tooltip("Bán kính hút vật phẩm (đơn vị world)")]
    [SerializeField] private float magnetRadius = 3.5f;
    [SerializeField] private float magnetPullSpeed = 9f;
    [Tooltip("Bán kính hút coin luôn bật quanh Player (vùng vô hình, đơn vị world). " +
             "Coin ngoài vùng này không bay lại nên không được cộng điểm.")]
    [SerializeField] private float coinMagnetRadius = 3.5f;
    [SerializeField] private float coinMagnetPullSpeed = 8f;

    [Header("--- Energy Shield ---")]
    [Tooltip("Thời gian miễn nhiễm sát thương đầu màn")]
    [SerializeField] private float shieldDuration = 5f;
    [SerializeField] private Color shieldTint = new Color(0.4f, 0.9f, 1f, 1f);

    private bool hasLaser;
    private bool hasRocket;
    private bool hasMagnet;
    private bool hasShield;

    private float rocketTimer;
    private float shieldTimer;
    private PlayerController player;
    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private ContactFilter2D pickupContactFilter;
    private readonly Collider2D[] magnetBuffer = new Collider2D[24];

    public bool HasLaser => hasLaser;
    public bool HasRocket => hasRocket;
    public bool HasMagnet => hasMagnet;
    public int LaserDamageMultiplier => Mathf.Max(1, laserDamageMultiplier);
    public bool IsShielded => shieldTimer > 0f;

    [Header("--- Active Boosters (runtime) ---")]
    [Tooltip("Thời gian Magnet cưỡng bức khi dùng Hyper Magnet booster")]
    [SerializeField] private float hyperMagnetDuration = 10f;

    /// <summary>Bán kính hút của Magnet vĩnh viễn (đã mua) — Booster Hyper Magnet dùng chung.</summary>
    public float MagnetRadius => magnetRadius;

    /// <summary>Thời gian cưỡng bức Hyper Magnet còn lại (giây).</summary>
    public float HyperMagnetTimeLeft { get; private set; }

    /// <summary>
    /// Bật cưỡng bức Hyper Magnet trong <paramref name="seconds"/> giây, bất kể
    /// người chơi có trang bị Magnet hay không.
    /// </summary>
    public void ActivateHyperMagnet(float seconds)
    {
        // Không truyền thời gian (<= 0) thì dùng thời lượng mặc định cấu hình trên tàu.
        if (seconds <= 0f)
            seconds = hyperMagnetDuration;

        HyperMagnetTimeLeft = Mathf.Max(HyperMagnetTimeLeft, seconds);
    }

    /// <summary>
    /// Kích hoạt Energy Shield tức thời (dùng cho Booster Shield / hồi sinh).
    /// </summary>
    public void ActivateShield(float seconds)
    {
        shieldTimer = Mathf.Max(shieldTimer, seconds);
        if (player != null)
            player.GrantInvulnerability(seconds);
    }

    private void Awake()
    {
        // Bộ lọc va chạm cho phép dò cả collider trigger (dùng cho nam châm hút vật phẩm).
        pickupContactFilter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        player = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    /// <summary>
    /// Đọc save để biết người chơi đã mua trang bị nào rồi BẬT cờ tương ứng.
    /// Nếu có Energy Shield thì kích hoạt bất tử ngay đầu màn.
    /// Đã mua Rocket mà quên gán prefab thì cảnh báo và tắt rocket để không lỗi.
    /// </summary>
    private void Start()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null)
            return;

        hasLaser = save.IsItemOwned(laserGunItemId);
        hasRocket = save.IsItemOwned(homingRocketItemId);
        hasMagnet = save.IsItemOwned(magnetItemId);
        hasShield = save.IsItemOwned(shieldItemId);

        if (hasShield)
        {
            shieldTimer = shieldDuration;
            if (player != null)
                player.GrantInvulnerability(shieldDuration);
        }

        if (hasRocket && rocketPrefab == null)
        {
            Debug.LogWarning("[PlayerEquipment] Đã mua Homing Rocket nhưng chưa gán rocketPrefab.");
            hasRocket = false;
        }
    }

    /// <summary>
    /// Mỗi frame cập nhật 3 trang bị có yếu tố thời gian: Shield (đếm ngược),
    /// Rocket (bắn theo nhịp), Magnet (hút vật phẩm quanh player).
    /// Laser không cần update vì chỉ nhân sát thương lúc bắn.
    /// </summary>
    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateShield(dt);
        UpdateRocket(dt);
        UpdatePickupMagnet(dt);

        // Hyper Magnet có thời hạn: đếm ngược khi đang bật.
        if (HyperMagnetTimeLeft > 0f)
            HyperMagnetTimeLeft = Mathf.Max(0f, HyperMagnetTimeLeft - dt);
    }

    // =====================================================================
    // LASER GUN — chỉ cần nhân sát thương, PlayerController đọc qua đây
    // =====================================================================
    /// <summary>Sát thương cuối cùng của 1 viên đạn sau khi áp trang bị.</summary>
    public int ResolveDamage(int baseDamage)
    {
        if (!hasLaser)
            return baseDamage;

        return baseDamage * LaserDamageMultiplier;
    }

    // =====================================================================
    // HOMING ROCKET
    // =====================================================================

    /// <summary>
    /// Cứ mỗi rocketInterval giây (nếu đã mua Rocket) thì tạo 1 tên lửa phía trên tàu
    /// và gán sát thương; tên lửa tự bay tìm mục tiêu qua HomingProjectile.
    /// </summary>
    private void UpdateRocket(float dt)
    {
        if (!hasRocket || rocketPrefab == null || Time.timeScale <= 0f)
            return;

        rocketTimer += dt;
        if (rocketTimer < rocketInterval)
            return;

        rocketTimer = 0f;

        Vector3 spawnPos = transform.position + Vector3.up * 0.4f;
        GameObject rocket = Instantiate(rocketPrefab, spawnPos, Quaternion.identity);

        // Bật chế độ tự tìm mục tiêu nếu prefab có script hỗ trợ.
        var homing = rocket.GetComponent<HomingProjectile>();
        if (homing != null)
            homing.Initialize(player != null ? player.PlayerDamage : 1);
    }

    // =====================================================================
    // PICKUP MAGNET
    // =====================================================================

    /// <summary>
    /// Quét các collider trong bán kính quanh player (không cấp phát bộ nhớ):
    /// - Có Magnet: hút CẢ power-up lẫn coin với bán kính lớn.
    /// - Không có Magnet: chỉ coin bị hút với bán kính nhỏ mặc định.
    /// Vật phẩm bị hút do PlayerEquipment kéo về player mỗi frame.
    /// </summary>
    private void UpdatePickupMagnet(float dt)
    {
        // Booster Hyper Magnet (có thời hạn) hành xử như đã trang bị Magnet vĩnh viễn.
        bool magnetActive = hasMagnet || HyperMagnetTimeLeft > 0f;
        float radius = magnetActive ? magnetRadius : coinMagnetRadius;
        float pullSpeed = magnetActive ? magnetPullSpeed : coinMagnetPullSpeed;

        // NonAlloc: tránh cấp phát mảng mỗi khung hình (GC spike trên mobile).
        int count = Physics2D.OverlapCircle(transform.position, radius, pickupContactFilter, magnetBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = magnetBuffer[i];
            if (hit == null || !hit.CompareTag("PowerUp"))
                continue;

            BulletUpgradeItem item = hit.GetComponent<BulletUpgradeItem>();
            CoinDrop coin = hit.GetComponent<CoinDrop>();
            if (item == null && coin == null)
                continue;

            // Coin có vùng hút cơ bản luôn bật. Các PowerUp khác chỉ bị hút
            // khi Player có Magnet hoặc đang bật Hyper Magnet.
            if (coin == null && !magnetActive)
                continue;

            if (item != null)
                item.SetBeingAttracted(true);
            if (coin != null)
                coin.SetBeingAttracted(transform, pullSpeed);

            if (coin == null)
            {
                Transform target = hit.transform;
                target.position = Vector3.MoveTowards(
                    target.position,
                    transform.position,
                    pullSpeed * dt);
            }
        }
    }

    // =====================================================================
    // ENERGY SHIELD
    // =====================================================================

    /// <summary>
    /// Đếm ngược thời gian khiên: tô màu xanh nhấp nháy cho tàu để biết đang được bảo vệ,
    /// hết thời gian thì trả lại màu gốc.
    /// </summary>
    private void UpdateShield(float dt)
    {
        if (shieldTimer <= 0f)
            return;

        shieldTimer -= dt;

        // Nhấp nháy xanh để người chơi biết đang được bảo vệ.
        if (spriteRenderer != null)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            spriteRenderer.color = Color.Lerp(originalColor, shieldTint, 0.35f + t * 0.35f);
        }

        if (shieldTimer <= 0f && spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }
}
