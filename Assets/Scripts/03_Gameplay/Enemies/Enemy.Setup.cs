using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>API cho EnemySpawner: Initialize và các hàm cấu hình phụ sau khi spawn. (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
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
            {
                bossController = gameObject.AddComponent<BossController>();
                bossControllerAddedAtRuntime = true;
            }
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
}
