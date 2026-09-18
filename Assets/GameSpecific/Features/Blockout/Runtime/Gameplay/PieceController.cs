using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Polycubes.Shapes;
using hp55games.Polycubes.Grid;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay.Events;
using hp55games.Blockout.InputSystem;

namespace hp55games.Blockout.Gameplay
{
    public sealed class PieceController : MonoBehaviour
    {
        private enum StepState { Waiting, Stepping }

        // Technical Doc Phase 3, open question 3 - resolved: AxisB already covers X, Y is the
        // well's vertical/fall axis and was never meant to be player-rotatable, so Z is the
        // remaining horizontal axis for AxisA. Still a single named swap point if that changes.
        private enum PhysicalRotationAxis { X, Y, Z }
        private const PhysicalRotationAxis AxisAMapsTo = PhysicalRotationAxis.Z;
        private const PhysicalRotationAxis AxisBMapsTo = PhysicalRotationAxis.X;

        public PolycubeShape Shape { get; private set; }
        public Vector3Int GridPosition { get; private set; }
        public float CurrentInterval { get; private set; }
        public bool IsLocked { get; private set; }

        // Testable without going through Unity's frame loop or the event bus registration required by Awake().
        public event Action<float> FallIntervalChanged;

        // Fires exactly once per lock, synchronously, with the Y layers cleared by this lock
        // (empty if none) - BlockoutSpawner uses the array to mirror the clear/collapse in
        // WellCellRenderer and dispatch to the active skin's clear behaviour BEFORE it spawns the
        // next piece, since that has to happen in this exact order (see BlockoutSpawner.OnPieceLocked).
        public event Action<int[]> Locked;

        private StepState _state;
        private float _stateTimer;
        private BlockoutFallCurveConfig _fallCurve;
        private BlockoutTimeDifficultyModifier _timeDifficulty;
        private VoxelGrid _grid;
        private IEventBus _eventBus;
        private IDisposable _moveSubscription;
        private IDisposable _rotateSubscription;
        private IDisposable _hardDropSubscription;

        private void Awake()
        {
            if (ServiceRegistry.TryResolve(out _eventBus))
            {
                _moveSubscription = _eventBus.Subscribe<PieceMoveRequestedEvent>(HandleMoveRequested);
                _rotateSubscription = _eventBus.Subscribe<PieceRotateRequestedEvent>(HandleRotateRequested);
                _hardDropSubscription = _eventBus.Subscribe<HardDropRequestedEvent>(HandleHardDropRequested);
            }
        }

        private void OnDestroy()
        {
            _moveSubscription?.Dispose();
            _rotateSubscription?.Dispose();
            _hardDropSubscription?.Dispose();
        }

