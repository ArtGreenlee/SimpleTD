using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;

[RequireComponent(typeof(PolygonCollider2D))]
public class GridManager : MonoBehaviour
{
    public enum WallPlacementValidationResult
    {
        Valid,
        EmptySelection,
        InvalidDirection,
        NoBuildableCells,
        EnemyEndpoint,
        ParallelWallTooClose,
        BlocksAllPaths,
        ForcedGapTooNarrow,
        TooShortNeedsBridge,
        InvalidPlacement,
    }

    /// <summary>
    /// Data stored per grid cell.
    /// </summary>
    [Serializable]
    public struct Cell
    {
        /// <summary>Cell index in the grid (0..CellsX-1,0..CellsY-1).</summary>
        public Vector2Int Index;

        /// <summary>World-space center position of this cell in the XY plane.</summary>
        public Vector3 WorldCenter;

        /// <summary>True if this cell is blocked for navigation/gameplay.</summary>
        public bool IsBlocked;

        /// <summary>True if this cell contains a wall (buildable surface for towers).</summary>
        public bool IsWall;

        /// <summary>True if towers can be placed on this wall cell.</summary>
        public bool TowerPlacementEnabled;

        /// <summary>
        /// Precomputed4-way neighbors (grid adjacency). Contains only in-bounds neighbors.
        /// </summary>
        public Vector2Int[] Neighbors;

        /// <summary>
        /// Precomputed distance (in grid cells) to the nearest blocked cell (wall). Used for path biasing.
        /// Larger values mean farther from walls.
        /// </summary>
        public float NearestBlocked;

        /// <summary>
        /// Non-negative score representing how "dangerous" this cell is.
        /// Higher values can bias pathfinding away from it.
        /// </summary>
        public float DangerScore;

        public Cell(Vector2Int index, Vector3 worldCenter)
        {
            Index = index;
            WorldCenter = worldCenter;
            IsBlocked = false;
            IsWall = false;
            TowerPlacementEnabled = false;
            NearestBlocked =0f;
            Neighbors = Array.Empty<Vector2Int>();
            DangerScore =0f;
        }
    }

    public enum  Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum MazeBuildMode
    {
        PerfectMaze,
        IslandMode
    }

    public enum WallTowerPlacementEnableMode
    {
        None,
        End
    }

    private struct Node
    {
        public int x;
        public int y;

