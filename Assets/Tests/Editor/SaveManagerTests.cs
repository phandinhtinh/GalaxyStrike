using System;
using System.Collections.Generic;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using NUnit.Framework;
using UnityEngine;

namespace GalaxyStrike.Tests
{
    /// <summary>
    /// Test EditMode cho <see cref="SaveManager"/>: năng lượng, tiền tệ, lưu/nạp, chống sửa save,
    /// tiến trình chiến dịch, IAP, quà đăng nhập và nhiệm vụ.
    ///
    /// Cô lập: mọi key PlayerPrefs dùng tiền tố riêng (<see cref="SaveManager.KeyPrefix"/>) nên
    /// không đụng save thật trong Editor; đồng hồ giả (<see cref="SaveManager.ClockOverride"/>)
    /// cho phép tua thời gian. Chạy bằng Window > General > Test Runner > EditMode.
    /// </summary>
    public sealed class SaveManagerTests
    {
        private const string Prefix = "GalaxyStrike.Tests.";
        private const string SaveKey = "GalaxyStrike_SaveV2";

        // Thứ Tư 25/11/2026 10:00 UTC (tuần bắt đầu từ thứ Hai 23/11).
        private static readonly DateTime Start = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            SaveManager.KeyPrefix = Prefix;
            now = Start;
            SaveManager.ClockOverride = () => now;
            DeleteTestKeys();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in createdObjects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            createdObjects.Clear();

            DeleteTestKeys();
            SaveManager.KeyPrefix = string.Empty;
            SaveManager.ClockOverride = null;
        }

        /// <summary>Tạo SaveManager mới. Hai lần gọi = hai lần mở game, cùng đọc một save.</summary>
        private SaveManager CreateManager()
        {
            var go = new GameObject("SaveManager (test)") { hideFlags = HideFlags.HideAndDontSave };
            createdObjects.Add(go);
            return go.AddComponent<SaveManager>();
        }

        private static void DeleteTestKeys()
        {
            PlayerPrefs.DeleteKey(Prefix + SaveKey);
            PlayerPrefs.DeleteKey(Prefix + "GalaxyStrike_MigratedV2");
            PlayerPrefs.DeleteKey(Prefix + "GalaxyStrike_Coins");
            PlayerPrefs.DeleteKey(Prefix + "SelectedShipIndex");
            PlayerPrefs.DeleteKey(Prefix + LevelManager.LegacyStageKey);
            for (int i = 0; i < 8; i++)
                PlayerPrefs.DeleteKey(Prefix + "Ship_Unlocked_" + i);
            PlayerPrefs.Save();
        }

        // =====================================================================
        // SAVE MỚI
        // =====================================================================

        [Test]
        public void NewSave_StartsWithDefaults()
        {
            SaveManager save = CreateManager();

            Assert.AreEqual(500, save.Coins);
            Assert.AreEqual(20, save.Gems);
            Assert.AreEqual(save.MaxEnergy, save.Energy);
            Assert.IsTrue(save.Data.HasShip(0));
            Assert.AreEqual(0, save.SelectedShipIndex);
            Assert.AreEqual(1, save.CampaignStage);
            Assert.AreEqual(0, save.HighestLevel);
        }

        // =====================================================================
        // NĂNG LƯỢNG
        // =====================================================================

        [Test]
        public void TryConsumeEnergy_DeductsCostPerRun()
        {
            SaveManager save = CreateManager();

            Assert.IsTrue(save.TryConsumeEnergy());

            Assert.AreEqual(save.MaxEnergy - save.EnergyCostPerRun, save.Energy);
        }

        [Test]
        public void TryConsumeEnergy_FailsWhenNotEnough_AndKeepsBalance()
        {
            SaveManager save = CreateManager();
            int runs = save.MaxEnergy / save.EnergyCostPerRun;
            for (int i = 0; i < runs; i++)
                Assert.IsTrue(save.TryConsumeEnergy(), "run " + i);

            int before = save.Energy;
            Assert.IsFalse(save.TryConsumeEnergy());
            Assert.AreEqual(before, save.Energy);
            Assert.GreaterOrEqual(save.Energy, 0);
        }

