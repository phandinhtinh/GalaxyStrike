using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // 1. Bổ sung thư viện quản lý UI Event Systems
using GalaxyStrike.Data;        // 2. Bổ sung namespace quản lý dữ liệu ScriptableObject
using GalaxyStrike.Save;        // 3. Dữ liệu người chơi tập trung (Giai đoạn 2)

public class PlayerController : MonoBehaviour
{
    [Header("--- Ship Data Config ---")]
    [Tooltip("Danh sách 8 file ShipDataSO tương ứng với 8 máy bay")]
    [SerializeField] private ShipDataSO[] allShipsData;

    [Header("Health & Defense")]
    [Tooltip("Số lượng máu tối đa của người chơi (mặc định 3 máu)")]
    [SerializeField] private int maxHealth = 3;
    [Tooltip("Thời gian bất tử nhấp nháy sau khi va chạm (giây)")]
    [SerializeField] private float invulnerabilityDuration = 1.5f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Movement Bounds")]
    [SerializeField] private float minMoveX = -2.0f;
    [SerializeField] private float maxMoveX =  2.0f;
    [SerializeField, Range(0f, 1f)] private float maxViewportY = 0.75f;
    [SerializeField, Range(0f, 0.1f)] private float viewportPadding = 0.035f;

    [Header("Shooting & Upgrades")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.5f;
    [Tooltip("Số lượng tia đạn bắn cùng lúc (1, 2, 3, 4, 5...)")]
    [SerializeField] private int bulletLevel = 1;
    [Tooltip("Số lượng tia đạn tối đa có thể nâng cấp")]
    [SerializeField] private int maxBulletLevel = 5;
    [Tooltip("Khoảng cách dàn ngang giữa các tia đạn")]
    [SerializeField] private float bulletSpacing = 0.25f;

    [Header("Touch Control")]
    [Tooltip("Đẩy tàu lên trên ngón tay bao nhiêu đơn vị world (ngón tay không che tàu)")]
    [SerializeField] private float touchOffsetWorld = 0.9f;

    public static PlayerController Instance { get; private set; }

    private int currentHealth;
    private bool isInvulnerable = false;
    private bool isDowned = false;
    private SpriteRenderer spriteRenderer;
    private float fireTimer;
    private bool isDragging = false;
    private int activeTouchId = -1;
    private Vector3 dragOffsetWorld;

    // Cache cho raycast UI (trước đây new List mỗi khung hình -> sinh rác GC khi vuốt màn hình).
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(4);


    private void Awake()
    {
        // Singleton để các script khác (UI, pickup...) truy cập player.
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        equipment = GetComponent<PlayerEquipment>();

        EnsureEngineTrail();
    }

    /// <summary>
    /// Tự gắn vệt lửa động cơ phía sau tàu (không cần author tay trong Scene).
    /// Chỉ tạo 1 lần; nếu đã có EngineTrail trên object/child thì thôi.
    /// </summary>
    private void EnsureEngineTrail()
    {
        if (GetComponentInChildren<EngineTrail>(true) != null)
            return;

        var trailGo = new GameObject("EngineTrail");
        trailGo.transform.SetParent(transform, false);
        // Gắn dưới đuôi tàu (tàu nhìn lên trên).
        trailGo.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        trailGo.AddComponent<ParticleSystem>();
        trailGo.AddComponent<EngineTrail>();
    }

    /// <summary>
    /// Xoá tham chiếu tĩnh khi tàu bị huỷ, để code khác gọi <see cref="Instance"/>
    /// nhận null thay vì một object đã chết (tránh MissingReferenceException).
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Đầu màn: nạp dữ liệu tàu đang chọn (sprite + chỉ số + nâng cấp vĩnh viễn),
    /// reset số tia đạn về 1 cho mỗi màn, và vẽ thanh máu trên HUD.
    /// </summary>
    private void Start()
    {
        // 1. Đọc và nạp dữ liệu Máy bay đã chọn từ HomeScene
        LoadSelectedShipData();
        GetComponentInChildren<EngineTrail>(true)?.AlignToShip(spriteRenderer);

        // Mỗi màn bắt đầu lại với 1 tia đạn; PowerUp nhặt trong màn sẽ nâng tạm thời.
        bulletLevel = 1;
        PlayerPrefs.DeleteKey("GalaxyStrike_BulletLevel");

        // Cập nhật thanh máu HUD theo chỉ số tàu + nâng cấp vừa áp.
        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// Áp dữ liệu của tàu đang chọn (từ SaveManager) lên player:
    /// đổi sprite, lấy chỉ số gốc của tàu, rồi cộng nâng cấp vĩnh viễn
    /// (HP / tốc độ bắn / tốc độ di chuyển / sát thương) qua ShipUpgradeCatalog.
    /// </summary>
    private void LoadSelectedShipData()
    {
        // Chi so goc cua tau (dung de tinh diem nang cap).
        int baseMaxHealth = maxHealth;
        float baseFireRate = fireRate;
        float baseMoveSpeed = moveSpeed;

        // Doc tau dang chon tu SaveManager (da hop nhat du lieu cu).
        int selectedShipIndex = SaveManager.Instance.SelectedShipIndex;

        if (allShipsData != null && selectedShipIndex >= 0 && selectedShipIndex < allShipsData.Length && allShipsData[selectedShipIndex] != null)
        {
            ShipDataSO data = allShipsData[selectedShipIndex];

            if (spriteRenderer != null && data.shipSprite != null)
            {
                spriteRenderer.sprite = data.shipSprite;
            }

            GetComponentInChildren<EngineTrail>(true)?.ConfigureForShip(data, spriteRenderer);

            baseMaxHealth = data.maxHealth;
            baseFireRate = data.fireRate;
            baseMoveSpeed = data.moveSpeed;

            if (data.bulletPrefab != null)
            {
                bulletPrefab = data.bulletPrefab;
            }
        }

        // Ap chi so nang cap vinh vien (Giai doan 2) len chi so goc cua tau.
        SaveData save = SaveManager.Instance.Data;
        maxHealth = ShipUpgradeCatalog.ApplyHealth(baseMaxHealth, save.maxHealthUpgrade);
        currentHealth = maxHealth;
        fireRate = ShipUpgradeCatalog.ApplyFireRate(baseFireRate, save.fireRateUpgrade);
        moveSpeed = ShipUpgradeCatalog.ApplyMoveSpeed(baseMoveSpeed, save.moveSpeedUpgrade);
        playerDamage = ShipUpgradeCatalog.ApplyDamage(1, save.damageUpgrade);
    }

    // Sat thuong goc cua mot vien dan (truoc khi ap nang cap).
    private int playerDamage = 1;

    /// <summary>Sat thuong hien tai cua 1 vien dan (da gom nang cap Damage).</summary>
    public int PlayerDamage => Mathf.Max(1, playerDamage);

    /// <summary>
    /// Vòng lặp chính mỗi frame: bỏ qua hoàn toàn khi game đang pause (timeScale = 0),
    /// ngược lại xử lý di chuyển rồi bắn tự động.
    /// </summary>
    private void Update()
    {
        // Nếu Game đang Pause (mở Settings), dừng toàn bộ cập nhật di chuyển và bắn
        if (Time.timeScale == 0f) return;

        HandleMovement();
        transform.position = ClampToPlayArea(transform.position);
        HandleShooting();
    }

    // Prevent a drag from starting on a UI control.

    /// <summary>
    /// Kiểm tra điểm chạm có đang nằm lên một control UI tương tác (Button/Toggle/Slider...)
    /// hay không. True thì bỏ qua việc kéo tàu, nên bấm nút UI không làm tàu giật.
    /// </summary>
    private bool IsPointerOverInteractiveUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            if (uiRaycastResults[i].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Xử lý di chuyển theo ưu tiên: có cảm ứng thì dùng cảm ứng; không có thì dùng
    /// chuột (editor) để kéo tàu; cuối cùng hỗ trợ bàn phím (WASD/mũi tên) để test.
    /// </summary>
    private void HandleMovement()
    {
        if (Input.touchCount > 0)
        {
            HandleTouchMovement();
            return;
        }

        // A paused game may skip the touch-end event; release the drag once no finger remains.
        if (activeTouchId >= 0)
            EndDrag();

        if (Input.GetMouseButtonDown(0))
            TryBeginDrag(Input.mousePosition, -1);

        if (Input.GetMouseButton(0) && isDragging && activeTouchId == -1)
        {
            MoveDraggedPointer(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0) && activeTouchId == -1)
            EndDrag();

        // Keyboard movement remains available for editor testing.
        if (!isDragging)
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            if (moveX != 0f || moveY != 0f)
            {
                Vector3 next = transform.position + new Vector3(moveX, moveY, 0f) * (moveSpeed * Time.deltaTime);
                transform.position = next;
            }
        }
    }

    /// <summary>
    /// Theo dõi ngón tay: ngón đầu tiên chạm (không chạm UI) thì bắt đầu kéo tàu,
    /// các frame sau di chuyển tàu theo ngón đó, nhấc tay thì kết thúc kéo.
    /// </summary>
    private void HandleTouchMovement()
    {
        bool activeTouchFound = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (!isDragging && touch.phase == TouchPhase.Began)
            {
                if (TryBeginDrag(touch.position, touch.fingerId))
                    return;
            }

            if (!isDragging || touch.fingerId != activeTouchId)
                continue;

            activeTouchFound = true;
            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                MoveDraggedPointer(touch.position);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndDrag();

            return;
        }

        if (isDragging && activeTouchId >= 0 && !activeTouchFound)
            EndDrag();
    }

    /// <summary>
    /// Bắt đầu kéo tàu: bỏ qua nếu chạm phải UI; nếu hợp lệ thì nhớ ngón đang dùng
    /// và lưu độ lệch giữa tàu và ngón để kéo tàu theo tương đối (không nhảy về ngón).
    /// </summary>
    private bool TryBeginDrag(Vector2 screenPosition, int touchId)
    {
        if (IsPointerOverInteractiveUI(screenPosition))
            return false;

        activeTouchId = touchId;
        // Preserve the ship's current position when the drag starts anywhere on screen.
        dragOffsetWorld = transform.position - ScreenPointerToShipPosition(screenPosition);
        isDragging = true;
        return true;
    }

    /// <summary>
    /// Di chuyển tàu theo vị trí ngón tay hiện tại (cộng độ lệch lúc bắt đầu kéo),
    /// sau đó kẹp trong vùng chơi.
    /// </summary>
    private void MoveDraggedPointer(Vector2 screenPosition)
    {
        transform.position = ScreenPointerToShipPosition(screenPosition) + dragOffsetWorld;
    }

    /// <summary>
    /// Kết thúc kéo: tắt trạng thái kéo và nhả ngón đang điều khiển.
    /// </summary>
    private void EndDrag()
    {
        isDragging = false;
        activeTouchId = -1;
    }

    /// <summary>
    /// Äá»•i toáº¡ Ä‘á»™ ngÃ³n tay sang vá»‹ trÃ­ tÃ u, cá»™ng thÃªm Ä‘á»™ lá»‡ch dá»c Ä‘á»ƒ ngÃ³n tay khÃ´ng che tÃ u.
    /// </summary>
    private Vector3 ScreenPointerToShipPosition(Vector3 screenPosition)
    {
        Vector3 world = ScreenPositionToWorldPosition(new Vector2(screenPosition.x, screenPosition.y));
        world.y += touchOffsetWorld;
        return world;
    }

    /// <summary>
    /// Đổi toạ độ màn hình (pixel) thành toạ độ world trên mặt phẳng z = 0,
    /// dùng cho việc kéo thả tàu.
    /// </summary>
    private Vector3 ScreenPositionToWorldPosition(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        float cameraDistance = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, cameraDistance));
        world.z = 0f;
        return world;
    }

    /// <summary>Giá»¯ tÃ u trong vÃ¹ng chÆ¡i há»£p lá»‡.</summary>
    private Vector3 ClampToPlayArea(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minMoveX, maxMoveX);
        position.z = 0f;

        Camera cam = Camera.main;
        if (cam == null)
            return position;

        Rect safeArea = UnityEngine.Device.Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            safeArea = cam.pixelRect;

        Vector3 safeMin = cam.ScreenToViewportPoint(safeArea.min);
        Vector3 safeMax = cam.ScreenToViewportPoint(safeArea.max);
        Vector3 viewport = cam.WorldToViewportPoint(position);
        Vector3 shipPosition = transform.position;
        Bounds hull = spriteRenderer != null
            ? spriteRenderer.bounds
            : new Bounds(shipPosition, Vector3.zero);
        Vector3 hullCenter = cam.WorldToViewportPoint(hull.center);
        Vector3 centerOffset = hullCenter - cam.WorldToViewportPoint(shipPosition);
        Vector3 hullWidth = cam.WorldToViewportPoint(hull.center + Vector3.right * hull.extents.x) - hullCenter;
        Vector3 hullHeight = cam.WorldToViewportPoint(hull.center + Vector3.up * hull.extents.y) - hullCenter;
        float halfWidth = Mathf.Abs(hullWidth.x) + Mathf.Abs(hullHeight.x);
        float halfHeight = Mathf.Abs(hullWidth.y) + Mathf.Abs(hullHeight.y);

        float minX = Mathf.Clamp01(safeMin.x) + viewportPadding + halfWidth - centerOffset.x;
        float maxX = Mathf.Clamp01(safeMax.x) - viewportPadding - halfWidth - centerOffset.x;
        float minY = Mathf.Clamp01(safeMin.y) + viewportPadding + halfHeight - centerOffset.y;
        float maxY = Mathf.Min(maxViewportY, Mathf.Clamp01(safeMax.y) - viewportPadding - halfHeight - centerOffset.y);
        float x = ClampViewportAxis(viewport.x, minX, maxX);
        float y = ClampViewportAxis(viewport.y, minY, maxY);

        if (x == viewport.x && y == viewport.y)
            return position;

        Vector3 clamped = cam.ViewportToWorldPoint(new Vector3(x, y, viewport.z));
        clamped.z = 0f;
        return clamped;
    }

    private static float ClampViewportAxis(float value, float min, float max)
    {
        return min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
    }

    // =====================================================================
    // PATTERN: Command — API di chuyển cho các command gọi vào.
    // =====================================================================

    /// <summary>Đặt tàu tới vị trí world (đã kẹp trong vùng chơi). Dùng cho touch/drag.</summary>
    public void MoveTo(Vector3 worldPosition)
    {
        transform.position = ClampToPlayArea(worldPosition);
    }

    /// <summary>Dịch chuyển theo trục vào (bàn phím). Axis đã chuẩn hoá nếu chéo.</summary>
    public void MoveBy(Vector2 axis, float deltaTime)
    {
        if (axis == Vector2.zero || deltaTime <= 0f)
            return;

        Vector3 next = transform.position + new Vector3(axis.x, axis.y, 0f) * (moveSpeed * deltaTime);
        transform.position = ClampToPlayArea(next);
    }

    /// <summary>Chuyển toạ độ ngón tay trên màn hình sang vị trí tàu (có offset che tay).</summary>
    public Vector3 ScreenToShipPosition(Vector3 screenPosition)
    {
        return ScreenPointerToShipPosition(screenPosition);
    }

    /// <summary>
    /// Bắn tự động: đếm thời gian, cứ đủ fireRate giây thì gọi FireBullets một lần
    /// (không cần người chơi bấm nút bắn).
    /// </summary>
    private void HandleShooting() 
    {
        // Đạn tự động phun liên tục theo thời gian
        fireTimer += Time.deltaTime;
        if (fireTimer >= fireRate)
        {
            fireTimer = 0f;
            if (bulletPrefab && firePoint)
            {
                FireBullets();
            }
        }
    }

    /// <summary>
    /// Bắn một loạt theo cấp đạn (bulletLevel):
    /// - Cấp 1: 1 tia thẳng.
    /// - Cấp 2+: nhiều tia dàn đều ngang theo bulletSpacing.
    /// - Cấp 3+: thêm góc toả hình quạt để đạn xoè hình quạt.
    /// Sau đó phát âm thanh bắn.
    /// </summary>
    private void FireBullets()
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        Vector3 origin = firePoint.position;
        Quaternion baseRotation = firePoint.rotation;

        if (bulletLevel <= 1)
        {
            // Cấp 1: Bắn 1 tia thẳng chính giữa
            SpawnBullet(origin, baseRotation);
        }
        else
        {
            // Cấp 2 trở lên: Dàn đều các tia đạn theo chiều ngang và góc quạt
            float totalWidth = (bulletLevel - 1) * bulletSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < bulletLevel; i++)
            {
                float xOffset = startX + (i * bulletSpacing);
                Vector3 spawnPos = origin + new Vector3(xOffset, 0, 0);

                // Thêm góc tỏa nhẹ hình cánh quạt (fan-spread) khi có từ 3 tia trở lên
                float spreadAngle = 0f;
                if (bulletLevel >= 3)
                {
                    float factor = ((float)i / (bulletLevel - 1)) * 2f - 1f; // từ -1 đến +1
                    spreadAngle = -factor * (bulletLevel * 2.5f); // Góc xòe từ -10 đến +10 độ
                }

                SpawnBullet(spawnPos, baseRotation * Quaternion.Euler(0, 0, spreadAngle));
            }
        }

        // Phát tiếng đạn bắn qua AudioManager
        if (AudioManager.Instance != null && AudioManager.Instance.playerShootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.playerShootSFX);
        }
    }

    /// <summary>
    /// Lấy 1 viên đạn từ BulletPool tại vị trí/hướng cho trước, gán sát thương
    /// (nâng cấp vĩnh viễn × trang bị) và bật xuyên thấu nếu đang dùng Laser.
    /// </summary>
    private void SpawnBullet(Vector3 position, Quaternion rotation)
    {
        // PATTERN: Object Pool — tái sử dụng đạn thay vì Instantiate/Destroy mỗi phát.
        BulletPool pool = BulletPool.Instance;
        GameObject bulletObject = pool.Spawn(bulletPrefab, position, rotation);

        if (bulletObject == null)
            return;

        // Sát thương = nâng cấp vĩnh viễn (Giai đoạn 2) × hệ số trang bị Laser (Giai đoạn 2).
        int finalDamage = equipment != null
            ? equipment.ResolveDamage(PlayerDamage)
            : PlayerDamage;

        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetDamage(finalDamage);

            // Laser Gun xuyên thấu qua nhiều mục tiêu.
            if (equipment != null && equipment.HasLaser)
                bullet.EnablePiercing(laserPierceTargets);
        }
    }

    [Tooltip("Số quái tối đa mà đạn Laser xuyên qua")]
    [SerializeField] private int laserPierceTargets = 2;

    private PlayerEquipment equipment;

    // Hàm gọi khi nhặt được vật phẩm Power-Up tăng số lượng đạn
    public void UpgradeBulletCount(int amount = 1)
    {
        // Cấp đạn chỉ tồn tại trong phạm vi màn hiện tại (không ghi PlayerPrefs).
        bulletLevel = Mathf.Clamp(bulletLevel + amount, 1, maxBulletLevel);
    }

    public int CurrentBulletLevel => bulletLevel;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int MaxBulletLevel => maxBulletLevel;

    /// <summary>
    /// Đặt thẳng cấp hỏa lực (dùng cho Booster Overdrive: nhảy tới 5 tia).
    /// Cấp được kẹp trong 1..<see cref="maxBulletLevel"/>.
    /// </summary>
    public void SetBulletLevel(int level)
    {
        bulletLevel = Mathf.Clamp(level, 1, maxBulletLevel);
    }

    /// <summary>Đặt thẳng máu hiện tại (kẹp 0..maxHealth) — dùng khi nạp lại trạng thái.</summary>
    public void SetHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        if (HUD_Panel.Instance != null)
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);
    }

    /// <summary>
    /// Cấp miễn nhiễm tức thời (dùng cho Energy Shield).
    /// Dùng bộ đếm thời gian thay vì coroutine để không tranh chấp với
    /// <see cref="InvulnerabilityRoutine"/> khi trúng đạn.
    /// </summary>
    public void GrantInvulnerability(float duration)
    {
        if (duration <= 0f)
            return;

        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    private float invulnerableUntil;

    /// <summary>
    /// Nhận sát thương: bỏ qua nếu đang bất tử; trừ máu, cập nhật thanh HP trên HUD.
    /// Nếu máu về 0 thì hiện Game Over, báo GameManager và huỷ tàu;
    /// ngược lại bắt đầu 1.5 giây bất tử (nhấp nháy) để tránh mất máu liên tục.
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        // Shield / bất tử tạm thời -> không nhận sát thương.
        if (isInvulnerable || Time.time < invulnerableUntil)
            return;

        currentHealth -= damage;

        // "Juice": rung camera nhẹ + haptic + tia lửa tại vị trí tàu.
        CameraShake.Play(0.32f);
        Haptics.Light();
        VfxSpawner.SpawnHitSpark(transform.position, new Color(1f, 0.45f, 0.35f, 1f));

        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);
        }

        if (currentHealth <= 0)
        {
            // Fatal: rung mạnh hơn để người chơi cảm nhận rõ khoảnh khắc gục.
            CameraShake.Play(0.45f);
            Haptics.Heavy();

            // KHÔNG Destroy ngay: giữ tàu để có thể hồi sinh tại chỗ (Revive).
            EnterDownedState();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }

    /// <summary>
    /// Chuyển tàu sang trạng thái "gục" (HP = 0): tắt collider + renderer để
    /// không trúng đạn thêm, rồi nhờ GameManager quyết định hồi sinh hay Game Over.
    /// </summary>
    private void EnterDownedState()
    {
        // Chống vào lại: nhiều nguồn sát thương cùng frame không được đẩy 2 lần
        // qua luồng Game Over/Revive.
        if (isDowned)
            return;

        isDowned = true;
        GetComponentInChildren<EngineTrail>(true)?.SetThrust(false);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (spriteRenderer != null) spriteRenderer.enabled = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDefeated();
        }
    }

    /// <summary>
    /// Hồi sinh tàu tại chỗ: đầy máu, bật lại collider/renderer và bất tử tạm thời.
    /// Trả về false nếu tàu không ở trạng thái gục (tránh hồi sinh 2 lần).
    /// </summary>
    public bool Revive(float shieldSeconds)
    {
        if (!isDowned)
            return false;

        isDowned = false;
        GetComponentInChildren<EngineTrail>(true)?.SetThrust(true);
        currentHealth = maxHealth;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        if (spriteRenderer != null) spriteRenderer.enabled = true;

        if (HUD_Panel.Instance != null)
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);

        GrantInvulnerability(Mathf.Max(0.1f, shieldSeconds));
        PlayerSkillVfx.PlayAegis(this, Mathf.Max(0.1f, shieldSeconds));
        VfxSpawner.SpawnPickup(transform.position, true);
        BattleFeedbackHUD.Instance?.ShowNotice("HULL RESTORED", "REVIVE SHIELD ACTIVE", new Color(0.2f, 1f, 0.8f), 2f, 3);
        return true;
    }

    /// <summary>Tàu đã cạn máu và đang chờ hồi sinh / Game Over.</summary>
    public bool IsDowned => isDowned;

    /// <summary>
    /// Coroutine nhấp nháy sprite (alpha 0.25) trong thời gian invulnerabilityDuration,
    /// biểu thị tàu đang bất tử tạm thời; kết thúc thì bật lại hitbox.
    /// </summary>
    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;

        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            float blinkInterval = 0.1f;
            Color originalColor = spriteRenderer.color;

            while (elapsed < invulnerabilityDuration)
            {
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.25f);
                yield return new WaitForSeconds(blinkInterval);
                spriteRenderer.color = originalColor;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval * 2f;
            }

            spriteRenderer.color = originalColor;
        }
        else
        {
            yield return new WaitForSeconds(invulnerabilityDuration);
        }

        isInvulnerable = false;
    }

    /// <summary>
    /// Unity gọi khi va chạm trigger với quái hoặc đạn quái:
    /// - Đang Shield/bất tử: chỉ xoá viên đạn, không mất máu.
    /// - Bình thường: mất 1 máu và xoá viên đạn (đạn quái thì trả về pool).
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy") || collision.CompareTag("EnemyBullet"))
        {
            // Shield đang bật: chặn sát thương nhưng vẫn xoá viên đạn.
            if (isInvulnerable || Time.time < invulnerableUntil)
            {
                if (collision.CompareTag("EnemyBullet"))
                    RecycleBullet(collision.gameObject);
                return;
            }

            TakeDamage(1);

            if (collision.CompareTag("EnemyBullet"))
            {
                RecycleBullet(collision.gameObject);
            }
        }
    }

    /// <summary>Đạn địch đang là object của pool: trả về pool thay vì Destroy.</summary>
    private void RecycleBullet(GameObject bullet)
    {
        if (bullet == null)
            return;

        if (BulletPool.HasInstanceAvailable)
            BulletPool.Instance.Despawn(bullet);
        else
            Destroy(bullet);
    }
}
