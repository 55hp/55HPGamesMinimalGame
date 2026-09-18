using UnityEngine;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Gameplay
{
    // Sole source of the fall step delay: starts at Config.StartStepDelay and decays by
    // Config.DecayPercent every Config.DecayIntervalSeconds of real elapsed time, floored at
    // Config.MinStepDelay. Purely time-based - nothing about piece count, layer clears, or a
    // per-piece phase feeds into it.
    //
    // One instance per run: constructed fresh by BlockoutSpawner.Initialize() so the delay
    // restarts from StartStepDelay on every new game.
    public sealed class BlockoutTimeDifficultyModifier
    {
        private readonly BlockoutTimeDifficultyConfig _config;

        private float _currentStepDelay;
        private float _unscaledTimeAccumulator;
        private int _lastTickedFrame = -1;

        public float CurrentStepDelay => _currentStepDelay;

        public BlockoutTimeDifficultyModifier(BlockoutTimeDifficultyConfig config)
        {
            _config = config;
            _currentStepDelay = config.StartStepDelay;
        }

        // Advances the real-time decay by unscaledDeltaTime. Call once per frame (e.g. from
        // BlockoutSpawner.Update() with Time.unscaledDeltaTime) - unscaled so decay keeps running
        // through a paused Time.timeScale.
        //
        // Guarded by frame number rather than trusting the call site to only ever run once per
        // frame: an accidental double call within the same frame must not double-apply the same
        // real-time slice. A single call spanning several decay boundaries (e.g. a lag spike) is
        // real elapsed time, not a duplicate trigger - the while loop below ticks once per
        // boundary crossed.
        public void Tick(float unscaledDeltaTime)
        {
            if (_lastTickedFrame == Time.frameCount) return;
            _lastTickedFrame = Time.frameCount;

            _unscaledTimeAccumulator += unscaledDeltaTime;
            while (_unscaledTimeAccumulator >= _config.DecayIntervalSeconds)
            {
                _unscaledTimeAccumulator -= _config.DecayIntervalSeconds;
                _currentStepDelay = Mathf.Max(_config.MinStepDelay, _currentStepDelay * (1f - _config.DecayPercent));
            }
        }
    }
}
