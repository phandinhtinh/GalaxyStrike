using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyStrike.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int lastScreenSize = new Vector2Int(0, 0);

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            // Tự động cập nhật nếu thay đổi độ phân giải hoặc xoay màn hình trên Simulator
            if (lastSafeArea != Screen.safeArea || 
                lastScreenSize.x != Screen.width || 
                lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;

            // Kiểm tra tránh lỗi chia cho 0 khi khởi chạy
            if (Screen.width == 0 || Screen.height == 0) return;

            // Lưu trạng thái màn hình
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            // Chuyển đổi tọa độ Pixel sang tỉ lệ Anchor (0.0 -> 1.0)
            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = safeArea.position + safeArea.size;

            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;

            // 1. Áp dụng Anchor mới
            rectTransform.anchorMin = minAnchor;
            rectTransform.anchorMax = maxAnchor;

            // 2. [ĐIỂM SỬA QUAN TRỌNG] Reset Offset về 0 để Container khớp hoàn toàn theo Anchor
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}