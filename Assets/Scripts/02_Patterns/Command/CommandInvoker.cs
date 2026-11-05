using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nơi nhận và thực thi command theo hàng đợi (PATTERN: Command — phần Invoker).
///
/// Ghi lại các command đã chạy để có thể replay / undo. Chỉ lưu <see cref="historyLimit"/>
/// command gần nhất để không phình bộ nhớ trên mobile.
/// </summary>
public sealed class CommandInvoker
{
    private readonly Queue<ICommand> pending = new Queue<ICommand>(16);
    private readonly List<ICommand> history = new List<ICommand>(64);

    /// <summary>
    /// Khởi tạo Invoker với giới hạn số command lưu trong lịch sử.
    /// Giá trị âm được kẹp về 0 (0 = không ghi lịch sử).
    /// </summary>
    public CommandInvoker(int historyLimit = 64)
    {
        HistoryLimit = Mathf.Max(0, historyLimit);
    }

    public int HistoryLimit { get; }

    /// <summary>Thêm vào hàng đợi, thực thi ở lần <see cref="Flush"/> kế tiếp.</summary>
    public void Enqueue(ICommand command)
    {
        if (command != null)
            pending.Enqueue(command);
    }

    /// <summary>Thực thi ngay và ghi lịch sử.</summary>
    public void Execute(ICommand command)
    {
        if (command == null)
            return;

        command.Execute();
        Remember(command);
    }

    /// <summary>Thực thi mọi command đang chờ (gọi ở đầu mỗi frame).</summary>
    public void Flush()
    {
        while (pending.Count > 0)
            Execute(pending.Dequeue());
    }

    /// <summary>Hoàn tác command gần nhất.</summary>
    public bool UndoLast()
    {
        if (history.Count == 0)
            return false;

        int last = history.Count - 1;
        ICommand command = history[last];
        history.RemoveAt(last);
        command.Undo();
        return true;
    }

    /// <summary>Danh sách command đã chạy (bản sao, an toàn để duyệt khi replay).</summary>
    public IReadOnlyList<ICommand> History => history;

    /// <summary>
    /// Xóa toàn bộ lịch sử command (không ảnh hưởng command đang chờ trong hàng đợi).
    /// </summary>
    public void ClearHistory()
    {
        history.Clear();
    }

    /// <summary>
    /// Ghi command vào lịch sử sau khi đã thực thi. Nếu vượt giới hạn
    /// thì xóa command CŨ NHẤT để bộ nhớ không phình trên mobile.
    /// </summary>
    private void Remember(ICommand command)
    {
        if (HistoryLimit == 0)
            return;

        history.Add(command);

        while (history.Count > HistoryLimit)
            history.RemoveAt(0);
    }
}
