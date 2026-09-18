using UnityEngine;

namespace hp55games.Mobile.Core.Config
{
    [CreateAssetMenu(menuName = "Config/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("App Info")]
        [Tooltip("Current app/build version string, shown in UI or logs.")]
        public string appVersion;

        [Header("Gameplay Defaults")]
        [Tooltip("Whether haptic feedback is enabled by default for new players.")]
        public bool enableHaptics = true;
        [Tooltip("Default difficulty level assigned to new players.")]
        public int defaultDifficulty = 1;
    }
}