using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Di chuyển: warp-in, quỹ đạo theo đội hình, cơ chế đặc biệt (Carrier/Hive) và nghiêng thân. (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
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
}
