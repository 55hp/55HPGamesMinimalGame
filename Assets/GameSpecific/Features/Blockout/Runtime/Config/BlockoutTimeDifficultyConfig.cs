using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    // Tuning for BlockoutTimeDifficultyModifier - the time+layer-clear based difficulty system
    // that sits alongside (never replaces) BlockoutFallCurveConfig's per-piece phase curve. See
    // BlockoutTimeDifficultyModifier for how these combine into the final fall interval.
    [CreateAssetMenu(fileName = "BlockoutTimeDifficulty", menuName = "hp55games/Blockout/Time Difficulty Config")]
    public sealed class BlockoutTimeDifficultyConfig : ScriptableObject, IConfigAsset
    {
        [SerializeField] private float _sessionBaseInterval = 2.0f;
        [SerializeField] private float _timerTickIntervalSeconds = 15.0f;
        [SerializeField] private float _timerDecrementPerTick = 0.15f;
        [SerializeField] private float _layerClearDecrement = 0.1f;
        [SerializeField] private float _combinedFloorInterval = 0.5f;

        public float SessionBaseInterval => _sessionBaseInterval;
        public float TimerTickIntervalSeconds => _timerTickIntervalSeconds;
        public float TimerDecrementPerTick => _timerDecrementPerTick;
        public float LayerClearDecrement => _layerClearDecrement;
        public float CombinedFloorInterval => _combinedFloorInterval;
    }
}
