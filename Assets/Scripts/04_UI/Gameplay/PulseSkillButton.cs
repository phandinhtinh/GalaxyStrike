using UnityEngine;

public sealed class PulseSkillButton : SkillButton
{
    [SerializeField, Min(0.1f)] private float radius = 2.75f;
    [SerializeField, Min(1)] private int damage = 4;

    protected override void ApplySkill(PlayerController player)
    {
        Vector2 center = player.transform.position;
        float radiusSquared = radius * radius;
        PlayerSkillVfx.PlayPulse(player, radius);
        BattleFeedbackHUD.Instance?.ShowNotice("PULSE DISCHARGED", "NEARBY THREATS SUPPRESSED", new Color(0.18f, 0.88f, 1f), 1.4f);
        CameraShake.Play(0.28f);

        foreach (EnemyBullet bullet in FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None))
        {
            if (((Vector2)bullet.transform.position - center).sqrMagnitude > radiusSquared)
                continue;

            if (BulletPool.HasInstanceAvailable)
                BulletPool.Instance.Despawn(bullet.gameObject);
            else
                Destroy(bullet.gameObject);
        }

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (((Vector2)enemy.transform.position - center).sqrMagnitude <= radiusSquared)
                enemy.TakeDamage(Mathf.Min(damage, enemy.Health));
        }
    }
}
