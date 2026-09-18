using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.Save;
using hp55games.Mobile.Core.Timing;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Achievements
{
    public sealed class BlockoutAchievementService : IBlockoutAchievementService
    {
        // Must match BlockoutSkin.UnlockAchievementId values, baked in by
        // BlockoutPeriodicElementImporter from blockout_periodic_elements.json's
        // unlockAchievementId field (Shop GDD table: Ferro/Rame/Zinco/Stagno/Piombo/Argento/Oro/
        // Mercurio/Antimonio/Arsenico/Zolfo).
        private const string FirstRunCompleted = "first_run_completed";
        private const string RunsCompleted10 = "runs_completed_10";
        private const string RunsCompleted50 = "runs_completed_50";
        private const string RunsCompleted100 = "runs_completed_100";
        private const string LoginStreak2 = "login_streak_2";
        private const string LoginStreak5 = "login_streak_5";
        private const string LoginStreak14 = "login_streak_14";
        private const string SingleRunScore5000 = "single_run_score_5000";
        private const string SingleRunScore15000 = "single_run_score_15000";
        private const string MultiClear3Plus = "multi_clear_3plus";
        private const string ElementsUnlocked20 = "elements_unlocked_20";

        // ITimeService key for the login-streak's day-granularity timestamp - namespaced so it
        // can never collide with an unrelated feature's own SetNowUtc key.
        private const string LoginTimestampKey = "blockout_last_login";

        public bool IsCompleted(string achievementId)
        {
            var save = ResolveSaveData();
            return save?.completedAchievementIds != null && save.completedAchievementIds.Contains(achievementId);
        }

        public string GetDescription(string achievementId)
        {
            switch (achievementId)
            {
                case FirstRunCompleted: return "Completa la prima run";
                case RunsCompleted10: return "Completa 10 run";
                case RunsCompleted50: return "Completa 50 run";
                case RunsCompleted100: return "Completa 100 run";
                case LoginStreak2: return "Gioca 2 giorni consecutivi";
                case LoginStreak5: return "Gioca 5 giorni consecutivi";
                case LoginStreak14: return "Gioca 14 giorni consecutivi";
                case SingleRunScore5000: return "Punteggio ≥ 5.000 in una singola run";
                case SingleRunScore15000: return "Punteggio ≥ 15.000 in una singola run";
                case MultiClear3Plus: return "Un clear multiplo da ≥3 strati simultanei in una run";
                case ElementsUnlocked20: return "Sblocca almeno 20 elementi";
                default: return achievementId;
            }
        }

        public void RecordRunCompleted()
        {
            var save = ResolveSaveData();
            if (save == null) return;

            save.runsCompleted++;
            ResolveSaveService()?.Save();

            if (save.runsCompleted >= 1) Complete(FirstRunCompleted);
            if (save.runsCompleted >= 10) Complete(RunsCompleted10);
            if (save.runsCompleted >= 50) Complete(RunsCompleted50);
            if (save.runsCompleted >= 100) Complete(RunsCompleted100);
        }

        public void RecordRunScore(int score)
        {
            if (score >= 5000) Complete(SingleRunScore5000);
            if (score >= 15000) Complete(SingleRunScore15000);
        }

        public void RecordMultiClear(int layerCount)
        {
            if (layerCount >= 3) Complete(MultiClear3Plus);
        }

        public void RecordLoginForToday()
        {
            if (!ServiceRegistry.TryResolve<ITimeService>(out var time))
            {
                Debug.LogError("[BlockoutAchievementService] ITimeService is not registered - login streak not updated.");
                return;
            }

            var save = ResolveSaveData();
            if (save == null) return;

            var lastLogin = time.GetLastUtc(LoginTimestampKey);
            var today = time.UtcNow.Date;

            if (lastLogin == DateTime.MinValue)
            {
                save.loginStreakDays = 1; // first login ever recorded
            }
            else
            {
                int daysSince = (today - lastLogin.Date).Days;
                if (daysSince == 1)
                {
                    save.loginStreakDays++;
                }
                else if (daysSince > 1)
                {
                    save.loginStreakDays = 1; // streak broken - today restarts it
                }
                // daysSince == 0 (already logged in today, or a suspicious/negative delta
                // ITimeService itself clamped to Zero): streak untouched, no double-count.
            }

            time.SetNowUtc(LoginTimestampKey); // also persists - TimeService.SetNowUtc calls Save()

            if (save.loginStreakDays >= 2) Complete(LoginStreak2);
            if (save.loginStreakDays >= 5) Complete(LoginStreak5);
            if (save.loginStreakDays >= 14) Complete(LoginStreak14);
        }

        public void RecheckElementsUnlockedThreshold()
        {
            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalog)) return;
            if (!ServiceRegistry.TryResolve<IBlockoutSkinService>(out var skinService)) return;

            int unlockedElements = 0;
            foreach (var skin in catalog.GetAll<BlockoutSkin>())
            {
                if (skin.AtomicNumber > 0 && skinService.IsUnlocked(skin)) unlockedElements++;
            }

            if (unlockedElements >= 20) Complete(ElementsUnlocked20);
        }

        // Marks achievementId completed (idempotent - a second call for an already-completed id
        // is a no-op) and grants every BlockoutSkin-element whose UnlockAchievementId matches it.
        private void Complete(string achievementId)
        {
            var save = ResolveSaveData();
            if (save == null) return;

            save.completedAchievementIds ??= new List<string>();
            if (save.completedAchievementIds.Contains(achievementId)) return;

            save.completedAchievementIds.Add(achievementId);
            ResolveSaveService()?.Save();

            GrantMatchingSkins(achievementId);
            RecheckElementsUnlockedThreshold(); // this unlock can itself be the 20th element
        }

        private static void GrantMatchingSkins(string achievementId)
        {
            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalog)) return;
            if (!ServiceRegistry.TryResolve<IBlockoutSkinService>(out var skinService)) return;

            foreach (var skin in catalog.GetAll<BlockoutSkin>())
            {
                if (skin.UnlockMethod == BlockoutSkinUnlockMethod.Achievement && skin.UnlockAchievementId == achievementId)
                {
                    skinService.GrantUnlock(skin.SkinId);
                }
            }
        }

        private static SaveData ResolveSaveData() => ResolveSaveService()?.Data;

        private static ISaveService ResolveSaveService()
        {
            if (ServiceRegistry.TryResolve<ISaveService>(out var service)) return service;
            Debug.LogError("[BlockoutAchievementService] ISaveService is not registered.");
            return null;
        }
    }
}
