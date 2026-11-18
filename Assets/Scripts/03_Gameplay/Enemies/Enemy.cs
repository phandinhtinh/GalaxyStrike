using System.Collections;
using UnityEngine;

/// <summary>
/// Quái địch.
///
/// Toàn bộ chuyển động được điều khiển qua 3 trục (theo thiết kế Galaxy Shooter):
///  - <see cref="SpawnOrigin"/>  : xuất hiện từ đỉnh / sườn / đáy / warp.
///  - <see cref="FormationType"/>: đội hình & quỹ đạo bay (Snapshot / Snake / Wall / V / X / Anchor / Dive).
///  - <see cref="SpecialSpawnType"/>: cơ chế đặc biệt (carrier, splitting, boss summon, thiên thạch).
/// </summary>
public class Enemy : MonoBehaviour
{
    public static event System.Action<Enemy> OnEnemyDied;

    [Header("Enemy Identity & Stats")]
    [SerializeField] private string enemyName = "Enemy";
    [SerializeField] private int health = 1;
    [SerializeField] private int scoreValue = 100;
    [SerializeField] private int coinValue = 1;

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

    // =====================================================================
    // PHASING (Warp-in)
    // =====================================================================

    /// <summary>
    /// Hiệu ứng Warp-in: giảm dần độ mờ (alpha 0.25 -> 1) trong 1.5 giây,
    /// trong lúc đó quái KHÔNG có hitbox (không thể bị bắn trúng).
    /// </summary>
    private void UpdatePhasing(float dt)
    {
        if (!isPhasing)
            return;

        phasingTimer -= dt;

        // Bóng mờ dần hiện rõ trong 1.5s, chưa có hitbox.
        if (spriteRenderer != null)
        {
            Color c = originalColor;
            c.a = Mathf.Lerp(0.25f, 1f, 1f - Mathf.Clamp01(phasingTimer / 1.5f));
            spriteRenderer.color = c;
        }

        if (phasingTimer <= 0f)
        {
            isPhasing = false;
            if (cachedCollider != null)
                cachedCollider.enabled = true;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }
    }

    // =====================================================================
    // QUỸ ĐẠO BAY — điều phối theo FormationType
    // =====================================================================

    /// <summary>
    /// Chọn hàm bay theo đội hình: quái "trên cao" (quái khủng, không bay xuống)
    /// dùng FlyUpperLane; còn lại switch theo FormationType (Snake/Wall/V/X/Anchor/Kamikaze/Boss).
    /// </summary>
    private void UpdateFlight(float dt)
    {
        if (staysInUpperLane && formation != FormationType.Boss)
        {
            FlyUpperLane(dt);
            return;
        }

        switch (formation)
        {
            case FormationType.Snake:
                FlySnake(dt);
                break;

            case FormationType.Wall:
                FlyWall(dt);
                break;

            case FormationType.VFormation:
                FlyVFormation(dt);
                break;

            case FormationType.PincerCross:
                FlyPincerCross(dt);
                break;

            case FormationType.DropAnchor:
                FlyDropAnchor(dt);
                break;

            case FormationType.Kamikaze:
                FlyKamikaze(dt);
                break;

            case FormationType.Boss:
                FlyBoss(dt);
                break;

            default:
                FlyWall(dt);
                break;
        }
    }

    /// <summary>
    /// SNAKE WAVE — Hàng một rồng rắn.
    /// Mỗi con bám theo một offset dọc trên cùng một đường cong chữ S,
    /// tạo thành dải dài nối đuôi nhau (bắn dây chuyền rất đã tay).
    /// </summary>
    private void FlySnake(float dt)
    {
        // Bố cục chung: cả hàng cùng tiến xuống, mỗi con lệch pha theo index.
        float lineProgress = elapsed * descentSpeed;
        float trail = snakeIndex * snakeSpacing;

        // Chữ S: hai sóng sin lệch pha để thân rắn uốn lượn.
        float along = lineProgress - trail;
        float x = Mathf.Sin(along * 0.9f + phaseOffset * 0.2f) * amplitude
                + Mathf.Sin(along * 0.45f) * amplitude * 0.35f;
        float y = snakeAnchor.y - along;

        transform.position = new Vector3(snakeAnchor.x + x, y, 0f);
    }

    /// <summary>WALL — Dàn hàng ngang tiến chậm như bức tường.</summary>
    private void FlyWall(float dt)
    {
        wallDescent += descentSpeed * dt;

        // Nhấp nhô nhẹ quanh quỹ đạo chính (không cộng dồn vào vị trí).
        float bob = Mathf.Sin(elapsed * 1.4f + phaseOffset) * 0.08f;

        transform.position = new Vector3(
            startPosition.x,
            startPosition.y - wallDescent + bob,
            0f);
    }

    private float wallDescent;

