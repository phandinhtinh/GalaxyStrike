using UnityEngine;

/// <summary>Scene-owned, reusable Pulse and Aegis visuals. No gameplay state lives here.</summary>
public sealed class PlayerSkillVfx : MonoBehaviour
{
    private const float PulseDuration = 0.62f;
    private static readonly Color Cyan = new Color(0.18f, 0.78f, 1f);
    private static readonly Color Ice = new Color(0.65f, 0.96f, 1f);
    private static readonly Color Gold = new Color(1f, 0.64f, 0.16f);
    private static readonly Color Amber = new Color(1f, 0.88f, 0.48f);
    private static PlayerSkillVfx instance;

    private Material lineMaterial;
    private Material particleMaterial;
    private Transform pulseRoot;
    private Transform aegisRoot;
    private LineRenderer pulseGlow, pulseRim, pulseEcho, pulseEchoGlow;
    private LineRenderer aegisGlow, aegisRim, aegisInner, aegisActivation;
    private readonly LineRenderer[] aegisArcs = new LineRenderer[3];
    private ParticleSystem pulseSparks, pulseFlash, aegisSparks;
    private PlayerController shieldOwner;
    private SpriteRenderer shieldHull;
    private float pulseAge, pulseRadius, shieldAge, shieldDuration, sparkTimer;

    public static void PlayPulse(PlayerController player, float radius)
    {
        PlayerSkillVfx fx = GetOrCreate(player);
        fx.pulseRoot.position = player.transform.position;
        fx.pulseRadius = Mathf.Max(0.1f, radius);
        fx.pulseAge = 0f;
        fx.pulseRoot.gameObject.SetActive(true);
        fx.pulseSparks.Clear();
        fx.pulseFlash.Clear();
        fx.pulseSparks.Play();
        fx.pulseFlash.Play();
        fx.MatchSorting(player.GetComponent<SpriteRenderer>(), fx.pulseRoot);
        // The visual front reaches the same radius as the instantaneous gameplay hit.
        fx.DrawPulse();
        for (int i = 0; i < 40; i++)
        {
            float angle = i * 360f / 40f + Random.Range(-3f, 3f);
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            fx.Emit(fx.pulseSparks, direction * radius * Random.Range(0.06f, 0.18f),
                direction * radius * Random.Range(1.2f, 1.6f),
                new Vector3(0.045f, Random.Range(0.15f, 0.28f), 1f),
                Random.Range(0.28f, 0.46f), Color.Lerp(Cyan, Ice, Random.value), 90f - angle);
        }
        fx.Emit(fx.pulseFlash, Vector3.zero, Vector3.zero, Vector3.one * radius * 0.95f,
            0.2f, new Color(Ice.r, Ice.g, Ice.b, 0.65f));
    }

    public static void PlayAegis(PlayerController player, float duration)
    {
        PlayerSkillVfx fx = GetOrCreate(player);
        bool continuing = fx.shieldOwner == player && fx.aegisRoot.gameObject.activeSelf;
        fx.shieldDuration = Mathf.Max(continuing ? fx.shieldDuration - fx.shieldAge : 0f, duration);
        fx.shieldAge = fx.sparkTimer = 0f;
        fx.shieldOwner = player;
        fx.shieldHull = player.GetComponent<SpriteRenderer>();
        fx.aegisRoot.gameObject.SetActive(true);
        fx.aegisSparks.Clear();
        fx.aegisSparks.Play();
        fx.MatchSorting(fx.shieldHull, fx.aegisRoot);
        fx.DrawAegis();
        float radius = fx.ShieldRadius;
        for (int i = 0; i < 18; i++)
        {
            float angle = i * Mathf.PI * 2f / 18f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
            fx.Emit(fx.aegisSparks, radial * radius * 0.7f, radial * radius * 0.7f,
                new Vector3(0.055f, 0.13f, 1f), 0.34f, Amber, 90f - angle * Mathf.Rad2Deg);
        }
    }

