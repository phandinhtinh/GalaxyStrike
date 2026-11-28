using System;
using System.Collections.Generic;

namespace GalaxyStrike.Data
{
    /// <summary>
    /// Lịch quà đăng nhập 7 ngày. Nhận liên tục mỗi ngày thì lên ngày kế tiếp; bỏ lỡ một ngày
    /// (hoặc đã nhận xong ngày 7) thì quay lại ngày 1. Ngày 7 là phần thưởng lớn nhất.
    /// </summary>
    public static class DailyLoginRewards
    {
        public readonly struct Day
        {
            public int Coins { get; }
            public int Gems { get; }

            public Day(int coins, int gems)
            {
                Coins = coins;
                Gems = gems;
            }
        }

        public static IReadOnlyList<Day> Days { get; } = Array.AsReadOnly(new[]
        {
            new Day(300, 5),
            new Day(400, 5),
            new Day(500, 10),
            new Day(600, 10),
            new Day(800, 15),
            new Day(1000, 20),
            new Day(2000, 50)
        });

        public static int Count => Days.Count;
    }
}
