using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.Save;
using hp55games.Mobile.Core.Timing;
using hp55games.Blockout.Achievements;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class BlockoutAchievementServiceTests
    {
        private sealed class FakeSaveService : ISaveService
        {
            public SaveData Data { get; } = new SaveData();
            public int SaveCallCount { get; private set; }
            public void Load() { }
            public void Save() => SaveCallCount++;
        }

        private sealed class FakeTimeService : ITimeService
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            public DateTime LocalNow => UtcNow;
            public double UnscaledGameTime => 0;
            public double MonotonicSeconds => 0;
            public bool IsClockSuspicious => false;

            private readonly Dictionary<string, DateTime> _stamps = new();

            public DateTime GetLastUtc(string key) => _stamps.TryGetValue(key, out var v) ? v : DateTime.MinValue;
            public void SetNowUtc(string key) => _stamps[key] = UtcNow;

            public TimeSpan SinceLast(string key)
            {
                var last = GetLastUtc(key);
                return last == DateTime.MinValue ? TimeSpan.Zero : UtcNow - last;
            }

            public bool HasPassed(string key, TimeSpan threshold) => SinceLast(key) >= threshold;
        }

        // Tracks unlocked SkinIds directly rather than reusing the real BlockoutSkinService, so
        // these tests verify the achievement service's own behavior (which ids it grants, when)
        // independent of BlockoutSkinService's implementation.
        private sealed class FakeSkinService : IBlockoutSkinService
        {
            public HashSet<string> Unlocked { get; } = new();

            // A HashSet alone would hide a double-grant (Add is already idempotent) - this counts
            // every call, so a test can tell "granted once" apart from "granted repeatedly but it
            // happened to be the same id every time".
            public Dictionary<string, int> GrantUnlockCallCount { get; } = new();

            public BlockoutSkin ActiveSkin => null;
            public bool IsUnlocked(BlockoutSkin skin) => skin != null && Unlocked.Contains(skin.SkinId);
            public void SetActiveSkin(string skinId) { }
            public UnlockSkinResult TryUnlockSkin(string skinId) => UnlockSkinResult.Success;

            public void GrantUnlock(string skinId)
            {
                Unlocked.Add(skinId);
                GrantUnlockCallCount[skinId] = GrantUnlockCallCount.TryGetValue(skinId, out var count) ? count + 1 : 1;
            }

            public IReadOnlyList<BlockoutSkinShopEntry> GetElementShopEntries() => new List<BlockoutSkinShopEntry>();
        }

        private sealed class FakeConfigCatalogService : IConfigCatalogService
        {
            private readonly List<BlockoutSkin> _skins;
            public FakeConfigCatalogService(List<BlockoutSkin> skins) => _skins = skins;

            public T Get<T>() where T : ScriptableObject, IConfigAsset => throw new NotSupportedException();

            public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset
            {
                if (typeof(T) == typeof(BlockoutSkin)) return (IReadOnlyList<T>)(object)_skins;
                return new List<T>();
            }
        }

        private FakeSaveService _saveService;
        private FakeTimeService _timeService;
        private FakeSkinService _skinService;
        private List<BlockoutSkin> _skins;
        private BlockoutAchievementService _service;

        [SetUp]
        public void SetUp()
        {
            _saveService = new FakeSaveService();
            _timeService = new FakeTimeService();
            _skinService = new FakeSkinService();
            _skins = new List<BlockoutSkin>();

            ServiceRegistry.Register<ISaveService>(_saveService);
            ServiceRegistry.Register<ITimeService>(_timeService);
            ServiceRegistry.Register<IBlockoutSkinService>(_skinService);
            ServiceRegistry.Register<IConfigCatalogService>(new FakeConfigCatalogService(_skins));

            _service = new BlockoutAchievementService();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var skin in _skins) GameObject.DestroyImmediate(skin);
        }

        private BlockoutSkin CreateAchievementSkin(string skinId, int atomicNumber, string achievementId)
        {
            var skin = ScriptableObject.CreateInstance<BlockoutSkin>();
            SetField(skin, "_skinId", skinId);
            SetField(skin, "_atomicNumber", atomicNumber);
            SetField(skin, "_unlockMethod", BlockoutSkinUnlockMethod.Achievement);
            SetField(skin, "_unlockAchievementId", achievementId);
            _skins.Add(skin);
            return skin;
        }

        private static void SetField(BlockoutSkin skin, string fieldName, object value)
        {
            typeof(BlockoutSkin).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, value);
        }

        [Test]
        public void RecordRunCompleted_CompletesFirstRunAchievement_AndGrantsItsSkin_OnTheFirstCall()
        {
            var iron = CreateAchievementSkin("element-26", 26, "first_run_completed");

            _service.RecordRunCompleted();

            Assert.IsTrue(_service.IsCompleted("first_run_completed"));
            Assert.IsTrue(_skinService.Unlocked.Contains(iron.SkinId));
            Assert.AreEqual(1, _saveService.Data.runsCompleted);
        }

        [Test]
        public void RecordRunCompleted_DoesNotCompleteRunsCompleted10_BeforeTheTenthRun()
        {
            for (int i = 0; i < 9; i++) _service.RecordRunCompleted();

            Assert.IsFalse(_service.IsCompleted("runs_completed_10"));
        }

        [Test]
        public void RecordRunCompleted_CompletesRunsCompleted10_OnTheTenthRun()
        {
            for (int i = 0; i < 10; i++) _service.RecordRunCompleted();

            Assert.IsTrue(_service.IsCompleted("runs_completed_10"));
        }

        [Test]
        public void RecordRunCompleted_DoesNotCompleteRunsCompleted50_BeforeTheFiftiethRun()
        {
            for (int i = 0; i < 49; i++) _service.RecordRunCompleted();

            Assert.IsFalse(_service.IsCompleted("runs_completed_50"));
        }

        [Test]
        public void RecordRunCompleted_CompletesRunsCompleted50_OnTheFiftiethRun()
        {
            for (int i = 0; i < 50; i++) _service.RecordRunCompleted();

            Assert.IsTrue(_service.IsCompleted("runs_completed_50"));
        }

        [Test]
        public void RecordRunCompleted_DoesNotCompleteRunsCompleted100_BeforeTheHundredthRun()
        {
            for (int i = 0; i < 99; i++) _service.RecordRunCompleted();

            Assert.IsFalse(_service.IsCompleted("runs_completed_100"));
        }

        [Test]
        public void RecordRunCompleted_CompletesRunsCompleted100_OnTheHundredthRun()
        {
            for (int i = 0; i < 100; i++) _service.RecordRunCompleted();

            Assert.IsTrue(_service.IsCompleted("runs_completed_100"));
        }

        [Test]
        public void RecordRunScore_CompletesTheMatchingThresholds_AndNoOthers()
        {
            _service.RecordRunScore(6000);

            Assert.IsTrue(_service.IsCompleted("single_run_score_5000"));
            Assert.IsFalse(_service.IsCompleted("single_run_score_15000"));
        }

        [Test]
        public void RecordRunScore_CompletesBothThresholds_WhenScoreClearsTheHigherOneToo()
        {
            _service.RecordRunScore(20000);

            Assert.IsTrue(_service.IsCompleted("single_run_score_5000"));
            Assert.IsTrue(_service.IsCompleted("single_run_score_15000"));
        }

        [Test]
        public void RecordMultiClear_DoesNotComplete_BelowThreeLayers()
        {
            _service.RecordMultiClear(2);

            Assert.IsFalse(_service.IsCompleted("multi_clear_3plus"));
        }

        [Test]
        public void RecordMultiClear_Completes_AtThreeOrMoreLayers()
        {
            _service.RecordMultiClear(3);

            Assert.IsTrue(_service.IsCompleted("multi_clear_3plus"));
        }

        [Test]
        public void RecordLoginForToday_FirstEverLogin_StartsTheStreakAtOne()
        {
            _service.RecordLoginForToday();

            Assert.AreEqual(1, _saveService.Data.loginStreakDays);
            Assert.IsFalse(_service.IsCompleted("login_streak_2"));
        }

        [Test]
        public void RecordLoginForToday_CalledTwiceOnTheSameDay_DoesNotDoubleCountTheStreak()
        {
            _service.RecordLoginForToday();
            _service.RecordLoginForToday();

            Assert.AreEqual(1, _saveService.Data.loginStreakDays);
        }

        [Test]
        public void RecordLoginForToday_OnConsecutiveDays_IncrementsTheStreak_AndCompletesTheMatchingAchievement()
        {
            _service.RecordLoginForToday(); // day 1

            _timeService.UtcNow = _timeService.UtcNow.AddDays(1);
            _service.RecordLoginForToday(); // day 2

            Assert.AreEqual(2, _saveService.Data.loginStreakDays);
            Assert.IsTrue(_service.IsCompleted("login_streak_2"));
            Assert.IsFalse(_service.IsCompleted("login_streak_5"));
        }

        [Test]
        public void RecordLoginForToday_AfterAGapDay_ResetsTheStreakToOne()
        {
            _service.RecordLoginForToday(); // day 1

            _timeService.UtcNow = _timeService.UtcNow.AddDays(1);
            _service.RecordLoginForToday(); // day 2 - streak = 2

            _timeService.UtcNow = _timeService.UtcNow.AddDays(3); // skips a day
            _service.RecordLoginForToday();

            Assert.AreEqual(1, _saveService.Data.loginStreakDays);
        }

        // Advances the fake clock by one day per login after the first (day 1 uses whatever
        // UtcNow already is), so `days` consecutive daily logins are recorded with no gaps.
        private void LogInForConsecutiveDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                if (i > 0) _timeService.UtcNow = _timeService.UtcNow.AddDays(1);
                _service.RecordLoginForToday();
            }
        }

        [Test]
        public void RecordLoginForToday_DoesNotCompleteLoginStreak5_BeforeTheFifthConsecutiveDay()
        {
            LogInForConsecutiveDays(4);

            Assert.IsFalse(_service.IsCompleted("login_streak_5"));
        }

        [Test]
        public void RecordLoginForToday_CompletesLoginStreak5_OnTheFifthConsecutiveDay()
        {
            LogInForConsecutiveDays(5);

            Assert.IsTrue(_service.IsCompleted("login_streak_5"));
        }

        [Test]
        public void RecordLoginForToday_DoesNotCompleteLoginStreak14_BeforeTheFourteenthConsecutiveDay()
        {
            LogInForConsecutiveDays(13);

            Assert.IsFalse(_service.IsCompleted("login_streak_14"));
        }

        [Test]
        public void RecordLoginForToday_CompletesLoginStreak14_OnTheFourteenthConsecutiveDay()
        {
            LogInForConsecutiveDays(14);

            Assert.IsTrue(_service.IsCompleted("login_streak_14"));
        }

        [Test]
        public void RecheckElementsUnlockedThreshold_CompletesElementsUnlocked20_AtExactlyTwentyUnlockedElements()
        {
            for (int i = 1; i <= 20; i++)
            {
                var skin = CreateAchievementSkin($"element-{i}", i, achievementId: $"filler-{i}");
                _skinService.GrantUnlock(skin.SkinId);
            }

            _service.RecheckElementsUnlockedThreshold();

            Assert.IsTrue(_service.IsCompleted("elements_unlocked_20"));
        }

        [Test]
        public void RecheckElementsUnlockedThreshold_DoesNotComplete_BelowTwentyUnlockedElements()
        {
            for (int i = 1; i <= 19; i++)
            {
                var skin = CreateAchievementSkin($"element-{i}", i, achievementId: $"filler-{i}");
                _skinService.GrantUnlock(skin.SkinId);
            }

            _service.RecheckElementsUnlockedThreshold();

            Assert.IsFalse(_service.IsCompleted("elements_unlocked_20"));
        }

        [Test]
        public void GetDescription_ReturnsTheGddText_ForAKnownAchievementId()
        {
            StringAssert.Contains("prima run", _service.GetDescription("first_run_completed"));
        }

        [Test]
        public void GetDescription_FallsBackToTheRawId_ForAnUnrecognizedAchievementId()
        {
            Assert.AreEqual("not_a_real_id", _service.GetDescription("not_a_real_id"));
        }

        // Every trigger family, each exercised with its condition true on every call (not just
        // the first) - runsCompleted only ever grows, a repeated score/layerCount at or above the
        // threshold stays at or above it, and RecheckElementsUnlockedThreshold is explicitly
        // documented as safe to call redundantly. In every case the matching skin must be granted
        // exactly once and its achievement id must appear exactly once in SaveData -
        // Complete()'s "already contains -> return" guard is what this is really testing.
        [Test]
        public void Achievement_UnlocksExactlyOnce_ForRunsCompletedFamily_AcrossRepeatedCalls()
        {
            var iron = CreateAchievementSkin("element-26", 26, "first_run_completed");

            _service.RecordRunCompleted();
            _service.RecordRunCompleted();
            _service.RecordRunCompleted();

            AssertGrantedExactlyOnce(iron.SkinId, "first_run_completed");
        }

        [Test]
        public void Achievement_UnlocksExactlyOnce_ForSingleRunScoreFamily_AcrossRepeatedCallsAtTheSameScore()
        {
            var mercury = CreateAchievementSkin("element-80", 80, "single_run_score_5000");

            _service.RecordRunScore(6000);
            _service.RecordRunScore(6000);
            _service.RecordRunScore(7000); // still above the threshold, not just equal to the first call

            AssertGrantedExactlyOnce(mercury.SkinId, "single_run_score_5000");
        }

        [Test]
        public void Achievement_UnlocksExactlyOnce_ForMultiClearFamily_AcrossRepeatedQualifyingClears()
        {
            var arsenic = CreateAchievementSkin("element-33", 33, "multi_clear_3plus");

            _service.RecordMultiClear(3);
            _service.RecordMultiClear(4);
            _service.RecordMultiClear(3);

            AssertGrantedExactlyOnce(arsenic.SkinId, "multi_clear_3plus");
        }

        [Test]
        public void Achievement_UnlocksExactlyOnce_ForLoginStreakFamily_AcrossRepeatedCallsOnceTheStreakIsLongEnough()
        {
            var lead = CreateAchievementSkin("element-82", 82, "login_streak_2");

            _service.RecordLoginForToday(); // day 1 - streak 1, condition not yet true
            _timeService.UtcNow = _timeService.UtcNow.AddDays(1);
            _service.RecordLoginForToday(); // day 2 - streak 2, condition becomes true
            _timeService.UtcNow = _timeService.UtcNow.AddDays(1);
            _service.RecordLoginForToday(); // day 3 - streak 3, condition still true

            AssertGrantedExactlyOnce(lead.SkinId, "login_streak_2");
        }

        [Test]
        public void Achievement_UnlocksExactlyOnce_ForElementsUnlockedFamily_AcrossRepeatedRechecks()
        {
            // Sulfur is deliberately never granted directly here - it's meant to be unlocked by
            // crossing the threshold itself (GrantMatchingSkins, called from inside Complete),
            // exactly like the real flow (some 20th coin purchase completing "elements_unlocked_20").
            var sulfur = CreateAchievementSkin("element-16", 16, "elements_unlocked_20");
            for (int i = 1; i <= 20; i++)
            {
                var filler = CreateAchievementSkin($"element-{i + 200}", i + 200, achievementId: $"filler-{i}");
                _skinService.GrantUnlock(filler.SkinId);
            }

            _service.RecheckElementsUnlockedThreshold(); // 20 fillers unlocked - condition becomes true, sulfur granted
            _service.RecheckElementsUnlockedThreshold(); // condition stays true (21 unlocked now, counting sulfur)
            _service.RecheckElementsUnlockedThreshold();

            AssertGrantedExactlyOnce(sulfur.SkinId, "elements_unlocked_20");
        }

        private void AssertGrantedExactlyOnce(string skinId, string achievementId)
        {
            Assert.IsTrue(_service.IsCompleted(achievementId));
            Assert.AreEqual(1, _skinService.GrantUnlockCallCount.TryGetValue(skinId, out var count) ? count : 0,
                $"GrantUnlock should have been called exactly once for \"{skinId}\".");
            Assert.AreEqual(1, CountOccurrences(_saveService.Data.completedAchievementIds, achievementId),
                $"\"{achievementId}\" should appear exactly once in completedAchievementIds.");
        }

        private static int CountOccurrences(List<string> list, string value)
        {
            int count = 0;
            foreach (var item in list)
            {
                if (item == value) count++;
            }
            return count;
        }
    }
}
