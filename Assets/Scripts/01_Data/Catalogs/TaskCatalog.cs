using System;
using System.Collections.Generic;

namespace GalaxyStrike.Data
{
    /// <summary>Chu kỳ reset của nhiệm vụ.</summary>
    public enum TaskPeriod
    {
        /// <summary>Reset lúc 00:00 UTC mỗi ngày (cùng mốc với quà đăng nhập).</summary>
        Daily = 0,
        /// <summary>Reset lúc 00:00 UTC thứ Hai hằng tuần.</summary>
        Weekly = 1,
        /// <summary>Thành tựu: không reset, nhận một lần.</summary>
        Achievement = 2
    }

    /// <summary>
    /// Chỉ số gameplay mà nhiệm vụ theo dõi. Giá trị enum là chỉ số trong mảng thống kê
    /// của SaveData: CHỈ thêm vào cuối, không đổi thứ tự.
    /// </summary>
    public enum TaskMetric
    {
        /// <summary>Số trận đã kết thúc (thắng hoặc thua).</summary>
        BattlesPlayed = 0,
        EnemiesDestroyed = 1,
        BossesDefeated = 2,
        /// <summary>Số lần dùng Skill (Pulse/Aegis) hoặc Booster.</summary>
        PowerUpsUsed = 3,
        StagesCleared = 4,
        /// <summary>Số ngày (UTC) có mở game.</summary>
        LoginDays = 5,
        /// <summary>Màn chiến dịch cao nhất đã qua (đọc thẳng từ save, không đếm).</summary>
        HighestStage = 6
    }

    public enum TaskRewardType
    {
        None = 0,
        Coins = 1,
        Gems = 2,
        Energy = 3
    }

    [Serializable]
    public readonly struct TaskReward
    {
        public readonly TaskRewardType type;
        public readonly int amount;

        public TaskReward(TaskRewardType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }

        public static TaskReward Coins(int amount) => new TaskReward(TaskRewardType.Coins, amount);
        public static TaskReward Gems(int amount) => new TaskReward(TaskRewardType.Gems, amount);
        public static TaskReward Energy(int amount) => new TaskReward(TaskRewardType.Energy, amount);
    }

    /// <summary>Một nhiệm vụ: đạt <see cref="target"/> của <see cref="metric"/> trong chu kỳ thì nhận thưởng.</summary>
    public sealed class TaskDefinition
    {
        /// <summary>ID cố định dùng để lưu trạng thái đã nhận. KHÔNG đổi sau khi phát hành.</summary>
        public readonly string id;
        public readonly TaskPeriod period;
        public readonly TaskMetric metric;
        public readonly int target;
        public readonly string title;
        public readonly TaskReward rewardOne;
        public readonly TaskReward rewardTwo;

        /// <summary>
        /// Dòng "Login today": nhận qua quà đăng nhập hằng ngày của SaveManager
        /// (dùng chung với popup Daily Reward, không nhận được 2 lần ở 2 nơi).
        /// </summary>
        public readonly bool isDailyLoginReward;

        public TaskDefinition(string id, TaskPeriod period, TaskMetric metric, int target, string title,
            TaskReward rewardOne, TaskReward rewardTwo, bool isDailyLoginReward = false)
        {
            this.id = id;
            this.period = period;
            this.metric = metric;
            this.target = Math.Max(1, target);
            this.title = title;
            this.rewardOne = rewardOne;
            this.rewardTwo = rewardTwo;
            this.isDailyLoginReward = isDailyLoginReward;
        }
    }

    /// <summary>
    /// Danh mục nhiệm vụ của popup TASK. Mỗi tab giữ đúng 6 dòng theo thứ tự của layout
    /// HomeScene (icon từng dòng lấy từ scene theo cùng chỉ số).
    /// Năng lượng tối đa là 30 nên không thưởng quá 30 Energy một lần.
    /// </summary>
    public static class TaskCatalog
    {
        public const int MetricCount = 7;