    /// <summary>
    /// V-FORMATION — Mũi tên chữ V.
    /// Leader ở đỉnh; khi leader chết, spawner gọi BreakVFormation() cho 2 cánh toả ra.
    /// </summary>
    private void FlyVFormation(float dt)
    {
        float speed = isVLeader ? descentSpeed : descentSpeed * 0.92f;
        Vector3 down = Vector3.down * (speed * dt);

        if (vBroken)
        {
            // Hai cánh tách ra mép màn hình theo cánh ĐÃ XÁC ĐỊNH lúc spawn.
            // Không dùng Sign(hiện tại - anchor) vì hai giá trị gần bằng nhau -> mất hướng.
            transform.position += down + new Vector3(vWingSide * descentSpeed * 1.5f, 0f, 0f) * dt;
            return;
        }

        transform.position += down;
    }

    private bool vBroken;

    /// <summary>Cánh của chữ V: -1 = trái, +1 = phải, 0 = leader giữa.</summary>
    private float vWingSide;

    /// <summary>Spawner gán cánh cho từng con khi dựng chữ V.</summary>
    public void SetVWingSide(float side)
    {
        vWingSide = side < 0f ? -1f : (side > 0f ? 1f : 0f);
    }

    /// <summary>Gọi khi con chỉ huy chữ V bị hạ: hai cánh tỏa ra hai bên.</summary>
    public void BreakVFormation()
    {
        vBroken = true;
    }

    /// <summary>
    /// PINCER CROSS — Hai hàng bay chéo cắt nhau ở tâm màn hình tạo chữ X.
    /// <paramref name="dir"/> = +1 bay chéo sang phải, -1 bay chéo sang trái.
    /// </summary>
    private void FlyPincerCross(float dt)
    {
        float speed = descentSpeed;
        transform.position += new Vector3(flankDirection * speed * 0.92f, -speed, 0f) * dt;
    }

    /// <summary>
    /// DROP & ANCHOR — Trụ pháo.
    /// Bay xuống tới <see cref="anchorY"/> rồi DỪNG HẲN, xả đạn liên tục.
    /// </summary>
    private void FlyDropAnchor(float dt)
    {
        if (!hasAnchored)
        {
            float nextY = Mathf.MoveTowards(transform.position.y, anchorY, descentSpeed * dt);
            transform.position = new Vector3(formationAnchor.x, nextY, 0f);

            if (Mathf.Abs(nextY - anchorY) < 0.01f)
            {
                hasAnchored = true;
                anchorHoldTime = 0f;
            }
            return;
        }

        // Neo tại chỗ, chỉ dao động rất nhẹ để trông "còn sống".
        anchorHoldTime += dt;
        float sway = Mathf.Sin(anchorHoldTime * 1.1f) * 0.12f;
        transform.position = new Vector3(formationAnchor.x + sway, anchorY, 0f);
    }

    /// <summary>
    /// DIVE-BOMBER / KAMIKAZE — Lượn vòng trên đỉnh, khóa mục tiêu, rồi lao thẳng xuống x2-x3.
    /// </summary>
    private void FlyKamikaze(float dt)
    {
        if (!isKamikazeLocked)
        {
            // Giai đoạn 1: lượn vòng tròn quanh vị trí neo trên đỉnh.
            float orbitAngle = elapsed * 2.2f + phaseOffset;
            float orbitRadius = Mathf.Max(0.4f, amplitude * 0.55f);

            transform.position = new Vector3(
                formationAnchor.x + Mathf.Cos(orbitAngle) * orbitRadius,
                formationAnchor.y + Mathf.Sin(orbitAngle) * orbitRadius * 0.45f,
                0f);

            if (elapsed >= kamikazeOrbitTime)
            {
                ResolvePlayerTarget();
                lockedTarget = playerTarget != null ? playerTarget.position : new Vector3(0f, -4f, 0f);
                isKamikazeLocked = true;
            }
            return;
        }

        // Giai đoạn 2: lao thẳng tới điểm đã khóa.
        Vector3 toTarget = lockedTarget - transform.position;
        float distance = toTarget.magnitude;

        // ĐÃ TỚI ĐÍCH: lao thẳng xuống và ra khỏi màn hình.
        // Nếu giữ hướng cũ, vector về 0 và quái sẽ đứng khựng tại chỗ.
        if (distance < 0.25f || hasDivedPast)
        {
            hasDivedPast = true;
            transform.position += Vector3.down * (descentSpeed * 2.6f * dt);

            // Rời mép dưới -> tự huỷ như mọi quái thoát màn khác.
            if (IsBelowScreen(0.25f))
                ForceEscape();
            return;
        }

        Vector3 direction = toTarget / distance;

        // Đánh dấu nếu đã lao xuống dưới điểm khóa (không quay lại đuổi nữa).
        if (transform.position.y < lockedTarget.y - 0.05f)
            hasDivedPast = true;

        transform.position += direction * (descentSpeed * 2.6f * dt);
    }

    /// <summary>BOSS — giữ vị trí trên cao; BossController tự lo dao động nếu có.</summary>
    private void FlyBoss(float dt)
    {
        if (bossController != null)
            return;

        // Fallback for a Boss formation whose prefab did not include BossController.
        // It still strafes in the upper lane instead of drifting down forever.
        FlyUpperLane(dt);
    }