        [Test]
        public void EnergyRefill_AddsOnePerInterval_AndKeepsRemainder()
        {
            SaveManager save = CreateManager();
            save.TryConsumeEnergy();
            int afterRun = save.Energy;
            int interval = save.EnergyRefillMinutes;

            now = Start.AddMinutes(interval * 2 + 1);
            save.ApplyEnergyRefill();

            Assert.AreEqual(afterRun + 2, save.Energy);
            // Một phút đã trôi trong nhịp thứ ba -> còn (interval - 1) phút.
            Assert.AreEqual((interval - 1) * 60, save.SecondsUntilNextEnergy());
        }

        [Test]
        public void EnergyRefill_IsCappedAtMax()
        {
            SaveManager save = CreateManager();
            save.TryConsumeEnergy();

            now = Start.AddDays(2);
            save.ApplyEnergyRefill();

            Assert.AreEqual(save.MaxEnergy, save.Energy);
            Assert.AreEqual(0, save.SecondsUntilNextEnergy());
        }

        [Test]
        public void EnergyRefill_CountsTimeWhileGameWasClosed()
        {
            SaveManager first = CreateManager();
            first.TryConsumeEnergy();
            int afterRun = first.Energy;

            now = Start.AddMinutes(first.EnergyRefillMinutes);
            SaveManager reopened = CreateManager();

            Assert.AreEqual(afterRun + 1, reopened.Energy);
        }

        // =====================================================================
        // TIỀN TỆ
        // =====================================================================

        [Test]
        public void TrySpendCurrency_FailsWithoutChangingBalance()
        {
            SaveManager save = CreateManager();

            Assert.IsFalse(save.TrySpendCurrency(CurrencyType.Gold, save.Coins + 1));
            Assert.IsFalse(save.TrySpendCurrency(CurrencyType.Gem, save.Gems + 1));

            Assert.AreEqual(500, save.Coins);
            Assert.AreEqual(20, save.Gems);
        }

        [Test]
        public void TrySpendCurrency_DeductsFromMatchingCurrency()
        {
            SaveManager save = CreateManager();

            Assert.IsTrue(save.TrySpendCurrency(CurrencyType.Gold, 200));
            Assert.IsTrue(save.TrySpendCurrency(CurrencyType.Gem, 5));

            Assert.AreEqual(300, save.Coins);
            Assert.AreEqual(15, save.Gems);
        }

        [Test]
        public void AddCoins_NeverGoesBelowZero()
        {
            SaveManager save = CreateManager();

            save.AddCoins(-10000);

            Assert.AreEqual(0, save.Coins);
        }

        // =====================================================================
        // LƯU / NẠP / CHỐNG SỬA
        // =====================================================================

        [Test]
        public void Data_SurvivesReopeningTheGame()
        {
            SaveManager first = CreateManager();
            first.AddCoins(250);
            first.AddGems(7);
            first.ReportLevelCleared(3);

            SaveManager reopened = CreateManager();

            Assert.AreEqual(750, reopened.Coins);
            Assert.AreEqual(27, reopened.Gems);
            Assert.AreEqual(3, reopened.HighestLevel);
        }

        [Test]
        public void TamperedSave_IsRejected_AndANewSaveIsCreated()
        {
            SaveManager first = CreateManager();
            first.AddCoins(99999);

            // Định dạng: tag.body.signature -> đổi một ký tự trong body làm chữ ký sai.
            string stored = PlayerPrefs.GetString(Prefix + SaveKey);
            string[] parts = stored.Split('.');
            Assert.AreEqual(3, parts.Length, "unexpected save format");
            char[] body = parts[1].ToCharArray();
            body[body.Length / 2] = body[body.Length / 2] == 'A' ? 'B' : 'A';
            PlayerPrefs.SetString(Prefix + SaveKey, parts[0] + "." + new string(body) + "." + parts[2]);

            SaveManager reopened = CreateManager();

            Assert.AreEqual(500, reopened.Coins);
        }

