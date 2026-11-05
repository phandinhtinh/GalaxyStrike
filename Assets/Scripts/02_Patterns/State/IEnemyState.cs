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
    /// <summary>Transform của con quái (state di chuyển trực tiếp Transform này).</summary>
    public Transform body;

    /// <summary>Vị trí quái xuất hiện ban đầu (mốc để tính quỹ đạo bay).</summary>
    public Vector3 spawnPosition;

    /// <summary>Điểm neo của đội hình (formation) mà quái bay tới rồi giữ vị trí.</summary>
    public Vector3 formationAnchor;

    /// <summary>Thời gian (giây) đã trôi qua kể từ khi vào state hiện tại.</summary>
    public float elapsed;

    /// <summary>Tốc độ bay xuống (đơn vị/giây).</summary>
    public float descentSpeed = 2f;

    /// <summary>Biên độ dao động ngang (snake/zigzag).</summary>
    public float amplitude = 1.5f;

    /// <summary>Tần số dao động (càng lớn dao động càng nhanh).</summary>
    public float frequency = 1.5f;

    /// <summary>Lệch pha để các con trong cùng đội hình không dao động giống hệt nhau.</summary>
    public float phaseOffset;

    /// <summary>Hướng bay ngang khi flank (1 = phải, -1 = trái).</summary>
    public float flankDirection = 1f;

    /// <summary>Chỉ số của quái trong hàng (dùng để xếp hình chữ V / wall).</summary>
    public int indexInRow;

    /// <summary>Khoảng cách giữa các con trong formation Snake.</summary>
    public float snakeSpacing = 0.85f;

    /// <summary>Độ cao mà quái dừng lại để "neo" (Drop & Anchor).</summary>
    public float anchorY = 1.6f;

    /// <summary>Thời gian giữ vị trí neo trước khi đi tiếp.</summary>
    public float anchorHoldTime;

    /// <summary>Đã neo xong chưa (tránh neo lặp lại).</summary>
    public bool hasAnchored;

    /// <summary>Thời gian quái lượn vòng trước khi lao vào player (Kamikaze).</summary>
    public float kamikazeOrbitTime = 1.4f;

    /// <summary>Đã khoá mục tiêu (vị trí player) để lao tới chưa.</summary>
    public bool isKamikazeLocked;

    /// <summary>Đã lao vượt qua player chưa (để biết lúc rời state).</summary>
    public bool hasDivedPast;

    /// <summary>Vị trí player đã khoá lúc bắt đầu lao.</summary>
    public Vector3 lockedTarget;

    /// <summary>Transform player (được lấy/cache trong ResolvePlayerPosition).</summary>
    public Transform playerTarget;

    /// <summary>Quãng đường đã bay xuống của formation Wall (dùng tính vị trí).</summary>
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
