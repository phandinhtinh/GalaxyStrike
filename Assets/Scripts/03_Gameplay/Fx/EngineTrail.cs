using UnityEngine;

/// <summary>
/// Vệt lửa động cơ gắn sau đuôi phi thuyền (ParticleSystem dựng bằng code).
///
/// Gắn lên Player hoặc một child "EngineTrail" của Player; hệ thống tự phát
/// particle liên tục và tự huỷ thế giới hạt, không cần asset.
///
/// Chỉ tạo 1 particle system nên chi phí gần như 0.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class EngineTrail : MonoBehaviour
{
    [Tooltip("Màu vệt lửa")]
    [SerializeField] private Color trailColor = new Color(0.35f, 0.85f, 1f, 0.85f);

    [Tooltip("Tốc độ phụt về phía sau (đơn vị/giây)")]
    [SerializeField] private float ejectSpeed = 3.2f;

    [Tooltip("Thời gian sống của mỗi hạt")]
    [SerializeField] private float particleLifetime = 0.35f;

    private ParticleSystem system;

    private void Awake()
    {
        system = GetComponent<ParticleSystem>();
        Configure();
    }

    /// <summary>
    /// Dựng cấu hình particle: bắn xuống theo local space để luôn phụt về sau
    /// bất kể tàu xoay thế nào, không gravity, emit liên tục.
    /// </summary>
    private void Configure()
    {
        ParticleSystem ps = system;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = particleLifetime;
        main.startSpeed = ejectSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = trailColor;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.02f;
        shape.rotation = new Vector3(90f, 0f, 0f); // phụt xuống (−Y)

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(trailColor, 0.4f),
                new GradientColorKey(trailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (renderer.sharedMaterial == null)
            renderer.sharedMaterial = BuildAdditiveMaterial();

        ps.Play();
    }

    private static Material BuildAdditiveMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) { name = "EngineTrail_Additive" } : null;
    }
}
