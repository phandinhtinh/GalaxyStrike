using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Giới hạn màn hình: kiểm tra trong khung hình và xử lý quái bay ra khỏi màn. (phần của <see cref="Enemy"/>)</summary>
public partial class Enemy
{
    /// <summary>Đã ra khỏi mép dưới màn hình chưa (dùng cho quái lao xuống).</summary>
    private bool IsBelowScreen(float margin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position.y < -8f;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        return viewport.y < -margin;
    }

    /// <summary>Buộc quái rời màn ngay: tính vào tiến trình màn rồi tự huỷ.</summary>
    private void ForceEscape()
    {
        if (hasExitedScreen)
            return;

        hasExitedScreen = true;
        NotifyEscaped();
        Despawn();
    }

    /// <summary>
    /// Quái có đang nằm trong vùng nhìn của camera không (có tính margin nhỏ).
    /// Dùng để không bắn và không tính va chạm khi còn ở ngoài màn.
    /// </summary>
    private bool IsVisibleOnScreen()
    {        Camera cam = Camera.main;
        if (cam == null)
            return true;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        if (viewport.z < 0f)
            return false;

        const float margin = 0.02f;
        return viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    // =====================================================================
    // RỜI MÀN HÌNH
    // =====================================================================

    /// <summary>
    /// Kiểm tra quái đã bay ra khỏi mép màn (trái/phải/trên/dưới, có biên rộng tay).
    /// Nếu có thì đánh dấu đã rời, tính vào tiến trình màn rồi tự huỷ.
    /// Boss và trụ pháo đã neo thì bỏ qua.
    /// </summary>
    private void CheckScreenExit()
    {
        // Boss và trụ pháo neo lại thì không bao giờ "rời màn".
        if (formation == FormationType.Boss || hasExitedScreen)
            return;
        if (formation == FormationType.DropAnchor && hasAnchored)
            return;

        if (elapsed < 0.35f)
            return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 viewport = cam.WorldToViewportPoint(transform.position);
            bool outside = viewport.z > 0f &&
                           (viewport.y < -0.15f || viewport.y > 1.25f ||
                            viewport.x < -0.2f || viewport.x > 1.2f);

            if (outside)
            {
                hasExitedScreen = true;
                NotifyEscaped();
                Despawn();
            }
            return;
        }

        if (transform.position.y < -8f || transform.position.y > 12f || Mathf.Abs(transform.position.x) > 9f)
        {
            hasExitedScreen = true;
            NotifyEscaped();
            Despawn();
        }
    }

    /// <summary>
    /// Xử lý khi quái rời màn (không bị giết): tắt collider, bắn event OnEnemyDied
    /// để spawner tiếp tục wave.
    /// </summary>
    private void NotifyEscaped()
    {
        if (isDead)
            return;

        isDead = true;
        if (cachedCollider != null)
            cachedCollider.enabled = false;

        OnEnemyDied?.Invoke(this);
    }
}
