using UnityEngine;

/// <summary>
/// Bộ command cho điều khiển phi thuyền (PATTERN: Command — phần Concrete Command).
///
/// Tách "ý định người chơi" khỏi "controller thực hiện". Nhờ vậy sau này muốn thêm
/// ghi/diễn lại (replay) hoặc key-binding cấu hình được, chỉ cần phát command khác,
/// không sửa PlayerController.
///
/// Lưu ý: chỉ chứa command di chuyển/phím. Việc bắn là tự động nên không cần command riêng.
/// </summary>
public static class PlayerCommands
{
    /// <summary>Di chuyển tới một vị trí world (touch/drag, joystick...).</summary>
    public sealed class MoveToWorldPosition : CommandBase
    {
        private readonly PlayerController _player;
        private readonly Vector3 _worldPosition;

        /// <summary>Nhận player đích và vị trí world cần di chuyển tới (được "đóng gói" vào command).</summary>
        public MoveToWorldPosition(PlayerController player, Vector3 worldPosition)
        {
            _player = player;
            _worldPosition = worldPosition;
        }

        public override string Name => "MoveToWorldPosition";

        /// <summary>
        /// Thực thi: bảo player đi tới vị trí đã lưu.
        /// Kiểm tra null để command cũ không gọi vào player đã bị hủy.
        /// </summary>
        public override void Execute()
        {
            if (_player != null)
                _player.MoveTo(_worldPosition);
        }
    }

    /// <summary>Dịch chuyển tương đối theo vector đã chuẩn hoá (bàn phím: WASD / mũi tên).</summary>
    public sealed class MoveByAxis : CommandBase
    {
        private readonly PlayerController _player;
        private readonly Vector2 _axis;
        private readonly float _deltaTime;

        /// <summary>Nhận player đích, hướng di chuyển (axis) và deltaTime của frame ghi command.</summary>
        public MoveByAxis(PlayerController player, Vector2 axis, float deltaTime)
        {
            _player = player;
            _axis = axis;
            _deltaTime = deltaTime;
        }

        public override string Name => "MoveByAxis";

        /// <summary>
        /// Thực thi: cho player dịch chuyển theo hướng và deltaTime đã lưu
        /// (kết quả giống hệt lúc ghi command, hỗ trợ replay).
        /// </summary>
        public override void Execute()
        {
            if (_player != null)
                _player.MoveBy(_axis, _deltaTime);
        }
    }
}