    /// <summary>Heavy enemies strafe and fire from the upper part of the screen.</summary>
    private void FlyUpperLane(float dt)
    {
        Camera cam = Camera.main;
        float targetY = GetUpperLaneY(cam);
        float y = Mathf.MoveTowards(transform.position.y, targetY, Mathf.Max(0.8f, descentSpeed) * dt);

        if (Mathf.Abs(y - targetY) < 0.02f)
            y = targetY + Mathf.Sin(elapsed * frequency * 0.55f + phaseOffset) * 0.12f;

        float cameraCenterX = cam != null ? cam.transform.position.x : 0f;
        float halfWidth = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : 2.8f;
        float edgePadding = Mathf.Min(0.45f, halfWidth * 0.2f);
        float maxSway = Mathf.Max(0.15f, halfWidth - edgePadding);
        float centerX = cameraCenterX;
        if (Mathf.Abs(formationAnchor.x - cameraCenterX) <= halfWidth)
            centerX = formationAnchor.x;
        else if (Mathf.Abs(startPosition.x - cameraCenterX) > halfWidth)
            centerX += Mathf.Sign(startPosition.x - cameraCenterX) * maxSway * 0.55f;

        float sway = Mathf.Min(Mathf.Max(0.3f, amplitude), maxSway);
        float x = centerX + Mathf.Sin(elapsed * frequency + phaseOffset) * sway;
        x = Mathf.Clamp(x, cameraCenterX - maxSway, cameraCenterX + maxSway);

        transform.position = new Vector3(x, y, 0f);
    }

    private static float GetUpperLaneY(Camera cam)
    {
        if (cam == null || !cam.orthographic)
            return 1.5f;

        return cam.ViewportToWorldPoint(
            new Vector3(0.5f, 0.62f, Mathf.Abs(cam.transform.position.z))).y;
    }

    // =====================================================================
    // CƠ CHẾ ĐẶC BIỆT (NHÓM III)
    // =====================================================================

    /// <summary>
    /// Xử lý cơ chế đặc biệt theo SpecialSpawnType (hiện chỉ có Carrier/Hive).
    /// </summary>
    private void UpdateSpecials(float dt)
    {
        if (specialSpawn == SpecialSpawnType.CarrierHive)
            UpdateCarrierHive(dt);
    }

    /// <summary>Carrier / Hive: tàu mẹ định kỳ phóng tàu con ra hai bên sườn.</summary>
    private void UpdateCarrierHive(float dt)
    {
        if (minionPrefab == null || Time.timeScale <= 0f)
            return;

        nextMinionTime -= dt;
        if (nextMinionTime > 0f)
            return;

        nextMinionTime = 2.4f;
        HiveSpawned?.Invoke(this, minionPrefab);
    }

    /// <summary>Spawner lắng nghe sự kiện này để tạo quái con (giữ Enemy không phụ thuộc Spawner).</summary>
    public static event System.Action<Enemy, GameObject> HiveSpawned;

    // =====================================================================
    // NGHIÊNG THEO VẬN TỐC
    // =====================================================================

    /// <summary>
    /// Tạo cảm giác "nghiêng khi lượn": tính vận tốc ngang từ chênh lệch vị trí
    /// giữa 2 frame, rồi xoay sprite quanh trục Z tỉ lệ với vận tốc đó (có giới hạn
    /// và làm mượt). Boss và trụ pháo đã neo thì không nghiêng.
    /// </summary>
    private void UpdateBankAngle(float dt)
    {
        if (formation == FormationType.Boss ||
            formation == FormationType.DropAnchor && hasAnchored)
        {
            return;
        }

        Vector3 current = transform.position;
        if (!hasPreviousPosition)
        {
            previousPosition = current;
            hasPreviousPosition = true;
            return;
        }

        float lateralVelocity = dt > 0f ? (current.x - previousPosition.x) / dt : 0f;
        previousPosition = current;

        float targetAngle = Mathf.Clamp(-lateralVelocity * bankAnglePerUnitSpeed, -maxBankAngle, maxBankAngle);

        float currentAngle = transform.eulerAngles.z;
        if (currentAngle > 180f)
            currentAngle -= 360f;

        float smoothed = Mathf.Lerp(currentAngle, targetAngle, 1f - Mathf.Exp(-bankSmoothing * dt));
        transform.rotation = Quaternion.Euler(0f, 0f, smoothed);
    }

    // =====================================================================
    // XẢ ĐẠN
    // =====================================================================