    private static PlayerSkillVfx GetOrCreate(PlayerController player)
    {
        if (instance != null && instance.gameObject.scene == player.gameObject.scene)
            return instance;
        var root = new GameObject("Player Skill VFX");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, player.gameObject.scene);
        instance = root.AddComponent<PlayerSkillVfx>();
        return instance;
    }

    private void Awake()
    {
        // Explicit soft textures prevent the solid squares produced by default particles.
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        lineMaterial = new Material(shader) { name = "Skill_SoftRibbon", mainTexture = CreateRibbon() };
        particleMaterial = new Material(shader)
        {
            name = "Skill_SoftSpark", mainTexture = EnemyHitVfx.CreateTexture(false)
        };
        pulseRoot = CreateRoot("Pulse Shockwave");
        pulseGlow = CreateLine(pulseRoot, "Front glow", 96, true);
        pulseRim = CreateLine(pulseRoot, "Front core", 96, true);
        pulseEchoGlow = CreateLine(pulseRoot, "Echo glow", 96, true);
        pulseEcho = CreateLine(pulseRoot, "Echo core", 96, true);
        pulseSparks = CreateParticles(pulseRoot, "Radial sparks", 48);
        pulseFlash = CreateParticles(pulseRoot, "Core flash", 2);
        aegisRoot = CreateRoot("Aegis Shield");
        aegisGlow = CreateLine(aegisRoot, "Shield halo", 96, true);
        aegisRim = CreateLine(aegisRoot, "Shield rim", 96, true);
        aegisInner = CreateLine(aegisRoot, "Inner contour", 96, true);
        aegisActivation = CreateLine(aegisRoot, "Activation ripple", 96, true);
        for (int i = 0; i < aegisArcs.Length; i++)
            aegisArcs[i] = CreateLine(aegisRoot, "Orbit arc " + i, 24, false);
        aegisSparks = CreateParticles(aegisRoot, "Shield motes", 40);
        pulseRoot.gameObject.SetActive(false);
        aegisRoot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
        {
            HideEffects();
            return;
        }
        if (pulseRoot.gameObject.activeSelf)
        {
            pulseAge += Time.deltaTime;
            if (pulseAge >= PulseDuration) pulseRoot.gameObject.SetActive(false);
            else DrawPulse();
        }
        if (!aegisRoot.gameObject.activeSelf) return;
        shieldAge += Time.deltaTime;
        if (shieldAge >= shieldDuration || shieldOwner == null ||
            !shieldOwner.gameObject.activeInHierarchy || shieldOwner.IsDowned)
        {
            aegisRoot.gameObject.SetActive(false);
            shieldOwner = null;
            return;
        }
        DrawAegis();
        sparkTimer += Time.deltaTime;
        if (sparkTimer >= 0.05f && shieldDuration - shieldAge > 0.25f)
        {
            sparkTimer %= 0.05f;
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
            Emit(aegisSparks, radial * ShieldRadius * 0.98f, radial * 0.16f,
                new Vector3(0.045f, 0.08f, 1f), 0.28f, Amber, 90f - angle * Mathf.Rad2Deg);
        }
    }

    private void LateUpdate()
    {
        // Follow after the controller moves, including when gameplay is paused.
        if (aegisRoot.gameObject.activeSelf && shieldOwner != null)
            aegisRoot.position = shieldHull != null ? shieldHull.bounds.center : shieldOwner.transform.position;
    }

    private void DrawPulse()
    {
        float t = Mathf.Clamp01(pulseAge / PulseDuration);
        float front = pulseRadius * Mathf.Lerp(0.12f, 1f, 1f - Mathf.Pow(1f - t, 3f));
        float alpha = Mathf.SmoothStep(1f, 0f, t);
        DrawArc(pulseGlow, front, 0f, 360f, 0.19f, WithAlpha(Cyan, alpha * 0.4f));
        DrawArc(pulseRim, front, 0f, 360f, 0.045f, WithAlpha(Ice, alpha));
        float echo = front * Mathf.Lerp(0.45f, 0.91f, t);
        DrawArc(pulseEchoGlow, echo, 0f, 360f, 0.12f, WithAlpha(Cyan, alpha * 0.22f));
        DrawArc(pulseEcho, echo, 0f, 360f, 0.022f, WithAlpha(Cyan, alpha * 0.7f));
    }

    private float ShieldRadius => shieldHull != null && shieldHull.sprite != null
        ? shieldHull.bounds.extents.magnitude * 1.06f + 0.03f : 0.65f;

    private void DrawAegis()
    {
        aegisRoot.position = shieldHull != null ? shieldHull.bounds.center : shieldOwner.transform.position;
        float radius = ShieldRadius;
        float fade = Mathf.SmoothStep(0f, 1f, shieldAge / 0.12f) *
                     Mathf.Clamp01((shieldDuration - shieldAge) / 0.3f);
        float breathe = 0.88f + Mathf.Sin(shieldAge * 5f) * 0.12f;
        float opening = Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0f, 1f, shieldAge / 0.18f));
        DrawArc(aegisGlow, radius * opening, 0f, 360f, radius * 0.16f, WithAlpha(Gold, fade * 0.42f));
        DrawArc(aegisRim, radius * opening, 0f, 360f, 0.025f, WithAlpha(Amber, fade * 0.8f));
        DrawArc(aegisInner, radius * 0.87f * opening, 0f, 360f, 0.015f, WithAlpha(Gold, fade * 0.25f));
        for (int i = 0; i < aegisArcs.Length; i++)
        {
            float start = shieldAge * 42f + i * 120f;
            DrawArc(aegisArcs[i], radius * opening * 1.025f, start, 58f, 0.05f,
                WithAlpha(Amber, fade * breathe));
        }
        float activation = Mathf.Clamp01(shieldAge / 0.4f);
        DrawArc(aegisActivation, radius * Mathf.Lerp(0.65f, 1.22f, activation),
            0f, 360f, 0.065f, WithAlpha(Gold, (1f - activation) * 0.7f));
    }

    private Transform CreateRoot(string label)
    {
        var root = new GameObject(label).transform;
        root.SetParent(transform, false);
        return root;
    }

    private LineRenderer CreateLine(Transform parent, string label, int points, bool loop)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = points;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 3;
        line.numCornerVertices = 0;
        return line;
    }

    private ParticleSystem CreateParticles(Transform parent, string label, int capacity)
    {
        var system = new GameObject(label).AddComponent<ParticleSystem>();
        system.transform.SetParent(parent, false);
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = capacity;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var tint = system.colorOverLifetime;
        tint.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.3f), new GradientAlphaKey(0f, 1f) });
        tint.color = gradient;
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        // Round flashes keep equal axes; elongated sparks have an explicit in-plane rotation.
        renderer.alignment = ParticleSystemRenderSpace.View;
        return system;
    }

    private void MatchSorting(SpriteRenderer hull, Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerID = hull != null ? hull.sortingLayerID : 0;
            renderer.sortingOrder = hull != null ? hull.sortingOrder + 3 : 8;
        }
    }

    private static void DrawArc(LineRenderer line, float radius, float start, float sweep, float width, Color color)
    {
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        int denominator = line.loop ? line.positionCount : line.positionCount - 1;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (start + sweep * i / denominator) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void Emit(ParticleSystem system, Vector3 position, Vector3 velocity,
        Vector3 size, float lifetime, Color color, float rotation = 0f)
    {
        system.Emit(new ParticleSystem.EmitParams
        {
            position = position, velocity = velocity, startSize3D = size,
            startLifetime = lifetime, startColor = color, rotation = rotation
        }, 1);
    }

    private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    private static Texture2D CreateRibbon()
    {
        var texture = new Texture2D(1, 32, TextureFormat.RGBA32, false)
        {
            name = "Skill_RibbonFalloff", wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[32];
        for (int y = 0; y < pixels.Length; y++)
        {
            float distance = Mathf.Abs((y + 0.5f) / pixels.Length * 2f - 1f);
            pixels[y] = new Color(1f, 1f, 1f, Mathf.Pow(1f - distance, 1.5f));
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void HideEffects()
    {
        pulseRoot.gameObject.SetActive(false);
        aegisRoot.gameObject.SetActive(false);
        shieldOwner = null;
    }

    private void OnDisable()
    {
        if (pulseRoot != null && aegisRoot != null) HideEffects();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (lineMaterial != null)
        {
            Destroy(lineMaterial.mainTexture);
            Destroy(lineMaterial);
        }
        if (particleMaterial != null)
        {
            Destroy(particleMaterial.mainTexture);
            Destroy(particleMaterial);
        }
    }
}
