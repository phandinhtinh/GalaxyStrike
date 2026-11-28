using UnityEngine;
using Screen = UnityEngine.Device.Screen;

namespace GalaxyStrike.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PopupFrameFitter : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float widthFraction = 0.94f;
        private const float HeightFraction = 0.88f;

        private RectTransform frame;
        private RectTransform container;
        private Canvas rootCanvas;
        private float animationScale = 1f;

        /// <summary>Hệ số phóng to/thu nhỏ của hiệu ứng mở/đóng popup, nhân vào tỉ lệ vừa khung.</summary>
        public float AnimationScale
        {
            get => animationScale;
            set
            {
                animationScale = value;
                Fit();
            }
        }

        public void SetWidthFraction(float fraction)
        {
            widthFraction = Mathf.Clamp(fraction, 0.1f, 1f);
            Fit();
        }

        private void OnEnable()
        {
            frame = (RectTransform)transform;
            container = frame.parent as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Fit();
        }

        private void LateUpdate()
        {
            Fit();
        }

        private void Fit()
        {
            if (container == null || rootCanvas == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            RectTransform canvasRect = (RectTransform)rootCanvas.transform;
            Rect canvasBounds = canvasRect.rect;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 safeMin = canvasBounds.min + Vector2.Scale(safeArea.min / screenSize, canvasBounds.size);
            Vector2 safeMax = canvasBounds.min + Vector2.Scale(safeArea.max / screenSize, canvasBounds.size);
            Vector2 localMin = container.InverseTransformPoint(canvasRect.TransformPoint(safeMin));
            Vector2 localMax = container.InverseTransformPoint(canvasRect.TransformPoint(safeMax));

            // The container may already be inside the HomeScene safe area.
            Vector2 min = Vector2.Max(container.rect.min, localMin);
            Vector2 max = Vector2.Min(container.rect.max, localMax);
            Vector2 available = max - min;
            Vector2 design = frame.rect.size;
            if (available.x <= 0f || available.y <= 0f || design.x <= 0f || design.y <= 0f)
                return;

            float scale = Mathf.Min(available.x * widthFraction / design.x, available.y * HeightFraction / design.y)
                * animationScale;
            Vector2 center = (min + max) * 0.5f - container.rect.center;
            Vector3 fittedScale = new Vector3(scale, scale, 1f);
            if (frame.anchoredPosition != center)
                frame.anchoredPosition = center;
            if (frame.localScale != fittedScale)
                frame.localScale = fittedScale;
        }
    }
}