        [Test]
        public void SaveIsWrittenUnderTestPrefix_NotTheRealKey()
        {
            string realSaveBefore = PlayerPrefs.GetString(SaveKey, string.Empty);

            SaveManager save = CreateManager();
            save.AddCoins(1);

            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + SaveKey));
            Assert.AreEqual(realSaveBefore, PlayerPrefs.GetString(SaveKey, string.Empty));
        }

        // =====================================================================
        // TIẾN TRÌNH CHIẾN DỊCH / THÀNH TÍCH
        // =====================================================================

        [Test]
        public void SaveCampaignStage_NeverMovesBackwards()
        {
            SaveManager save = CreateManager();

            save.SaveCampaignStage(12);
            save.SaveCampaignStage(3);

            Assert.AreEqual(12, save.CampaignStage);
        }

        [Test]
        public void ReportLevelCleared_KeepsHighest()
        {
            SaveManager save = CreateManager();

            save.ReportLevelCleared(8);
            save.ReportLevelCleared(2);

            Assert.AreEqual(8, save.HighestLevel);
        }

        [Test]
        public void ReportScore_KeepsBest()
        {
            SaveManager save = CreateManager();

            save.ReportScore(4200);
            save.ReportScore(1000);

            Assert.AreEqual(4200, save.BestScore);
        }

        // =====================================================================
        // IAP
        // =====================================================================

        [Test]
        public void IapPurchase_IsGrantedOncePerTransaction()
        {
            SaveManager save = CreateManager();

            Assert.IsTrue(save.TryApplyIapPurchase("GPA.1234", 0, 650, 0));
            Assert.IsFalse(save.TryApplyIapPurchase("GPA.1234", 0, 650, 0), "re-delivered order");

            Assert.AreEqual(20 + 650, save.Gems);
            Assert.IsTrue(save.IsIapTransactionProcessed("GPA.1234"));
        }

        [Test]
        public void IapPurchase_IsRememberedAfterReopening()
        {
            CreateManager().TryApplyIapPurchase("GPA.5678", 2500, 0, 0);

            SaveManager reopened = CreateManager();

            Assert.IsFalse(reopened.TryApplyIapPurchase("GPA.5678", 2500, 0, 0));
            Assert.AreEqual(500 + 2500, reopened.Coins);
        }

        [Test]
        public void IapPurchase_WithoutTransactionId_IsRejected()
        {
            SaveManager save = CreateManager();

            Assert.IsFalse(save.TryApplyIapPurchase(null, 1000, 0, 0));
            Assert.IsFalse(save.TryApplyIapPurchase(string.Empty, 1000, 0, 0));
            Assert.AreEqual(500, save.Coins);
        }

        // =====================================================================
        // QUÀ ĐĂNG NHẬP
        // =====================================================================

        [Test]
        public void DailyLogin_CanBeClaimedOncePerUtcDay()
        {
            SaveManager save = CreateManager();

            Assert.IsTrue(save.TryClaimDailyLoginReward());
            Assert.IsFalse(save.TryClaimDailyLoginReward());
            Assert.AreEqual(500 + SaveManager.DailyLoginCoins, save.Coins);

            now = Start.Date.AddDays(1).AddMinutes(1);
            Assert.IsFalse(save.IsDailyLoginRewardClaimed);
            Assert.IsTrue(save.TryClaimDailyLoginReward());
            Assert.AreEqual(500 + DailyLoginRewards.Days[0].Coins + DailyLoginRewards.Days[1].Coins, save.Coins);
        }

        [Test]
        public void DailyLogin_ConsecutiveDaysAdvanceThroughSevenDayCycle()
        {
            SaveManager save = CreateManager();
            int expectedCoins = 500, expectedGems = 20;

            for (int day = 0; day < DailyLoginRewards.Count; day++)
            {
                now = Start.Date.AddDays(day).AddHours(9);
                Assert.AreEqual(day, save.DailyLoginDayIndex);
                Assert.AreEqual(day, save.DailyLoginDaysClaimed);
                Assert.IsTrue(save.TryClaimDailyLoginReward());
                Assert.AreEqual(day + 1, save.DailyLoginDaysClaimed);
                expectedCoins += DailyLoginRewards.Days[day].Coins;
                expectedGems += DailyLoginRewards.Days[day].Gems;
            }

            Assert.AreEqual(expectedCoins, save.Coins);
            Assert.AreEqual(expectedGems, save.Gems);

            // Xong ngày 7 -> hôm sau bắt đầu lại từ ngày 1.
            now = Start.Date.AddDays(DailyLoginRewards.Count).AddHours(9);
            Assert.AreEqual(0, save.DailyLoginDayIndex);
            Assert.AreEqual(0, save.DailyLoginDaysClaimed);
        }

        [Test]
        public void DailyLogin_MissingADayResetsToDayOne()
        {
            SaveManager save = CreateManager();
            Assert.IsTrue(save.TryClaimDailyLoginReward());
            now = Start.Date.AddDays(1).AddHours(9);
            Assert.IsTrue(save.TryClaimDailyLoginReward());
            Assert.AreEqual(1, save.DailyLoginDayIndex);

            // Bỏ lỡ một ngày.
            now = Start.Date.AddDays(3).AddHours(9);
            Assert.AreEqual(0, save.DailyLoginDayIndex);

            int coins = save.Coins;
            Assert.IsTrue(save.TryClaimDailyLoginReward());
            Assert.AreEqual(coins + DailyLoginRewards.Days[0].Coins, save.Coins);
        }

        // =====================================================================
        // SỰ KIỆN OnDataChanged
        // =====================================================================

        private sealed class DataListener : MonoBehaviour
        {
            public int calls;
            public void Handle() => calls++;
        }

        [Test]
        public void NotifyChanged_SkipsAndRemovesDestroyedListeners()
        {
            SaveManager save = CreateManager();
            var go = new GameObject("listener (test)") { hideFlags = HideFlags.HideAndDontSave };
            DataListener listener = go.AddComponent<DataListener>();
            save.OnDataChanged += listener.Handle;
            UnityEngine.Object.DestroyImmediate(go);

            // Trước đây handler của UI đã huỷ ném MissingReferenceException ngay trong AddCoins.
            Assert.DoesNotThrow(() => save.AddCoins(10));
            Assert.AreEqual(510, save.Coins);
        }

        [Test]
        public void NotifyChanged_OneFailingListenerDoesNotBreakOthersOrTheCaller()
        {
            SaveManager save = CreateManager();
            int calls = 0;
            save.OnDataChanged += () => throw new InvalidOperationException("listener boom");
            save.OnDataChanged += () => calls++;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("listener boom"));
            Assert.DoesNotThrow(() => save.AddCoins(10));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(510, save.Coins);
        }

        [Test]
        public void BattlePass_TierGrantsGemsAndCapsEnergy()
        {
            SaveManager save = CreateManager();
            save.AddExperience(BattlePassRewards.Tiers[BattlePassRewards.Tiers.Count - 1].RequiredExperience);

            int gemTier = -1, energyTier = -1;
            for (int i = 0; i < BattlePassRewards.Tiers.Count; i++)
            {
                if (gemTier < 0 && BattlePassRewards.Tiers[i].Gems > 0) gemTier = i;
                if (energyTier < 0 && BattlePassRewards.Tiers[i].Energy > 0) energyTier = i;
            }
            Assert.GreaterOrEqual(gemTier, 0);
            Assert.GreaterOrEqual(energyTier, 0);

            int gems = save.Gems;
            Assert.IsTrue(save.TryClaimBattlePassTier(gemTier));
            Assert.AreEqual(gems + BattlePassRewards.Tiers[gemTier].Gems, save.Gems);
            Assert.IsFalse(save.TryClaimBattlePassTier(gemTier));

            // Năng lượng đang đầy -> không vượt quá tối đa.
            Assert.IsTrue(save.TryClaimBattlePassTier(energyTier));
            Assert.AreEqual(save.MaxEnergy, save.Energy);
            Assert.IsTrue(save.IsBattlePassTierClaimed(energyTier));
        }

        [Test]
        public void BattlePass_LockedTierCannotBeClaimed()
        {
            SaveManager save = CreateManager();
            int last = BattlePassRewards.Tiers.Count - 1;
            Assert.IsFalse(save.TryClaimBattlePassTier(last));
            Assert.IsFalse(save.IsBattlePassTierClaimed(last));
        }

        [Test]
        public void DailyLogin_StreakSurvivesReopeningTheGame()
        {
            CreateManager().TryClaimDailyLoginReward();
            now = Start.Date.AddDays(1).AddHours(9);

            SaveManager reopened = CreateManager();
            Assert.AreEqual(1, reopened.DailyLoginDayIndex);
        }

        // =====================================================================
        // NHIỆM VỤ
        // =====================================================================

        private static TaskDefinition Find(IReadOnlyList<TaskDefinition> tasks, string id)
        {
            foreach (TaskDefinition task in tasks)
            {
                if (task.id == id)
                    return task;
            }
            Assert.Fail("task not found: " + id);
            return null;
        }

        [Test]
        public void TaskMetric_CountsTowardsDailyWeeklyAndLifetime()
        {
            SaveManager save = CreateManager();
            TaskDefinition daily = Find(TaskCatalog.Daily, "daily_battles_3");
            TaskDefinition weekly = Find(TaskCatalog.Weekly, "weekly_battles_20");
            TaskDefinition lifetime = Find(TaskCatalog.Achievements, "ach_battles_10");

            save.RecordTaskMetric(TaskMetric.BattlesPlayed, 2);

            Assert.AreEqual(2, save.GetTaskProgress(daily));
            Assert.AreEqual(2, save.GetTaskProgress(weekly));
            Assert.AreEqual(2, save.GetTaskProgress(lifetime));
        }

        [Test]
        public void DailyTasks_ResetNextDay_WeeklyKeepsUntilMonday()
        {
            SaveManager save = CreateManager();
            TaskDefinition daily = Find(TaskCatalog.Daily, "daily_kills_50");
            TaskDefinition weekly = Find(TaskCatalog.Weekly, "weekly_kills_500");
            save.RecordTaskMetric(TaskMetric.EnemiesDestroyed, 40);

            now = new DateTime(2026, 11, 29, 23, 0, 0, DateTimeKind.Utc); // Chủ nhật, cùng tuần
            Assert.AreEqual(0, save.GetTaskProgress(daily));
            Assert.AreEqual(40, save.GetTaskProgress(weekly));

            now = new DateTime(2026, 11, 30, 0, 30, 0, DateTimeKind.Utc); // Thứ Hai, tuần mới
            Assert.AreEqual(0, save.GetTaskProgress(weekly));
        }

        [Test]
        public void TryClaimTask_RequiresCompletion_AndGrantsOnce()
        {
            SaveManager save = CreateManager();
            TaskDefinition task = Find(TaskCatalog.Daily, "daily_battles_3");

            save.RecordTaskMetric(TaskMetric.BattlesPlayed, 2);
            Assert.IsFalse(save.TryClaimTask(task), "not complete yet");

            save.RecordTaskMetric(TaskMetric.BattlesPlayed);
            Assert.IsTrue(save.TryClaimTask(task));
            Assert.IsFalse(save.TryClaimTask(task), "already claimed");

            Assert.AreEqual(500 + task.rewardOne.amount, save.Coins);
            Assert.AreEqual(20 + task.rewardTwo.amount, save.Gems);
            Assert.IsTrue(save.IsTaskClaimed(task));
        }

        [Test]
        public void ClaimedDailyTask_BecomesAvailableAgainNextDay()
        {
            SaveManager save = CreateManager();
            TaskDefinition task = Find(TaskCatalog.Daily, "daily_stages_1");
            save.RecordTaskMetric(TaskMetric.StagesCleared);
            Assert.IsTrue(save.TryClaimTask(task));

            now = Start.AddDays(1);
            Assert.IsFalse(save.IsTaskClaimed(task));
            Assert.IsFalse(save.IsTaskComplete(task));

            save.RecordTaskMetric(TaskMetric.StagesCleared);
            Assert.IsTrue(save.TryClaimTask(task));
        }

        [Test]
        public void Achievement_ClaimedOnce_EvenAfterReset()
        {
            SaveManager save = CreateManager();
            TaskDefinition task = Find(TaskCatalog.Achievements, "ach_battles_10");
            save.RecordTaskMetric(TaskMetric.BattlesPlayed, 10);
            Assert.IsTrue(save.TryClaimTask(task));

            now = Start.AddDays(14);
            Assert.IsTrue(save.IsTaskClaimed(task));
            Assert.IsFalse(save.TryClaimTask(task));
        }

        [Test]
        public void LoginDay_IsCountedOncePerDay()
        {
            SaveManager save = CreateManager();
            TaskDefinition weekly = Find(TaskCatalog.Weekly, "weekly_login_5");

            save.RecordLoginIfNewDay();
            save.RecordLoginIfNewDay();
            Assert.AreEqual(1, save.GetTaskProgress(weekly));

            now = Start.AddDays(1);
            save.RecordLoginIfNewDay();
            Assert.AreEqual(2, save.GetTaskProgress(weekly));
        }

        [Test]
        public void TaskProgress_IsSavedAcrossSessions()
        {
            TaskDefinition task = Find(TaskCatalog.Weekly, "weekly_powerups_30");
            SaveManager first = CreateManager();
            first.RecordTaskMetric(TaskMetric.PowerUpsUsed, 7);
            first.FlushProgress();

            SaveManager reopened = CreateManager();

            Assert.AreEqual(7, reopened.GetTaskProgress(task));
        }

        [Test]
        public void HighestStageAchievement_ReadsCampaignProgress()
        {
            SaveManager save = CreateManager();
            TaskDefinition task = Find(TaskCatalog.Achievements, "ach_stage_10");

            save.ReportLevelCleared(10);

            Assert.IsTrue(save.IsTaskComplete(task));
        }
    }
}
