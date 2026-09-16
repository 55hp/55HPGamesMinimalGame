using System;
using System.Collections.Generic;

namespace hp55games.Mobile.Core.Save
{
    [Serializable]
    public class SaveData
    {
        // Starting balance for a fresh save (no save.json on disk yet - see SaveService.Load).
        // 4000 by design: enough to test unlocking once the 118 elements have a real cost, still
        // not enough to unlock everything at once. Irrelevant once a save exists - this default
        // only applies before the very first Save().
        public int coins = 4000;

        /// <summary>
        /// SkinId of the currently active cosmetic skin (Blockout skin system). Empty/null means
        /// no choice persisted yet - the consuming service falls back to the default skin.
        /// </summary>
        public string activeSkinId;

        /// <summary>
        /// SkinIds the player has spent coins to unlock (Blockout skin system). A skin whose
        /// BlockoutSkin.UnlockedByDefault is true (the always-free default) doesn't need to be
        /// listed here - it's unlocked regardless.
        /// </summary>
        public List<string> unlockedSkinIds = new List<string>();

        public string lastProfile = "default";
        public OptionsData options = new OptionsData();

        /// <summary>
        /// Generic player progress (best scores, records, stats).
        /// </summary>
        public PlayerProgressData progress = new PlayerProgressData();

        public List<TimeStampEntry> timeStamps = new List<TimeStampEntry>();

        public string lastUtcIso;
        public double lastMonotonicSeconds;

        /// <summary>
        /// Achievement ids the player has completed (Blockout Periodic Table shop - see
        /// IBlockoutAchievementService). An id landing here is permanent - achievements never
        /// un-complete.
        /// </summary>
        public List<string> completedAchievementIds = new List<string>();

        /// <summary>
        /// Total Blockout runs completed (well-full/game-over reached at least once), across all
        /// sessions - the "runs completed" achievement family's counter.
        /// </summary>
        public int runsCompleted;

        /// <summary>
        /// Current consecutive-day login streak (Blockout achievement family) - see
        /// IBlockoutAchievementService.RecordLoginForToday, which increments/resets this.
        /// </summary>
        public int loginStreakDays;
    }
}