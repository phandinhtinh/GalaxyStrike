/// <summary>
/// Một hành động đóng gói thành object (PATTERN: Command).
///
/// Lợi ích trong game này:
///  - Đổi phím/cử chỉ không phải sửa PlayerController.
///  - Ghi/diễn lại input (replay) bằng cách phát lại danh sách command.
///  - Dễ viết unit test cho từng hành động riêng.
/// </summary>
public interface ICommand
{
    /// <summary>Tên để debug / hiển thị trong replay log.</summary>
    string Name { get; }

    /// <summary>Thực thi hành động.</summary>
    void Execute();

    /// <summary>Hoàn tác (chỉ hỗ trợ với command có thể đảo ngược, ví dụ Undo nâng cấp).</summary>
    void Undo();
}

/// <summary>Command không cần hoàn tác — lớp đệm để tránh viết lại Undo rỗng.</summary>
public abstract class CommandBase : ICommand
{
    public abstract string Name { get; }
    public abstract void Execute();
    public virtual void Undo() { }
}
