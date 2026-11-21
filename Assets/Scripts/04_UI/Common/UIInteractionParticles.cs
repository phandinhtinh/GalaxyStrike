using UnityEngine;
using UnityEngine.UI;

/// <summary>One bounded mesh per canvas. Click sparks remain responsive inside paused menus.</summary>
public sealed class UIInteractionParticles : MaskableGraphic
{
    private struct Spark
    {
        public Vector2 position, velocity;
        public Color tint;
        public float age, lifetime, size;
    }

    private readonly Spark[] sparks = new Spark[96];
    private readonly UIVertex[] quad = new UIVertex[4];
    private int count;

    public static void Burst(RectTransform button, Color tint)
    {
        Canvas canvas = button.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        Transform existing = canvas.transform.Find("InteractionSparks");
        UIInteractionParticles fx;
        if (existing == null)
        {
            var go = new GameObject("InteractionSparks", typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            fx = go.AddComponent<UIInteractionParticles>();
            fx.raycastTarget = false;
        }
        else fx = existing.GetComponent<UIInteractionParticles>();
        fx.transform.SetAsLastSibling();
        Vector2 center = fx.rectTransform.InverseTransformPoint(button.TransformPoint(button.rect.center));
        // Scale with the touched control so small close buttons and large CTAs share the same feel.
        Vector2 extent = fx.rectTransform.InverseTransformVector(button.TransformVector(button.rect.size));
        float radius = Mathf.Clamp(Mathf.Min(Mathf.Abs(extent.x), Mathf.Abs(extent.y)) * 0.38f, 12f, 110f);
        for (int i = 0; i < 12 && fx.count < fx.sparks.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            fx.sparks[fx.count++] = new Spark
            {
                position = center + direction * radius,
                velocity = direction * radius * Random.Range(1f, 2f), tint = tint,
                lifetime = Random.Range(0.25f, 0.45f), size = Mathf.Clamp(radius * 0.075f, 3f, 8f)
            };
        }
        fx.SetVerticesDirty();
    }

    private void Update()
    {
        if (count == 0) return;
        float dt = Time.unscaledDeltaTime;
        for (int i = count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark.age += dt;
            if (spark.age >= spark.lifetime) { sparks[i] = sparks[--count]; continue; }
            spark.position += spark.velocity * dt;
            spark.velocity *= Mathf.Exp(-3f * dt);
            sparks[i] = spark;
        }
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        count = 0;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < count; i++)
        {
            Spark spark = sparks[i];
            Color tint = spark.tint;
            tint.a *= 1f - spark.age / spark.lifetime;
            float size = spark.size * (1f - spark.age / spark.lifetime * 0.7f);
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = spark.position + Vector2.left * size;
            quad[0] = vertex;
            vertex.position = spark.position + Vector2.up * size;
            quad[1] = vertex;
            vertex.position = spark.position + Vector2.right * size;
            quad[2] = vertex;
            vertex.position = spark.position + Vector2.down * size;
            quad[3] = vertex;
            mesh.AddUIVertexQuad(quad);
        }
    }
}
