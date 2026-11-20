using UnityEngine;
using UnityEngine.UI;

/// <summary>Bounded UI particles rendered in one mesh, animated while gameplay is paused.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VictoryCelebration : MaskableGraphic
{
    private struct Spark
    {
        public Vector2 position;
        public Vector2 velocity;
        public Color tint;
        public float age;
        public float lifetime;
        public float size;
        public float angle;
        public float spin;
        public float gravity;
    }

    private readonly Spark[] sparks = new Spark[96];
    private readonly UIVertex[] quad = new UIVertex[4];
    private int count;
    private float ambientTimer;

    public void Play()
    {
        raycastTarget = false;
        count = 0;
        ambientTimer = 0f;
        for (int i = 0; i < 64; i++) Emit(false);
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        count = 0;
        base.OnDisable();
    }

    private void Emit(bool ambient)
    {
        if (count >= sparks.Length) return;
        Rect bounds = rectTransform.rect;
        float side = Random.value < 0.5f ? -1f : 1f;
        sparks[count++] = new Spark
        {
            position = ambient
                ? new Vector2(side * bounds.width * 0.40f, Random.Range(-0.30f, 0.30f) * bounds.height)
                : new Vector2(side * bounds.width * 0.26f, bounds.height * 0.08f),
            velocity = ambient
                ? new Vector2(Random.Range(-8f, 8f), Random.Range(14f, 30f))
                : new Vector2(side * Random.Range(90f, 240f), Random.Range(240f, 540f)),
            tint = Random.value < 0.65f ? new Color(1f, 0.8f, 0.35f) : new Color(0.35f, 0.9f, 1f),
            lifetime = Random.Range(1.4f, 2.5f),
            size = ambient ? Random.Range(4f, 7f) : Random.Range(6f, 12f),
            angle = Random.Range(0f, 360f),
            spin = Random.Range(-160f, 160f),
            gravity = ambient ? 0f : 420f
        };
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark.age += dt;
            if (spark.age >= spark.lifetime)
            {
                sparks[i] = sparks[--count];
                continue;
            }
            spark.velocity.y -= spark.gravity * dt;
            spark.position += spark.velocity * dt;
            spark.angle += spark.spin * dt;
            sparks[i] = spark;
        }
        ambientTimer += dt;
        if (ambientTimer >= 0.14f)
        {
            ambientTimer = 0f;
            Emit(true);
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < count; i++)
        {
            Spark spark = sparks[i];
            Color tint = spark.tint;
            tint.a = Mathf.Clamp01(spark.age / 0.08f) * (1f - spark.age / spark.lifetime);
            float angle = spark.angle * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spark.size;
            Vector2 up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * spark.size * 0.4f;
            AddQuad(mesh, spark.position, right * 2.2f, up * 2.2f,
                new Color(tint.r, tint.g, tint.b, tint.a * 0.12f));
            AddQuad(mesh, spark.position, right, up, tint);
        }
    }

    private void AddQuad(VertexHelper mesh, Vector2 center, Vector2 right, Vector2 up, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = tint;
        vertex.position = center - right - up;
        quad[0] = vertex;
        vertex.position = center - right + up;
        quad[1] = vertex;
        vertex.position = center + right + up;
        quad[2] = vertex;
        vertex.position = center + right - up;
        quad[3] = vertex;
        mesh.AddUIVertexQuad(quad);
    }
}
