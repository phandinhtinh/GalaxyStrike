using System.Collections;
using UnityEngine;

public sealed class AmbushWarning : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float duration;
    private float elapsed;

    public static AmbushWarning Create(Vector3 position, float warningTime, float width)
    {
        GameObject obj = new GameObject("AmbushWarning");
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        renderer.sortingOrder = 50;
        obj.transform.localScale = new Vector3(width, 0.18f, 1f);

        AmbushWarning warning = obj.AddComponent<AmbushWarning>();
        warning.spriteRenderer = renderer;
        warning.duration = warningTime;
        return warning;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // Nhấp nháy 2 lần/giây để dễ nhận ra.
        if (spriteRenderer != null)
        {
            float blink = Mathf.PingPong(Time.time * 6f, 1f);
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(0.25f, 0.95f, blink);
            spriteRenderer.color = c;
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private static Sprite cachedSprite;

    /// <summary>Sprite vuông 1x1 dựng runtime, không cần asset.</summary>
    public static Sprite GetSquareSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.name = "GeneratedSquare";
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();

        cachedSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        cachedSprite.name = "GeneratedSquareSprite";
        return cachedSprite;
    }
}
