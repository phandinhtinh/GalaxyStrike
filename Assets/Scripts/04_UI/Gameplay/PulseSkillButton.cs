using System.Collections;
using UnityEngine;

public sealed class PulseSkillButton : SkillButton
{
    [SerializeField, Min(0.1f)] private float radius = 2.75f;
    [SerializeField, Min(1)] private int damage = 4;

    private static readonly Color PulseColor = new Color(0.37f, 0.89f, 1f);

    protected override void ApplySkill(PlayerController player)
    {
        Vector2 center = player.transform.position;
        float radiusSquared = radius * radius;
        StartCoroutine(AnimatePulse(center));
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

    private IEnumerator AnimatePulse(Vector2 center)
    {
        const float duration = 0.38f;
        var wave = new GameObject("Pulse Wave");
        wave.transform.position = center;
        var renderer = wave.AddComponent<SpriteRenderer>();
        renderer.sprite = RingSprite;
        renderer.sortingOrder = 5;
        Destroy(wave, duration);

        // The ring texture has padding around its visible edge.
        float diameterScale = radius * 2f / (renderer.sprite.bounds.size.x * 0.9f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / duration;
            float expansion = 1f - Mathf.Pow(1f - progress, 3f);
            wave.transform.localScale = Vector3.one * (diameterScale * Mathf.Lerp(0.12f, 1f, expansion));
            renderer.color = new Color(PulseColor.r, PulseColor.g, PulseColor.b, 1f - progress);
            yield return null;
        }
    }
}