        // fallCurve/grid are passed in rather than resolved here so the tick/locking logic stays
        // testable without needing IConfigCatalogService wired up (see PieceControllerTests).
        // timeDifficulty is the same principle applied to the step delay - null is a valid "not
        // wired up" value (tests that don't care about it simply omit it), in which case
        // CurrentInterval falls back to fallCurve.BaseInterval, unmodified.
        public void Initialize(PolycubeShape shape, Vector3Int startPosition, BlockoutFallCurveConfig fallCurve, VoxelGrid grid, BlockoutTimeDifficultyModifier timeDifficulty = null)
        {
            Shape = shape;
            GridPosition = startPosition;
            _fallCurve = fallCurve;
            _timeDifficulty = timeDifficulty;
            _grid = grid;
            CurrentInterval = ComputeCurrentInterval();
            _state = StepState.Waiting;
            _stateTimer = 0f;
            IsLocked = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Advances the Waiting -> Stepping -> Waiting loop by dt seconds of simulated time.
        // No-ops once locked: a locked piece is done, the spawner owns what happens next.
        public void Tick(float deltaTime)
        {
            if (IsLocked) return;

            // Re-checked every tick, not just on a step transition: the time-based modifier's own
            // decay timer can shift the interval at any point during this piece's fall.
            RefreshCurrentInterval();

            _stateTimer += deltaTime;

            switch (_state)
            {
                case StepState.Waiting:
                    if (_stateTimer >= CurrentInterval)
                    {
                        _stateTimer -= CurrentInterval;

                        var nextPosition = GridPosition + Vector3Int.down;
                        if (PlacementRules.CanPlaceAt(_grid, Shape, nextPosition))
                        {
                            _state = StepState.Stepping;
                            // Logical grid position updates at the START of Stepping, not at the end —
                            // avoids ambiguous state during the visual transition.
                            GridPosition = nextPosition;
                        }
                        else
                        {
                            Lock();
                        }
                    }
                    break;

                case StepState.Stepping:
                    if (_stateTimer >= _fallCurve.StepDuration)
                    {
                        _stateTimer -= _fallCurve.StepDuration;
                        _state = StepState.Waiting;
                        RefreshCurrentInterval();
                    }
                    break;
            }
        }

        // The floor or a stacked cell blocked the next step down: write the shape into the grid
        // at its current (last valid) position and stop ticking. The spawner listens for this to
        // spawn the next piece.
        private void Lock()
        {
            PlacementRules.LockInto(_grid, Shape, GridPosition);
            var clearedLayerYs = new List<int>();
            int layersCleared = PlacementRules.ClearFullLayersTouchedBy(_grid, Shape, GridPosition, clearedLayerYs);

            IsLocked = true;
            Locked?.Invoke(clearedLayerYs.ToArray());
            _eventBus?.Publish(new PieceLockedEvent { GridPosition = GridPosition });

            if (layersCleared > 0)
            {
                int points = ScoreCalculator.PointsForSimultaneousClears(layersCleared);
                _eventBus?.Publish(new LayersClearedEvent { LayerCount = layersCleared, PointsAwarded = points });
            }
        }

        // Recomputes CurrentInterval from the time-based step delay (if wired) and fires
        // FallIntervalChanged/FallIntervalChangedEvent only when the value actually moved. Called
        // on every Tick (the modifier's own decay can change the delay mid-piece) and right after
        // finishing a Stepping animation.
        private void RefreshCurrentInterval()
        {
            float newInterval = ComputeCurrentInterval();
            bool changed = !Mathf.Approximately(newInterval, CurrentInterval);
            CurrentInterval = newInterval;

            if (changed)
            {
                FallIntervalChanged?.Invoke(CurrentInterval);
                _eventBus?.Publish(new FallIntervalChangedEvent { NewInterval = CurrentInterval });
            }
        }

        private float ComputeCurrentInterval() =>
            _timeDifficulty != null ? _timeDifficulty.CurrentStepDelay : _fallCurve.BaseInterval;

        // Same discrete-check pattern as the fall step: attempt via CanPlaceAt, only commit if valid.
        private void HandleMoveRequested(PieceMoveRequestedEvent evt)
        {
            if (IsLocked) return;

            var candidate = GridPosition + DeltaFor(evt.Direction);
            if (PlacementRules.CanPlaceAt(_grid, Shape, candidate))
            {
                GridPosition = candidate;
            }
        }

        private void HandleRotateRequested(PieceRotateRequestedEvent evt)
        {
            if (IsLocked) return;

            var axis = evt.Axis == RotateAxis.AxisA ? AxisAMapsTo : AxisBMapsTo;
            var rotated = Rotate(Shape, axis, evt.Steps90);

            // allowAboveTop: rotation is only ever blocked by the well's side walls and floor,
            // never by its open top (no wireframe there) - unlike a fall step/move/hard drop,
            // which all still use the strict, walls-and-ceiling CanPlaceAt overload.
            if (PlacementRules.CanPlaceAt(_grid, rotated, GridPosition, allowAboveTop: true))
            {
                Shape = rotated;
            }
        }

        // Repeats the downward step immediately (no Waiting/Stepping animation) until the next
        // step would be invalid, then locks at the last valid position - same rule as a normal
        // fall step, just without waiting for the interval.
        private void HandleHardDropRequested(HardDropRequestedEvent evt)
        {
            if (IsLocked) return;

            while (true)
            {
                var next = GridPosition + Vector3Int.down;
                if (!PlacementRules.CanPlaceAt(_grid, Shape, next)) break;
                GridPosition = next;
            }

            Lock();
        }

        private static Vector3Int DeltaFor(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left: return new Vector3Int(-1, 0, 0);
                case MoveDirection.Right: return new Vector3Int(1, 0, 0);
                case MoveDirection.Forward: return new Vector3Int(0, 0, 1);
                case MoveDirection.Back: return new Vector3Int(0, 0, -1);
                default: return Vector3Int.zero;
            }
        }

        private static PolycubeShape Rotate(PolycubeShape shape, PhysicalRotationAxis axis, int steps90)
        {
            switch (axis)
            {
                case PhysicalRotationAxis.X: return shape.RotatedX(steps90);
                case PhysicalRotationAxis.Y: return shape.RotatedY(steps90);
                default: return shape.RotatedZ(steps90);
            }
        }
    }
}
