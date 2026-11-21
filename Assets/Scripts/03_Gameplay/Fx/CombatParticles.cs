using UnityEngine;

/// <summary>Scene-owned particle pools for explosions, impacts and collectible feedback.</summary>
public sealed class CombatParticles : MonoBehaviour
{
    private static CombatParticles instance;
    private ParticleSystem embers, streaks, flashes, rings;
    private Material softMaterial, ringMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private static CombatParticles GetOrCreate()
    {
        if (instance != null) return instance;
        instance = new GameObject("[Combat Particles]").AddComponent<CombatParticles>();
        return instance;
    }

    private void Awake()
    {
        Material template = Resources.Load<Material>("Vfx/CombatAdditive");
        Shader shader = template != null ? template.shader : Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        softMaterial = template != null ? new Material(template) : new Material(shader);
        softMaterial.name = "Combat_Soft_Runtime";
        softMaterial.mainTexture = EnemyHitVfx.CreateTexture(false);
        ringMaterial = new Material(softMaterial) { name = "Combat_Ring_Runtime" };
        ringMaterial.mainTexture = EnemyHitVfx.CreateTexture(true);
        embers = CreateSystem("Embers", 512, false, false);
        streaks = CreateSystem("Directional Sparks", 384, false, true);
        flashes = CreateSystem("Core Flashes", 64, false, false);
        rings = CreateSystem("Shockwaves", 64, true, false);
    }

    public static void Explosion(Vector3 position, float scale, Color tint)
    {
        var fx = GetOrCreate();
        scale = Mathf.Clamp(scale, 0.4f, 2.5f);
        for (int i = 0; i < Mathf.RoundToInt(18f * scale); i++)
        {
            Vector3 direction = Direction(Random.Range(0f, 360f));
            fx.Emit(fx.embers, position, direction * Random.Range(1f, 3.5f) * scale,
                Vector3.one * Random.Range(0.12f, 0.3f) * scale,
                Random.Range(0.32f, 0.65f), Color.Lerp(tint, Color.white, Random.Range(0f, 0.25f)));
        }
        fx.RadialSparks(position, tint, Mathf.RoundToInt(12f * scale), scale, 3.6f);
        fx.Emit(fx.flashes, position, Vector3.zero, Vector3.one * 1.35f * scale,
            0.18f, Color.Lerp(tint, Color.white, 0.7f));
        fx.Emit(fx.rings, position, Vector3.zero, Vector3.one * 1.8f * scale,
            0.48f, new Color(tint.r, tint.g, tint.b, 0.65f));
    }

    public static void Impact(Vector3 position, Color tint)
    {
        var fx = GetOrCreate();
        fx.RadialSparks(position, tint, 7, 0.65f, 2.5f);
        fx.Emit(fx.flashes, position, Vector3.zero, Vector3.one * 0.55f,
            0.14f, Color.Lerp(tint, Color.white, 0.45f));
    }

    public static void Pickup(Vector3 position, Color tint, bool upgrade)
    {
        var fx = GetOrCreate();
        fx.RadialSparks(position, tint, upgrade ? 16 : 9, upgrade ? 1f : 0.65f, 2f);
        fx.Emit(fx.rings, position, Vector3.zero, Vector3.one * (upgrade ? 1.8f : 1f),
            0.45f, new Color(tint.r, tint.g, tint.b, 0.75f));
        fx.Emit(fx.flashes, position, Vector3.zero, Vector3.one * 0.75f,
            0.2f, Color.Lerp(tint, Color.white, 0.35f));
    }

    private void RadialSparks(Vector3 position, Color tint, int count, float scale, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count + Random.Range(-10f, 10f);
            Emit(streaks, position, Direction(angle) * Random.Range(0.6f, 1.2f) * speed * scale,
                new Vector3(0.05f, Random.Range(0.15f, 0.3f), 1f) * scale,
                Random.Range(0.22f, 0.45f), Color.Lerp(tint, Color.white, Random.Range(0.1f, 0.6f)), 90f - angle);
        }
    }

    private static Vector3 Direction(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
    }

    private void Emit(ParticleSystem system, Vector3 position, Vector3 velocity,
        Vector3 size, float lifetime, Color tint, float rotation = 0f)
    {
        system.Emit(new ParticleSystem.EmitParams
        {
            position = position, velocity = velocity, startSize3D = size,
            startLifetime = lifetime, startColor = tint, rotation = rotation
        }, 1);
    }

    private ParticleSystem CreateSystem(string systemName, int limit, bool ring, bool elongated)
    {
        var child = new GameObject(systemName);
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startSpeed = 0f;
        main.startSize3D = elongated;
        main.startColor = new Color(1f, 0.75f, 0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = limit;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.3f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, ring ? 0.2f : 1f, 1f, ring ? 1.5f : 0f));
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = ring ? 28 : 30;
        renderer.sharedMaterial = ring ? ringMaterial : softMaterial;
        system.Play();
        return system;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Release(softMaterial);
        Release(ringMaterial);
    }

    private static void Release(Material material)
    {
        if (material == null) return;
        Destroy(material.mainTexture);
        Destroy(material);
    }
}