        public Node(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public Vector2Int DirectionToVector2Int(Direction dir)
    {
        return dir switch
        {
            Direction.Up => new Vector2Int(0, 1),
            Direction.Down => new Vector2Int(0, -1),
            Direction.Left => new Vector2Int(-1, 0),
            Direction.Right => new Vector2Int(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }

    [Header("Grid")]
    [Min(0.001f)]
    [SerializeField] private float spacing =1f;

    [Min(1)]
    [SerializeField] private int cellsX =10;

    // Interpreted as Y cell count (grid is on XY plane)
    [Min(1)]
    [SerializeField] private int cellsZ =10;

    [Header("Maze Generation")]
    [SerializeField] private Pathfinding pathfinding;
    [SerializeField] private MazeData mazeDataAsset;
    [Tooltip("Random seed. If 0, a random seed is used when generating a new maze.")]
    [SerializeField] private int seed = 0;
    [SerializeField] private MazeBuildMode mazeBuildMode = MazeBuildMode.PerfectMaze;
    [Tooltip("Corridor width in grid cells.")]
    [Min(1)]
    [SerializeField] private int corridorWidth = 1;
    [Tooltip("Wall thickness in grid cells.")]
    [Min(1)]
    [SerializeField] private int wallThickness = 1;

    [Header("Island Mode")]
    [Tooltip("Maximum number of single-cell island walls placed inside the border.")]
    [SerializeField, Min(0)] private int islandModeMaxCells = 24;
    [Tooltip("Minimum poisson-disc spacing between island wall cells, measured in grid cells.")]
    [SerializeField, Min(1f)] private float islandModeMinCellSpacing = 2f;
    [Tooltip("Minimum distance in grid cells that island walls must stay away from the outer border.")]
    [SerializeField, Min(0f)] private float islandModeMinDistanceFromBorder = 1f;
    [Tooltip("Number of poisson-disc candidate attempts generated from each active sample.")]
    [SerializeField, Min(1)] private int islandModeRejectionSamples = 30;

    [Header("Player Wall Placement")]
    [Tooltip("If true, the player can build walls along cells adjacent to tower-placement-enabled walls.")]
    public bool wallPlacementEnabled = false;

    [Tooltip("Minimum world-space length required for a player wall placement unless it bridges two separate existing wall groups.")]
    [SerializeField, Min(0f)] private float minimumWallPlacementLengthUnits = 2f;

    [Tooltip("Controls which newly built player wall cells get tower placement enabled.")]
    [SerializeField] private WallTowerPlacementEnableMode wallTowerPlacementEnableMode = WallTowerPlacementEnableMode.End;

    [Header("Wall Placement Selection")]
    [Tooltip("World-space range used to score a wall by nearby empty cells inside the field.")]
    [SerializeField, Min(0f)] private float towerPlacementScoreRange = 4f;
    [Tooltip("Amount subtracted from nearby wall scores after one wall is selected.")]
    [SerializeField, Min(0f)] private float towerPlacementNeighborPenalty = 8f;
    [Tooltip("Maximum number of wall cells that will allow tower placement.")]
    [SerializeField, Min(0)] private int enabledWallPlacementCount = 12;
    [Tooltip("If true, border walls can be included when selecting walls to enable tower placement on.")]
    [SerializeField] private bool includeBorderWallsInTowerEnablementSelection = true;

    [Header("Maze Rendering")]
    [SerializeField] private Material meshMaterial;
    [SerializeField] private Material meshPlacementDisabledMaterial;
    [SerializeField] private int mazeRendererSortingOrder = -10;
    [SerializeField] private bool createMasks = false;
    [SerializeField] private GameObject maskPrefab;
    [SerializeField] private bool roundMazeMeshCorners = false;
    [SerializeField, Min(0f)] private float mazeMeshCornerRoundAmount = 0.1f;
    [SerializeField, Min(1)] private int mazeMeshCornerFilletSegments = 4;
    [SerializeField] private bool filletConvexMeshVertices = true;
    [SerializeField] private bool filletConcaveMeshVertices = false;

    [NonSerialized] private Dictionary<Vector2Int, Cell> _cellsByIndex;

    private readonly Dictionary<Vector2Int, Tower> _towersByCell = new Dictionary<Vector2Int, Tower>(128);

    [Header("Maze Bounds")]
    [Tooltip("If a MazeGenerator sets bounds, cells outside are considered outside the maze.")]
    [SerializeField] private bool useMazeBoundsForPlacement = true;

    [SerializeField, HideInInspector] private bool hasMazeBounds;
    [SerializeField, HideInInspector] private RectInt mazeBounds;
    [SerializeField, HideInInspector] private List<GameObject> generatedMaskObjects = new List<GameObject>(4);

    public static GridManager instance;
    public MazeData mazeData => mazeDataAsset;

    public int WalkabilityVersion { get; private set; }

    public bool TryGetMazeBounds(out RectInt bounds)
    {
        bounds = mazeBounds;
        return hasMazeBounds;
    }

    public bool CardinalAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(b.x - a.x);
        int dy = Mathf.Abs(b.y - a.y);
        return (dx + dy) == 1;
    }


    public void ClearMazeBounds()
    {
        hasMazeBounds = false;
        mazeBounds = default;
    }

    public void SetMazeBounds(RectInt bounds)
    {
        // Clamp to non-negative and to grid size if available.
        int x = Mathf.Max(0, bounds.x);
        int y = Mathf.Max(0, bounds.y);
        int w = Mathf.Max(0, bounds.width);
        int h = Mathf.Max(0, bounds.height);

        if (w == 0 || h == 0)
        {
            ClearMazeBounds();
            return;
        }

        hasMazeBounds = true;
        mazeBounds = new RectInt(x, y, w, h);
    }

    /// <summary>
    /// Returns true if the cell is inside the active maze bounds.
    /// If no bounds are set (or bounds are disabled), returns true.
    /// </summary>
    public bool IsInMazeBounds(Vector2Int idx)
    {
        if (!useMazeBoundsForPlacement) return true;
        if (!hasMazeBounds) return true;
        return mazeBounds.Contains(idx);
    }

    public void Awake()
    {
        instance = this;
    }

    public void ConfigureMaze(Pathfinding pathfindingRef, MazeData data, MazeBuildMode buildMode, int mazeSeed, int mazeCorridorWidth, int mazeWallThickness, int islandMaxCells, float islandMinCellSpacing, float islandMinDistanceFromBorder, int islandRejectionSamples, float scoreRange, float neighborPenalty, int enabledCount, bool includeBorderWallsInTowerEnablementSelection, Material placementEnabledMaterial, Material placementDisabledMaterial)
    {
        if (pathfindingRef != null || pathfinding == null) pathfinding = pathfindingRef;
        if (data != null || mazeDataAsset == null) mazeDataAsset = data;

        mazeBuildMode = buildMode;
        seed = mazeSeed;
        corridorWidth = Mathf.Max(1, mazeCorridorWidth);
        wallThickness = Mathf.Max(1, mazeWallThickness);
        islandModeMaxCells = Mathf.Max(0, islandMaxCells);
        islandModeMinCellSpacing = Mathf.Max(1f, islandMinCellSpacing);
        islandModeMinDistanceFromBorder = Mathf.Max(0f, islandMinDistanceFromBorder);
        islandModeRejectionSamples = Mathf.Max(1, islandRejectionSamples);
        towerPlacementScoreRange = Mathf.Max(0f, scoreRange);
        towerPlacementNeighborPenalty = Mathf.Max(0f, neighborPenalty);
        enabledWallPlacementCount = Mathf.Max(0, enabledCount);
        this.includeBorderWallsInTowerEnablementSelection = includeBorderWallsInTowerEnablementSelection;

        if (placementEnabledMaterial != null || meshMaterial == null) meshMaterial = placementEnabledMaterial;
        if (placementDisabledMaterial != null || meshPlacementDisabledMaterial == null) meshPlacementDisabledMaterial = placementDisabledMaterial;
    }

    public int CellsX => cellsX;
    public int CellsY => cellsZ;

    public float GetSpacing() { return spacing; }
    public float MinimumWallPlacementLengthUnits => minimumWallPlacementLengthUnits;
    public bool TryGetCell(int x, int y, out Cell cell)
    {
        if (_cellsByIndex == null)
        {
            cell = default;
            return false;
        }

        return _cellsByIndex.TryGetValue(new Vector2Int(x, y), out cell);
    }

    public Tower GetFirstTowerInDirection(Vector3 towerPosition, Direction dir)
    {
        Vector2Int d = DirectionToVector2Int(dir);
        Vector2Int current = WorldToGrid(towerPosition) + d;
        while (IsInBounds(current))
        {
            if (TryGetTowerAtCell(current, out var tower))
            {
                return tower;
            }
            current += d;
        }
        return null;
    }

    public Vector3 GetEdgeOfGridInDirection(Vector3 pos, Direction dir)
    {
        Vector2Int d = DirectionToVector2Int(dir);
        Vector2Int current = WorldToGrid(pos);
        while (IsInBounds(current))
        {
            current += d;
        }
        // Step back to last in-bounds cell.
        current -= d;
        if (TryGetCell(current.x, current.y, out var cell))
        {
            return cell.WorldCenter;
        }
        else
        {
            // Fallback to original position if something goes wrong.
            return pos;
        }
    }

    public bool TrySetCell(Cell cell)
    {
        if (_cellsByIndex == null) return false;

        int x = cell.Index.x;
        int y = cell.Index.y;
        if (x <0 || y <0 || x >= cellsX || y >= cellsZ) return false;

        bool blockedChanged = false;
        if (_cellsByIndex.TryGetValue(cell.Index, out var existing))
        {
            blockedChanged = existing.IsBlocked != cell.IsBlocked;
        }

        _cellsByIndex[cell.Index] = cell;

        if (blockedChanged)
        {
            WalkabilityVersion++;
        }

        // Recompute adjacency for this cell and its surrounding neighbors,
        // because diagonal permissions depend on adjacent blocked state.
        RecomputeAdjacencyAround(cell.Index);

        MarkNearestBlockedDirty();
        return true;
    }

    private void RecomputeAdjacencyAround(Vector2Int center)
    {
        if (_cellsByIndex == null) return;

        for (int dy =-1; dy <=1; dy++)
        {
            for (int dx =-1; dx <=1; dx++)
            {
                var idx = new Vector2Int(center.x + dx, center.y + dy);
                if (!IsInBounds(idx)) continue;
                if (!_cellsByIndex.TryGetValue(idx, out var c)) continue;
                c.Neighbors = ComputeNeighbors(idx);
                _cellsByIndex[idx] = c;
            }
        }
    }

    public Vector3 GetCellWorldCenter(int x, int y)
    {
        float width = cellsX * spacing;
        float height = cellsZ * spacing;

        float x0 = -width *0.5f;
        float y0 = -height *0.5f;

        return transform.TransformPoint(new Vector3(x0 + (x +0.5f) * spacing, y0 + (y +0.5f) * spacing,0f));
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        if (TryWorldToCell(world, out var cell)) return cell;
        return new Vector2Int(-1, -1);
    }

    public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(world);

        float width = cellsX * spacing;
        float height = cellsZ * spacing;
        float x0 = -width *0.5f;
        float y0 = -height *0.5f;

        int x = Mathf.FloorToInt((local.x - x0) / spacing);
        int y = Mathf.FloorToInt((local.y - y0) / spacing);

        if (x <0 || y <0 || x >= cellsX || y >= cellsZ)
        {
            cell = default;
            return false;
        }

        cell = new Vector2Int(x, y);
        return true;
    }

    private void EnsureCells()
    {
        int desired = Mathf.Max(1, cellsX) * Mathf.Max(1, cellsZ);
        if (_cellsByIndex != null && _cellsByIndex.Count == desired) return;

        _cellsByIndex = new Dictionary<Vector2Int, Cell>(desired);

        // Build cells.
        for (int y =0; y < cellsZ; y++)
        {
            for (int x =0; x < cellsX; x++)
            {
                var idx = new Vector2Int(x, y);
                var center = GetCellWorldCenter(x, y);
                var cell = new Cell(idx, center);
                _cellsByIndex[idx] = cell;
            }
        }

        // Precompute adjacency.
        RecomputeAllAdjacency();
    }

    private void RecomputeAllAdjacency()
    {
        if (_cellsByIndex == null) return;

        var keys = new List<Vector2Int>(_cellsByIndex.Keys);
        for (int i =0; i < keys.Count; i++)
        {
            var idx = keys[i];
            var c = _cellsByIndex[idx];
            c.Neighbors = ComputeNeighbors(idx);
            _cellsByIndex[idx] = c;
        }
    }

    public Vector2Int[] GetAllCellsByIndex()
    {
        if (_cellsByIndex == null) return Array.Empty<Vector2Int>();

        Vector2Int[] allCells = new Vector2Int[_cellsByIndex.Count];
        int i = 0;
        foreach (var cell in _cellsByIndex.Keys)
        {
            allCells[i++] = cell;
        }
        return allCells;
    }

    private Vector2Int[] ComputeNeighbors(Vector2Int idx)
    {
        //8-way adjacency with corner-cutting prevention.
        // A diagonal is only allowed if BOTH adjacent cardinals are not blocked.

        int count =0;
        Vector2Int n;

        // Cardinals
        n = new Vector2Int(idx.x +1, idx.y);
        if (IsInBounds(n)) count++;
        n = new Vector2Int(idx.x -1, idx.y);
        if (IsInBounds(n)) count++;
        n = new Vector2Int(idx.x, idx.y +1);
        if (IsInBounds(n)) count++;
        n = new Vector2Int(idx.x, idx.y -1);
        if (IsInBounds(n)) count++;

        // Diagonals (require both adjacent cardinals to be passable)
        n = new Vector2Int(idx.x +1, idx.y +1);
        if (CanUseDiagonal(idx, n)) count++;
        n = new Vector2Int(idx.x +1, idx.y -1);
        if (CanUseDiagonal(idx, n)) count++;
        n = new Vector2Int(idx.x -1, idx.y -1);
        if (CanUseDiagonal(idx, n)) count++;
        n = new Vector2Int(idx.x -1, idx.y +1);
        if (CanUseDiagonal(idx, n)) count++;

        var result = new Vector2Int[count];
        int wi =0;

        // Cardinals
        n = new Vector2Int(idx.x +1, idx.y);
        if (IsInBounds(n)) result[wi++] = n;
        n = new Vector2Int(idx.x -1, idx.y);
        if (IsInBounds(n)) result[wi++] = n;
        n = new Vector2Int(idx.x, idx.y +1);
        if (IsInBounds(n)) result[wi++] = n;
        n = new Vector2Int(idx.x, idx.y -1);
        if (IsInBounds(n)) result[wi++] = n;

        // Diagonals
        n = new Vector2Int(idx.x +1, idx.y +1);
        if (CanUseDiagonal(idx, n)) result[wi++] = n;
        n = new Vector2Int(idx.x +1, idx.y -1);
        if (CanUseDiagonal(idx, n)) result[wi++] = n;
        n = new Vector2Int(idx.x -1, idx.y -1);
        if (CanUseDiagonal(idx, n)) result[wi++] = n;
        n = new Vector2Int(idx.x -1, idx.y +1);
        if (CanUseDiagonal(idx, n)) result[wi++] = n;

        return result;
    }

    private bool CanUseDiagonal(Vector2Int from, Vector2Int to)
    {
        if (!IsInBounds(to)) return false;

        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (Mathf.Abs(dx) !=1 || Mathf.Abs(dy) !=1) return false;

        // Adjacent cardinals that would be "cut" if blocked.
        Vector2Int a = new Vector2Int(from.x + dx, from.y);
        Vector2Int b = new Vector2Int(from.x, from.y + dy);

        if (!IsInBounds(a) || !IsInBounds(b)) return false;

        // Need cell data to check blocked state.
        if (_cellsByIndex == null) return false;

        if (!_cellsByIndex.TryGetValue(a, out var ca) || ca.IsBlocked) return false;
        if (!_cellsByIndex.TryGetValue(b, out var cb) || cb.IsBlocked) return false;

        return true;
    }

    private bool IsInBounds(Vector2Int idx)
    {
        return idx.x >=0 && idx.y >=0 && idx.x < cellsX && idx.y < cellsZ;
    }

    public Vector3 GetRandomPointInsideMazeWithinRadius(Vector3 point, float radius)
    {
        // get random point within radius of point that isn't a wall and isn't outside the maze bounds (if enabled).
        for (int i =0; i <100; i++)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate = point + new Vector3(randomOffset.x, randomOffset.y, 0f);
            if (TryWorldToCell(candidate, out var cellIdx) && IsInBounds(cellIdx) && _cellsByIndex.TryGetValue(cellIdx, out var cell) && !cell.IsBlocked && IsInMazeBounds(cellIdx))
            {
                return cell.WorldCenter;
            }
        }
        return point;
    }


    public Vector3 GetNearestCenterOfCorridor(Vector3 pos)
    {
        if (_cellsByIndex == null) return pos;

        RecomputeNearestBlocked();

        bool foundOpen = false;
        float bestDistSqr = float.PositiveInfinity;
        float bestNearestBlocked = float.NegativeInfinity;
        Vector3 best = pos;

        Vector3 query = pos;
        query.z = 0f;

        int w = CellsX;
        int h = CellsY;
        const float tieEpsilon = 0.0001f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!TryGetCell(x, y, out var cell)) continue;
                if (cell.IsBlocked) continue;
                if (!IsInMazeBounds(cell.Index)) continue;

                Vector3 center = cell.WorldCenter;
                center.z = 0f;
                float distSqr = (center - query).sqrMagnitude;

                bool betterDist = distSqr < bestDistSqr - tieEpsilon;
                bool sameDist = Mathf.Abs(distSqr - bestDistSqr) <= tieEpsilon;
                bool betterCorridorCenter = sameDist && cell.NearestBlocked > bestNearestBlocked;

