using System.Collections;
using UnityEngine;

namespace GalaxyStrike.UI
{
    /// <summary>
    /// Hiệu ứng mở/đóng cho popup toàn màn hình (nền tối + khung ở giữa):
    ///  - Mở: nền mờ dần lên, khung nảy từ nhỏ ra (ease-out-back).
    ///  - Đóng: nền mờ đi, khung thu nhẹ rồi mới ẩn object.
    /// Gắn lên object gốc của popup; mở popup vẫn bằng SetActive(true) như cũ (hiệu ứng chạy trong
    /// OnEnable), còn đóng thì gọi <see cref="Close"/> thay cho SetActive(false).
    /// Chạy bằng thời gian unscaled nên vẫn hoạt động khi game đang tạm dừng (timeScale = 0).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopupTransition : MonoBehaviour
    {
        private const float OpenFade = 0.16f;
        private const float OpenScaleTime = 0.32f;
        private const float OpenFromScale = 0.82f;
        private const float CloseTime = 0.14f;
        private const float CloseToScale = 0.9f;

        private CanvasGroup group;
        private RectTransform card;
        private PopupFrameFitter fitter;
        private Vector3 cardBaseScale = Vector3.one;
        private Coroutine routine;
        private bool closing;

        /// <summary>Đang chạy hiệu ứng đóng (object vẫn active cho tới khi chạy xong).</summary>
        public bool IsClosing => closing;

        /// <summary>Gắn hiệu ứng lên popup (nếu chưa có). Gọi được cả khi popup đang ẩn.</summary>
        public static PopupTransition Attach(GameObject root, RectTransform card = null)
        {
            if (root == null)
                return null;

            if (!root.TryGetComponent(out PopupTransition transition))
                transition = root.AddComponent<PopupTransition>();
            if (card != null)
                transition.SetCard(card);
            return transition;
        }

        /// <summary>Đóng popup có hiệu ứng; popup không có hiệu ứng thì ẩn ngay như trước.</summary>
        public static void Close(GameObject root)
        {
            if (root == null)
                return;

            if (root.activeInHierarchy && root.TryGetComponent(out PopupTransition transition) && transition.enabled)
                transition.PlayClose();
            else
                root.SetActive(false);
        }

        private void Awake()
        {
            if (!TryGetComponent(out group))
                group = gameObject.AddComponent<CanvasGroup>();
            if (card == null)
                SetCard(FindCard(transform));
        }

        private void SetCard(RectTransform target)
        {
            if (card != null)
                ApplyScale(1f);

            card = target;
            fitter = card != null ? card.GetComponent<PopupFrameFitter>() : null;
            if (card != null && fitter == null)
                cardBaseScale = card.localScale;
        }

        private void OnEnable()
        {
            closing = false;
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(OpenRoutine());
        }

        private void OnDisable()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            closing = false;
            // Trả về trạng thái hiển thị bình thường để lần bật sau (hoặc code khác) không bị kẹt mờ.
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }
            ApplyScale(1f);
        }

        private void PlayClose()
        {
            if (closing)
                return;

            closing = true;
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(CloseRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            group.blocksRaycasts = true;
            group.alpha = 0f;
            ApplyScale(OpenFromScale);

            float total = Mathf.Max(OpenFade, OpenScaleTime);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Clamp01(t / OpenFade);
                ApplyScale(Mathf.LerpUnclamped(OpenFromScale, 1f, EaseOutBack(Mathf.Clamp01(t / OpenScaleTime))));
                yield return null;
            }

            group.alpha = 1f;
            ApplyScale(1f);
            routine = null;
        }

        private IEnumerator CloseRoutine()
        {
            // Không nhận bấm nữa trong lúc đang đóng (tránh bấm đúp vào nền/nút).
            group.blocksRaycasts = false;
            float startAlpha = group.alpha;
            for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
            {
                float k = t / CloseTime;
                group.alpha = Mathf.Lerp(startAlpha, 0f, k);
                ApplyScale(Mathf.Lerp(1f, CloseToScale, k * k));
                yield return null;
            }

            routine = null;
            closing = false;
            gameObject.SetActive(false); // OnDisable trả alpha/scale về mặc định.
        }

        private void ApplyScale(float k)
        {
            if (card == null)
                return;

            if (fitter != null)
                fitter.AnimationScale = k;
            else
                card.localScale = cardBaseScale * k;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>
        /// Khung của popup: con trực tiếp có <see cref="PopupFrameFitter"/>, hoặc con có tên kiểu
        /// Frame/Card/Box/Dialog/Content/Window. Không tìm thấy thì chỉ mờ dần, không phóng to.
        /// </summary>
        private static RectTransform FindCard(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.GetComponent<PopupFrameFitter>() != null)
                    return (RectTransform)child;
            }

            string[] hints = { "Frame", "Card", "Box", "Dialog", "Content", "Window" };
            foreach (Transform child in root)
            {
                foreach (string hint in hints)
                {
                    if (child.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return child as RectTransform;
                }
            }
            return null;
        }
    }
}
