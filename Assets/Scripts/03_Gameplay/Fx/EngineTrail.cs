using UnityEngine;

/// <summary>Compact layered ion exhaust with a continuous core and a fading world-space wake.</summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class EngineTrail : MonoBehaviour
{
    [SerializeField] private Color trailColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float ejectSpeed = 3.2f;
    [SerializeField, Range(0.1f, 0.6f)] private float particleLifetime = 0.24f;

    private ParticleSystem plume;
    private ParticleSystem core;
    private ParticleSystem embers;
    private Material exhaustMaterial;

    private void Awake()
    {
        exhaustMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"))
        {
            name = "Engine_IonExhaust",
            mainTexture = EnemyHitVfx.CreateTexture(false)
        };
        plume = GetComponent<ParticleSystem>();
        Configure(plume, 95f, particleLifetime, ejectSpeed, 0.16f, 0.38f, false, false);
        core = CreateLayer("WhiteBlueCore");
        Configure(core, 130f, 0.10f, 1.6f, 0.09f, 0.25f, false, true);
        embers = CreateLayer("FadingWake");
        Configure(embers, 16f, 0.32f, 2.2f, 0.035f, 0.10f, true, false);
    }

    private void OnEnable() => SetThrust(true);
    private void OnDisable() => SetThrust(false);

    public void AlignToShip(SpriteRenderer ship)
    {
        if (ship == null || ship.sprite == null) return;
        // A trail attached directly to the ship must not reposition the player.
        if (transform == ship.transform) return;
        Bounds bounds = ship.sprite.bounds;
        transform.localPosition = new Vector3(bounds.center.x,
            bounds.min.y + bounds.size.y * 0.08f, 0f);
        float width = Mathf.Clamp(bounds.size.x / 1.2f, 0.75f, 1.35f);
        transform.localScale = new Vector3(width, 1f, 1f);
        foreach (var renderer in GetComponentsInChildren<ParticleSystemRenderer>())
        {
            renderer.sortingLayerID = ship.sortingLayerID;
            renderer.sortingOrder = ship.sortingOrder - 1;
        }
    }

    public void SetThrust(bool active)
    {
        if (plume == null) return;
        if (active)
        {
            plume.Play(true);
        }
        else
        {
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private ParticleSystem CreateLayer(string layerName)
    {
        var layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);
        return layer.AddComponent<ParticleSystem>();
    }

    private void Configure(ParticleSystem system, float rate, float lifetime, float speed,
        float width, float length, bool worldSpace, bool whiteCore)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(width * 0.85f, width);
        main.startSizeY = new ParticleSystem.MinMaxCurve(length * 0.85f, length);
        main.startSizeZ = 1f;
        main.startColor = Color.white;
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.maxParticles = worldSpace ? 32 : 64;
        main.gravityModifier = 0f;

        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = worldSpace ? 5f : 2f;
        shape.radius = worldSpace ? 0.025f : 0.012f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.97f, 1f), 0f),
                new GradientColorKey(whiteCore ? Color.white : trailColor, 0.3f),
                new GradientColorKey(new Color(0.08f, 0.3f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.7f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.05f));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = exhaustMaterial;
    }

    private void OnDestroy()
    {
        if (exhaustMaterial == null) return;
        Destroy(exhaustMaterial.mainTexture);
        Destroy(exhaustMaterial);
    }
}
