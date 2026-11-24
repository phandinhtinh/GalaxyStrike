using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Xả đạn: nhịp bắn, cảnh báo ngắm, các kiểu đạn (Single/Spread/Radial/Spiral/Burst). (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
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
}
