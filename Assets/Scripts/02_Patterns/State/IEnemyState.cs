using UnityEngine;

/// <summary>
/// Một trạng thái hành vi của quái (PATTERN: State / State Machine).
///
/// Mỗi trạng thái chỉ lo MỘT kiểu bay/chờ. <see cref="EnemyStateMachine"/> giữ trạng thái hiện
/// tại và chuyển trạng thái; <see cref="Enemy"/> chỉ hỏi máy trạng thái "giờ mày ở đâu".
/// </summary>
public interface IEnemyState
{
    /// <summary>Tên để debug/log (ví dụ "Snake", "Kamikaze").</summary>
    string Name { get; }

    /// <summary>Gọi 1 lần khi bắt đầu vào trạng thái.</summary>
    void Enter(EnemyStateMachine machine, EnemyContext ctx);

    /// <summary>Gọi mỗi frame khi đang ở trạng thái này.</summary>
    void Tick(EnemyStateMachine machine, EnemyContext ctx, float deltaTime);

    /// <summary>Gọi 1 lần khi rời trạng thái.</summary>
    void Exit(EnemyStateMachine machine, EnemyContext ctx);
}

/// <summary>
/// Dữ liệu dùng chung cho mọi trạng thái (tách khỏi MonoBehaviour để dễ test).
///
/// Enemy truyền <c>transform</c> của chính nó vào đây — trạng thái không cần biết đến class Enemy.
/// </summary>
public sealed class EnemyContext
{
    public Transform body;
    public Vector3 spawnPosition;
    public Vector3 formationAnchor;
    public float elapsed;
    public float descentSpeed = 2f;
    public float amplitude = 1.5f;
    public float frequency = 1.5f;
    public float phaseOffset;
    public float flankDirection = 1f;
    public int indexInRow;
    public float snakeSpacing = 0.85f;
    public float anchorY = 1.6f;
    public float anchorHoldTime;
    public bool hasAnchored;
    public float kamikazeOrbitTime = 1.4f;
    public bool isKamikazeLocked;
    public bool hasDivedPast;
    public Vector3 lockedTarget;
    public Transform playerTarget;
    public float wallDescent;

    /// <summary>Lấy vị trí player (có cache) — trạng thái Kamikaze dùng.</summary>
    public Vector3 ResolvePlayerPosition()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerTarget = player != null ? player.transform : null;
        }

        return playerTarget != null ? playerTarget.position : new Vector3(0f, -4f, 0f);
    }
}