    /// <summary>
    /// Điều khiển bắn: chưa thể bắn thì bỏ qua; đang trong loạt burst thì bắn từng
    /// viên theo burstInterval; bình thường đếm fireTimer, hiện tia cảnh báo ngắm
    /// (aimWarning) trước khi bắn rồi gọi FireVolley đúng nhịp fireRate.
    /// </summary>
    private void UpdateShooting()
    {
        if (aimWarning != null) aimWarning.enabled = false;
        if (!canShoot || bulletPrefab == null || Time.timeScale <= 0f || isPhasing || elapsed < 0.75f)
            return;

        // Chỉ bắn khi đã thực sự nằm trong khung hình.
        if (!IsVisibleOnScreen())
            return;

        ResolvePlayerTarget();
        if (playerTarget != null && (playerTarget.position - transform.position).sqrMagnitude < 1f)
        {
            burstShotsRemaining = 0;
            shotCharging = false;
            return;
        }

        if (burstShotsRemaining > 0)
        {
            burstTimer += Time.deltaTime;
            if (burstTimer >= burstInterval)
            {
                burstTimer = 0f;
                FireBurstStep();
                burstShotsRemaining--;
            }
            return;
        }

        fireTimer += Time.deltaTime;
        if (aimWarning != null && fireTimer >= fireRate - aimWarningDuration)
        {
            if (!shotCharging)
            {
                chargedDirection = aimAtPlayer || firePattern == EnemyFirePattern.Aimed
                    ? GetDirectionToPlayer() : Vector3.down;
                shotCharging = true;
            }
            aimWarning.enabled = true;
            aimWarning.SetPosition(0, transform.position);
            aimWarning.SetPosition(1, transform.position + chargedDirection * 14f);
        }
        if (fireTimer < fireRate)
            return;

        fireTimer = 0f;
        FireVolley();
        shotCharging = false;
        if (aimWarning != null) aimWarning.enabled = false;
    }

    /// <summary>Đã ra khỏi mép dưới màn hình chưa (dùng cho quái lao xuống).</summary>
    private bool IsBelowScreen(float margin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position.y < -8f;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        return viewport.y < -margin;
    }

    /// <summary>Buộc quái rời màn ngay: tính vào tiến trình màn rồi tự huỷ.</summary>
    private void ForceEscape()
    {
        if (hasExitedScreen)
            return;

        hasExitedScreen = true;
        NotifyEscaped();
        Destroy(gameObject);
    }