                if (!foundOpen || betterDist || betterCorridorCenter)
                {
                    foundOpen = true;
                    bestDistSqr = distSqr;
                    bestNearestBlocked = cell.NearestBlocked;
                    best = center;
                }
            }
        }

        if (foundOpen)
        {
            best.z = pos.z;
            return best;
        }

        if (TryWorldToCell(pos, out var idx))
        {
            Vector3 center = GetCellWorldCenter(idx.x, idx.y);
            center.z = pos.z;
            return center;
        }

        return pos;
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        EnsureComponents();
        EnsureCells();

        int w = CellsX;
        int h = CellsY;
        if (w <= 0 || h <= 0)
        {
            Debug.LogError("GridManager has invalid size.", this);
            return;
        }

        int actualSeed = seed != 0 ? seed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        bool[,] open = mazeBuildMode == MazeBuildMode.IslandMode
            ? BuildIslandModeLayout(w, h, actualSeed, out RectInt bounds)
            : BuildPerfectMazeLayout(w, h, actualSeed, out bounds);
        ApplyMaze(open, w, h, bounds, actualSeed, enabledWalls: null, saveMazeData: true);

        Debug.Log($"Maze generated (mode={mazeBuildMode}, seed={actualSeed}, corridorWidth={corridorWidth}, wallThickness={wallThickness}).", this);
    }

    [ContextMenu("Save Current MazeData To Public Variable")]
    public void SaveCurrentMazeDataToPublicVariable()
    {
        EnsureCells();
        if (mazeDataAsset == null)
        {
            Debug.LogError("Cannot save maze data: mazeData reference is null.", this);
            return;
        }

        int w = CellsX;
        int h = CellsY;
        bool[,] open = new bool[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!TryGetCell(x, y, out var cell))
                {
                    open[x, y] = true;
                    continue;
                }

                open[x, y] = !cell.IsBlocked;
            }
        }

        SaveMazeDataFromOpen(open, w, h, seed);
        Debug.Log("Saved current maze state to mazeData.", this);
    }

    [ContextMenu("Load Current MazeData From Public Variable")]
    public void LoadCurrentMazeDataFromPublicVariable()
    {
        EnsureComponents();
        EnsureCells();

        if (!TryRebuildFromMazeData(logFailures: true))
        {
            Debug.LogError("Cannot load maze data: mazeData is missing or incompatible with the current grid.", this);
        }
    }

    [ContextMenu("Clear Generated Maze")]
    public void ClearGeneratedWalls()
    {
        EnsureComponents();
        EnsureCells();

        ResetGridWallsAndBlocked();
        ClearMazeBounds();
        ClearGeneratedMesh();
        ClearMazeData();

        if (pathfinding != null)
        {
            pathfinding.ResetFlowMaps();
        }
    }

    public void Rebuild()
    {
        EnsureComponents();
        EnsureCells();

        int w = CellsX;
        int h = CellsY;
        bool[,] open = new bool[w, h];
        var enabledWalls = new HashSet<Vector2Int>();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!TryGetCell(x, y, out var cell))
                {
                    open[x, y] = true;
                    continue;
                }

                open[x, y] = !cell.IsBlocked;
                if (cell.IsWall && cell.TowerPlacementEnabled)
                {
                    enabledWalls.Add(cell.Index);
                }
            }
        }

        BuildMazeMeshFromBlocked(open, w, h, enabledWalls);
        RecomputeNearestBlocked();
        RefreshWallPlacementIndicatorCells();
    }

    private bool TryRebuildFromMazeData(bool logFailures = false)
    {
        EnsureCells();
        if (mazeDataAsset == null) return false;
        if (mazeDataAsset.gridWidth != CellsX || mazeDataAsset.gridHeight != CellsY) return false;
        if (mazeDataAsset.blockedCells == null || mazeDataAsset.blockedCells.Count == 0) return false;

        int w = CellsX;
        int h = CellsY;
        bool[,] open = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                open[x, y] = true;
            }
        }

        for (int i = 0; i < mazeDataAsset.blockedCells.Count; i++)
        {
            var blocked = mazeDataAsset.blockedCells[i];
            if (blocked.x < 0 || blocked.y < 0 || blocked.x >= w || blocked.y >= h)
            {
                if (logFailures)
                {
                    Debug.LogWarning("MazeData contains blocked cells outside the current grid.", this);
                }
                continue;
            }

            open[blocked.x, blocked.y] = false;
        }

        RectInt bounds = mazeDataAsset.hasMazeBounds
            ? mazeDataAsset.mazeBounds
            : new RectInt(0, 0, w, h);

        HashSet<Vector2Int> enabledWalls = null;
        if (mazeDataAsset.wallPlacementCells != null && mazeDataAsset.wallPlacementCells.Count > 0)
        {
            enabledWalls = new HashSet<Vector2Int>();
            for (int i = 0; i < mazeDataAsset.wallPlacementCells.Count; i++)
            {
                var entry = mazeDataAsset.wallPlacementCells[i];
                if (!entry.towerPlacementEnabled) continue;
                enabledWalls.Add(new Vector2Int(entry.x, entry.y));
            }
        }

        ApplyMaze(open, w, h, bounds, mazeDataAsset.seed, enabledWalls, saveMazeData: false);
        return true;
    }

    private bool[,] BuildPerfectMazeLayout(int w, int h, int actualSeed, out RectInt bounds)
    {
        int cs = Mathf.Max(1, corridorWidth);
        int wt = Mathf.Max(1, wallThickness);
        int step = cs + wt;

        int nodesX = (w - wt) / step;
        int nodesY = (h - wt) / step;
        if (nodesX < 1 || nodesY < 1)
        {
            Debug.LogError("Grid too small for given corridorWidth/wallThickness.", this);
            bounds = default;
            return new bool[w, h];
        }

        int regionW = wt + nodesX * step;
        int regionH = wt + nodesY * step;
        int offsetX = Mathf.Max(0, (w - regionW) / 2);
        int offsetY = Mathf.Max(0, (h - regionH) / 2);
        bounds = new RectInt(offsetX, offsetY, regionW, regionH);

        bool[,] open = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                open[x, y] = true;
            }
        }

        for (int y = offsetY; y < offsetY + regionH; y++)
        {
            for (int x = offsetX; x < offsetX + regionW; x++)
            {
                open[x, y] = false;
            }
        }

        void CarveBlock(int gx, int gy)
        {
            for (int dy = 0; dy < cs; dy++)
            {
                for (int dx = 0; dx < cs; dx++)
                {
                    int x = gx + dx;
                    int y = gy + dy;
                    if (x >= 0 && y >= 0 && x < w && y < h)
                    {
                        open[x, y] = true;
                    }
                }
            }
        }

        int NodeToGridX(int nx) => offsetX + wt + nx * step;
        int NodeToGridY(int ny) => offsetY + wt + ny * step;

        void CarveConnection(Node from, Node to)
        {
            int fromX = NodeToGridX(from.x);
            int fromY = NodeToGridY(from.y);
            int toX = NodeToGridX(to.x);
            int toY = NodeToGridY(to.y);

            CarveBlock(toX, toY);

            int midX = Mathf.Min(fromX, toX) + cs;
            int midY = Mathf.Min(fromY, toY) + cs;

            if (from.x != to.x)
            {
                for (int dy = 0; dy < cs; dy++)
                {
                    for (int dx = 0; dx < wt; dx++)
                    {
                        int x = midX + dx;
                        int y = fromY + dy;
                        if (x >= 0 && y >= 0 && x < w && y < h)
                        {
                            open[x, y] = true;
                        }
                    }
                }
            }
            else
            {
                for (int dy = 0; dy < wt; dy++)
                {
                    for (int dx = 0; dx < cs; dx++)
                    {
                        int x = fromX + dx;
                        int y = midY + dy;
                        if (x >= 0 && y >= 0 && x < w && y < h)
                        {
                            open[x, y] = true;
                        }
                    }
                }
            }
        }

        var rng = new System.Random(actualSeed);
        var path = new List<Node>(nodesX * nodesY);
        bool snakeRows = rng.Next(0, 2) == 0;
        bool reversePrimary = rng.Next(0, 2) == 0;
        bool reverseSecondary = rng.Next(0, 2) == 0;

        if (snakeRows)
        {
            for (int rowIndex = 0; rowIndex < nodesY; rowIndex++)
            {
                int y = reversePrimary ? (nodesY - 1 - rowIndex) : rowIndex;
                bool reverseThisRow = ((rowIndex & 1) == 1) ^ reverseSecondary;
                for (int colIndex = 0; colIndex < nodesX; colIndex++)
                {
                    int x = reverseThisRow ? (nodesX - 1 - colIndex) : colIndex;
                    path.Add(new Node(x, y));
                }
            }
        }
        else
        {
            for (int colIndex = 0; colIndex < nodesX; colIndex++)
            {
                int x = reversePrimary ? (nodesX - 1 - colIndex) : colIndex;
                bool reverseThisColumn = ((colIndex & 1) == 1) ^ reverseSecondary;
                for (int rowIndex = 0; rowIndex < nodesY; rowIndex++)
                {
                    int y = reverseThisColumn ? (nodesY - 1 - rowIndex) : rowIndex;
                    path.Add(new Node(x, y));
                }
            }
        }

        if (path.Count > 0)
        {
            Node start = path[0];
            CarveBlock(NodeToGridX(start.x), NodeToGridY(start.y));

            for (int i = 1; i < path.Count; i++)
            {
                CarveConnection(path[i - 1], path[i]);
            }
        }

        return open;
    }

    private bool[,] BuildIslandModeLayout(int w, int h, int actualSeed, out RectInt bounds)
    {
        int borderThickness = Mathf.Max(1, wallThickness);
        int minDistanceFromBorder = Mathf.FloorToInt(Mathf.Max(0f, islandModeMinDistanceFromBorder));
        bounds = new RectInt(0, 0, w, h);

        bool[,] open = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isBorder = x < borderThickness
                    || y < borderThickness
                    || x >= w - borderThickness
                    || y >= h - borderThickness;
                open[x, y] = !isBorder;
            }
        }

        int innerMinX = borderThickness;
        int innerMinY = borderThickness;
        int innerW = Mathf.Max(0, w - borderThickness * 2);
        int innerH = Mathf.Max(0, h - borderThickness * 2);
        if (innerW <= 0 || innerH <= 0 || islandModeMaxCells <= 0)
        {
            return open;
        }

        if (minDistanceFromBorder * 2 >= innerW || minDistanceFromBorder * 2 >= innerH)
        {
            return open;
        }

        foreach (Vector2Int sample in GeneratePoissonDiscIslandCells(innerW, innerH, actualSeed, minDistanceFromBorder))
        {
            int x = innerMinX + sample.x;
            int y = innerMinY + sample.y;
            if (x < innerMinX || y < innerMinY || x >= innerMinX + innerW || y >= innerMinY + innerH) continue;
            open[x, y] = false;
        }

        return open;
    }

    private List<Vector2Int> GeneratePoissonDiscIslandCells(int width, int height, int actualSeed, int minDistanceFromBorder)
    {
        var result = new List<Vector2Int>(Mathf.Max(0, islandModeMaxCells));
        if (width <= 0 || height <= 0 || islandModeMaxCells <= 0) return result;

        float minDistance = Mathf.Max(1f, islandModeMinCellSpacing);
        int borderInset = Mathf.Max(0, minDistanceFromBorder);
        if (borderInset * 2 >= width || borderInset * 2 >= height) return result;
        float cellSize = minDistance / Mathf.Sqrt(2f);
        int gridWidth = Mathf.Max(1, Mathf.CeilToInt(width / cellSize));
        int gridHeight = Mathf.Max(1, Mathf.CeilToInt(height / cellSize));
        Vector2?[,] sampleGrid = new Vector2?[gridWidth, gridHeight];

        var rng = new System.Random(actualSeed);
        var activeSamples = new List<Vector2>(Mathf.Max(1, islandModeMaxCells));
        var samples = new List<Vector2>(Mathf.Max(1, islandModeMaxCells));
        var usedCells = new HashSet<Vector2Int>();

        Vector2 first = new Vector2(
            borderInset + (float)rng.NextDouble() * Mathf.Max(0f, width - borderInset * 2),
            borderInset + (float)rng.NextDouble() * Mathf.Max(0f, height - borderInset * 2));
        AddSample(first);

        while (activeSamples.Count > 0 && result.Count < islandModeMaxCells)
        {
            int activeIndex = rng.Next(activeSamples.Count);
            Vector2 center = activeSamples[activeIndex];
            bool accepted = false;

            for (int attempt = 0; attempt < islandModeRejectionSamples && result.Count < islandModeMaxCells; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = minDistance * (1f + (float)rng.NextDouble());
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (candidate.x < borderInset || candidate.y < borderInset || candidate.x >= width - borderInset || candidate.y >= height - borderInset) continue;
                if (!IsValidCandidate(candidate)) continue;

                AddSample(candidate);
                accepted = true;
            }

            if (!accepted)
            {
                activeSamples.RemoveAt(activeIndex);
            }
        }

        return result;

        void AddSample(Vector2 sample)
        {
            samples.Add(sample);
            activeSamples.Add(sample);

            int gx = Mathf.Clamp((int)(sample.x / cellSize), 0, gridWidth - 1);
            int gy = Mathf.Clamp((int)(sample.y / cellSize), 0, gridHeight - 1);
            sampleGrid[gx, gy] = sample;

            Vector2Int cell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(sample.x), 0, width - 1),
                Mathf.Clamp(Mathf.FloorToInt(sample.y), 0, height - 1));

            if (usedCells.Add(cell) && result.Count < islandModeMaxCells)
            {
                result.Add(cell);
            }
        }

        bool IsValidCandidate(Vector2 candidate)
        {
            Vector2Int candidateCell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(candidate.x), 0, width - 1),
                Mathf.Clamp(Mathf.FloorToInt(candidate.y), 0, height - 1));
            if (usedCells.Contains(candidateCell)) return false;

            int gx = Mathf.Clamp((int)(candidate.x / cellSize), 0, gridWidth - 1);
            int gy = Mathf.Clamp((int)(candidate.y / cellSize), 0, gridHeight - 1);
            int searchRadius = Mathf.CeilToInt(minDistance / cellSize);
            float minDistanceSqr = minDistance * minDistance;

            for (int y = Mathf.Max(0, gy - searchRadius); y <= Mathf.Min(gridHeight - 1, gy + searchRadius); y++)
            {
                for (int x = Mathf.Max(0, gx - searchRadius); x <= Mathf.Min(gridWidth - 1, gx + searchRadius); x++)
                {
                    Vector2? existing = sampleGrid[x, y];
                    if (!existing.HasValue) continue;
                    if ((existing.Value - candidate).sqrMagnitude < minDistanceSqr) return false;
                }
            }

            return true;
        }
    }

    private void ApplyMaze(bool[,] open, int w, int h, RectInt bounds, int usedSeed, HashSet<Vector2Int> enabledWalls, bool saveMazeData)
    {
        if (open == null) return;

        ResetGridWallsAndBlocked();
        SetMazeBounds(bounds);
        BuildMazeMeshFromBlocked(open, w, h, enabledWalls);
        RefreshMazeBorderMasks();
        RecomputeNearestBlocked();
        RefreshWallPlacementIndicatorCells();

        if (saveMazeData)
        {
            SaveMazeDataFromOpen(open, w, h, usedSeed);
        }

        if (pathfinding != null)
        {
            pathfinding.ResetFlowMaps();
        }
    }

    private void RefreshMazeBorderMasks()
    {
        ClearGeneratedMasks();

        if (!createMasks) return;
        if (maskPrefab == null) return;

        Rect mazeRect = GetMazeBoundsRectWorld();
        Rect screenRect = GetMaskScreenRectWorld();

        Rect visibleMaze = IntersectRects(screenRect, mazeRect);
        if (visibleMaze.width <= 0f || visibleMaze.height <= 0f)
        {
            CreateMaskRect(screenRect);
            return;
        }

        // Left strip.
        TryCreateMaskRect(Rect.MinMaxRect(
            screenRect.xMin,
            screenRect.yMin,
            visibleMaze.xMin,
            screenRect.yMax));

        // Right strip.
        TryCreateMaskRect(Rect.MinMaxRect(
            visibleMaze.xMax,
            screenRect.yMin,
            screenRect.xMax,
            screenRect.yMax));

        // Bottom strip.
        TryCreateMaskRect(Rect.MinMaxRect(
            visibleMaze.xMin,
            screenRect.yMin,
            visibleMaze.xMax,
            visibleMaze.yMin));

        // Top strip.
        TryCreateMaskRect(Rect.MinMaxRect(
            visibleMaze.xMin,
            visibleMaze.yMax,
            visibleMaze.xMax,
            screenRect.yMax));
    }

    private void ClearGeneratedMasks()
    {
        if (generatedMaskObjects == null)
        {
            generatedMaskObjects = new List<GameObject>(4);
            return;
        }

        for (int i = generatedMaskObjects.Count - 1; i >= 0; i--)
        {
            var go = generatedMaskObjects[i];
            if (go == null) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(go);
            }
            else
#endif
            {
                Destroy(go);
            }
        }

        generatedMaskObjects.Clear();
    }

    private Rect GetMazeBoundsRectWorld()
    {
        float width = cellsX * spacing;
        float height = cellsZ * spacing;
        float x0 = -width * 0.5f;
        float y0 = -height * 0.5f;

        RectInt bounds = hasMazeBounds ? mazeBounds : new RectInt(0, 0, cellsX, cellsZ);
        float minX = x0 + bounds.xMin * spacing;
        float maxX = x0 + bounds.xMax * spacing;
        float minY = y0 + bounds.yMin * spacing;
        float maxY = y0 + bounds.yMax * spacing;

        Vector3 worldMin = transform.TransformPoint(new Vector3(minX, minY, 0f));
        Vector3 worldMax = transform.TransformPoint(new Vector3(maxX, maxY, 0f));
        return Rect.MinMaxRect(
            Mathf.Min(worldMin.x, worldMax.x),
            Mathf.Min(worldMin.y, worldMax.y),
            Mathf.Max(worldMin.x, worldMax.x),
            Mathf.Max(worldMin.y, worldMax.y));
    }

    private Rect GetMaskScreenRectWorld()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 c = cam.transform.position;
            return Rect.MinMaxRect(c.x - halfWidth, c.y - halfHeight, c.x + halfWidth, c.y + halfHeight);
        }

        float width = cellsX * spacing;
        float height = cellsZ * spacing;
        float expand = Mathf.Max(width, height) * 2f;
        Vector3 center = transform.position;
        return Rect.MinMaxRect(
            center.x - (width * 0.5f + expand),
            center.y - (height * 0.5f + expand),
            center.x + (width * 0.5f + expand),
            center.y + (height * 0.5f + expand));
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        float minX = Mathf.Max(a.xMin, b.xMin);
        float maxX = Mathf.Min(a.xMax, b.xMax);
        float minY = Mathf.Max(a.yMin, b.yMin);
        float maxY = Mathf.Min(a.yMax, b.yMax);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void TryCreateMaskRect(Rect rect)
    {
        if (rect.width <= 0.0001f || rect.height <= 0.0001f) return;
        CreateMaskRect(rect);
    }

    private void CreateMaskRect(Rect rect)
    {
        if (maskPrefab == null) return;

        GameObject go = Instantiate(maskPrefab, transform);
        go.name = $"MazeMask_{generatedMaskObjects.Count}";

        Transform t = go.transform;
        t.position = new Vector3(rect.center.x, rect.center.y, t.position.z);
        t.rotation = Quaternion.identity;
        t.localScale = new Vector3(rect.width, rect.height, 1f);

        generatedMaskObjects.Add(go);
    }

    private void ResetGridWallsAndBlocked()
    {
        EnsureCells();
        if (_cellsByIndex == null) return;

        int w = CellsX;
        int h = CellsY;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!TryGetCell(x, y, out var cell)) continue;
                if (!cell.IsWall && !cell.IsBlocked && !cell.TowerPlacementEnabled) continue;
                cell.IsWall = false;
                cell.IsBlocked = false;
                cell.TowerPlacementEnabled = false;
                TrySetCell(cell);
            }
        }
    }

    private void SaveMazeDataFromOpen(bool[,] open, int w, int h, int usedSeed)
    {
        if (mazeDataAsset == null || open == null) return;

        mazeDataAsset.gridWidth = w;
        mazeDataAsset.gridHeight = h;
        mazeDataAsset.seed = usedSeed;
        mazeDataAsset.blockedCells.Clear();
        mazeDataAsset.wallPlacementCells.Clear();

        if (TryGetMazeBounds(out var bounds))
        {
            mazeDataAsset.hasMazeBounds = true;
            mazeDataAsset.mazeBounds = bounds;
        }
        else
        {
            mazeDataAsset.hasMazeBounds = false;
            mazeDataAsset.mazeBounds = default;
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (open[x, y]) continue;

                mazeDataAsset.blockedCells.Add(new MazeData.BlockedCell(x, y));

                bool towerPlacementEnabled = false;
                if (TryGetCell(x, y, out var cell))
                {
                    towerPlacementEnabled = cell.TowerPlacementEnabled;
                }

                mazeDataAsset.wallPlacementCells.Add(new MazeData.WallPlacementCell(x, y, towerPlacementEnabled));
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(mazeDataAsset);
        }
#endif
    }

    private void ClearMazeData()
    {
        if (mazeDataAsset == null) return;
        mazeDataAsset.Clear();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(mazeDataAsset);
        }
