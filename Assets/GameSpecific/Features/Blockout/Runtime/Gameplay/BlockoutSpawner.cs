using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Blockout.Rendering;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Gameplay
{
    // Owns the well's VoxelGrid and keeps spawning pieces into it as each one locks. Driven by
    // BlockoutGameplayState.EnterAsync (Phase 4), which calls Initialize(...). Visuals are fully
    // delegated to WellCellRenderer (Phase 5, pooled) - this class only decides WHICH grid cells
    // are shown and WHAT COLOR they mean (active vs locked), never touches a GameObject/Renderer
    // directly.
    //
    // Registers itself into ServiceRegistry (Awake/OnDestroy) rather than being found via
    // FindObjectOfType (README §0 rule 2) - BlockoutGameplayState and BlockoutInputHandler both
    // need to reach the active spawner, and neither can hold a scene-authored [SerializeField] to
    // it (the state is a plain C# class constructed outside this scene; the input handler's
    // Awake() has no ordering guarantee relative to this one's within the same scene load).
    // ServiceRegistry.Unregister only removes the entry if it still holds this instance, so even
    // if the unload/load ordering ever stopped being strictly sequential, a late OnDestroy can't
    // clobber a newer spawner's registration.
    public sealed class BlockoutSpawner : MonoBehaviour
    {
        [Tooltip("Renders the well's occupied cells (pooled). Missing reference: pieces still spawn/lock/clear normally, just with no visual.")]
        [SerializeField] private WellCellRenderer _cellRenderer;

        private VoxelGrid _grid;
        private int _wellWidth;
        private int _wellHeight;
        private int _wellDepth;
        private BlockoutFallCurveConfig _fallCurve;
        private BlockoutTimeDifficultyModifier _timeDifficulty;

        private IReadOnlyList<PolycubeShape> _shapes;
        private System.Random _random;

        // Spawns elapsed since each _shapes[i] was last picked, indexed by position in _shapes
        // (not PolycubeShape identity - the class has no Equals/GetHashCode, so comparing
        // instances would be fragile). PickNextShapeIndex clamps each value to _shapes.Count - 1
        // before summing into weights: a piece can't repeat immediately (weight 0 right after
        // being picked) and its odds grow back linearly the longer it's been benched, capped so
        // no single shape's weight runs away over a long run.
        private int[] _spawnsSinceLastPick;

        private PieceController _controller;

        // All sourced from the active skin (BlockoutGameplayState reads
        // IBlockoutSkinService.ActiveSkin and passes them into Initialize) instead of being fixed
        // on this component: every falling piece uses the skin's single base colour and surface
        // params (metallic/smoothness/emission, see CellSurface).
        private Color _pieceColor = Color.white;
        private IBlockoutClearBehaviour _clearBehaviour;
        private CellSurface? _cellSurface;

        // Source of the per-level colours: a locked cube takes its level's colour
        // (GetLevelColor(cell.y)), both at lock time and again whenever a clear shifts it down a
        // level. Null in tests that don't pass one - locked cubes then keep the piece colour.
        private BlockoutWellConfig _wellConfig;

        // The active piece's currently-shown cell positions and color: tracked so a change in
        // GridPosition/Shape (fall step, move, rotate, hard drop) hides exactly the old set and
        // shows exactly the new one, instead of re-showing every cell every frame regardless of
        // whether anything actually moved. Null when there's no active (unlocked) piece.
        private Vector3Int[] _shownCells;
        private Vector3Int[] _stepFromCells;
        private Vector3Int[] _stepToCells;
        private bool _visualStepActive;
        private Color _activeColor;

        private bool _spawningStopped;

        // True once SpawnNext refused to spawn because the well is already full at the computed
        // spawn position (see SpawnNext). Public so PlayMode tests can read this instead of a
        // silent lock.
        public bool SpawnBlockedWellFull { get; private set; }
        public PieceController CurrentPiece => _controller;

        // The seed this run's spawn order was built from - exposed for logging/repro (the same
        // seed always produces the same spawn sequence for a given shape set).
        public int CurrentSeed { get; private set; }

        // Fired once: either SpawnNext refused to spawn because the well is full (see
        // SpawnBlockedWellFull), or, after a lock and its clears, a locked cell remains at
        // Y >= h, i.e. outside the well (CheckGameOver). BlockoutGameplayState listens for this to publish
        // BlockoutGameOverEvent and drive the FSM to ResultState; no recovery happens here.
        public event Action WellFull;

        // grid/fallCurve/shapes/dimensions/skin look/wellConfig are passed in rather than
        // resolved here so the spawn validation logic stays testable without needing
        // IConfigCatalogService/IBlockoutSkinService wired up (mirrors PieceController.Initialize).
        // Called by BlockoutGameplayState.EnterAsync, which is the sole entry point - this class
        // doesn't self-start.
        public void Initialize(VoxelGrid grid, BlockoutFallCurveConfig fallCurve, BlockoutTimeDifficultyConfig timeDifficultyConfig, IReadOnlyList<PolycubeShape> shapes, int seed, int wellWidth, int wellHeight, int wellDepth, Color pieceColor, IBlockoutClearBehaviour clearBehaviour, CellSurface? cellSurface = null, BlockoutWellConfig wellConfig = null)
        {
            _pieceColor = pieceColor;
            _wellConfig = wellConfig;
            _clearBehaviour = clearBehaviour;
            _cellSurface = cellSurface;

            // Fresh per run, per spec: the step delay restarts from StartStepDelay every new game.
            _timeDifficulty = new BlockoutTimeDifficultyModifier(timeDifficultyConfig);

            if (_cellRenderer == null)
            {
                Debug.LogError("[BlockoutSpawner] _cellRenderer is not assigned in the Inspector - pieces will spawn with no visual.", this);
            }

            // Leaves no visual trace of a previous run: every locked cell, plus whatever the
            // active piece was showing.
            _cellRenderer?.HideAll();
            _controller = null;
            _shownCells = null;
            _stepFromCells = null;
            _stepToCells = null;
            _visualStepActive = false;

            _grid = grid;
            _fallCurve = fallCurve;
            _shapes = shapes;
            _wellWidth = wellWidth;
            _wellHeight = wellHeight;
            _wellDepth = wellDepth;

            CurrentSeed = seed;
            _random = new System.Random(seed);

            // Every piece starts at Count - 1 (the cap PickNextShapeIndex clamps weights to), not
            // 0 - all-zero weights would sum to 0 and break the very first roll of the run.
            _spawnsSinceLastPick = new int[shapes.Count];
            for (int i = 0; i < _spawnsSinceLastPick.Length; i++)
                _spawnsSinceLastPick[i] = shapes.Count - 1;

            _spawningStopped = false;
            SpawnBlockedWellFull = false;

            SpawnNext();
        }

        private void Awake()
        {
            // See the class doc for why this is ServiceRegistry, not left for FindObjectOfType.
            ServiceRegistry.Register<BlockoutSpawner>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<BlockoutSpawner>(this);
        }

        private void Update()
        {
            // Ticked unconditionally (not gated on having an active piece): the session timer
            // runs on real elapsed time for the whole run, independent of piece lifecycle.
            _timeDifficulty?.Tick(Time.unscaledDeltaTime);

            if (_controller == null) return;
            SyncActivePieceVisual();
        }

        // Shows the active piece's current cells and hides whichever cells it previously
        // occupied, but only when GridPosition/Shape actually changed since the last call - a
        // fall step, move, rotate, and hard drop all go through this same path.
        private void SyncActivePieceVisual()
        {
            if (_cellRenderer == null) return;

            var shapeCells = _controller.Shape.Cells;
            var newCells = new Vector3Int[shapeCells.Count];
            for (int i = 0; i < shapeCells.Count; i++)
            {
                newCells[i] = _controller.GridPosition + shapeCells[i];
            }

            if (_controller.IsStepping && !_controller.IsLocked)
            {
                SyncVisualStep(newCells);
                return;
            }

            FinishVisualStep();

            if (_shownCells != null && CellsEqual(_shownCells, newCells)) return;

            if (_shownCells != null)
            {
                foreach (var cell in _shownCells) _cellRenderer.HideCell(cell);
            }

            foreach (var cell in newCells) _cellRenderer.ShowCell(cell, _activeColor, _cellSurface);
            _shownCells = newCells;
        }

        private void SyncVisualStep(Vector3Int[] targetCells)
        {
            if (_visualStepActive && !CellsEqual(_stepToCells, targetCells))
            {
                // A move or rotation during the interpolation changes the target shape. Finish the
                // previous visual step cleanly, then apply the new input at the current grid cell.
                FinishVisualStep();
            }

            if (!_visualStepActive)
            {
                if (_shownCells == null || _shownCells.Length != targetCells.Length)
                {
                    foreach (var cell in _shownCells ?? Array.Empty<Vector3Int>())
                        _cellRenderer.HideCell(cell);

                    foreach (var cell in targetCells)
                        _cellRenderer.ShowCell(cell, _activeColor, _cellSurface);

                    _shownCells = targetCells;
                    return;
                }

                _stepFromCells = (Vector3Int[])_shownCells.Clone();
                _stepToCells = (Vector3Int[])targetCells.Clone();
                _visualStepActive = true;

                var startPositions = new Vector3[_stepFromCells.Length];
                for (int i = 0; i < startPositions.Length; i++)
                    startPositions[i] = _stepFromCells[i];

                _cellRenderer.MoveCells(_stepFromCells, _stepToCells, startPositions);
                _shownCells = _stepToCells;
            }

            float easedProgress = _controller.EvaluatedStepProgress;
            for (int i = 0; i < _stepToCells.Length; i++)
            {
                var visualPosition = Vector3.Lerp(_stepFromCells[i], _stepToCells[i], easedProgress);
                _cellRenderer.SetCellVisualPosition(_stepToCells[i], visualPosition);
            }
        }

        private void FinishVisualStep()
        {
            if (!_visualStepActive) return;

            for (int i = 0; i < _stepToCells.Length; i++)
                _cellRenderer.SetCellVisualPosition(_stepToCells[i], _stepToCells[i]);

            _stepFromCells = null;
            _stepToCells = null;
            _visualStepActive = false;
        }

        private static bool CellsEqual(Vector3Int[] a, Vector3Int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void SpawnNext()
        {
            if (_spawningStopped) return;

            var shapeIndex = PickNextShapeIndex();
            RecordPick(shapeIndex);
            var shape = _shapes[shapeIndex];

            var startPosition = CenteredTopStart(shape);

            // The stack has grown all the way up to the spawn point. Without this check the new
            // piece would silently lock on its very first tick, indistinguishable from a freeze.
            if (!PlacementRules.CanPlaceAt(_grid, shape, startPosition))
            {
                SpawnBlockedWellFull = true;
                _spawningStopped = true;
                Debug.LogError($"[BlockoutSpawner] Well is full at spawn - the next piece's start position {startPosition} is already occupied. Stopping spawns.", this);
                WellFull?.Invoke();
                return;
            }

            _activeColor = _pieceColor;

            var pieceObject = new GameObject("BlockoutPieceController");
            _controller = pieceObject.AddComponent<PieceController>();
            _controller.Locked += OnPieceLocked;
            _controller.Initialize(shape, startPosition, _fallCurve, _grid, _timeDifficulty);

            SyncActivePieceVisual(); // show immediately rather than waiting for the next Update()
        }

        // Weighted random pick with a linear cooldown: a shape can't repeat immediately (its
        // weight is 0 right after being picked) and its odds grow back by 1 per spawn the longer
        // it's been benched, capped at _shapes.Count - 1 so no single shape's weight runs away
        // over a long run.
        private int PickNextShapeIndex()
        {
            // The capped-weight formula below would give a cap of Count - 1 = 0 for every entry
            // (an always-zero sum) - only one choice exists anyway, so skip straight to it.
            if (_shapes.Count == 1) return 0;

            int cap = _shapes.Count - 1;
            int totalWeight = 0;
            for (int i = 0; i < _shapes.Count; i++)
                totalWeight += Mathf.Min(_spawnsSinceLastPick[i], cap);

            int roll = _random.Next(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < _shapes.Count; i++)
            {
                cumulative += Mathf.Min(_spawnsSinceLastPick[i], cap);
                if (roll < cumulative) return i;
            }

            return _shapes.Count - 1; // unreachable while roll < totalWeight; kept as a safe fallback
        }

        private void RecordPick(int pickedIndex)
        {
            for (int i = 0; i < _spawnsSinceLastPick.Length; i++)
                _spawnsSinceLastPick[i] = i == pickedIndex ? 0 : _spawnsSinceLastPick[i] + 1;
        }

        private void OnPieceLocked(int[] clearedLayerYs)
        {
            _controller.Locked -= OnPieceLocked;

            // Locking and respawning below are fully synchronous (no frame boundary in between),
            // so Update() never gets a chance to observe this controller's final GridPosition
            // before _controller is reassigned to the next piece - sync here instead, or the
            // shown cells are left wherever they were on the last regular frame (looks frozen
            // mid-air after a hard drop in particular, since that can skip several cells at once).
            SyncActivePieceVisual();

            if (_cellRenderer != null && _shownCells != null)
            {
                // Recolors the already-shown cells in place (ShowCell again at the same position),
                // rather than hiding and re-showing them. Per cell, not per piece: a piece
                // spanning two levels ends up in two colours.
                foreach (var cell in _shownCells)
                {
                    var levelColor = _wellConfig != null ? _wellConfig.GetLevelColor(cell.y) : _activeColor;
                    _cellRenderer.ShowCell(cell, levelColor, _cellSurface);
                }
            }

            // These cells are now permanent (locked), not "the active piece" anymore - nothing
            // further should hide them on this spawner's account.
            _shownCells = null;

            // Must run before SpawnNext() below: SpawnNext immediately shows the next piece's
            // cells via WellCellRenderer, and CollapseLayer's shift-everything-above-down pass
            // would wrongly drag those freshly-shown cells down with it if the next piece were
            // already on screen when this runs.
            HandleLayerClears(clearedLayerYs);

            Destroy(_controller.gameObject);
            _controller = null;

            // Judged only now, after the lock AND the clear/collapse above have resolved: a piece
            // can lock above h and be fine if the collapse brings everything back down.
            if (CheckGameOver())
            {
                // Same game-over path as a blocked spawn: BlockoutGameplayState only listens for
                // WellFull, it doesn't care which of the two triggers raised it.
                SpawnBlockedWellFull = true;
                _spawningStopped = true;
                Debug.Log("[BlockoutSpawner] A locked cell remains above the well's game-over line (Y >= height). Stopping spawns.", this);
                WellFull?.Invoke();
                return;
            }

            SpawnNext();
        }

        private bool CheckGameOver() => _grid.AnyOccupiedAtOrAbove(_wellHeight);

        // Mirrors PlacementRules.ClearFullLayersTouchedBy's grid collapse in the pooled visuals
        // (WellCellRenderer has no way to hear about a clear on its own - VoxelGrid's occupancy
        // data carries no rendering/color concept) and hands the exact cleared cell
        // positions/colors to the active skin's clear behaviour. clearedLayerYs is already in the
        // highest-first order PlacementRules processed it in, which CollapseLayer relies on for a
        // simultaneous multi-layer clear to collapse correctly.
        private void HandleLayerClears(int[] clearedLayerYs)
        {
            if (_cellRenderer == null || clearedLayerYs == null || clearedLayerYs.Length == 0) return;

            var clearedPositions = new List<Vector3Int>();
            var clearedColors = new List<Color>();

            foreach (var y in clearedLayerYs)
            {
                _cellRenderer.CollapseLayer(y, _wellWidth, _wellDepth, _grid.StorageHeight, clearedPositions, clearedColors,
                    _wellConfig != null ? _wellConfig.GetLevelColor : null);
            }

            _clearBehaviour?.OnLayersCleared(new BlockoutClearContext(clearedPositions, clearedColors, clearedLayerYs.Length));
        }

        // Centers the shape horizontally in the well and anchors its reference cell (the shape's
        // local (0,0,0), i.e. GridPosition) at Y = h - 1, the well's top row.
        // Game over is then any locked cell that ends up at Y >= h, outside the well.
        // Cells with a positive local Y therefore start above h - expected, never a block.
        private Vector3Int CenteredTopStart(PolycubeShape shape)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;

            foreach (var cell in shape.Cells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
                if (cell.z < minZ) minZ = cell.z;
                if (cell.z > maxZ) maxZ = cell.z;
            }

            int originX = (_wellWidth - (maxX - minX + 1)) / 2 - minX;
            int originZ = (_wellDepth - (maxZ - minZ + 1)) / 2 - minZ;
            int originY = _wellHeight - 1;

            return new Vector3Int(originX, originY, originZ);
        }
    }
}
