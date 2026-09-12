using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Gameplay
{
    // Standalone wiring for a first visual test of PieceController's fall + lock behavior:
    // resolves its own config, owns the well's VoxelGrid, and keeps spawning pieces into it as
    // each one locks. Not driven by BlockoutGameplayState or the FSM - delete once the real game
    // state owns spawning, well placement, and rendering.
    public sealed class BlockoutSpawner : MonoBehaviour
    {
        private VoxelGrid _grid;
        private int _wellWidth;
        private int _wellHeight;
        private int _wellDepth;
        private BlockoutFallCurveConfig _fallCurve;

        // Cycled in order (rather than picked randomly) so a run is repeatable while eyeballing
        // fall/lock timing - call it if you'd rather have random.
        private IReadOnlyList<PolycubeShape> _shapes;
        private int _nextShapeIndex;

        private PieceController _controller;
        private Transform[] _cellCubes;
        private Renderer[] _cellRenderers;
        private Vector3Int[] _cellOffsets;

        // Every cube's Renderer.material access up above instantiates a unique Material that
        // Unity never destroys on its own. Locked cubes are never despawned (no layer-clear/
        // pooling yet), so there's no per-cube destroy point - tracked here instead and released
        // in OnDestroy, the only point where cleanup doesn't break a still-visible locked cell.
        private readonly List<Material> _spawnedMaterials = new();

        private bool _spawningStopped;

        // Fixed per-shape palette (indexed by the shape's position in the set passed to
        // Initialize - wraps if there are ever more shapes than colors): the same shape always
        // gets the same color, every run. Distinct from each other and from the wireframe's green
        // (BlockoutWellWireframe). Editable here rather than hardcoded so Franci can retune it
        // without recompiling. Locked cells are dimmed from whichever of these was used, at lock
        // time (LockCurrentPieceColor), reusing the cube's existing material instance rather than
        // creating a new one or a separate render system.
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
        // spawn position (see SpawnNext). Not recovered from here - that's BlockoutGameplayState's
        // job (out of scope). Public so PlayMode tests can assert this instead of a silent lock.
        public bool SpawnBlockedWellFull { get; private set; }
        public PieceController CurrentPiece => _controller;
        public IReadOnlyList<Transform> CurrentPieceCubes => _cellCubes;

        private void Start()
        {
            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[BlockoutSpawner] IConfigCatalogService is not registered - add a ConfigCatalogInstaller (with a populated ConfigCatalog) to the scene.", this);
                return;
            }

            var fallCurve = catalogService.Get<BlockoutFallCurveConfig>();
            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            if (fallCurve == null || wellConfig == null)
            {
                Debug.LogError("[BlockoutSpawner] Missing BlockoutFallCurveConfig or BlockoutWellConfig in the catalog.", this);
                return;
            }

            var well = new BlockoutWell(wellConfig);
            Initialize(well.Grid, fallCurve, BlockoutShapeSet.BuildDefault(), well.Width, well.Height, well.Depth);
        }

        // grid/fallCurve/shapes/dimensions are passed in rather than resolved here so the spawn
        // validation logic stays testable without needing IConfigCatalogService wired up (mirrors
        // PieceController.Initialize).
        public void Initialize(VoxelGrid grid, BlockoutFallCurveConfig fallCurve, IReadOnlyList<PolycubeShape> shapes, int wellWidth, int wellHeight, int wellDepth)
        {
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

        private void Update()
        {
            if (_controller == null) return;
            SyncCubesToCurrentGridPosition();
        }

        // Locked cubes stay visible for the rest of the session (no layer-clear/pooling yet), so
        // their materials can only be released once the spawner itself goes away - scene unload
        // or leaving Play mode. There's no earlier safe point without also making cells disappear.
        private void OnDestroy()
        {
            foreach (var material in _spawnedMaterials)
            {
                if (material != null) Destroy(material);
            }
        }

        private void SyncCubesToCurrentGridPosition()
        {
            for (int i = 0; i < _cellCubes.Length; i++)
            {
                _cellCubes[i].position = (Vector3)(_controller.GridPosition + _cellOffsets[i]);
            }
        }

        private void SpawnNext()
        {
            if (_spawningStopped) return;

            var shapeIndex = _nextShapeIndex;
            var shape = _shapes[shapeIndex];
            _nextShapeIndex = (_nextShapeIndex + 1) % _shapes.Count;

            var startPosition = CenteredTopStart(shape);

            // The stack may have grown all the way up to the spawn point (no game-over/well-full
            // handling exists yet - that's BlockoutGameplayState's job). Without this check the
            // new piece would silently lock on its very first tick, indistinguishable from a freeze.
            if (!PlacementRules.CanPlaceAt(_grid, shape, startPosition))
            {
                SpawnBlockedWellFull = true;
                _spawningStopped = true;
                Debug.LogError($"[BlockoutSpawner] Well is full at spawn - the next piece's start position {startPosition} is already occupied. Stopping spawns (no recovery here; well-full/game-over handling belongs to BlockoutGameplayState, out of scope for this spawner).", this);
                return;
            }

            var cells = shape.Cells;

            // TEMPORARY placeholder visuals: one plain cube per shape cell, not pooled, no
            // material/renderer service. Once the piece locks, LockCurrentPieceColor dims these
            // same cubes in place - that's the "locked cell" placeholder, so stacking is visible.
            // Replace with WellCellRenderer once that lands.
            var color = _pieceColors != null && _pieceColors.Length > 0
                ? _pieceColors[shapeIndex % _pieceColors.Length]
                : Color.white;
            _cellOffsets = new Vector3Int[cells.Count];
            _cellCubes = new Transform[cells.Count];
            _cellRenderers = new Renderer[cells.Count];

            for (int i = 0; i < cells.Count; i++)
            {
                _cellOffsets[i] = cells[i];

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "BlockoutPiece (TEMP)";
                var renderer = cube.GetComponent<Renderer>();
                renderer.material.color = color;
                cube.transform.position = (Vector3)(startPosition + cells[i]);

                _cellCubes[i] = cube.transform;
                _cellRenderers[i] = renderer;
                _spawnedMaterials.Add(renderer.material);
            }

            var pieceObject = new GameObject("BlockoutPieceController (TEMP)");
            _controller = pieceObject.AddComponent<PieceController>();
            _controller.Locked += OnPieceLocked;
            _controller.Initialize(shape, startPosition, _fallCurve, _grid);
        }

        private void OnPieceLocked()
        {
            _controller.Locked -= OnPieceLocked;

            // Locking and respawning below are fully synchronous (no frame boundary in between),
            // so Update() never gets a chance to observe this controller's final GridPosition
            // before _controller is reassigned to the next piece - sync here instead, or these
            // cubes are left wherever they were on the last regular frame (looks frozen mid-air
            // after a hard drop in particular, since that can skip several cells in one go).
            SyncCubesToCurrentGridPosition();
            LockCurrentPieceColor();

            // Don't touch _cellCubes any further after this - leaving them where they are IS the
            // locked placeholder. Only the (invisible) controller object is discarded.
            Destroy(_controller.gameObject);
            _controller = null;
            SpawnNext();
        }

        // Dims each cube's already-instantiated material in place (same hue, less saturated and
        // darker) rather than creating a new material or a separate render system.
        private void LockCurrentPieceColor()
        {
            for (int i = 0; i < _cellRenderers.Length; i++)
            {
                Color.RGBToHSV(_cellRenderers[i].material.color, out float h, out float s, out float v);
                _cellRenderers[i].material.color = Color.HSVToRGB(h, s * LockedSaturationFactor, v * LockedValueFactor);
            }
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
