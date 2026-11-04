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
    [SerializeField] private float minMoveY = -4.0f;
    [SerializeField] private float maxMoveY = -1.2f;

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
    private SpriteRenderer spriteRenderer;
    private float fireTimer;
    private bool isDragging = false;
    private int activeTouchId = -1;
    private Vector3 dragOffsetWorld;

    // Cache cho raycast UI (trước đây new List mỗi khung hình -> sinh rác GC khi vuốt màn hình).
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(4);


    private void Awake()
    {
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        equipment = GetComponent<PlayerEquipment>();
    }

    private void Start()
    {
        // 1. Đọc và nạp dữ liệu Máy bay đã chọn từ HomeScene
        LoadSelectedShipData();

        // Mỗi màn bắt đầu lại với 1 tia đạn; PowerUp nhặt trong màn sẽ nâng tạm thời.
        bulletLevel = 1;
        PlayerPrefs.DeleteKey("GalaxyStrike_BulletLevel");

        // Cập nhật thanh máu HUD theo chỉ số tàu + nâng cấp vừa áp.
        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);
        }
    }

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

    private void Update()
    {
        // Nếu Game đang Pause (mở Settings), dừng toàn bộ cập nhật di chuyển và bắn
        if (Time.timeScale == 0f) return;

        HandleMovement();
        HandleShooting();
    }

    // Prevent a drag from starting on a UI control.
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
                transform.position = ClampToPlayArea(next);
            }
        }
    }

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

    private void MoveDraggedPointer(Vector2 screenPosition)
    {
        Vector3 target = ScreenPointerToShipPosition(screenPosition) + dragOffsetWorld;
        transform.position = ClampToPlayArea(target);
    }

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
        position.y = Mathf.Clamp(position.y, minMoveY, maxMoveY);
        position.z = 0f;
        return position;
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

    /// <summary>
    /// Cấp miễn nhiễm tức thời (dùng cho Energy Shield).
    /// Dùng bộ đếm thời gian thay vì coroutine để không tranh chấp với
    /// <see cref="InvulnerabilityRoutine"/> khi trúng đạn.
    /// </summary>
    public void GrantInvulnerability(float duration)
    {
        if (duration <= 0f)
            return;

        isInvulnerable = true;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    private float invulnerableUntil;

    public void TakeDamage(int damage = 1)
    {
        // Shield / bất tử tạm thời -> không nhận sát thương.
        if (isInvulnerable || Time.time < invulnerableUntil)
            return;

        currentHealth -= damage;

        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.UpdatePlayerHPBar(currentHealth, maxHealth);
        }

        if (currentHealth <= 0)
        {
            if (GameOverPanelUI.Instance != null)
            {
                GameOverPanelUI.Instance.ShowGameOver();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }

            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }

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