    /// <summary>
    /// Quái có đang nằm trong vùng nhìn của camera không (có tính margin nhỏ).
    /// Dùng để không bắn và không tính va chạm khi còn ở ngoài màn.
    /// </summary>
    private bool IsVisibleOnScreen()
    {        Camera cam = Camera.main;
        if (cam == null)
            return true;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        if (viewport.z < 0f)
            return false;

        const float margin = 0.02f;
        return viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    /// <summary>
    /// Bắn một "loạt" theo pattern hiện tại: Burst/AimedBurst/FanBurst bắn viên đầu
    /// rồi để UpdateShooting bắn tiếp các viên còn lại; các pattern khác gọi ngay hàm xả tương ứng.
    /// </summary>
    private void FireVolley()
    {
        firePattern = GetNextVolleyPattern();

        switch (firePattern)
        {
            case EnemyFirePattern.Burst:
            case EnemyFirePattern.AimedBurst:
            case EnemyFirePattern.FanBurst:
                burstShotIndex = 0;
                FireBurstStep();
                burstShotIndex++;
                burstShotsRemaining = Mathf.Max(0, burstShots - 1);
                burstTimer = 0f;
                break;
            case EnemyFirePattern.Spread:
                FireSpread();
                break;
            case EnemyFirePattern.RadialBloom:
                FireRadialBloom();
                break;
            case EnemyFirePattern.Spiral:
                FireSpiral();
                break;
            case EnemyFirePattern.Aimed:
                FireSingle(GetDirectionToPlayer());
                break;
            case EnemyFirePattern.Single:
            default:
                FireSingle(aimAtPlayer ? GetDirectionToPlayer() : Vector3.down);
                break;
        }
    }

    /// <summary>
    /// Chọn pattern cho loạt đạn kế tiếp. Khó hơn thì luân phiên nhiều pattern hơn:
    /// - Boss: theo bossPhase (1..3), phase càng cao càng nhiều pattern.
    /// - Quái thường: dựa trên HP tối đa (quái trâu bắn đa dạng hơn).
    /// </summary>
    private EnemyFirePattern GetNextVolleyPattern()
    {
        int index = volleyPatternIndex++;

        if (bossController != null)
        {
            switch (bossPhase)
            {
                case 1:
                    return index % 2 == 0 ? baseFirePattern : EnemyFirePattern.AimedBurst;
                case 2:
                    switch (index % 3)
                    {
                        case 0: return baseFirePattern;
                        case 1: return EnemyFirePattern.Spread;
                        default: return EnemyFirePattern.FanBurst;
                    }
                default:
                    switch (index % 4)
                    {
                        case 0: return baseFirePattern;
                        case 1: return EnemyFirePattern.FanBurst;
                        case 2: return EnemyFirePattern.RadialBloom;
                        default: return EnemyFirePattern.Spiral;
                    }
            }
        }

        if (MaxHealth >= 12)
        {
            switch (index % 4)
            {
                case 0: return baseFirePattern;
                case 1: return EnemyFirePattern.Spread;
                case 2: return EnemyFirePattern.FanBurst;
                default: return EnemyFirePattern.RadialBloom;
            }
        }

        if (MaxHealth >= 8)
        {
            switch (index % 3)
            {
                case 0: return baseFirePattern;
                case 1: return EnemyFirePattern.AimedBurst;
                default: return EnemyFirePattern.Spread;
            }
        }

        if (MaxHealth >= 5)
            return index % 2 == 0 ? baseFirePattern : EnemyFirePattern.AimedBurst;

        return baseFirePattern;
    }

    /// <summary>
    /// Chọn hình dạng viên đạn kế tiếp từ bulletVariants (xoay vòng).
    /// Số loại được dùng tăng theo độ mạnh (Boss Phase / HP tối đa).
    /// Không có biến thể thì trả về bulletPrefab mặc định.
    /// </summary>
    private GameObject GetNextBulletVariant()
    {
        if (bulletVariants == null || bulletVariants.Length == 0)
            return bulletPrefab;

        int desiredTypes = bossController != null
            ? 2 + bossPhase
            : MaxHealth >= 12 ? 4
            : MaxHealth >= 8 ? 3
            : MaxHealth >= 5 ? 2
            : 1;

        int distinctCount = CountDistinctBulletVariants();
        if (distinctCount == 0)
            return bulletPrefab;

        int typeCount = Mathf.Min(desiredTypes, distinctCount);
        int selectedType = projectileVariantCursor++ % typeCount;
        int currentType = 0;

        for (int i = 0; i < bulletVariants.Length; i++)
        {
            GameObject candidate = bulletVariants[i] != null ? bulletVariants[i] : bulletPrefab;
            if (candidate == null || IsDuplicateBulletVariant(i, candidate))
                continue;

            if (currentType == selectedType)
                return candidate;
            currentType++;
        }

        return bulletPrefab;
    }

    /// <summary>
    /// Đếm số loại đạn KHÁC NHAU trong bulletVariants (bỏ qua phần tử trùng và null).
    /// </summary>
    private int CountDistinctBulletVariants()
    {
        int count = 0;
        for (int i = 0; i < bulletVariants.Length; i++)
        {
            GameObject candidate = bulletVariants[i] != null ? bulletVariants[i] : bulletPrefab;
            if (candidate != null && !IsDuplicateBulletVariant(i, candidate))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Kiểm tra phần tử thứ <paramref name="index"/> của bulletVariants đã xuất hiện
    /// ở vị trí trước đó chưa (để chỉ dùng mỗi prefab đạn một lần).
    /// </summary>
    private bool IsDuplicateBulletVariant(int index, GameObject candidate)
    {
        for (int i = 0; i < index; i++)
        {
            GameObject previous = bulletVariants[i] != null ? bulletVariants[i] : bulletPrefab;
            if (previous == candidate)
                return true;
        }
        return false;
    }

    // =====================================================================
    // XẢ ĐẠN
    // =====================================================================

    /// <summary>
    /// Bắn TỪNG viên trong loạt burst (UpdateShooting gọi lại mỗi burstInterval).
    /// FanBurst: mỗi viên lệch dần sang hai bên theo spreadAngle (hình quạt).
    /// Các loại burst khác: bắn theo hướng player (hoặc thẳng xuống).
    /// </summary>
    private void FireBurstStep()
    {
        if (firePattern == EnemyFirePattern.FanBurst)
        {
            float t = burstShots > 1 ? burstShotIndex / (float)(burstShots - 1) - 0.5f : 0f;
            float angle = t * spreadAngle;
            float rad = angle * Mathf.Deg2Rad;
            SpawnBullet(new Vector3(Mathf.Sin(rad), -Mathf.Cos(rad), 0f));
        }
        else
        {
            bool aimed = firePattern == EnemyFirePattern.AimedBurst || aimAtPlayer;
            SpawnBullet(aimed ? GetDirectionToPlayer() : Vector3.down);
        }
        burstShotIndex++;
    }

    /// <summary>
    /// Vector đơn vị chỉ từ quái tới player (đã chuẩn hoá).
    /// Đang "charge" thì dùng hướng đã chốt trước đó (để tia cảnh báo khớp với đạn thật).
    /// Không có player thì bắn thẳng xuống.
    /// </summary>
    private Vector3 GetDirectionToPlayer()
    {
        if (shotCharging) return chargedDirection;
        ResolvePlayerTarget();
        if (playerTarget == null)
            return Vector3.down;

        Vector3 dir = (playerTarget.position - transform.position).normalized;
        return dir.sqrMagnitude > 0.001f ? dir : Vector3.down;
    }

    /// <summary>
    /// Bắn đúng 1 viên theo hướng cho trước.
    /// </summary>
    private void FireSingle(Vector3 direction)
    {
        SpawnBullet(direction);
    }

    /// <summary>
    /// Bắn hình quạt: chia đều <see cref="spreadCount"/> viên trong góc <see cref="spreadAngle"/>,
    /// lấy hướng player (hoặc xuống) làm trục giữa.
    /// </summary>
    private void FireSpread()
    {
        Vector3 baseDir = aimAtPlayer ? GetDirectionToPlayer() : Vector3.down;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < spreadCount; i++)
        {
            float t = spreadCount > 1 ? (i / (float)(spreadCount - 1)) - 0.5f : 0f;
            float angle = (baseAngle + t * spreadAngle) * Mathf.Deg2Rad;
            SpawnBullet(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }
    }

    /// <summary>
    /// Bắn toả tròn 360 độ đều nhau (tối thiểu 6 viên), mỗi loạt xoay lệch theo thời gian
    /// để không trùng hướng cũ.
    /// </summary>
    private void FireRadialBloom()
    {
        int count = Mathf.Max(6, radialCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + elapsed * 20f;
            float rad = angle * Mathf.Deg2Rad;
            SpawnBullet(new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f));
        }
    }

    /// <summary>
    /// Bắn xoắn ốc: mỗi viên tăng thêm <see cref="spiralStep"/> độ so với viên trước,
    /// tạo vệt xoáy tròn.
    /// </summary>
    private void FireSpiral()
    {
        spiralAngle += spiralStep;
        float rad = spiralAngle * Mathf.Deg2Rad;
        SpawnBullet(new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f));
    }

    /// <summary>
    /// Tạo viên đạn thật qua BulletPool (tái sử dụng object) tại họng súng,
    /// rồi gán hướng bay. twinMuzzles = true thì bắn 2 nòng lệch nhau.
    /// </summary>
    private void SpawnBullet(Vector3 direction)
    {
        GameObject projectile = GetNextBulletVariant();
        if (projectile == null)
            return;
        int count = twinMuzzles ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = Vector3.down * 0.18f;
            if (twinMuzzles) offset += Vector3.right * ((i == 0 ? -1f : 1f) * muzzleSpacing);
            GameObject bulletObject = BulletPool.Instance.Spawn(projectile, transform.position + offset, Quaternion.identity);
            if (bulletObject != null && bulletObject.TryGetComponent(out EnemyBullet enemyBullet))
                enemyBullet.SetDirection(direction);
        }
    }

