using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quái địch.
///
/// Toàn bộ chuyển động được điều khiển qua 3 trục (theo thiết kế Galaxy Shooter):
///  - <see cref="SpawnOrigin"/>  : xuất hiện từ đỉnh / sườn / đáy / warp.
///  - <see cref="FormationType"/>: đội hình & quỹ đạo bay (Snapshot / Snake / Wall / V / X / Anchor / Dive).
///  - <see cref="SpecialSpawnType"/>: cơ chế đặc biệt (carrier, splitting, boss summon, thiên thạch).
/// </summary>
public partial class Enemy : MonoBehaviour, IPoolable
{
    public static event System.Action<Enemy> OnEnemyDied;

    public static event System.Action<Enemy, Enemy> SplitChildSpawned;
    private static readonly List<Enemy> activeEnemies = new List<Enemy>();
    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;

    private void OnEnable() => activeEnemies.Add(this);
    private void OnDisable() => activeEnemies.Remove(this);

    [Header("Enemy Identity & Stats")]
    [SerializeField] private string enemyName = "Enemy";
    [SerializeField] private int health = 1;
    [SerializeField] private int scoreValue = 100;
    [SerializeField] private int coinValue = 1;

    [Header("Damage Impact VFX")]
    [SerializeField] private Color hitColor = new Color(1f, 0.7f, 0.25f);
    [SerializeField, Range(3, 18)] private int hitSparkCount = 6;
    [SerializeField, Range(0.4f, 2f)] private float hitScale = 0.8f;
    [SerializeField, Range(20f, 180f)] private float hitSpread = 100f;
    [SerializeField] private bool hitPulse;
    private float nextHitEffectTime;

    [Header("Hostile Shooting")]
    [SerializeField] private bool canShoot = false;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 2.5f;
    [Tooltip("Distinct projectile visuals rotated across each volley; duplicate entries are skipped.")]
    [SerializeField] private GameObject[] bulletVariants;
    [SerializeField] private bool twinMuzzles;
    [SerializeField, Min(0f)] private float muzzleSpacing = 0.18f;
    [SerializeField] private bool preferAimedShots;
    [SerializeField] private bool useArchetypeFirePattern;
    [SerializeField] private EnemyFirePattern archetypeFirePattern;
    [SerializeField, Min(2)] private int burstShots = 3;
    [SerializeField, Min(0.06f)] private float burstInterval = 0.13f;
    [SerializeField] private LineRenderer aimWarning;
    [SerializeField, Min(0f)] private float aimWarningDuration = 0.45f;
    private bool shotCharging;
    private Vector3 chargedDirection;
    private int burstShotsRemaining;
    private int burstShotIndex;
    private float burstTimer;

    [Header("Item Drop")]
    [Tooltip("Tỷ lệ rơi item tăng đạn (0.15 = 15%)")]
    [SerializeField, Range(0f, 1f)] private float bulletUpgradeDropChance = 0.15f;
    [SerializeField] private GameObject bulletUpgradePrefab;

    [Header("Special Spawn")]
    [Tooltip("Prefab quái con dùng cho Splitting / Carrier / Boss Summon")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private int minionCount = 3;

    // ----- Trạng thái cơ bản -----
    private int maxHealth;
    private float initialFireRate;
    private bool isDead;
    private float fireTimer;
    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private Coroutine flashRoutine;
    private Collider2D cachedCollider;
    private BossController bossController;

    // ----- Trục điều khiển -----
    private SpawnOrigin spawnOrigin = SpawnOrigin.TopCascade;
    private FormationType formation = FormationType.Wall;
    private SpecialSpawnType specialSpawn = SpecialSpawnType.None;

    // ----- Bay -----
    private Vector3 startPosition;
    private Vector3 formationAnchor;
    private float elapsed;
    private float descentSpeed = 2f;
    private float amplitude = 1.5f;
    private float frequency = 1.5f;
    private float phaseOffset;
    private float flankDirection = 1f;

    // Snake: các con cùng Wave chia sẻ path bằng index tuần tự.
    private int snakeIndex;
    private float snakeSpacing = 0.85f;
    private Vector3 snakeAnchor;

    // V-Formation
    private bool isVLeader;
    private Vector3 leaderOffset;

    // Drop & Anchor
    private float anchorY = 1.6f;
    private bool hasAnchored;
    private float anchorHoldTime = 0f;

    // Kamikaze
    private bool isKamikazeLocked;
    private float kamikazeOrbitTime = 1.4f;
    private Vector3 lockedTarget;
    private bool hasDivedPast;

    // Warp-in phasing
    private bool isPhasing;
    private float phasingTimer;

    // Xả đạn
    private EnemyFirePattern firePattern = EnemyFirePattern.Single;
    private EnemyFirePattern baseFirePattern = EnemyFirePattern.Single;
    private int volleyPatternIndex;
    private int projectileVariantCursor;
    private int bossPhase = 1;
    private bool staysInUpperLane;
    private int spreadCount = 3;
    private float spreadAngle = 35f;
    private int radialCount = 8;
    private float spiralAngle;
    private float spiralStep = 27f;
    private bool aimAtPlayer;

    // Carrier / Hive
    private float nextMinionTime;

    // Nghiêng theo vận tốc
    private Vector3 previousPosition;
    private bool hasPreviousPosition;
    private const float bankAnglePerUnitSpeed = 9f;
    private const float maxBankAngle = 32f;
    private const float bankSmoothing = 10f;

    private bool hasExitedScreen;
    private Transform playerTarget;

    public int Health => health;
    public int MaxHealth => maxHealth > 0 ? maxHealth : Mathf.Max(1, health);
    public int BaseHealth => MaxHealth;
    public float HealthRatio => maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0f;
    public bool IsEntering => false;
    public bool IsDead => isDead;
    public FormationType Formation => formation;
    public bool IsVLeader => isVLeader;

    // =====================================================================
    // VÒNG ĐỜI
    // =====================================================================
    private void Awake()
    {
        // Lưu HP gốc (dùng tính tỉ lệ máu) và tốc độ bắn gốc (để Boss đổi Phase nhân lại từ đây).
        maxHealth = Mathf.Max(1, health);
        initialFireRate = Mathf.Max(0.1f, fireRate);

        // Cache các component để trong Update không phải GetComponent nhiều lần.
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        cachedCollider = GetComponent<Collider2D>();
        bossController = GetComponent<BossController>();

        CaptureSpawnDefaults();
    }

    /// <summary>
    /// Random thời điểm bắn đầu tiên và pha dao động ban đầu để các con trong cùng
    /// wave không bắn/dao động giống hệt nhau (tránh nhìn "máy móc").
    /// </summary>
    private void Start()
    {
        fireTimer = Random.Range(0f, Mathf.Max(0.1f, fireRate));
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// Vòng lặp chính mỗi frame (thứ tự quan trọng): cộng dồn thời gian sống ->
    /// cập nhật phasing (warp-in) -> di chuyển theo đội hình -> nghiêng theo vận tốc ->
    /// xử lý bắn -> cơ chế đặc biệt -> kiểm tra rời màn hình.
    /// </summary>
    private void Update()
    {
        if (isDead)
            return;

        float dt = Time.deltaTime;
        elapsed += dt;

        UpdatePhasing(dt);
        UpdateFlight(dt);
        UpdateBankAngle(dt);
        UpdateShooting();
        UpdateSpecials(dt);
        CheckScreenExit();
    }
}
