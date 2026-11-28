using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hàng 3 ngôi sao đánh giá ở bảng Victory.
/// Sao trống hiện sẵn; sao đạt được lần lượt "đóng dấu" xuống (to -> vừa, nảy nhẹ) kèm tiếng chuông.
/// Chạy bằng thời gian unscaled vì bảng Victory dừng game (timeScale = 0).
/// </summary>
public sealed class VictoryStars : MonoBehaviour
{
    private const string FullSprite = "UI/Victory/Star_Full";
    private const string EmptySprite = "UI/Victory/Star_Empty";
    private const float FirstDelay = 0.35f;   // chờ bảng Victory nảy xong
    private const float StepDelay = 0.28f;
    private const float StampTime = 0.3f;

    // Sao giữa to và cao hơn một chút -> hàng sao cong nhẹ.
    private static readonly Vector2[] Positions = { new Vector2(-118f, -12f), new Vector2(0f, 8f), new Vector2(118f, -12f) };
    private static readonly float[] Sizes = { 92f, 112f, 92f };

    private readonly Image[] fills = new Image[3];
    private Coroutine routine;

    /// <summary>
    /// Số sao theo độ an toàn của tàu khi qua màn:
    /// 3 sao khi còn ≥ 80% giáp, 2 sao khi còn ≥ 40%, còn lại 1 sao; đã phải hồi sinh thì chỉ 1 sao.
    /// </summary>
    public static int Rate(int health, int maxHealth, bool revived)
    {
        if (revived || maxHealth <= 0)
            return 1;

        float hull = Mathf.Clamp01((float)health / maxHealth);
        return hull >= 0.8f ? 3 : hull >= 0.4f ? 2 : 1;
    }

    /// <summary>Tạo hàng sao làm con của <paramref name="parent"/>, tâm tại <paramref name="center"/>.</summary>
    public static VictoryStars Create(RectTransform parent, Vector2 center)
    {
        var go = new GameObject("Victory_Stars", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(360f, 130f);

        var stars = go.AddComponent<VictoryStars>();
        stars.Build();
        return stars;
    }

    private void Build()
    {
        Sprite full = Resources.Load<Sprite>(FullSprite);
        Sprite empty = Resources.Load<Sprite>(EmptySprite);
        for (int i = 0; i < 3; i++)
        {
            Image slot = CreateImage("Star_Slot_" + (i + 1), empty, Positions[i], Sizes[i]);
            if (empty == null)
                slot.color = new Color(0.2f, 0.25f, 0.38f, 1f);

            fills[i] = CreateImage("Star_" + (i + 1), full, Positions[i], Sizes[i]);
            if (full == null)
                fills[i].color = new Color(1f, 0.8f, 0.2f, 1f);
            fills[i].gameObject.SetActive(false);
        }
    }

    private Image CreateImage(string name, Sprite sprite, Vector2 position, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Hiện <paramref name="earned"/> sao (1–3) có hiệu ứng.</summary>
    public void Play(int earned)
    {
        earned = Mathf.Clamp(earned, 0, 3);
        if (routine != null)
            StopCoroutine(routine);

        foreach (Image fill in fills)
            fill.gameObject.SetActive(false);

        if (isActiveAndEnabled)
            routine = StartCoroutine(PlayRoutine(earned));
        else
            ShowInstant(earned);
    }

    private void OnDisable()
    {
        routine = null;
    }

    private void ShowInstant(int earned)
    {
        for (int i = 0; i < fills.Length; i++)
        {
            fills[i].gameObject.SetActive(i < earned);
            fills[i].rectTransform.localScale = Vector3.one;
            fills[i].color = Color.white;
        }
    }

    private IEnumerator PlayRoutine(int earned)
    {
        yield return WaitUnscaled(FirstDelay);

        for (int i = 0; i < earned; i++)
        {
            Image fill = fills[i];
            RectTransform rect = fill.rectTransform;
            fill.gameObject.SetActive(true);
            AudioManager.Play(SfxCue.PowerUp);
            Haptics.Light();

            for (float t = 0f; t < StampTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / StampTime);
                // Rơi từ to (2.2x) xuống, hơi vượt nhỏ hơn 1 rồi bật lại.
                float scale = k < 0.65f
                    ? Mathf.Lerp(2.2f, 0.88f, EaseIn(k / 0.65f))
                    : Mathf.Lerp(0.88f, 1f, EaseOut((k - 0.65f) / 0.35f));
                rect.localScale = new Vector3(scale, scale, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 0f, EaseOut(k)));
                fill.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
                yield return null;
            }

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            fill.color = Color.white;
            yield return WaitUnscaled(StepDelay - StampTime * 0.5f);
        }

        routine = null;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }

    private static float EaseIn(float t) => t * t;
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