    /// <summary>
    /// Tìm và cache Transform của player (theo tag "Player") để quái ngắm bắn/lao tới.
    /// Đã có thì không tìm lại (tiết kiệm FindGameObjectWithTag).
    /// </summary>
    private void ResolvePlayerTarget()
    {
        if (playerTarget != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerTarget = playerObject.transform;
    }

    // =====================================================================
    // RỜI MÀN HÌNH
    // =====================================================================

    /// <summary>
    /// Kiểm tra quái đã bay ra khỏi mép màn (trái/phải/trên/dưới, có biên rộng tay).
    /// Nếu có thì đánh dấu đã rời, tính vào tiến trình màn rồi tự huỷ.
    /// Boss và trụ pháo đã neo thì bỏ qua.
    /// </summary>
    private void CheckScreenExit()
    {
        // Boss và trụ pháo neo lại thì không bao giờ "rời màn".
        if (formation == FormationType.Boss || hasExitedScreen)
            return;
        if (formation == FormationType.DropAnchor && hasAnchored)
            return;

        if (elapsed < 0.35f)
            return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 viewport = cam.WorldToViewportPoint(transform.position);
            bool outside = viewport.z > 0f &&
                           (viewport.y < -0.15f || viewport.y > 1.25f ||
                            viewport.x < -0.2f || viewport.x > 1.2f);

            if (outside)
            {
                hasExitedScreen = true;
                NotifyEscaped();
                Destroy(gameObject);
            }
            return;
        }

        if (transform.position.y < -8f || transform.position.y > 12f || Mathf.Abs(transform.position.x) > 9f)
        {
            hasExitedScreen = true;
            NotifyEscaped();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Xử lý khi quái rời màn (không bị giết): tắt collider, bắn event OnEnemyDied
    /// và báo GameManager đã "xử lý" xong con này (để tiến độ màn vẫn tăng).
    /// </summary>
    private void NotifyEscaped()
    {
        if (isDead)
            return;

        isDead = true;
        if (cachedCollider != null)
            cachedCollider.enabled = false;

        OnEnemyDied?.Invoke(this);

        if (GameManager.Instance != null)
            GameManager.Instance.AddEnemyDefeated(this);
    }

    // =====================================================================
    // KHỞI TẠO (Spawner gọi)
    // =====================================================================
    /// <summary>Khởi tạo đầy đủ một con quái theo 3 trục cấu hình.</summary>
    public void Initialize(
        SpawnOrigin origin,
        FormationType formationType,
        SpecialSpawnType special,
        Vector3 spawnPosition,
        Vector3 anchor,
        float speed,
        float moveAmplitude,
        float moveFrequency,
        int indexInRow,
        bool isLeader,
        Transform targetPlayer)
    {
        spawnOrigin = origin;
        formation = formationType;
        specialSpawn = special;

        descentSpeed = Mathf.Max(0.5f, speed);
        amplitude = Mathf.Max(0.3f, moveAmplitude);
        frequency = Mathf.Max(0.2f, moveFrequency);

        formationAnchor = anchor;
        startPosition = spawnPosition;
        snakeAnchor = anchor;

        snakeIndex = Mathf.Max(0, indexInRow);
        isVLeader = isLeader;
        playerTarget = targetPlayer;

        elapsed = 0f;
        hasAnchored = false;
        anchorHoldTime = 0f;
        isKamikazeLocked = false;
        vBroken = false;
        hasPreviousPosition = false;
        hasDivedPast = false;
        wallDescent = 0f;
        volleyPatternIndex = 0;
        projectileVariantCursor = 0;
        bossPhase = 1;
        staysInUpperLane = false;

        // Khôi phục nhịp bắn gốc của Prefab (Boss đổi Phase sẽ nhân lại từ đây).
        fireRate = initialFireRate;
        fireTimer = Random.Range(0f, Mathf.Max(0.1f, fireRate));

        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;

        if (formationType == FormationType.Boss)
        {
            if (bossController == null)
                bossController = gameObject.AddComponent<BossController>();
            bossController.BeginEntry(spawnPosition);
        }

        // Leader chữ V đi trước, hai cánh lùi lại.
        if (formationType == FormationType.VFormation && !isLeader)
            leaderOffset = new Vector3(0f, 0f, 0f);

        // Warp-in: ẩn hitbox + bóng mờ trong 1.5s đầu.
        if (origin == SpawnOrigin.WarpIn)
        {
            isPhasing = true;
            phasingTimer = 1.5f;
            if (cachedCollider != null)
                cachedCollider.enabled = false;
            if (spriteRenderer != null)
            {
                Color c = originalColor;
                c.a = 0.25f;
                spriteRenderer.color = c;
            }
        }

        // Trụ pháo: neo ở 1/3 phía trên màn hình.
        Camera cam = Camera.main;
        anchorY = cam != null
            ? cam.ViewportToWorldPoint(new Vector3(0.5f, 0.66f, Mathf.Abs(cam.transform.position.z))).y
            : 1.6f;

        // Carrier bắt đầu phóng tàu con sau 1s.
        nextMinionTime = 1f;
    }

    /// <summary>Overload tương thích cũ.</summary>
    /// <summary>
    /// Khôi phục lại nhịp bắn/trạng thái về tốc độ gốc của prefab (chỉ 1 tia).
    /// Dùng khi đổi màn mà không muốn giữ cấp đạn của màn trước.
    /// </summary>
    public void InitializeEntry(Vector3 targetLocalPos, float speed)
    {
        Initialize(SpawnOrigin.TopCascade, FormationType.Wall, SpecialSpawnType.None,
            targetLocalPos, targetLocalPos, speed, 1f, 1f, 0, false, null);
    }

    // =====================================================================
    // CẤU HÌNH PHỤ (Spawner gọi)
    // =====================================================================

    /// <summary>
    /// Đặt hướng bay ngang khi Pincer/SideFlank (âm = trái, còn lại = phải).
    /// </summary>
    public void SetFlankDirection(float direction)
    {
        flankDirection = direction < 0f ? -1f : 1f;
    }

    /// <summary>
    /// Đặt thời gian lượn vòng trước khi Kamikaze khoá mục tiêu (tối thiểu 0.4s).
    /// </summary>
    public void SetKamikazeOrbitTime(float seconds)
    {
        kamikazeOrbitTime = Mathf.Max(0.4f, seconds);
    }

    /// <summary>
    /// Cấu hình kiểu bắn: base pattern (ưu tiên archetype nếu prefab bật cờ),
    /// số tia quạt (1..9), góc quạt (5..140 độ) và số tia toả tròn (6..24).
    /// </summary>
    public void SetFirePattern(EnemyFirePattern pattern, int spread = 3, float angle = 35f, int radial = 8)
    {
        baseFirePattern = useArchetypeFirePattern ? archetypeFirePattern : pattern;
        firePattern = baseFirePattern;
        spreadCount = Mathf.Clamp(spread, 1, 9);
        spreadAngle = Mathf.Clamp(angle, 5f, 140f);
        radialCount = Mathf.Clamp(radial, 6, 24);
    }

    /// <summary>
    /// Bật/tắt chế độ quái chỉ bay và đánh ở "làn trên" màn hình (không bay xuống).
    /// Thường dùng cho quái tầm xa / quái khủng.
    /// </summary>
    public void SetStaysInUpperLane(bool stays)
    {
        staysInUpperLane = stays;
    }

    /// <summary>
    /// Quyết định quái có được bắn hay không cho wave này:
    /// - Quái "nguy hiểm cao" (isHighThreat) luôn bắn và luôn ngắm player.
    /// - Quái thường chỉ bắn theo xác suất regularEnemyFireChance (giảm tải đạn trên mobile).
    /// </summary>
    public void ConfigureFormationShooting(bool isHighThreat, float regularEnemyFireChance)
    {
        if (isHighThreat)
        {
            canShoot = true;
        }
        else
        {
            canShoot = canShoot && Random.value < Mathf.Clamp01(regularEnemyFireChance);
        }

        aimAtPlayer = isHighThreat || preferAimedShots;
    }

    /// <summary>
    /// Đặt giai đoạn Boss (1..3) — quyết định GetNextVolleyPattern dùng combo đạn nào.
    /// </summary>
    public void SetBossPhase(int phase)
    {
        bossPhase = Mathf.Clamp(phase, 1, 3);
    }

    /// <summary>
    /// Đặt tỉ lệ rơi item tăng đạn (0..1) cho quái này.
    /// </summary>
    public void SetDropChance(float chance)
    {
        bulletUpgradeDropChance = Mathf.Clamp01(chance);
    }

    /// <summary>
    /// Đổi tốc độ bắn theo hệ số: multiplier &gt; 1 = bắn nhanh hơn, &lt; 1 = chậm hơn.
    /// Luôn tính từ tốc độ gốc (initialFireRate) nên gọi nhiều lần không bị cộng dồn.
    /// </summary>
    public void SetFireRateMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.1f, multiplier);
        fireRate = Mathf.Max(0.1f, initialFireRate / safeMultiplier);
    }

