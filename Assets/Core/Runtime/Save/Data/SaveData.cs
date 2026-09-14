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