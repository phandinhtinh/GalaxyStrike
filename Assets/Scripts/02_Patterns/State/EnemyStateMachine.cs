using UnityEngine;

/// <summary>
/// Máy trạng thái của quái (PATTERN: State Machine).
///
/// Chuyển từ khối <c>switch (formation)</c> lớn trong Enemy sang các state object.
/// Mỗi state nắm quyền điều khiển vị trí; máy này chỉ điều phối và chuyển đổi.
///
/// An toàn khi chuyển đổi: <see cref="ChangeState"/> có thể gọi từ bên trong Tick()
/// (state hiện tại tự yêu cầu chuyển) — máy hoãn việc chuyển tới cuối Tick để không
/// phá vỡ vòng thực thi.
/// </summary>
public sealed class EnemyStateMachine
{
    private IEnemyState _current;
    private IEnemyState _pending;
    private bool _isTicking;
    private bool _initialized;

    public string CurrentName => _current != null ? _current.Name : "(none)";
    public IEnemyState Current => _current;

    /// <summary>Đặt state ban đầu. Khi đã Tick lần nào thì phải qua <see cref="ChangeState"/>.</summary>
    public void SetInitialState(IEnemyState state, EnemyContext ctx)
    {
        _current = state;
        _pending = null;
        _initialized = true;

        if (_current != null)
            _current.Enter(this, ctx);
    }

    /// <summary>Yêu cầu chuyển state. Không tạo state mới nếu trùng state hiện tại.</summary>
    public void ChangeState(IEnemyState next, EnemyContext ctx)
    {
        if (next == null || ReferenceEquals(next, _current))
            return;

        if (_isTicking)
        {
            _pending = next;
            return;
        }

        ApplyState(next, ctx);
    }

    /// <summary>Chạy mỗi frame: tick state hiện tại rồi áp chuyển đổi đang chờ.</summary>
    public void Tick(EnemyContext ctx, float deltaTime)
    {
        if (!_initialized || _current == null)
            return;

        _isTicking = true;
        _current.Tick(this, ctx, deltaTime);
        _isTicking = false;

        if (_pending != null)
        {
            IEnemyState next = _pending;
            _pending = null;
            ApplyState(next, ctx);
        }
    }

    private void ApplyState(IEnemyState next, EnemyContext ctx)
    {
        _current?.Exit(this, ctx);
        _current = next;
        _current?.Enter(this, ctx);
    }
}
