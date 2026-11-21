using UnityEngine;

/// <summary>Gameplay VFX entry points. Optional authored prefabs retain priority.</summary>
public static class VfxSpawner
{
    private static GameObject explosionPrefab;
    private static GameObject hitSparkPrefab;

    public static void Configure(GameObject explosion, GameObject hitSpark)
    {
        explosionPrefab = explosion;
        hitSparkPrefab = hitSpark;
    }

    public static void SpawnExplosion(Vector3 position, float scale = 1f, Color? tint = null)
    {
        if (explosionPrefab != null)
        {
            GameObject effect = Object.Instantiate(explosionPrefab, position, Quaternion.identity);
            effect.transform.localScale *= scale;
            Object.Destroy(effect, 2f);
            return;
        }
        CombatParticles.Explosion(position, scale, tint ?? new Color(1f, 0.65f, 0.2f));
    }

    public static void SpawnHitSpark(Vector3 position, Color? tint = null)
    {
        if (hitSparkPrefab != null)
        {
            GameObject effect = Object.Instantiate(hitSparkPrefab, position, Quaternion.identity);
            Object.Destroy(effect, 0.5f);
            return;
        }
        CombatParticles.Impact(position, tint ?? new Color(1f, 0.9f, 0.5f));
    }

    public static void SpawnPickup(Vector3 position, bool upgrade = false)
    {
        CombatParticles.Pickup(position,
            upgrade ? new Color(0.2f, 1f, 0.8f) : new Color(1f, 0.8f, 0.25f), upgrade);
    }
}
