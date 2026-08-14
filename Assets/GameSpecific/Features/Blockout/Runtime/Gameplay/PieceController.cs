using System;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Polycubes.Shapes;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay.Events;

namespace hp55games.Blockout.Gameplay
{
    public sealed class PieceController : MonoBehaviour
    {
        private enum StepState { Waiting, Stepping }

        public PolycubeShape Shape { get; private set; }
        public Vector3Int GridPosition { get; private set; }
        public int PhaseIndex { get; private set; }
        public float CurrentInterval { get; private set; }

        // Testable without going through Unity's frame loop or the event bus registration required by Awake().
        public event Action<float> FallIntervalChanged;

        private StepState _state;
        private float _stateTimer;
        private BlockoutFallCurveConfig _fallCurve;
        private IEventBus _eventBus;

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _eventBus);
        }

        // fallCurve is passed in rather than resolved here so the tick/phase logic stays testable
        // without needing IConfigCatalogService wired up (see PieceControllerTests).
        public void Initialize(PolycubeShape shape, Vector3Int startPosition, BlockoutFallCurveConfig fallCurve)
        {
            Shape = shape;
            GridPosition = startPosition;
            _fallCurve = fallCurve;
            PhaseIndex = 0;
            CurrentInterval = _fallCurve.IntervalForPhase(PhaseIndex);
            _state = StepState.Waiting;
            _stateTimer = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Advances the Waiting -> Stepping -> Waiting loop by dt seconds of simulated time.
        public void Tick(float deltaTime)
        {
            _stateTimer += deltaTime;

            switch (_state)
            {
                case StepState.Waiting:
                    if (_stateTimer >= CurrentInterval)
                    {
                        _stateTimer -= CurrentInterval;
                        _state = StepState.Stepping;
                        // Logical grid position updates at the START of Stepping, not at the end —
                        // avoids ambiguous state during the visual transition.
                        GridPosition += Vector3Int.down;
                    }
                    break;

                case StepState.Stepping:
                    if (_stateTimer >= _fallCurve.StepDuration)
                    {
                        _stateTimer -= _fallCurve.StepDuration;
                        _state = StepState.Waiting;
                        AdvancePhase();
                    }
                    break;
            }
        }

        private void AdvancePhase()
        {
            PhaseIndex++;
            float newInterval = _fallCurve.IntervalForPhase(PhaseIndex);
            bool changed = !Mathf.Approximately(newInterval, CurrentInterval);
            CurrentInterval = newInterval;

            if (changed)
            {
                FallIntervalChanged?.Invoke(CurrentInterval);
                _eventBus?.Publish(new FallIntervalChangedEvent { NewInterval = CurrentInterval });
            }
        }
    }
}
