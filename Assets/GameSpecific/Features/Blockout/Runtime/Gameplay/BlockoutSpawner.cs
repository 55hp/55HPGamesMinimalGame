using System;
using System.Collections.Generic;
using UnityEngine;
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
    public sealed class BlockoutSpawner : MonoBehaviour
    {
        private VoxelGrid _grid;
        private int _wellWidth;
        private int _wellHeight;
        private int _wellDepth;
        private BlockoutFallCurveConfig _fallCurve;
        private BlockoutTimeDifficultyModifier _timeDifficulty;

        // Cycled in order (rather than picked randomly) so a run is repeatable while eyeballing
        // fall/lock timing - call it if you'd rather have random.
        private IReadOnlyList<PolycubeShape> _shapes;
        private int _nextShapeIndex;

        private WellCellRenderer _cellRenderer;
        private PieceController _controller;

        [Tooltip("Technical Doc Phase 2 stand-in for the active skin's clear behaviour, until Phase 3's active-skin service resolves this from the persisted active BlockoutSkin instead. Left unassigned, a layer clear still collapses/scores normally, just with no visual reaction dispatched.")]
        [SerializeField] private BlockoutClearBehaviour _clearBehaviour;

        // The active piece's currently-shown cell positions and color: tracked so a change in
        // GridPosition/Shape (fall step, move, rotate, hard drop) hides exactly the old set and
        // shows exactly the new one, instead of re-showing every cell every frame regardless of
        // whether anything actually moved. Null when there's no active (unlocked) piece.
        private Vector3Int[] _shownCells;
        private Color _activeColor;

        private bool _spawningStopped;

        // Fixed per-shape palette (indexed by the shape's position in the set passed to
        // Initialize - wraps if there are ever more shapes than colors): the same shape always
        // gets the same color, every run. Distinct from each other and from the wireframe's green
        // (BlockoutWellWireframe). Editable here rather than hardcoded so Franci can retune it
        // without recompiling. Locked cells are dimmed from whichever of these was used, at lock
        // time (OnPieceLocked), independent of WellCellRenderer's own rendering mechanism.
        [SerializeField]
        private Color[] _pieceColors =
        {
            new Color(0.902f, 0.098f, 0.294f), // red
            new Color(0.961f, 0.510f, 0.192f), // orange
            new Color(1.000f, 0.882f, 0.098f), // yellow
            new Color(0.263f, 0.388f, 0.847f), // blue
            new Color(0.569f, 0.118f, 0.706f), // purple
            new Color(0.275f, 0.941f, 0.941f), // cyan
            new Color(0.941f, 0.196f, 0.902f), // magenta
            new Color(0.980f, 0.745f, 0.831f), // pink
            new Color(0.604f, 0.388f, 0.141f), // brown
            new Color(0.502f, 0.000f, 0.000f), // maroon
            new Color(0.000f, 0.000f, 0.459f), // navy
            new Color(0.863f, 0.745f, 1.000f), // lavender
        };

        private const float LockedSaturationFactor = 0.35f;
        private const float LockedValueFactor = 0.55f;

        // True once SpawnNext refused to spawn because the well is already full at the computed
        // spawn position (see SpawnNext). Public so PlayMode tests (and BlockoutDebugOverlay) can
        // read this instead of a silent lock.
        public bool SpawnBlockedWellFull { get; private set; }
        public PieceController CurrentPiece => _controller;

        // Fired once, the moment SpawnNext refuses to spawn because the well is full - see
        // SpawnBlockedWellFull. BlockoutGameplayState listens for this to publish
        // BlockoutGameOverEvent and drive the FSM to ResultState; no recovery happens here.
        public event Action WellFull;

        // grid/fallCurve/shapes/dimensions are passed in rather than resolved here so the spawn
        // validation logic stays testable without needing IConfigCatalogService wired up (mirrors
        // PieceController.Initialize). Called by BlockoutGameplayState.EnterAsync, which is the
        // sole entry point - this class doesn't self-start. clearBehaviour is null by default so
        // production callers (which don't pass one) leave whatever's assigned in the Inspector
        // untouched - only a non-null value here overrides it, which is what lets tests exercise
        // the clear-behaviour dispatch without an Inspector-assigned asset.
        public void Initialize(VoxelGrid grid, BlockoutFallCurveConfig fallCurve, BlockoutTimeDifficultyConfig timeDifficultyConfig, IReadOnlyList<PolycubeShape> shapes, int wellWidth, int wellHeight, int wellDepth, BlockoutClearBehaviour clearBehaviour = null)
        {
            if (clearBehaviour != null) _clearBehaviour = clearBehaviour;

            // Fresh per run, per spec (the session timer restarts from zero on every new game) -
            // dispose the previous run's subscription before replacing it.
            _timeDifficulty?.Dispose();
            _timeDifficulty = new BlockoutTimeDifficultyModifier(timeDifficultyConfig);

            if (_cellRenderer == null)
            {
                _cellRenderer = FindObjectOfType<WellCellRenderer>();
                if (_cellRenderer == null)
                {
                    Debug.LogError("[BlockoutSpawner] No WellCellRenderer found in the scene - pieces will spawn with no visual.", this);
                }
            }

            // Leaves no visual trace of a previous run: every locked cell, plus whatever the
            // active piece was showing.
            _cellRenderer?.HideAll();
            _controller = null;
            _shownCells = null;

            _grid = grid;
            _fallCurve = fallCurve;
            _shapes = shapes;
            _wellWidth = wellWidth;
            _wellHeight = wellHeight;
            _wellDepth = wellDepth;
            _nextShapeIndex = 0;
            _spawningStopped = false;
            SpawnBlockedWellFull = false;

            SpawnNext();
        }

        private void OnDestroy()
        {
            _timeDifficulty?.Dispose();
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

            if (_shownCells != null && CellsEqual(_shownCells, newCells)) return;

            if (_shownCells != null)
            {
                foreach (var cell in _shownCells) _cellRenderer.HideCell(cell);
            }

            foreach (var cell in newCells) _cellRenderer.ShowCell(cell, _activeColor);
            _shownCells = newCells;
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

            var shapeIndex = _nextShapeIndex;
            var shape = _shapes[shapeIndex];
            _nextShapeIndex = (_nextShapeIndex + 1) % _shapes.Count;

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

            _activeColor = _pieceColors != null && _pieceColors.Length > 0
                ? _pieceColors[shapeIndex % _pieceColors.Length]
                : Color.white;

            var pieceObject = new GameObject("BlockoutPieceController (TEMP)");
            _controller = pieceObject.AddComponent<PieceController>();
            _controller.Locked += OnPieceLocked;
            _controller.Initialize(shape, startPosition, _fallCurve, _grid, _timeDifficulty);

            SyncActivePieceVisual(); // show immediately rather than waiting for the next Update()
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
                Color.RGBToHSV(_activeColor, out float h, out float s, out float v);
                var lockedColor = Color.HSVToRGB(h, s * LockedSaturationFactor, v * LockedValueFactor);

                // Recolors the already-shown cells in place (ShowCell again at the same position),
                // rather than hiding and re-showing them - that's the "locked cell" placeholder.
                foreach (var cell in _shownCells) _cellRenderer.ShowCell(cell, lockedColor);
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
            SpawnNext();
        }

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
                _cellRenderer.CollapseLayer(y, _wellWidth, _wellDepth, _wellHeight, clearedPositions, clearedColors);
            }

            IBlockoutClearBehaviour behaviour = _clearBehaviour;
            behaviour?.OnLayersCleared(new BlockoutClearContext(clearedPositions, clearedColors, clearedLayerYs.Length));
        }

        // Centers the shape horizontally in the well and drops its topmost cell to the well's
        // ceiling, so any shape from the set spawns fully in-bounds regardless of how its cells
        // extend from (0,0,0).
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
            int originY = _wellHeight - 1 - maxY;

            return new Vector3Int(originX, originY, originZ);
        }
    }
}
