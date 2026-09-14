using System;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay.Events;

namespace hp55games.Blockout.Gameplay
{
    // Time+layer-clear based difficulty: sits alongside PhasedIntervalCurve (PieceController's
    // per-piece PhaseIndex), never derived from or touching it. Tracks its own "session
    // interval", starting at Config.SessionBaseInterval and independently reduced by real
    // elapsed time (every TimerTickIntervalSeconds, via Tick) and by every layer clear
    // (LayerClearDecrement per clear event, regardless of how many layers that one clear
    // covered - LayersClearedEvent already fires exactly once per lock). The ratio of the
    // current session interval to its starting value multiplies onto whatever interval
    // PhasedIntervalCurve reports for the active piece - see ApplyTo.
    //
    // One instance per run: constructed fresh by BlockoutSpawner.Initialize() so the session
    // timer restarts from zero on every new game, and disposed (unsubscribed) when replaced.
    public sealed class BlockoutTimeDifficultyModifier : IDisposable
    {
        private readonly BlockoutTimeDifficultyConfig _config;
        private readonly IDisposable _layersClearedSubscription;

        private float _sessionInterval;
        private float _unscaledTimeAccumulator;
        private int _lastTickedFrame = -1;

        public float SessionInterval => _sessionInterval;

        // Resolves IEventBus itself (a service dependency, same as PieceController.Awake) rather
        // than taking it as a constructor parameter - config is the only external data this needs.
        public BlockoutTimeDifficultyModifier(BlockoutTimeDifficultyConfig config)
        {
            _config = config;
            _sessionInterval = config.SessionBaseInterval;

            if (ServiceRegistry.TryResolve<IEventBus>(out var eventBus))
            {
                _layersClearedSubscription = eventBus.Subscribe<LayersClearedEvent>(OnLayersCleared);
            }
        }

        // Advances the real-time reducer by unscaledDeltaTime. Call once per frame (e.g. from
        // BlockoutSpawner.Update() with Time.unscaledDeltaTime) - unscaled so the 15s timer keeps
        // running through a paused Time.timeScale, per spec ("non pausabile").
        //
        // Guarded by frame number rather than trusting the call site to only ever run once per
        // frame: an accidental double call within the same frame (e.g. two Update() paths ending
        // up here) must not double-apply the same real-time slice. A genuinely long frame (lag
        // spike spanning multiple 15s boundaries) is not this case - that's real elapsed time and
        // the while loop below correctly ticks once per boundary crossed.
        public void Tick(float unscaledDeltaTime)
        {
            if (_lastTickedFrame == Time.frameCount) return;
            _lastTickedFrame = Time.frameCount;

            _unscaledTimeAccumulator += unscaledDeltaTime;
            while (_unscaledTimeAccumulator >= _config.TimerTickIntervalSeconds)
            {
                _unscaledTimeAccumulator -= _config.TimerTickIntervalSeconds;
                ReduceSessionInterval(_config.TimerDecrementPerTick);
            }
        }

        // Multiplies phaseInterval by this modifier's current ratio (sessionInterval /
        // SessionBaseInterval), then applies the floor shared with the phase-based system - the
        // single floor point for the fully-combined value. Safe even once sessionInterval has
        // been reduced to 0 (ratio 0 -> combined floor still rescues the result to
        // CombinedFloorInterval).
        public float ApplyTo(float phaseInterval)
        {
            float ratio = _sessionInterval / _config.SessionBaseInterval;
            return Mathf.Max(phaseInterval * ratio, _config.CombinedFloorInterval);
        }

        public void Dispose() => _layersClearedSubscription?.Dispose();

        // Fires once per LayersClearedEvent regardless of LayerCount - matches spec: a
        // simultaneous multi-layer clear reduces the interval by exactly LayerClearDecrement,
        // not LayerClearDecrement * LayerCount.
        private void OnLayersCleared(LayersClearedEvent evt) => ReduceSessionInterval(_config.LayerClearDecrement);

        private void ReduceSessionInterval(float amount) => _sessionInterval = Mathf.Max(0f, _sessionInterval - amount);
    }
}
