using System;
using System.Collections.Generic;

namespace hp55games.Mobile.Core.Save
{
    [Serializable]
    public class SaveData
    {
        public int coins;

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
    }
}