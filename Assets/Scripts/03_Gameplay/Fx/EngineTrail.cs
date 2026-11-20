using System.Collections.Generic;
using GalaxyStrike.Data;
using UnityEngine;

/// <summary>Ship-authored nozzle positions and layered exhaust, reused when the selected ship changes.</summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class EngineTrail : MonoBehaviour
{
    // Keep the original serialized settings for scenes without ShipData.
    [SerializeField] private Color trailColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float ejectSpeed = 3.2f;
    [SerializeField, Range(0.1f, 0.6f)] private float particleLifetime = 0.24f;

    private sealed class Emitter
    {
        public ParticleSystem plume;
        public ParticleSystem core;
        public ParticleSystem wake;
    }

    private readonly List<Emitter> emitters = new List<Emitter>();
    private int activeEmitterCount = 1;
    private Material exhaustMaterial;
    private ShipDataSO shipData;
    private bool thrustActive = true;

    private void Awake()
    {
        exhaustMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"))
        {
            name = "Engine_ShipExhaust",
            mainTexture = EnemyHitVfx.CreateTexture(false)
        };
        EnsureEmitters(1);
        ConfigureEmitter(emitters[0], trailColor, new Color(0.08f, 0.3f, 1f),
            0.16f, particleLifetime, ejectSpeed, 2f, 16f, 1f);
    }

    private void OnEnable() => SetThrust(true);
    private void OnDisable() => SetThrust(false);

    public void ConfigureForShip(ShipDataSO data, SpriteRenderer ship)
    {
        if (data == null) return;
        bool resume = thrustActive;
        SetThrust(false);
        shipData = data;
        activeEmitterCount = data.exhaustNozzles != null && data.exhaustNozzles.Length > 0
            ? data.exhaustNozzles.Length : 1;
        EnsureEmitters(activeEmitterCount);
        for (int i = 0; i < activeEmitterCount; i++)
        {
            ShipDataSO.ExhaustNozzle nozzle = GetNozzle(i);
            ConfigureEmitter(emitters[i], data.exhaustColor, data.exhaustTailColor,
                data.exhaustWidth, data.exhaustLifetime, data.exhaustSpeed,
                data.exhaustSpread, data.exhaustWakeRate / activeEmitterCount,
                nozzle != null ? nozzle.strength : 1f);
        }
        AlignToShip(ship);
        SetThrust(resume);
    }

    private ShipDataSO.ExhaustNozzle GetNozzle(int index)
    {
        if (shipData == null || shipData.exhaustNozzles == null ||
            index >= shipData.exhaustNozzles.Length) return null;
        return shipData.exhaustNozzles[index];
    }

    public void AlignToShip(SpriteRenderer ship)
    {
        if (ship == null || ship.sprite == null) return;
        // Nozzle offsets are in sprite space. Never move the Player if attached to it directly.
        if (transform != ship.transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
        }
        Bounds bounds = ship.sprite.bounds;
        for (int i = 0; i < activeEmitterCount; i++)
        {
            var nozzle = GetNozzle(i);
            Vector2 position = nozzle != null ? nozzle.position : new Vector2(0.5f, 0.08f);
            Vector3 offset = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, ship.flipX ? 1f - position.x : position.x),
                Mathf.Lerp(bounds.min.y, bounds.max.y, ship.flipY ? 1f - position.y : position.y), 0f);
            foreach (var system in new[] { emitters[i].plume, emitters[i].core, emitters[i].wake })
            {
                var shape = system.shape;
                shape.position = offset;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = ship.sortingLayerID;
                renderer.sortingOrder = ship.sortingOrder - 1;
            }
        }
    }

    public void SetThrust(bool active)
    {
        thrustActive = active;
        for (int i = 0; i < emitters.Count; i++)
        {
            foreach (var system in new[] { emitters[i].plume, emitters[i].core, emitters[i].wake })
            {
                if (active && isActiveAndEnabled && i < activeEmitterCount)
                    system.Play(false);
                else
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void EnsureEmitters(int count)
    {
        while (emitters.Count < count)
        {
            int index = emitters.Count;
            emitters.Add(new Emitter
            {
                plume = index == 0 ? GetComponent<ParticleSystem>() : CreateLayer("Plume_" + index),
                core = CreateLayer("Core_" + index),
                wake = CreateLayer("Wake_" + index)
            });
        }
    }

    private ParticleSystem CreateLayer(string layerName)
    {
        var layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);
        return layer.AddComponent<ParticleSystem>();
    }

    private void ConfigureEmitter(Emitter emitter, Color color, Color tailColor,
        float width, float lifetime, float speed, float spread, float wakeRate, float strength)
    {
        strength = Mathf.Clamp(strength, 0.25f, 1.5f);
        width = Mathf.Clamp(width, 0.04f, 0.3f) * strength;
        lifetime = Mathf.Clamp(lifetime, 0.1f, 0.5f);
        speed = Mathf.Clamp(speed, 1f, 5f) * Mathf.Sqrt(strength);
        Configure(emitter.plume, 95f, lifetime, speed, width, width * 2.4f, false, color, tailColor, spread);
        Configure(emitter.core, 130f, 0.10f, speed * 0.5f, width * 0.55f, width * 1.6f, false,
            Color.Lerp(color, Color.white, 0.8f), color, spread * 0.5f);
        Configure(emitter.wake, wakeRate, lifetime * 1.4f, speed * 0.7f, width * 0.22f,
            width * 0.65f, true, color, tailColor, spread + 3f);
    }

    private void Configure(ParticleSystem system, float rate, float lifetime, float speed,
        float width, float length, bool worldSpace, Color color, Color tailColor, float spread)
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
        shape.angle = Mathf.Clamp(spread, 0f, 15f);
        shape.radius = width * 0.07f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var tint = system.colorOverLifetime;
        tint.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(color, Color.white, 0.65f), 0f),
                new GradientColorKey(color, 0.3f),
                new GradientColorKey(tailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.7f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        tint.color = gradient;
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