        public static readonly IReadOnlyList<TaskDefinition> Daily = new[]
        {
            new TaskDefinition("daily_battles_3", TaskPeriod.Daily, TaskMetric.BattlesPlayed, 3, "Play 3 battles",
                TaskReward.Coins(500), TaskReward.Gems(5)),
            new TaskDefinition("daily_kills_50", TaskPeriod.Daily, TaskMetric.EnemiesDestroyed, 50, "Destroy 50 enemies",
                TaskReward.Coins(800), TaskReward.Energy(10)),
            new TaskDefinition("daily_boss_1", TaskPeriod.Daily, TaskMetric.BossesDefeated, 1, "Defeat 1 Boss",
                TaskReward.Gems(10), TaskReward.Coins(1000)),
            new TaskDefinition("daily_powerups_3", TaskPeriod.Daily, TaskMetric.PowerUpsUsed, 3, "Use 3 Power-ups",
                TaskReward.Coins(600), TaskReward.Energy(10)),
            new TaskDefinition("daily_stages_1", TaskPeriod.Daily, TaskMetric.StagesCleared, 1, "Clear 1 stage",
                TaskReward.Gems(5), TaskReward.Coins(300)),
            new TaskDefinition("daily_login", TaskPeriod.Daily, TaskMetric.LoginDays, 1, "Login today",
                TaskReward.Coins(GalaxyStrike.Save.SaveManager.DailyLoginCoins),
                TaskReward.Gems(GalaxyStrike.Save.SaveManager.DailyLoginGems),
                isDailyLoginReward: true),
        };

        public static readonly IReadOnlyList<TaskDefinition> Weekly = new[]
        {
            new TaskDefinition("weekly_battles_20", TaskPeriod.Weekly, TaskMetric.BattlesPlayed, 20, "Play 20 battles",
                TaskReward.Coins(2500), TaskReward.Gems(20)),
            new TaskDefinition("weekly_kills_500", TaskPeriod.Weekly, TaskMetric.EnemiesDestroyed, 500, "Destroy 500 enemies",
                TaskReward.Coins(4000), TaskReward.Energy(30)),
            new TaskDefinition("weekly_boss_5", TaskPeriod.Weekly, TaskMetric.BossesDefeated, 5, "Defeat 5 Bosses",
                TaskReward.Gems(60), TaskReward.Coins(2000)),
            new TaskDefinition("weekly_powerups_30", TaskPeriod.Weekly, TaskMetric.PowerUpsUsed, 30, "Use 30 Power-ups",
                TaskReward.Coins(3000), TaskReward.Energy(30)),
            new TaskDefinition("weekly_stages_10", TaskPeriod.Weekly, TaskMetric.StagesCleared, 10, "Clear 10 stages",
                TaskReward.Gems(30), TaskReward.Coins(1800)),
            new TaskDefinition("weekly_login_5", TaskPeriod.Weekly, TaskMetric.LoginDays, 5, "Login 5 days",
                TaskReward.Coins(1500), TaskReward.Gems(15)),
        };

        public static readonly IReadOnlyList<TaskDefinition> Achievements = new[]
        {
            new TaskDefinition("ach_battles_10", TaskPeriod.Achievement, TaskMetric.BattlesPlayed, 10, "Complete 10 battles",
                TaskReward.Gems(25), TaskReward.Coins(1500)),
            new TaskDefinition("ach_kills_1000", TaskPeriod.Achievement, TaskMetric.EnemiesDestroyed, 1000, "Destroy 1000 enemies",
                TaskReward.Gems(40), TaskReward.Energy(30)),
            new TaskDefinition("ach_boss_10", TaskPeriod.Achievement, TaskMetric.BossesDefeated, 10, "Defeat 10 Bosses",
                TaskReward.Coins(3000), TaskReward.Gems(50)),
            new TaskDefinition("ach_powerups_100", TaskPeriod.Achievement, TaskMetric.PowerUpsUsed, 100, "Use 100 Power-ups",
                TaskReward.Coins(5000), TaskReward.Energy(30)),
            new TaskDefinition("ach_login_7", TaskPeriod.Achievement, TaskMetric.LoginDays, 7, "Login for 7 days",
                TaskReward.Gems(35), TaskReward.Coins(2000)),
            new TaskDefinition("ach_stage_10", TaskPeriod.Achievement, TaskMetric.HighestStage, 10, "Reach stage 10",
                TaskReward.Coins(1000), TaskReward.Gems(15)),
        };

        /// <summary>Danh sách theo thứ tự tab của popup: Daily, Weekly, Achievements.</summary>
        public static IReadOnlyList<TaskDefinition> ForTab(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: return Daily;
                case 1: return Weekly;
                default: return Achievements;
            }
        }
    }
}