    /// <summary>Gán prefab quái con cho Carrier / Splitting / Boss Summon.</summary>
    public void SetMinionPrefab(GameObject prefab, int count = 3)
    {
        minionPrefab = prefab;
        minionCount = Mathf.Clamp(count, 1, 8);
    }

    // =====================================================================
    // SÁT THƯƠNG / CHẾT
    // =====================================================================

    /// <summary>
    /// Bị đạn của player bắn trúng: trừ HP, trừ HP tổng của màn (GameManager),
    /// rồi chết nếu HP ≤ 0, ngược lại nháy đỏ để báo trúng đạn.
    /// Bỏ qua khi đã chết, damage ≤ 0 hoặc đang phasing (warp-in chưa có hitbox).
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0 || isPhasing)
            return;

        health -= damage;

        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyDamaged(damage);

        if (health <= 0)
        {
            Die();
        }
        else
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(DamageFlashRoutine());
        }
    }

    /// <summary>
    /// Hiệu ứng nháy đỏ khi trúng đạn: đổi màu sprite sang đỏ 0.08 giây rồi trả lại
    /// màu gốc (nếu quái chưa chết).
    /// </summary>
    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            if (!isDead && spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// Quái chết vì bị hạ: tắt collider, nếu là loại Splitting thì tách thành nhiều
    /// quái con, rơi power-up/đồng xu, phát âm nổ, cộng điểm/coin/tiến độ qua GameManager,
    /// rồi tự huỷ object.
    /// </summary>
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        if (cachedCollider != null)
            cachedCollider.enabled = false;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        // Splitting: vỡ thành nhiều quái nhỏ trước khi biến mất.
        if (specialSpawn == SpecialSpawnType.Splitting && minionPrefab != null)
        {
            for (int i = 0; i < minionCount; i++)
            {
                float angle = (360f / minionCount) * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * 0.4f;

                // PATTERN: Factory — quái con tách ra cũng tạo qua EnemyFactory.
                EnemyFactory.Create(minionPrefab, transform.position + offset);
            }
        }

        TryDropPowerUp();
        DropCoinVisual(coinValue);
        OnEnemyDied?.Invoke(this);

        // VFX: nổ theo kích thước quái; Boss nổ to hơn + rung camera mạnh.
        bool isBossHull = enemyName != null && enemyName.Contains("Boss");
        VfxSpawner.SpawnExplosion(
            transform.position,
            isBossHull ? 2.4f : 1f,
            isBossHull ? new Color(1f, 0.4f, 0.15f, 1f) : (Color?)null);
        CameraShake.Play(isBossHull ? 0.75f : 0.12f);
        Haptics.Vibrate(isBossHull ? 1f : 0.45f);

        if (AudioManager.Instance != null && AudioManager.Instance.enemyExplodeSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyExplodeSFX);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
            GameManager.Instance.AddEnemyDefeated(this);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Tạo một đồng xu rơi ra tại vị trí quái chết (nạp prefab Coin từ Resources).
    /// Coin chỉ được cộng khi Player thật sự nhặt (CoinDrop.OnTriggerEnter2D).
    /// </summary>
    private void DropCoinVisual(int value)
    {
        if (value <= 0)
            return;

        GameObject coinPrefab = Resources.Load<GameObject>("Prefabs/Coin");
        if (coinPrefab == null)
            return;

        GameObject coinObject = Instantiate(coinPrefab, transform.position, Quaternion.identity);
        CoinDrop coin = coinObject.GetComponent<CoinDrop>();
        if (coin != null)
            coin.SetValue(value);
    }

    /// <summary>
    /// Xác suất rơi item tăng đạn (gắn thêm power-up khi quái chết):
    /// - Boss: tối thiểu 60% (thưởng phải đáng giá).
    /// - Quái thường: xác suất cơ bản tăng theo độ trâu bệnh, nhưng chặn trần 15%.
    /// Nếu trúng xác suất thì tạo prefab power-up tại vị trí quái.
    /// </summary>
    private void TryDropPowerUp()
    {
        bool isBoss = enemyName.Contains("Boss");
        float chance = Mathf.Clamp01(bulletUpgradeDropChance);
        if (isBoss)
        {
            // Keep boss rewards meaningful without guaranteeing a drop from every stage boss.
            chance = Mathf.Max(chance, 0.6f);
        }
        else
        {
            if (enemyName.Contains("Elite") || maxHealth >= 15)
                chance *= 1.6f;
            else if (maxHealth >= 8)
                chance *= 1.35f;

            chance = Mathf.Min(chance, 0.15f);
        }

        if (Random.value >= chance)
            return;

        GameObject prefabToSpawn = bulletUpgradePrefab;
        if (prefabToSpawn == null)
            prefabToSpawn = Resources.Load<GameObject>("Prefabs/PowerUp_Bullet");

        if (prefabToSpawn != null)
            Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
    }
}
