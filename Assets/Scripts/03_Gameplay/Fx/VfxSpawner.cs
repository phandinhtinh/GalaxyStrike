using UnityEngine;

/// <summary>
/// Sinh hiệu ứng thị giác nhẹ (không cần asset): vụ nổ và tia lửa va chạm.
///
/// Chỉ dùng sprite/particle sinh tại chỗ bằng code nên chạy được ngay mà không
/// phải author prefab. Muốn dùng prefab đẹp hơn thì gán vào
/// <see cref="explosionPrefab"/> / <see cref="hitSparkPrefab"/> — class sẽ ưu tiên prefab.
///
/// Tự huỷ object sau khi phát xong để không phình bộ nhớ.
/// </summary>
public static class VfxSpawner
{
    private static ParticleSystem sharedExplosion;
    private static GameObject explosionPrefab;
    private static GameObject hitSparkPrefab;

    /// <summary>
    /// Cho phép gameplay gán prefab VFX (ví dụ từ inspector của Enemy/Tool).
    /// Để null thì dùng hiệu ứng sinh bằng code.
    /// </summary>
    public static void Configure(GameObject explosion, GameObject hitSpark)
    {
        explosionPrefab = explosion;
        hitSparkPrefab = hitSpark;
    }

    /// <summary>Vụ nổ khi quái chết / boss chết.</summary>
    public static void SpawnExplosion(Vector3 position, float scale = 1f, Color? tint = null)
    {
        if (explosionPrefab != null)
        {
            GameObject inst = Object.Instantiate(explosionPrefab, position, Quaternion.identity);
            inst.transform.localScale *= scale;
            DestroyAfter(inst, 2f);
            return;
        }

        ParticleSystem ps = GetSharedExplosion();
        if (ps == null) return;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            startColor = tint ?? new Color(1f, 0.65f, 0.2f, 1f)
        };

        ps.Emit(emit, Mathf.RoundToInt(28 * Mathf.Max(0.5f, scale)));
    }

    /// <summary>Tia lửa nhỏ khi đạn trúng quái (rất rẻ, nhiều lần/phút).</summary>
    public static void SpawnHitSpark(Vector3 position, Color? tint = null)
    {
        if (hitSparkPrefab != null)
        {
            GameObject inst = Object.Instantiate(hitSparkPrefab, position, Quaternion.identity);
            DestroyAfter(inst, 0.5f);
            return;
        }

        ParticleSystem ps = GetSharedExplosion();
        if (ps == null) return;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            startColor = tint ?? new Color(1f, 1f, 0.7f, 1f)
        };

        ps.Emit(emit, 6);
    }

    /// <summary>
    /// Một ParticleSystem dùng chung (sinh lười) đặt trong DontDestroyOnLoad để
    /// không bị huỷ khi chuyển Scene. Emit tại vị trí bất kỳ nên không cần nhiều instance.
    /// </summary>
    private static ParticleSystem GetSharedExplosion()
    {
        if (sharedExplosion != null)
            return sharedExplosion;

        var go = new GameObject("[VFX]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        var emission = ps.emission;
        emission.enabled = false; // chỉ Emit() thủ công

        var shape = ps.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetSoftParticleMaterial();

        sharedExplosion = ps;
        return ps;
    }

    /// <summary>
    /// Material particle "mềm" dựng từ shader mặc định của Unity (không cần asset).
    /// Dùng sprite-default particle để có texture tròn mờ dựng sẵn.
    /// </summary>
    private static Material GetSoftParticleMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        return new Material(shader) { name = "VFX_SoftAdditive" };
    }

    private static void DestroyAfter(GameObject go, float seconds)
    {
        if (go == null) return;
        Object.Destroy(go, seconds);
    }
}
