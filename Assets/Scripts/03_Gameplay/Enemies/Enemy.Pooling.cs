using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tái sử dụng qua BulletPool: lưu giá trị gốc của prefab và reset trạng thái khi lấy lại. (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
    // =====================================================================
    // POOL (EnemyFactory lấy quái từ BulletPool thay vì Instantiate/Destroy)
    // =====================================================================

    // Giá trị gốc của prefab cho các field bị Spawner/Boss sửa trong lúc chơi.
    private bool defaultCanShoot;
    private float defaultDropChance;
    private GameObject defaultMinionPrefab;
    private int defaultMinionCount;
    private float defaultKamikazeOrbitTime;
    private Vector3 defaultScale;
    private bool bossControllerAddedAtRuntime;

    private void CaptureSpawnDefaults()
    {
        defaultCanShoot = canShoot;
        defaultDropChance = bulletUpgradeDropChance;
        defaultMinionPrefab = minionPrefab;
        defaultMinionCount = minionCount;
        defaultKamikazeOrbitTime = kamikazeOrbitTime;
        defaultScale = transform.localScale;
    }

    /// <summary>
    /// Lấy lại từ pool: đưa mọi trạng thái về như prefab vừa Instantiate.
    /// Các trạng thái bay/đội hình được <see cref="Initialize"/> đặt lại ngay sau đó.
    /// </summary>
    void IPoolable.OnSpawned()
    {
        health = maxHealth;
        isDead = false;
        hasExitedScreen = false;
        isPhasing = false;
        phasingTimer = 0f;
        nextHitEffectTime = 0f;
        flashRoutine = null;

        canShoot = defaultCanShoot;
        aimAtPlayer = false;
        bulletUpgradeDropChance = defaultDropChance;
        minionPrefab = defaultMinionPrefab;
        minionCount = defaultMinionCount;
        kamikazeOrbitTime = defaultKamikazeOrbitTime;

        shotCharging = false;
        chargedDirection = Vector3.zero;
        burstShotsRemaining = 0;
        burstShotIndex = 0;
        burstTimer = 0f;
        if (aimWarning != null)
            aimWarning.enabled = false;

        firePattern = EnemyFirePattern.Single;
        baseFirePattern = EnemyFirePattern.Single;
        spreadCount = 3;
        spreadAngle = 35f;
        radialCount = 8;
        spiralAngle = 0f;

        // Trạng thái bay về đúng giá trị khởi tạo của field: quái con tách ra (Splitting)
        // không qua Initialize nên phải giống hệt một object vừa Instantiate.
        spawnOrigin = SpawnOrigin.TopCascade;
        formation = FormationType.Wall;
        specialSpawn = SpecialSpawnType.None;
        startPosition = Vector3.zero;
        formationAnchor = Vector3.zero;
        snakeAnchor = Vector3.zero;
        snakeIndex = 0;
        descentSpeed = 2f;
        amplitude = 1.5f;
        frequency = 1.5f;
        isVLeader = false;
        vBroken = false;
        wallDescent = 0f;
        anchorY = 1.6f;
        hasAnchored = false;
        anchorHoldTime = 0f;
        isKamikazeLocked = false;
        hasDivedPast = false;
        volleyPatternIndex = 0;
        projectileVariantCursor = 0;
        bossPhase = 1;
        staysInUpperLane = false;
        hasPreviousPosition = false;
        fireRate = initialFireRate;

        flankDirection = 1f;
        leaderOffset = Vector3.zero;
        lockedTarget = Vector3.zero;
        vWingSide = 0f;
        playerTarget = null;
        elapsed = 0f;
        nextMinionTime = 1f;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        fireTimer = Random.Range(0f, Mathf.Max(0.1f, fireRate));

        transform.rotation = Quaternion.identity;
        transform.localScale = defaultScale;
        if (cachedCollider != null)
            cachedCollider.enabled = true;
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }

    void IPoolable.OnDespawned()
    {
        // Tên lửa đuổi / thanh máu Boss coi IsDead là "mất mục tiêu". Trước đây object bị
        // Destroy nên tham chiếu thành null; giờ object chỉ tắt nên phải đánh dấu chết.
        isDead = true;
        StopAllCoroutines();
        flashRoutine = null;

        // BossController gắn thêm lúc chạy (prefab không có) phải gỡ, nếu không lần sau
        // prefab này ra sân như quái thường vẫn bay kiểu Boss và bị tính là Boss.
        if (bossControllerAddedAtRuntime && bossController != null)
        {
            Destroy(bossController);
            bossController = null;
            bossControllerAddedAtRuntime = false;
        }
    }

    /// <summary>Gỡ quái khỏi màn: trả về pool nếu đến từ pool, ngược lại Destroy.</summary>
    public void Despawn()
    {
        BulletPool.Release(gameObject);
    }
}
