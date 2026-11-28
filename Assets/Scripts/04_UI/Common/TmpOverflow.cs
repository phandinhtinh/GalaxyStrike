using TMPro;

namespace GalaxyStrike.UI
{
    /// <summary>
    /// TextMeshPro (ugui 2.x) chỉ tìm ký tự "…" trong typeface đậm/nghiêng riêng của font khi chữ
    /// in đậm/nghiêng; font không có typeface đó (vd LiberationSans SDF) thì TMP tự chuyển sang
    /// Truncate và ghi cảnh báo. Dùng hàm này thay cho gán Ellipsis trực tiếp.
    /// </summary>
    public static class TmpOverflow
    {
        public static void SetEllipsisSafe(TMP_Text text)
        {
            if (text == null)
                return;

            bool styled = (text.fontStyle & (FontStyles.Bold | FontStyles.Italic)) != 0;
            text.overflowMode = styled ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
        }
    }
}
