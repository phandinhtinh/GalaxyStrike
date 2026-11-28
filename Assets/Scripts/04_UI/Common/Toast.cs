using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI
{
    public enum ToastKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Thông báo ngắn ("toast") hiện phía dưới màn hình rồi tự ẩn: không đủ năng lượng / Coin,
    /// mua thành công, lên cấp... Tự tạo một Canvas overlay riêng (sống qua các Scene), chạy bằng
    /// thời gian unscaled nên vẫn hiện khi game đang dừng. Tin trùng đang hiện chỉ được kéo dài
    /// thay vì xếp hàng lặp lại.
    /// </summary>
    public sealed class Toast : MonoBehaviour
    {
        private const float MaxTextWidth = 860f;
        private const float FadeIn = 0.18f;
        private const float FadeOut = 0.25f;
        private const int MaxQueued = 3;
        private const float MinVisibleWhenQueued = 0.7f;

        private static Toast instance;

        private readonly Queue<(string message, ToastKind kind, float duration)> queue =
            new Queue<(string, ToastKind, float)>();

        private RectTransform panel;
        private Image background;
        private Outline outline;
        private TextMeshProUGUI label;
        private CanvasGroup group;
        private string currentMessage;
        private float currentHoldUntil;
        private Coroutine routine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>Hiện một thông báo. Gọi được từ bất kỳ Scene nào.</summary>
        public static void Show(string message, ToastKind kind = ToastKind.Info, float duration = 2.2f)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Ensure().Enqueue(message, kind, Mathf.Max(0.8f, duration));
        }

        private static Toast Ensure()
        {
            if (instance != null)
                return instance;

            var go = new GameObject("[Toast]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Toast>();
            instance.Build();
            return instance;
        }

        private void Enqueue(string message, ToastKind kind, float duration)
        {
            // Đang hiện đúng tin này -> chỉ kéo dài, không lặp lại.
            if (routine != null && message == currentMessage)
            {
                currentHoldUntil = Time.unscaledTime + duration;
                return;
            }

            foreach (var queued in queue)
            {
                if (queued.message == message)
                    return;
            }

            if (queue.Count >= MaxQueued)
                queue.Dequeue();
            queue.Enqueue((message, kind, duration));

            if (routine == null)
                routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (queue.Count > 0)
            {
                var (message, kind, duration) = queue.Dequeue();
                Apply(message, kind);
                currentMessage = message;
                currentHoldUntil = Time.unscaledTime + FadeIn + duration;

                if (kind == ToastKind.Warning || kind == ToastKind.Error)
                    Haptics.Light();

                for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
                {
                    float k = t / FadeIn;
                    group.alpha = k;
                    panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - k) * (1f - k));
                    yield return null;
                }
                group.alpha = 1f;
                panel.localScale = Vector3.one;

                // Có tin mới đang chờ (người chơi bấm liên tiếp) -> chỉ giữ tin hiện tại đủ để đọc lướt
                // rồi nhường chỗ, tránh tin mới phải đợi hết vài giây mới hiện.
                float shownAt = Time.unscaledTime;
                while (Time.unscaledTime < currentHoldUntil &&
                       !(queue.Count > 0 && Time.unscaledTime >= shownAt + MinVisibleWhenQueued))
                    yield return null;

                for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
                {
                    group.alpha = 1f - t / FadeOut;
                    yield return null;
                }
                group.alpha = 0f;
            }

            currentMessage = null;
            routine = null;
        }

        private void Apply(string message, ToastKind kind)
        {
            Color accent;
            switch (kind)
            {
                case ToastKind.Success: accent = new Color(0.35f, 1f, 0.55f); break;
                case ToastKind.Warning: accent = new Color(1f, 0.78f, 0.25f); break;
                case ToastKind.Error: accent = new Color(1f, 0.38f, 0.38f); break;
                default: accent = new Color(0.35f, 0.88f, 1f); break;
            }

            outline.effectColor = accent;
            label.color = kind == ToastKind.Info ? Color.white : Color.Lerp(Color.white, accent, 0.35f);
            label.text = message;

            Vector2 size = label.GetPreferredValues(message, MaxTextWidth, 0f);
            size.x = Mathf.Min(size.x, MaxTextWidth);
            label.rectTransform.sizeDelta = size;
            panel.sizeDelta = size + new Vector2(80f, 48f);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("Toast_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            // Phía trên thanh điều hướng dưới, không che nút giữa màn hình.
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.22f);
            panel.pivot = new Vector2(0.5f, 0.5f);

            background = panel.GetComponent<Image>();
            background.sprite = CreateRoundedSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color(0.03f, 0.09f, 0.22f, 0.94f);
            background.raycastTarget = false;
            outline = panel.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3f, -3f);

            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            label = new GameObject("Toast_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(panel, false);
            label.fontSize = 38f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        /// <summary>Sprite bo góc 9-slice sinh bằng code (không phụ thuộc asset UI mặc định của Editor).</summary>
        private static Sprite CreateRoundedSprite()
        {
            const int size = 64;
            const float radius = 26f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }
    }
}
