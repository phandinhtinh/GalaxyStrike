using UnityEngine;

/// <summary>Shared, scene-owned impact systems. Each enemy supplies its own visual settings.</summary>
public sealed class EnemyHitVfx : MonoBehaviour
{
    private static EnemyHitVfx instance;
    private ParticleSystem sparks;
    private ParticleSystem flash;
    private ParticleSystem rings;

    public static void Emit(Vector3 position, Vector2 direction, Color color, int count,
        float scale, float spread, bool ring)
    {
        if (instance == null)
        {
            var root = new GameObject("[Enemy Hit VFX]");
            instance = root.AddComponent<EnemyHitVfx>();
            instance.sparks = instance.CreateSystem("Sparks", false, false, 384);
            instance.flash = instance.CreateSystem("Flash", false, true, 96);
            instance.rings = instance.CreateSystem("Pulse", true, true, 64);
        }

        float heading = direction.sqrMagnitude > 0.001f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : -90f;
        for (int i = 0; i < count; i++)
        {
            float angle = heading + Random.Range(-spread, spread);
            Vector3 velocity = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity * (Random.Range(1.5f, 4.5f) * scale),
                rotation = 90f - angle,
                startSize3D = new Vector3(0.06f, Random.Range(0.18f, 0.32f), 1f) * scale,
                startLifetime = Random.Range(0.18f, 0.32f),
                startColor = Color.Lerp(color, Color.white, Random.Range(0.15f, 0.65f))
            };
            instance.sparks.Emit(particle, 1);
        }

        var glow = new ParticleSystem.EmitParams
        {
            position = position,
            startSize = 0.6f * scale,
            startLifetime = 0.14f,
            startColor = Color.Lerp(color, Color.white, 0.65f)
        };
        instance.flash.Emit(glow, 1);
        if (ring)
        {
            glow.startSize = 0.65f * scale;
            glow.startLifetime = 0.22f;
            glow.startColor = new Color(color.r, color.g, color.b, 0.65f);
            instance.rings.Emit(glow, 1);
        }
    }

    private ParticleSystem CreateSystem(string systemName, bool ring, bool round, int limit)
    {
        var child = new GameObject(systemName);
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startSpeed = 0f;
        main.startSize3D = !round;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = limit;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var fading = system.colorOverLifetime;
        fading.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fading.color = gradient;
        var sizing = system.sizeOverLifetime;
        sizing.enabled = true;
        sizing.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, ring ? 0.4f : 1f, 1f, ring ? 1.4f : 0f));

        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 30;
        var material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        material.name = "EnemyHit_" + systemName;
        material.mainTexture = CreateTexture(ring);
        renderer.sharedMaterial = material;
        system.Play();
        return system;
    }

    internal static Texture2D CreateTexture(bool ring)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = ring ? "EnemyHitRing" : "EnemyHitSoft";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + 0.5f) / size * 2f - 1f,
                    (y + 0.5f) / size * 2f - 1f).magnitude;
                float alpha = ring ? Mathf.Clamp01(1f - Mathf.Abs(radius - 0.78f) / 0.12f)
                    : Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        foreach (var renderer in GetComponentsInChildren<ParticleSystemRenderer>())
        {
            Material material = renderer.sharedMaterial;
            if (material == null) continue;
            Destroy(material.mainTexture);
            Destroy(material);
        }
    }
}