#endif
    }

    private HashSet<Vector2Int> ComputeEnabledTowerPlacementWallCells(bool[,] open, int w, int h)
    {
        var enabledWalls = new HashSet<Vector2Int>();
        if (open == null || enabledWallPlacementCount <= 0) return enabledWalls;

        var wallCells = new List<Vector2Int>(256);
        var emptyFieldCenters = new List<Vector3>(256);
        float range = Mathf.Max(0f, towerPlacementScoreRange);
        float rangeSqr = range * range;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = new Vector2Int(x, y);
                if (open[x, y])
                {
                    if (IsInMazeBounds(idx))
                    {
                        emptyFieldCenters.Add(GetCellWorldCenter(x, y));
                    }
                    continue;
                }

                if (!includeBorderWallsInTowerEnablementSelection && IsBorderWallCell(x, y, w, h))
                {
                    continue;
                }

                wallCells.Add(idx);
            }
        }

        var scores = new Dictionary<Vector2Int, float>(wallCells.Count);
        for (int i = 0; i < wallCells.Count; i++)
        {
            Vector2Int wallCell = wallCells[i];
            Vector3 wallCenter = GetCellWorldCenter(wallCell.x, wallCell.y);
            float score = 0f;

            for (int j = 0; j < emptyFieldCenters.Count; j++)
            {
                if ((emptyFieldCenters[j] - wallCenter).sqrMagnitude <= rangeSqr)
                {
                    score += 1f;
                }
            }

            scores[wallCell] = score;
        }

        int selections = Mathf.Min(enabledWallPlacementCount, wallCells.Count);
        for (int selectionIndex = 0; selectionIndex < selections; selectionIndex++)
        {
            bool foundBest = false;
            Vector2Int bestCell = default;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < wallCells.Count; i++)
            {
                Vector2Int candidate = wallCells[i];
                if (enabledWalls.Contains(candidate)) continue;

                float score = scores.TryGetValue(candidate, out var currentScore) ? currentScore : float.NegativeInfinity;
                if (!foundBest
                    || score > bestScore
                    || (Mathf.Approximately(score, bestScore) && (candidate.y < bestCell.y || (candidate.y == bestCell.y && candidate.x < bestCell.x))))
                {
                    foundBest = true;
                    bestCell = candidate;
                    bestScore = score;
                }
            }

            if (!foundBest) break;

            enabledWalls.Add(bestCell);
            Vector3 selectedCenter = GetCellWorldCenter(bestCell.x, bestCell.y);

            for (int i = 0; i < wallCells.Count; i++)
            {
                Vector2Int candidate = wallCells[i];
                if (enabledWalls.Contains(candidate)) continue;

                Vector3 candidateCenter = GetCellWorldCenter(candidate.x, candidate.y);
                if ((candidateCenter - selectedCenter).sqrMagnitude > rangeSqr) continue;

                scores[candidate] = scores[candidate] - towerPlacementNeighborPenalty;
            }
        }

        return enabledWalls;
    }

    private static bool IsBorderWallCell(int x, int y, int w, int h)
    {
        return x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1;
    }

    private static bool IsBlockedForMesh(bool[,] open, int w, int h, int x, int y)
    {
        if (open == null) return false;
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        return !open[x, y];
    }

    private bool IsConvexOuterCornerForFillet(bool[,] open, int w, int h, int x, int y, int sx, int sy)
    {
        bool sideXBlocked = IsBlockedForMesh(open, w, h, x + sx, y);
        bool sideYBlocked = IsBlockedForMesh(open, w, h, x, y + sy);

        // Convex outer corner when both adjacent side neighbors are empty.
        return !sideXBlocked && !sideYBlocked;
    }

    private bool IsConcaveCornerForFillet(bool[,] open, int w, int h, int x, int y, int sx, int sy)
    {
        bool sideXBlocked = IsBlockedForMesh(open, w, h, x + sx, y);
        bool sideYBlocked = IsBlockedForMesh(open, w, h, x, y + sy);
        bool diagonalBlocked = IsBlockedForMesh(open, w, h, x + sx, y + sy);

        // Concave corner around an inside notch (3-of-4 pattern): both side neighbors blocked, diagonal empty.
        return sideXBlocked && sideYBlocked && !diagonalBlocked;
    }

    private bool ShouldFilletCorner(bool[,] open, int w, int h, int x, int y, int sx, int sy)
    {
        bool isConvex = IsConvexOuterCornerForFillet(open, w, h, x, y, sx, sy);
        if (isConvex) return filletConvexMeshVertices;

        bool isConcave = IsConcaveCornerForFillet(open, w, h, x, y, sx, sy);
        if (isConcave) return filletConcaveMeshVertices;

        return false;
    }

    private static void AddPointIfNotDuplicate(List<Vector2> points, Vector2 p)
    {
        if (points == null) return;
        if (points.Count > 0)
        {
            var last = points[points.Count - 1];
            if ((last - p).sqrMagnitude <= 0.0000001f) return;
        }

        points.Add(p);
    }

    private static void AddArcPointsClockwise(List<Vector2> points, Vector2 center, float radius, float startDeg, float endDeg, int segments, bool includeEndPoint)
    {
        if (points == null) return;
        if (radius <= 0f || segments <= 0) return;

        int maxStep = includeEndPoint ? segments : segments - 1;
        if (maxStep <= 0) return;

        for (int step = 1; step <= maxStep; step++)
        {
            float t = step / (float)segments;
            float aDeg = Mathf.Lerp(startDeg, endDeg, t);
            float aRad = aDeg * Mathf.Deg2Rad;
            var p = center + new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad)) * radius;
            AddPointIfNotDuplicate(points, p);
        }
    }

    private void BuildVisualCellPolygon(bool[,] open, int w, int h, int x, int y, float half, float filletRadius, int filletSegments, List<Vector2> points)
    {
        points.Clear();

        bool useFillet = roundMazeMeshCorners && filletRadius > 0f;
        bool roundBL = useFillet && ShouldFilletCorner(open, w, h, x, y, -1, -1);
        bool roundTL = useFillet && ShouldFilletCorner(open, w, h, x, y, -1, 1);
        bool roundTR = useFillet && ShouldFilletCorner(open, w, h, x, y, 1, 1);
        bool roundBR = useFillet && ShouldFilletCorner(open, w, h, x, y, 1, -1);

        float minX = -half;
        float maxX = half;
        float minY = -half;
        float maxY = half;

        float rBL = roundBL ? filletRadius : 0f;
        float rTL = roundTL ? filletRadius : 0f;
        float rTR = roundTR ? filletRadius : 0f;
        float rBR = roundBR ? filletRadius : 0f;

        Vector2 leftBottom = new Vector2(minX, minY + rBL);
        Vector2 leftTop = new Vector2(minX, maxY - rTL);
        Vector2 topLeft = new Vector2(minX + rTL, maxY);
        Vector2 topRight = new Vector2(maxX - rTR, maxY);
        Vector2 rightTop = new Vector2(maxX, maxY - rTR);
        Vector2 rightBottom = new Vector2(maxX, minY + rBR);
        Vector2 bottomRight = new Vector2(maxX - rBR, minY);
        Vector2 bottomLeft = new Vector2(minX + rBL, minY);

        AddPointIfNotDuplicate(points, leftBottom);
        AddPointIfNotDuplicate(points, leftTop);
        if (roundTL)
        {
            AddArcPointsClockwise(points, new Vector2(minX + rTL, maxY - rTL), rTL, 180f, 90f, filletSegments, includeEndPoint: true);
        }
        else
        {
            AddPointIfNotDuplicate(points, topLeft);
        }

        AddPointIfNotDuplicate(points, topRight);
        if (roundTR)
        {
            AddArcPointsClockwise(points, new Vector2(maxX - rTR, maxY - rTR), rTR, 90f, 0f, filletSegments, includeEndPoint: true);
        }
        else
        {
            AddPointIfNotDuplicate(points, rightTop);
        }

        AddPointIfNotDuplicate(points, rightBottom);
        if (roundBR)
        {
            AddArcPointsClockwise(points, new Vector2(maxX - rBR, minY + rBR), rBR, 0f, -90f, filletSegments, includeEndPoint: true);
        }
        else
        {
            AddPointIfNotDuplicate(points, bottomRight);
        }

        AddPointIfNotDuplicate(points, bottomLeft);
        if (roundBL)
        {
            // Do not add the final endpoint because it equals the first point (leftBottom).
            AddArcPointsClockwise(points, new Vector2(minX + rBL, minY + rBL), rBL, -90f, -180f, filletSegments, includeEndPoint: false);
        }
    }

    private void BuildMazeMeshFromBlocked(bool[,] open, int w, int h, HashSet<Vector2Int> enabledWalls = null)
    {
        EnsureComponents();
        if (_meshFilter == null || _meshRenderer == null || _polygonCollider == null) return;

        HashSet<Vector2Int> enabledWallCells = enabledWalls ?? ComputeEnabledTowerPlacementWallCells(open, w, h);

        float cellSize = GetSpacing();
        float half = cellSize * 0.5f;
        float filletRadius = Mathf.Clamp(mazeMeshCornerRoundAmount, 0f, Mathf.Max(0f, half - 0.0001f));
        int filletSegments = Mathf.Max(1, mazeMeshCornerFilletSegments);

        var vertices = new List<Vector3>(1024);
        var trianglesPlacementEnabled = new List<int>(1536);
        var trianglesPlacementDisabled = new List<int>(1536);
        var uvs = new List<Vector2>(1024);
        var normals = new List<Vector3>(1024);
        var colliderPaths = new List<Vector2[]>(1024);
        var visualCellPolygon = new List<Vector2>(16);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (open[x, y]) continue;

                Vector2Int idx = new Vector2Int(x, y);
                bool towerPlacementEnabled = enabledWallCells.Contains(idx);

                TrySetWallAtCell(idx, isWall: true);
                TrySetTowerPlacementEnabledAtCell(idx, towerPlacementEnabled);

                Vector3 worldCenter = GetCellWorldCenter(x, y);
                Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

                BuildVisualCellPolygon(open, w, h, x, y, half, filletRadius, filletSegments, visualCellPolygon);

                int baseIndex = vertices.Count;
                for (int p = 0; p < visualCellPolygon.Count; p++)
                {
                    var lp = visualCellPolygon[p];
                    vertices.Add(new Vector3(localCenter.x + lp.x, localCenter.y + lp.y, 0f));

                    float u = (lp.x + half) / Mathf.Max(0.0001f, cellSize);
                    float v = (lp.y + half) / Mathf.Max(0.0001f, cellSize);
                    uvs.Add(new Vector2(u, v));
                    normals.Add(Vector3.back);
                }

                colliderPaths.Add(new[]
                {
                    new Vector2(localCenter.x - half, localCenter.y - half),
                    new Vector2(localCenter.x - half, localCenter.y + half),
                    new Vector2(localCenter.x + half, localCenter.y + half),
                    new Vector2(localCenter.x + half, localCenter.y - half),
                });

                var targetTriangles = towerPlacementEnabled ? trianglesPlacementEnabled : trianglesPlacementDisabled;
                for (int p = 1; p < visualCellPolygon.Count - 1; p++)
                {
                    targetTriangles.Add(baseIndex + 0);
                    targetTriangles.Add(baseIndex + p);
                    targetTriangles.Add(baseIndex + p + 1);
                }
            }
        }

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "Generated Maze Mesh";
        }

        _mesh.Clear();
        _mesh.indexFormat = IndexFormat.UInt32;
        _mesh.SetVertices(vertices);
        _mesh.subMeshCount = 2;
        _mesh.SetTriangles(trianglesPlacementEnabled, 0);
        _mesh.SetTriangles(trianglesPlacementDisabled, 1);
        _mesh.SetUVs(0, uvs);
        _mesh.SetNormals(normals);
        _mesh.RecalculateBounds();

        _meshFilter.sharedMesh = _mesh;

        Material placementEnabledMaterial = meshMaterial != null ? meshMaterial : _meshRenderer.sharedMaterial;
        Material placementDisabledMaterial = meshPlacementDisabledMaterial != null
            ? meshPlacementDisabledMaterial
            : placementEnabledMaterial;

        if (placementEnabledMaterial != null)
        {
            _meshRenderer.sharedMaterials = new[] { placementEnabledMaterial, placementDisabledMaterial };
        }

        _meshRenderer.sortingOrder = mazeRendererSortingOrder;

        _polygonCollider.pathCount = colliderPaths.Count;
        for (int i = 0; i < colliderPaths.Count; i++)
        {
            _polygonCollider.SetPath(i, colliderPaths[i]);
        }
        _polygonCollider.enabled = colliderPaths.Count > 0;
    }

    private void ClearGeneratedMesh()
    {
        EnsureComponents();

        if (_meshFilter != null && _meshFilter.sharedMesh == _mesh)
        {
            _meshFilter.sharedMesh = null;
        }

        if (_polygonCollider != null)
        {
            _polygonCollider.pathCount = 0;
            _polygonCollider.enabled = false;
        }
    }


    private void EnsureComponents()
    {
        if (_polygonCollider == null) _polygonCollider = GetComponent<PolygonCollider2D>();
    }

    [Header("Generated Mesh Components")]
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private MeshRenderer _meshRenderer;
    private PolygonCollider2D _polygonCollider;

    private Mesh _mesh;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (!Application.isPlaying) return;

        EnsureComponents();
        EnsureCells();

        if (!TryRebuildFromMazeData())
        {
            ApplyWallBlocking();
            RecomputeNearestBlocked();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        EnsureComponents();
        EnsureCells();

        if (!TryRebuildFromMazeData())
        {
            ApplyWallBlocking();
            RecomputeNearestBlocked();
        }
    }

    private void OnValidate()
    {
        // Keep fields sane in editor, but don't run runtime logic.
        spacing = Mathf.Max(0.001f, spacing);
        cellsX = Mathf.Max(1, cellsX);
        cellsZ = Mathf.Max(1, cellsZ);
        corridorWidth = Mathf.Max(1, corridorWidth);
        wallThickness = Mathf.Max(1, wallThickness);
        islandModeMaxCells = Mathf.Max(0, islandModeMaxCells);
        islandModeMinCellSpacing = Mathf.Max(1f, islandModeMinCellSpacing);
        islandModeMinDistanceFromBorder = Mathf.Max(0f, islandModeMinDistanceFromBorder);
        islandModeRejectionSamples = Mathf.Max(1, islandModeRejectionSamples);
        towerPlacementScoreRange = Mathf.Max(0f, towerPlacementScoreRange);
        towerPlacementNeighborPenalty = Mathf.Max(0f, towerPlacementNeighborPenalty);
        enabledWallPlacementCount = Mathf.Max(0, enabledWallPlacementCount);
    }

    /// <summary>
    /// Finds all <see cref="Wall"/> instances and blocks any grid cells whose centers lie inside the wall collider bounds.
    /// This allows level geometry to automatically affect navigation.
    /// </summary>
    private void ApplyWallBlocking()
    {
        EnsureCells();
        if (_cellsByIndex == null) return;

        Wall[] walls = FindObjectsByType<Wall>(FindObjectsSortMode.None);
        bool anyWalkabilityChanged = false;

        if (walls != null && walls.Length > 0)
        {
            foreach (var wall in walls)
            {
                if (wall == null) continue;

				Collider2D c2d = wall.col != null ? wall.col : wall.GetComponent<Collider2D>();
				if (c2d == null) continue;

				Vector2 center = c2d.bounds.center;
				// Approximate any wall collider as a circle using its bounds extents.
				var ext = c2d.bounds.extents;
				float radius = Mathf.Max(0f, Mathf.Max(ext.x, ext.y));
                float rSqr = radius * radius;

                // Iterate all cells; typical grids are small.
                for (int y =0; y < cellsZ; y++)
                {
                    for (int x =0; x < cellsX; x++)
                    {
                        var idx = new Vector2Int(x, y);
                        if (!_cellsByIndex.TryGetValue(idx, out var c)) continue;

                        Vector3 p3 = c.WorldCenter;
                        Vector2 p = new Vector2(p3.x, p3.y);
                        if ((p - center).sqrMagnitude <= rSqr)
                        {
                            if (!c.IsBlocked) anyWalkabilityChanged = true;
                            c.IsBlocked = true;
                            c.IsWall = true;
                            _cellsByIndex[idx] = c;

                            MarkNearestBlockedDirty();
                        }
                    }
                }
            }
        }

        if (ApplyMazeDataBlocking())
        {
            anyWalkabilityChanged = true;
        }

        // Blocked state changed -> recompute adjacency for diagonal corner rules.
        RecomputeAllAdjacency();

        if (anyWalkabilityChanged)
        {
            WalkabilityVersion++;
        }
    }

    private bool ApplyMazeDataBlocking()
    {
        if (mazeDataAsset == null) return false;

        bool anyChanged = false;
        MazeData data = mazeDataAsset;
        if (data.blockedCells == null || data.blockedCells.Count == 0) return false;
        if (data.gridWidth != cellsX || data.gridHeight != cellsZ) return false;

        if (data.hasMazeBounds)
        {
            hasMazeBounds = true;
            mazeBounds = data.mazeBounds;
        }

        for (int b = 0; b < data.blockedCells.Count; b++)
        {
            var blocked = data.blockedCells[b];
            var idx = new Vector2Int(blocked.x, blocked.y);
            if (!_cellsByIndex.TryGetValue(idx, out var c)) continue;

            if (!c.IsBlocked) anyChanged = true;
            c.IsBlocked = true;
            c.IsWall = true;
            _cellsByIndex[idx] = c;
            MarkNearestBlockedDirty();
        }

        if (data.wallPlacementCells != null && data.wallPlacementCells.Count > 0)
        {
            for (int w = 0; w < data.wallPlacementCells.Count; w++)
            {
                var wc = data.wallPlacementCells[w];
                var idx = new Vector2Int(wc.x, wc.y);
                if (!_cellsByIndex.TryGetValue(idx, out var c)) continue;

                c.TowerPlacementEnabled = wc.towerPlacementEnabled;
                _cellsByIndex[idx] = c;
            }
        }

        return anyChanged;
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Maze Mesh (Editor)")]
    private void RebuildMazeMeshInEditor()
    {
        EnsureComponents();
        EnsureCells();
        TryRebuildFromMazeData();

        UnityEditor.EditorUtility.SetDirty(this);
        if (_meshFilter != null) UnityEditor.EditorUtility.SetDirty(_meshFilter);
        if (_meshRenderer != null) UnityEditor.EditorUtility.SetDirty(_meshRenderer);
        if (_polygonCollider != null) UnityEditor.EditorUtility.SetDirty(_polygonCollider);
    }
#endif

    public bool TryGetTowerAtCell(Vector2Int idx, out Tower tower)
    {
        return _towersByCell.TryGetValue(idx, out tower) && tower != null;
    }

    public void SetTowerAtCell(Vector2Int idx, Tower tower)
    {
        if (tower == null) return;
        _towersByCell[idx] = tower;
    }

    public void ClearTowerAtCell(Vector2Int idx, Tower tower = null)
    {
        if (!_towersByCell.TryGetValue(idx, out var existing)) return;
        if (tower != null && existing != tower) return;
        _towersByCell.Remove(idx);
    }

    public bool IsWallAtCell(Vector2Int idx)
    {
        if (_cellsByIndex == null) return false;
        if (!_cellsByIndex.TryGetValue(idx, out var c)) return false;
        return c.IsWall;
    }

    public bool TrySetWallAtCell(Vector2Int idx, bool isWall, bool notifyMineTowers = true)
    {
        if (_cellsByIndex == null) return false;
        if (!_cellsByIndex.TryGetValue(idx, out var c)) return false;

        bool wasWall = c.IsWall;
        bool wasBlocked = c.IsBlocked;
        c.IsWall = isWall;
        if (isWall) c.IsBlocked = true;
        _cellsByIndex[idx] = c;

        if (wasBlocked != c.IsBlocked)
        {
            WalkabilityVersion++;
        }
        RecomputeAdjacencyAround(idx);

        MarkNearestBlockedDirty();

        if (notifyMineTowers && !wasWall && isWall)
        {
            ProjectileTower.ReleaseMineProjectilesForPlacedWalls(new List<Vector2Int>(1) { idx });
        }

        return true;
    }

    public bool TrySetTowerPlacementEnabledAtCell(Vector2Int idx, bool towerPlacementEnabled)
    {
        if (_cellsByIndex == null) return false;
        if (!_cellsByIndex.TryGetValue(idx, out var c)) return false;

        c.TowerPlacementEnabled = towerPlacementEnabled;
        _cellsByIndex[idx] = c;
        return true;
    }

    private bool _nearestBlockedDirty = true;

    public void MarkNearestBlockedDirty()
    {
        _nearestBlockedDirty = true;
    }

    public void RecomputeNearestBlocked()
    {
        EnsureCells();
        if (_cellsByIndex == null) return;
        if (!_nearestBlockedDirty) return;

        int w = cellsX;
        int h = cellsZ;

        // Multi-source BFS (Manhattan distance) from all blocked cells.
        var dist = new float[w, h];
        for (int y =0; y < h; y++)
        for (int x =0; x < w; x++)
        dist[x, y] = float.PositiveInfinity;

        var q = new Queue<Vector2Int>(w * h);
        for (int y =0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = new Vector2Int(x, y);
                if (_cellsByIndex.TryGetValue(idx, out var c) && c.IsBlocked)
                {
                    dist[x, y] =0f;
                    q.Enqueue(idx);
                }
            }
        }

        // If no blocked cells, set large distance.
        if (q.Count ==0)
        {
            for (int y =0; y < h; y++)
            for (int x =0; x < w; x++)
            {
                var idx = new Vector2Int(x, y);
                if (_cellsByIndex.TryGetValue(idx, out var c))
                {
                    c.NearestBlocked = float.PositiveInfinity;
                    _cellsByIndex[idx] = c;
                }
            }
            _nearestBlockedDirty = false;
            return;
        }

        //4-neighbor BFS
        var dirs = new[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };
        while (q.Count >0)
        {
            var cur = q.Dequeue();
            float cd = dist[cur.x, cur.y];

            for (int i =0; i <4; i++)
            {
                var n = cur + dirs[i];
                if (n.x <0 || n.y <0 || n.x >= w || n.y >= h) continue;

                float nd = cd +1f;
                if (nd < dist[n.x, n.y])
                {
                    dist[n.x, n.y] = nd;
                    q.Enqueue(n);
                }
            }
        }

        // Write back to cells.
        for (int y =0; y < h; y++)
        {
            for (int x =0; x < w; x++)
            {
                var idx = new Vector2Int(x, y);
                if (_cellsByIndex.TryGetValue(idx, out var c))
                {
                    c.NearestBlocked = dist[x, y];
                    _cellsByIndex[idx] = c;
                }
            }
        }

        _nearestBlockedDirty = false;
    }

    // -------------------- Player wall placement system --------------------

    private readonly HashSet<Vector2Int> _wallPlacementIndicatorCells = new HashSet<Vector2Int>(64);
    private readonly List<Vector2Int> _wallPlacementCommitScratch = new List<Vector2Int>(32);

    /// <summary>
    /// Returns the set of cells where the player can start drawing a wall.
    /// These are non-blocked, in-bounds cells that are 4-way adjacent to a wall cell
    /// whose <see cref="Cell.TowerPlacementEnabled"/> is true.
    /// </summary>
    public IReadOnlyCollection<Vector2Int> GetWallPlacementIndicatorCells()
    {
        return _wallPlacementIndicatorCells;
    }

    public bool IsWallPlacementIndicatorCell(Vector2Int idx)
    {
        return _wallPlacementIndicatorCells.Contains(idx);
    }

    /// <summary>
    /// Recomputes the set of "wall placement indicator" cells: empty cells inside the maze that
    /// are 4-way adjacent to a tower-placement-enabled wall cell.
    /// </summary>
    public void RefreshWallPlacementIndicatorCells()
    {
        _wallPlacementIndicatorCells.Clear();
        if (_cellsByIndex == null) return;

        foreach (var kvp in _cellsByIndex)
        {
            var c = kvp.Value;
            if (!c.IsWall) continue;
            if (!c.TowerPlacementEnabled) continue;

            Vector2Int idx = c.Index;
            TryAddIndicatorIfBuildable(new Vector2Int(idx.x + 1, idx.y));
            TryAddIndicatorIfBuildable(new Vector2Int(idx.x - 1, idx.y));
            TryAddIndicatorIfBuildable(new Vector2Int(idx.x, idx.y + 1));
            TryAddIndicatorIfBuildable(new Vector2Int(idx.x, idx.y - 1));
        }
    }

    private void TryAddIndicatorIfBuildable(Vector2Int idx)
    {
        if (!TryGetWallPlacementContinuationDirection(idx, out var dir)) return;
        if (!IsCellAvailableForPlayerWall(idx, dir)) return;

        _wallPlacementCommitScratch.Clear();
        _wallPlacementCommitScratch.Add(idx);
        if (!CanCommitWallPlacement(_wallPlacementCommitScratch)) return;

        _wallPlacementIndicatorCells.Add(idx);
    }

    /// <summary>
    /// Returns true if the given cell can be turned into a wall by the player wall-placement
    /// system. The cell must be in-bounds, in the maze, not blocked and not occupied by a tower.
    /// </summary>
    public bool IsCellAvailableForPlayerWall(Vector2Int idx)
    {
        if (!TryGetWallPlacementContinuationDirection(idx, out var dir)) return false;
        return IsCellAvailableForPlayerWall(idx, dir);
    }

    public bool IsCellAvailableForPlayerWall(Vector2Int idx, Direction placementDir)
    {
        if (!IsCellAvailableForPlayerWallBase(idx)) return false;
        if (pathfinding != null && !pathfinding.IsWallPlacementCellAllowed(idx, placementDir)) return false;
        return true;
    }

    private bool IsCellAvailableForPlayerWallBase(Vector2Int idx)
    {
        if (_cellsByIndex == null) return false;
        if (!IsInBounds(idx)) return false;
        if (!IsInMazeBounds(idx)) return false;
        if (!_cellsByIndex.TryGetValue(idx, out var c)) return false;
        if ( c.IsBlocked) return false;
        if (_towersByCell.TryGetValue(idx, out var t) && t != null) return false;
        return true;
    }

    /// <summary>
    /// Returns the run of cells (excluding <paramref name="start"/>) that the player can build a
    /// wall over by dragging from <paramref name="start"/> in <paramref name="dir"/>. Stops at the
    /// first cell that is blocked, occupied, or out of bounds.
    /// </summary>
    public List<Vector2Int> GetWallPlacementBuildCells(Vector2Int start, Direction dir)
    {
        var result = new List<Vector2Int>(8);
        if (_cellsByIndex == null) return result;
        if (!IsCellAvailableForPlayerWall(start, dir)) return result;

        Vector2Int step = DirectionToVector2Int(dir);
        Vector2Int cur = start + step;
        while (IsCellAvailableForPlayerWall(cur, dir))
        {
            result.Add(cur);
            cur += step;
        }
        return result;
    }

    /// <summary>
    /// Returns the run of cells (excluding <paramref name="start"/>) that can be built while
    /// satisfying full commit validation (path connectivity/chokepoint rules included).
    /// </summary>
    public List<Vector2Int> GetWallPlacementBuildCellsWithinCommitRules(Vector2Int start, Direction dir)
    {
        var result = new List<Vector2Int>(8);
        var run = GetWallPlacementBuildCells(start, dir);
        if (run.Count == 0) return result;

        _wallPlacementCommitScratch.Clear();
        _wallPlacementCommitScratch.Add(start);

        for (int i = 0; i < run.Count; i++)
        {
            var idx = run[i];
            _wallPlacementCommitScratch.Add(idx);
            if (!CanCommitWallPlacement(_wallPlacementCommitScratch)) break;
            result.Add(idx);
        }

        return result;
    }

    /// <summary>
    /// Attempts to determine the forced continuation direction for player wall placement from
    /// <paramref name="start"/>, based on an adjacent wall cell that has tower placement enabled.
    /// The returned direction points away from that adjacent wall (continuing the wall line).
    /// </summary>
    public bool TryGetWallPlacementContinuationDirection(Vector2Int start, out Direction dir)
    {
        dir = Direction.Right;
        if (_cellsByIndex == null) return false;
        if (!IsInBounds(start)) return false;

        // If the adjacent enabled wall is on the left, continue to the right, etc.
        if (IsTowerPlacementEnabledWall(new Vector2Int(start.x - 1, start.y)))
        {
            dir = Direction.Right;
            return true;
        }

        if (IsTowerPlacementEnabledWall(new Vector2Int(start.x + 1, start.y)))
        {
            dir = Direction.Left;
            return true;
        }

        if (IsTowerPlacementEnabledWall(new Vector2Int(start.x, start.y - 1)))
        {
            dir = Direction.Up;
            return true;
        }

        if (IsTowerPlacementEnabledWall(new Vector2Int(start.x, start.y + 1)))
        {
            dir = Direction.Down;
            return true;
        }

        return false;
    }

    private bool IsTowerPlacementEnabledWall(Vector2Int idx)
    {
        if (!IsInBounds(idx)) return false;
        if (!_cellsByIndex.TryGetValue(idx, out var c)) return false;
        return c.IsWall && c.TowerPlacementEnabled;
    }

    private bool TryInferPlacementDirection(IList<Vector2Int> cells, out Direction dir)
    {
        dir = Direction.Right;
        if (cells == null || cells.Count == 0) return false;

        if (cells.Count >= 2)
        {
            Vector2Int d = cells[1] - cells[0];
            if (d == Vector2Int.right)
            {
                dir = Direction.Right;
                return true;
            }

            if (d == Vector2Int.left)
            {
                dir = Direction.Left;
                return true;
            }

            if (d == Vector2Int.up)
            {
                dir = Direction.Up;
                return true;
            }

            if (d == Vector2Int.down)
            {
                dir = Direction.Down;
                return true;
            }
        }

        return TryGetWallPlacementContinuationDirection(cells[0], out dir);
    }

    /// <summary>
    /// Commits a set of cells as player-placed walls. Updates wall/blocking state, rebuilds the
    /// maze mesh and polygon collider, refreshes indicator cells, and resets pathfinding flow maps.
    /// </summary>
    public bool CanCommitWallPlacement(IList<Vector2Int> cells)
    {
        return GetWallPlacementCommitValidationResult(cells) == WallPlacementValidationResult.Valid;
    }

    public WallPlacementValidationResult GetWallPlacementValidationResult(IList<Vector2Int> cells)
    {
        WallPlacementValidationResult commitResult = GetWallPlacementCommitValidationResult(cells);
        if (commitResult != WallPlacementValidationResult.Valid) return commitResult;

        if (!SatisfiesWallPlacementLengthRule(cells))
        {
            return WallPlacementValidationResult.TooShortNeedsBridge;
        }

        return WallPlacementValidationResult.Valid;
    }

    public string GetWallPlacementValidationMessage(IList<Vector2Int> cells)
    {
        return GetWallPlacementValidationResult(cells) switch
        {
            WallPlacementValidationResult.Valid => string.Empty,
            WallPlacementValidationResult.EmptySelection => "Invalid",
            WallPlacementValidationResult.InvalidDirection => "wall must be straight",
            WallPlacementValidationResult.NoBuildableCells => "can't build there",
            WallPlacementValidationResult.EnemyEndpoint => "can't block an endpoint",
            WallPlacementValidationResult.ParallelWallTooClose => "too close to a parallel wall",
            WallPlacementValidationResult.BlocksAllPaths => "would block enemy path",
            WallPlacementValidationResult.ForcedGapTooNarrow => "would force too narrow a gap",
            WallPlacementValidationResult.TooShortNeedsBridge => "Too Short",
            _ => "invalid wall placement",
        };
    }

    private WallPlacementValidationResult GetWallPlacementCommitValidationResult(IList<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return WallPlacementValidationResult.EmptySelection;
        if (!TryInferPlacementDirection(cells, out var placementDir)) return WallPlacementValidationResult.InvalidDirection;

        bool anyBuildable = false;
        for (int i = 0; i < cells.Count; i++)
        {
            if (!IsCellAvailableForPlayerWallBase(cells[i])) continue;

            if (pathfinding != null)
            {
                Pathfinding.WallPlacementRuleFailure cellFailure = pathfinding.GetWallPlacementCellRuleFailure(cells[i], placementDir);
                WallPlacementValidationResult mappedCellFailure = MapWallPlacementRuleFailure(cellFailure);
                if (mappedCellFailure != WallPlacementValidationResult.Valid)
                {
                    return mappedCellFailure;
                }
            }

            anyBuildable = true;
        }

        if (!anyBuildable) return WallPlacementValidationResult.NoBuildableCells;

        if (pathfinding != null)
        {
            Pathfinding.WallPlacementRuleFailure commitFailure = pathfinding.GetWallPlacementCommitRuleFailure(cells);
            WallPlacementValidationResult mappedCommitFailure = MapWallPlacementRuleFailure(commitFailure);
            if (mappedCommitFailure != WallPlacementValidationResult.Valid)
            {
                return mappedCommitFailure;
            }
        }

        return WallPlacementValidationResult.Valid;
    }

    private static WallPlacementValidationResult MapWallPlacementRuleFailure(Pathfinding.WallPlacementRuleFailure failure)
    {
        return failure switch
        {
            Pathfinding.WallPlacementRuleFailure.None => WallPlacementValidationResult.Valid,
            Pathfinding.WallPlacementRuleFailure.EnemyEndpoint => WallPlacementValidationResult.EnemyEndpoint,
            Pathfinding.WallPlacementRuleFailure.ParallelWallTooClose => WallPlacementValidationResult.ParallelWallTooClose,
            Pathfinding.WallPlacementRuleFailure.BlocksAllPaths => WallPlacementValidationResult.BlocksAllPaths,
            Pathfinding.WallPlacementRuleFailure.ForcedGapTooNarrow => WallPlacementValidationResult.ForcedGapTooNarrow,
            _ => WallPlacementValidationResult.InvalidPlacement,
        };
    }

    public bool CanFinalizeWallPlacement(IList<Vector2Int> cells)
    {
        return GetWallPlacementValidationResult(cells) == WallPlacementValidationResult.Valid;
    }

    private bool SatisfiesWallPlacementLengthRule(IList<Vector2Int> cells)
    {
        float minimumLength = Mathf.Max(0f, minimumWallPlacementLengthUnits);
        if (minimumLength <= 0f) return true;

        float placementLength = cells.Count * Mathf.Max(0.0001f, GetSpacing());
        if (placementLength >= minimumLength) return true;

        return ConnectsSeparateExistingWallGroups(cells);
    }

    private bool ConnectsSeparateExistingWallGroups(IList<Vector2Int> cells)
    {
        if (_cellsByIndex == null || cells == null || cells.Count == 0) return false;

        var proposedCells = new HashSet<Vector2Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            proposedCells.Add(cells[i]);
        }

        var touchedWallSeeds = new List<Vector2Int>(8);
        var touchedWallSeedSet = new HashSet<Vector2Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            TryAddTouchedWallSeed(new Vector2Int(cell.x + 1, cell.y), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeed(new Vector2Int(cell.x - 1, cell.y), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeed(new Vector2Int(cell.x, cell.y + 1), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeed(new Vector2Int(cell.x, cell.y - 1), proposedCells, touchedWallSeeds, touchedWallSeedSet);
        }

        if (touchedWallSeeds.Count < 2) return false;

        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        int touchedComponentCount = 0;

        for (int i = 0; i < touchedWallSeeds.Count; i++)
        {
            Vector2Int seed = touchedWallSeeds[i];
            if (visited.Contains(seed)) continue;

            touchedComponentCount++;
            if (touchedComponentCount >= 2) return true;

            visited.Add(seed);
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                TryVisitConnectedWall(new Vector2Int(current.x + 1, current.y), proposedCells, visited, queue);
                TryVisitConnectedWall(new Vector2Int(current.x - 1, current.y), proposedCells, visited, queue);
                TryVisitConnectedWall(new Vector2Int(current.x, current.y + 1), proposedCells, visited, queue);
                TryVisitConnectedWall(new Vector2Int(current.x, current.y - 1), proposedCells, visited, queue);
            }
        }

        return false;
    }

    private void TryAddTouchedWallSeed(Vector2Int idx, HashSet<Vector2Int> proposedCells, List<Vector2Int> touchedWallSeeds, HashSet<Vector2Int> touchedWallSeedSet)
    {
        if (proposedCells.Contains(idx)) return;
        if (!IsWallAtCell(idx)) return;
        if (!touchedWallSeedSet.Add(idx)) return;
        touchedWallSeeds.Add(idx);
    }

    private void TryVisitConnectedWall(Vector2Int idx, HashSet<Vector2Int> proposedCells, HashSet<Vector2Int> visited, Queue<Vector2Int> queue)
    {
        if (proposedCells.Contains(idx)) return;
        if (visited.Contains(idx)) return;
        if (!IsWallAtCell(idx)) return;

        visited.Add(idx);
        queue.Enqueue(idx);
    }

    public bool TryCommitWallPlacement(IList<Vector2Int> cells)
    {
        if (!CanFinalizeWallPlacement(cells)) return false;
        if (!TryInferPlacementDirection(cells, out var placementDir)) return false;

        bool any = false;
        Vector2Int lastPlaced = default;
        List<Vector2Int> placedCells = new List<Vector2Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            var idx = cells[i];
            if (!IsCellAvailableForPlayerWall(idx, placementDir)) continue;
            if (TrySetWallAtCell(idx, isWall: true, notifyMineTowers: false))
            {
                any = true;
                lastPlaced = idx;
                placedCells.Add(idx);
            }
        }

        if (!any) return false;

        switch (wallTowerPlacementEnableMode)
        {
            case WallTowerPlacementEnableMode.End:
                TrySetTowerPlacementEnabledAtCell(lastPlaced, true);
                break;
            case WallTowerPlacementEnableMode.None:
            default:
                break;
        }

            ProjectileTower.ReleaseMineProjectilesForPlacedWalls(placedCells);

        Rebuild();
        RefreshWallPlacementIndicatorCells();

        if (pathfinding != null)
        {
            pathfinding.ResetFlowMaps();
        }

        PathViz pathViz = FindFirstObjectByType<PathViz>();
        if (pathViz != null)
        {
            pathViz.ResetRenderedPath();
        }

        return true;
    }
}
