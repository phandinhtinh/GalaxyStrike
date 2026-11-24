using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Nhận sát thương, chết, rơi coin và power-up. (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
    // =====================================================================
    // SÁT THƯƠNG / CHẾT
    // =====================================================================

    /// <summary>
    /// Bị đạn của player bắn trúng: trừ HP, trừ HP tổng của màn (GameManager),
    /// rồi chết nếu HP ≤ 0, ngược lại chớp màu theo hiệu ứng riêng để báo trúng đạn.
    /// Bỏ qua khi đã chết, damage ≤ 0 hoặc đang phasing (warp-in chưa có hitbox).
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(int damage, Vector3 impactPosition)
    {
        if (isDead || damage <= 0 || isPhasing)
            return;

        health -= damage;

        if (Time.time >= nextHitEffectTime)
        {
            nextHitEffectTime = Time.time + 0.045f;
            Vector2 outward = impactPosition - transform.position;
            EnemyHitVfx.Emit(impactPosition, outward, hitColor, hitSparkCount, hitScale, hitSpread, hitPulse);
            if (health > 0)
                AudioManager.Play(SfxCue.Hit);
        }

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
    /// Hiệu ứng trúng đạn: chớp màu riêng của Enemy trong 0.08 giây rồi trả lại
    /// màu gốc (nếu quái chưa chết).
    /// </summary>
    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(originalColor, hitColor, 0.65f);
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
                Enemy child = EnemyFactory.Create(minionPrefab, transform.position + offset);
                if (child != null) SplitChildSpawned?.Invoke(this, child);
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

        Despawn();
    }

    /// <summary>
    /// Tạo một đồng xu rơi ra tại vị trí quái chết (nạp prefab Coin từ Resources).
    /// Coin chỉ được cộng khi Player thật sự nhặt (CoinDrop.OnTriggerEnter2D).
    /// </summary>
    private void DropCoinVisual(int value)
    {
        if (value <= 0)
            return;

        CoinDrop.Spawn(transform.position, value);
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
