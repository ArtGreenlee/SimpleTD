using System;
using System.Collections.Generic;
using UnityEngine;

public class Pathfinding : MonoBehaviour
{
    public enum WallPlacementRuleFailure
    {
        None,
        GridUnavailable,
        InvalidSelection,
        EnemyEndpoint,
        ParallelWallTooClose,
        BlocksAllPaths,
        ForcedGapTooNarrow,
    }

    public static Pathfinding instance;

    [Header("Path Endpoints")]
    [Tooltip("Explicit start transforms used by systems that need path endpoints (for example PathViz).")]
    [SerializeField] private List<Transform> pathStarts = new List<Transform>();
    [Tooltip("Explicit goal transforms used by systems that need path endpoints (for example PathViz).")]
    [SerializeField] private List<Transform> pathGoals = new List<Transform>();

    [SerializeField] private GridManager gridManager;

    [Header("Weights")]
    [Tooltip("Prefer cells far from walls so enemies stay near corridor centers. Higher values increase bias.")]
    [Min(0f)]
    [SerializeField] private float wallCenteringWeight =0.35f;

    [Tooltip("Minimum NearestBlocked used for centering bias normalization.")]
    [Min(0.0001f)]
    [SerializeField] private float centeringNormalize =4f;

    [Header("Wall Placement Rules")]
    [Tooltip("Minimum perpendicular spacing from an existing parallel wall line, in grid cells. Set to 0 to disable.")]
    [Min(0)]
    [SerializeField] private int parallelWallMinDistance = 2;

    [Tooltip("Rejects player wall placements that force all start-to-goal routes through a bottleneck this many cells wide or narrower. Set to 0 to disable.")]
    [Min(0)]
    [SerializeField] private int minimumForcedGapWidth = 0;

    public int MinimumForcedGapWidth => minimumForcedGapWidth;

    // Diagonals are always enabled; GridManager already prevents corner-cutting.

    private struct FlowData
    {
        public float[] cost; // Dijkstra distance from each cell to goal
        public Vector2[] dir; // desired direction from cell toward lower cost neighbor
        public float lastBuildTime;
        public Vector2Int goalCell;
        public int w;
        public int h;
		public int walkabilityVersion;
    }

	public float GetPathDistanceToGoal(Vector3 position, Transform goal)
	{
		if (goal == null) return float.PositiveInfinity;
		if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
		if (gridManager == null) return float.PositiveInfinity;

		if (!gridManager.TryWorldToCell(position, out var fromCell))
		{
			return Vector3.Distance(position, goal.position);
		}

		if (!gridManager.TryWorldToCell(goal.position, out var goalCell))
		{
			return Vector3.Distance(position, goal.position);
		}

		EnsureFlow(goalCell, goal);

		int key = goal.GetInstanceID();
		if (!_flowByGoal.TryGetValue(key, out var flow)) return float.PositiveInfinity;
		if (flow.cost == null) return float.PositiveInfinity;

		int idx = ToIndex(fromCell, flow.w);
		if (idx < 0 || idx >= flow.cost.Length) return float.PositiveInfinity;

		float c = flow.cost[idx];
		if (float.IsPositiveInfinity(c)) return float.PositiveInfinity;

		// Convert grid-cost units to approximate world units.
		return c * Mathf.Max(0.0001f, gridManager.GetSpacing());
	}

    private readonly Dictionary<int, FlowData> _flowByGoal = new Dictionary<int, FlowData>(4);

    private void Awake()
    {
        instance = this;
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
    }

    /// <summary>
    /// Clears cached flow fields (call when maze changes).
    /// </summary>
    public void ResetFlowMaps()
    {
        _flowByGoal.Clear();
    }

    public bool IsWallPlacementCellAllowed(Vector2Int cell, GridManager.Direction placementDir)
    {
        return GetWallPlacementCellRuleFailure(cell, placementDir) == WallPlacementRuleFailure.None;
    }

    public WallPlacementRuleFailure GetWallPlacementCellRuleFailure(Vector2Int cell, GridManager.Direction placementDir)
    {
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return WallPlacementRuleFailure.GridUnavailable;

        if (IsEnemyEndpointCell(cell)) return WallPlacementRuleFailure.EnemyEndpoint;
        if (HasParallelWallTooClose(cell, placementDir)) return WallPlacementRuleFailure.ParallelWallTooClose;
        return WallPlacementRuleFailure.None;
    }

    public bool CanCommitWallPlacement(IList<Vector2Int> cells)
    {
        return GetWallPlacementCommitRuleFailure(cells) == WallPlacementRuleFailure.None;
    }

    public WallPlacementRuleFailure GetWallPlacementCommitRuleFailure(IList<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return WallPlacementRuleFailure.InvalidSelection;
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return WallPlacementRuleFailure.GridUnavailable;

        int w = gridManager.CellsX;
        int h = gridManager.CellsY;
        if (w <= 0 || h <= 0) return WallPlacementRuleFailure.InvalidSelection;

        var blocked = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (gridManager.TryGetCell(x, y, out var c))
                {
                    blocked[x, y] = c.IsBlocked;
                }
            }
        }

        for (int i = 0; i < cells.Count; i++)
        {
            var idx = cells[i];
            if (idx.x < 0 || idx.y < 0 || idx.x >= w || idx.y >= h) return WallPlacementRuleFailure.InvalidSelection;
            blocked[idx.x, idx.y] = true;
        }

        var starts = GatherEndpointCells(pathStarts);
        var goals = GatherEndpointCells(pathGoals);
        if (starts.Count == 0 || goals.Count == 0) return WallPlacementRuleFailure.None;

        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            if (blocked[s.x, s.y]) return WallPlacementRuleFailure.EnemyEndpoint;
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (blocked[g.x, g.y]) return WallPlacementRuleFailure.EnemyEndpoint;
        }

        var visited = new bool[w, h];
        var queue = new Queue<Vector2Int>(starts.Count * 2);
        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            if (visited[s.x, s.y]) continue;
            visited[s.x, s.y] = true;
            queue.Enqueue(s);
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            AppendWalkableNeighbors(cur, blocked, w, h, _neighborScratch);
            for (int i = 0; i < _neighborScratch.Count; i++)
            {
                var n = _neighborScratch[i];
                if (visited[n.x, n.y]) continue;

                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (!visited[g.x, g.y]) return WallPlacementRuleFailure.BlocksAllPaths;
        }

        if (minimumForcedGapWidth > 0)
        {
            int minCutWidth = ComputeStartGoalMinCutWidth(blocked, starts, goals);
            if (minCutWidth <= minimumForcedGapWidth) return WallPlacementRuleFailure.ForcedGapTooNarrow;
        }

        return WallPlacementRuleFailure.None;
    }

    public IReadOnlyList<Transform> GetPathStarts()
    {
        return pathStarts;
    }

    public IReadOnlyList<Transform> GetPathGoals()
    {
        return pathGoals;
    }

    public Vector3 GetDirection(Vector3 position, Transform goal)
    {
        if (goal == null) return Vector3.zero;
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return Vector3.zero;

        if (!gridManager.TryWorldToCell(position, out var fromCell))
        {
            // Off-grid: steer to goal directly.
            Vector3 to = goal.position - position;
            to.z =0f;
            return to.sqrMagnitude >0.000001f ? to.normalized : Vector3.zero;
        }

        if (!gridManager.TryWorldToCell(goal.position, out var goalCell))
        {
            Vector3 to = goal.position - position;
            to.z =0f;
            return to.sqrMagnitude >0.000001f ? to.normalized : Vector3.zero;
        }

        EnsureFlow(goalCell, goal);

        int key = goal.GetInstanceID();
        if (!_flowByGoal.TryGetValue(key, out var flow)) return Vector3.zero;
        if (flow.cost == null || flow.dir == null) return Vector3.zero;

        int idx = ToIndex(fromCell, flow.w);
        if (idx <0 || idx >= flow.dir.Length) return Vector3.zero;

        Vector2 d = flow.dir[idx];
        if (d.sqrMagnitude <=0.000001f)
        {
            // Fallback: direct to goal.
            Vector3 to = goal.position - position;
            to.z =0f;
            return to.sqrMagnitude >0.000001f ? to.normalized : Vector3.zero;
        }

        return new Vector3(d.x, d.y,0f);
    }

    private void EnsureFlow(Vector2Int goalCell, Transform goal)
    {
        if (gridManager != null)
        {
            gridManager.RecomputeNearestBlocked();
        }

        int key = goal != null ? goal.GetInstanceID() :0;

        if (!_flowByGoal.TryGetValue(key, out var flow))
        {
            flow = new FlowData();
            _flowByGoal[key] = flow;
        }

        int w = gridManager.CellsX;
        int h = gridManager.CellsY;
        bool sizeMismatch = flow.w != w || flow.h != h;
        bool goalMismatch = flow.goalCell != goalCell;
		int currentWalkability = gridManager != null ? gridManager.WalkabilityVersion : 0;
		bool walkabilityMismatch = flow.walkabilityVersion != currentWalkability;

		if (sizeMismatch || goalMismatch || walkabilityMismatch || flow.cost == null || flow.dir == null)
        {
            BuildFlowField(ref flow, goalCell, w, h, currentWalkability);
            _flowByGoal[key] = flow;
        }
    }

	private void BuildFlowField(ref FlowData flow, Vector2Int goalCell, int w, int h, int walkabilityVersion)
    {
        flow.w = w;
        flow.h = h;
        flow.goalCell = goalCell;
        flow.lastBuildTime = Time.time;
		flow.walkabilityVersion = walkabilityVersion;

        int n = w * h;
        flow.cost = new float[n];
        flow.dir = new Vector2[n];

        for (int i =0; i < n; i++)
        {
            flow.cost[i] = float.PositiveInfinity;
            flow.dir[i] = Vector2.zero;
        }

        if (goalCell.x <0 || goalCell.y <0 || goalCell.x >= w || goalCell.y >= h) return;

        if (!gridManager.TryGetCell(goalCell.x, goalCell.y, out var gCell) || gCell.IsBlocked)
        {
            // If the goal is inside a wall, flow is meaningless; leave as zeros.
            return;
        }

        // Dijkstra from goal across all passable cells.
        var pq = new MinHeap(n);
        int gIdx = ToIndex(goalCell, w);
        flow.cost[gIdx] =0f;
        pq.Push(gIdx,0f);

        while (pq.Count >0)
        {
            var cur = pq.Pop();
            int ci = cur.index;
            float cCost = cur.priority;
            if (cCost > flow.cost[ci]) continue;

            var cxy = FromIndex(ci, w);
            if (!gridManager.TryGetCell(cxy.x, cxy.y, out var cell)) continue;

            var neighbors = cell.Neighbors;
            if (neighbors == null || neighbors.Length ==0) continue;

            for (int ni =0; ni < neighbors.Length; ni++)
            {
                var nxy = neighbors[ni];
                if (!gridManager.TryGetCell(nxy.x, nxy.y, out var nc)) continue;
                if (nc.IsBlocked) continue;

                float step = StepCost(cell, nc);
                float alt = cCost + step;

                int nIndex = ToIndex(nxy, w);
                if (alt < flow.cost[nIndex])
                {
                    flow.cost[nIndex] = alt;
                    pq.Push(nIndex, alt);
                }
            }
        }

        // Convert distances to a per-cell preferred direction (a flow field).
        for (int y =0; y < h; y++)
        {
            for (int x =0; x < w; x++)
            {
                if (!gridManager.TryGetCell(x, y, out var cell)) continue;
                if (cell.IsBlocked) continue;

                int ci = ToIndex(x, y, w);
                float myCost = flow.cost[ci];
                if (float.IsPositiveInfinity(myCost)) continue;

                Vector2 bestDir = Vector2.zero;
                float bestNeighborCost = myCost;

                var neighbors = cell.Neighbors;
                if (neighbors == null) continue;

                for (int i =0; i < neighbors.Length; i++)
                {
                    var nxy = neighbors[i];
                    if (!gridManager.TryGetCell(nxy.x, nxy.y, out var nc)) continue;
                    if (nc.IsBlocked) continue;

                    float nCost = flow.cost[ToIndex(nxy, w)];
                    if (nCost < bestNeighborCost)
                    {
                        bestNeighborCost = nCost;
                        Vector3 v = (nc.WorldCenter - cell.WorldCenter);
                        bestDir = new Vector2(v.x, v.y);
                    }
                }

                if (bestDir.sqrMagnitude >0.000001f)
                    flow.dir[ci] = bestDir.normalized;
            }
        }
    }

    private float StepCost(GridManager.Cell from, GridManager.Cell to)
    {
        // Base movement cost (slightly higher for diagonals).
        bool diagonal = (from.Index.x != to.Index.x) && (from.Index.y != to.Index.y);
        float baseCost = diagonal ?1.41421356f :1f;

        // Prefer corridor centers: penalize being close to walls.
        float nb = Mathf.Max(0f, to.NearestBlocked);
        float centerFactor =1f - Mathf.Clamp01(nb / Mathf.Max(0.0001f, centeringNormalize));
        float wallPenalty = wallCenteringWeight * centerFactor;

		return baseCost + wallPenalty;
    }

    private static int ToIndex(Vector2Int c, int w) => c.y * w + c.x;
    private static int ToIndex(int x, int y, int w) => y * w + x;

    private static Vector2Int FromIndex(int i, int w)
    {
        int x = i - (i / w) * w;
        int y = i / w;
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Runs A* from <paramref name="from"/> to <paramref name="to"/> on the grid and returns the
    /// normalised world-space direction the caller should move to travel from start toward finish.
    /// Operates independently of all flow maps.
    /// Returns Vector3.zero when no path exists or inputs are off-grid.
    /// </summary>
    public Vector3 FindDirection(Vector3 from, Vector3 to)
    {
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return Vector3.zero;

        gridManager.RecomputeNearestBlocked();

        if (!gridManager.TryWorldToCell(from, out Vector2Int startCell))
        {
            Vector3 direct = to - from;
            direct.z = 0f;
            return direct.sqrMagnitude > 0.000001f ? direct.normalized : Vector3.zero;
        }

        if (!gridManager.TryWorldToCell(to, out Vector2Int goalCell))
        {
            Vector3 direct = to - from;
            direct.z = 0f;
            return direct.sqrMagnitude > 0.000001f ? direct.normalized : Vector3.zero;
        }

        if (startCell == goalCell)
        {
            Vector3 direct = to - from;
            direct.z = 0f;
            return direct.sqrMagnitude > 0.000001f ? direct.normalized : Vector3.zero;
        }

        int w = gridManager.CellsX;
        int h = gridManager.CellsY;
        int n = w * h;

        float[] gCost = new float[n];
        int[] parent = new int[n];
        for (int i = 0; i < n; i++)
        {
            gCost[i] = float.PositiveInfinity;
            parent[i] = -1;
        }

        int startIdx = ToIndex(startCell, w);
        int goalIdx  = ToIndex(goalCell,  w);

        gCost[startIdx] = 0f;

        var open = new MinHeap(64);
        open.Push(startIdx, AStarHeuristic(startCell, goalCell));

        bool found = false;
        while (open.Count > 0)
        {
            var cur = open.Pop();
            int ci = cur.index;

            if (ci == goalIdx)
            {
                found = true;
                break;
            }

            float curG = gCost[ci];
            if (cur.priority - AStarHeuristic(FromIndex(ci, w), goalCell) > curG + 0.0001f)
                continue; // stale entry

            Vector2Int cxy = FromIndex(ci, w);
            if (!gridManager.TryGetCell(cxy.x, cxy.y, out var cell)) continue;

            var neighbors = cell.Neighbors;
            if (neighbors == null) continue;

            for (int ni = 0; ni < neighbors.Length; ni++)
            {
                Vector2Int nxy = neighbors[ni];
                if (!gridManager.TryGetCell(nxy.x, nxy.y, out var nc)) continue;
                if (nc.IsBlocked) continue;

                int nIdx = ToIndex(nxy, w);
                float tentativeG = curG + StepCost(cell, nc);
                if (tentativeG < gCost[nIdx])
                {
                    gCost[nIdx] = tentativeG;
                    parent[nIdx] = ci;
                    open.Push(nIdx, tentativeG + AStarHeuristic(nxy, goalCell));
                }
            }
        }

        if (!found) return Vector3.zero;

        // Trace path back to the first step after start.
        int step = goalIdx;
        while (step != -1 && parent[step] != startIdx && parent[step] != -1)
            step = parent[step];

        if (step == -1 || step == startIdx) return Vector3.zero;

        Vector2Int firstStepCell = FromIndex(step, w);
        if (!gridManager.TryGetCell(firstStepCell.x, firstStepCell.y, out var firstCell)) return Vector3.zero;

        Vector3 dir = firstCell.WorldCenter - from;
        dir.z = 0f;
        return dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.zero;
    }

    private static float AStarHeuristic(Vector2Int a, Vector2Int b)
    {
        // Octile distance for 8-way movement.
        int dx = Mathf.Abs(b.x - a.x);
        int dy = Mathf.Abs(b.y - a.y);
        return (dx + dy) + (1.41421356f - 2f) * Mathf.Min(dx, dy);
    }

    private bool IsEnemyEndpointCell(Vector2Int cell)
    {
        if (ContainsEndpointCell(pathStarts, cell)) return true;
        if (ContainsEndpointCell(pathGoals, cell)) return true;
        return false;
    }

    private bool ContainsEndpointCell(List<Transform> endpoints, Vector2Int target)
    {
        if (endpoints == null || endpoints.Count == 0) return false;
        for (int i = 0; i < endpoints.Count; i++)
        {
            var t = endpoints[i];
            if (t == null) continue;
            if (!gridManager.TryWorldToCell(t.position, out var cell)) continue;
            if (cell == target) return true;
        }
        return false;
    }

    private List<Vector2Int> GatherEndpointCells(List<Transform> endpoints)
    {
        var result = new List<Vector2Int>(endpoints != null ? endpoints.Count : 0);
        if (endpoints == null) return result;

        for (int i = 0; i < endpoints.Count; i++)
        {
            var t = endpoints[i];
            if (t == null) continue;
            if (!gridManager.TryWorldToCell(t.position, out var cell)) continue;
            if (!result.Contains(cell)) result.Add(cell);
        }

        return result;
    }

    private bool HasParallelWallTooClose(Vector2Int cell, GridManager.Direction placementDir)
    {
        if (parallelWallMinDistance <= 0) return false;

        bool horizontal = placementDir == GridManager.Direction.Left || placementDir == GridManager.Direction.Right;
        Vector2Int perpStep = horizontal ? Vector2Int.up : Vector2Int.right;

        for (int d = 1; d <= parallelWallMinDistance; d++)
        {
            if (IsParallelWallCell(cell + perpStep * d, horizontal)) return true;
            if (IsParallelWallCell(cell - perpStep * d, horizontal)) return true;
        }

        return false;
    }

    private bool IsParallelWallCell(Vector2Int cell, bool horizontal)
    {
        if (!gridManager.TryGetCell(cell.x, cell.y, out var c)) return false;
        if (!c.IsWall) return false;

        if (horizontal)
        {
            return IsWallCell(new Vector2Int(cell.x - 1, cell.y)) || IsWallCell(new Vector2Int(cell.x + 1, cell.y));
        }

        return IsWallCell(new Vector2Int(cell.x, cell.y - 1)) || IsWallCell(new Vector2Int(cell.x, cell.y + 1));
    }

    private bool IsWallCell(Vector2Int cell)
    {
        if (!gridManager.TryGetCell(cell.x, cell.y, out var c)) return false;
        return c.IsWall;
    }

    private readonly List<Vector2Int> _neighborScratch = new List<Vector2Int>(8);

    private static readonly Vector2Int[] CardinalSteps =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    private static readonly Vector2Int[] DiagonalSteps =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
    };

    private static bool IsInBounds(int x, int y, int w, int h)
    {
        return x >= 0 && y >= 0 && x < w && y < h;
    }

    private void AppendWalkableNeighbors(Vector2Int from, bool[,] blocked, int w, int h, List<Vector2Int> result)
    {
        result.Clear();

        for (int i = 0; i < CardinalSteps.Length; i++)
        {
            int nx = from.x + CardinalSteps[i].x;
            int ny = from.y + CardinalSteps[i].y;
            if (!IsInBounds(nx, ny, w, h)) continue;
            if (blocked[nx, ny]) continue;
            result.Add(new Vector2Int(nx, ny));
        }

        for (int i = 0; i < DiagonalSteps.Length; i++)
        {
            int dx = DiagonalSteps[i].x;
            int dy = DiagonalSteps[i].y;
            int nx = from.x + dx;
            int ny = from.y + dy;
            if (!IsInBounds(nx, ny, w, h)) continue;
            if (blocked[nx, ny]) continue;

            int ax = from.x + dx;
            int ay = from.y;
            int bx = from.x;
            int by = from.y + dy;

            if (!IsInBounds(ax, ay, w, h) || blocked[ax, ay]) continue;
            if (!IsInBounds(bx, by, w, h) || blocked[bx, by]) continue;

            result.Add(new Vector2Int(nx, ny));
        }
    }

    private int ComputeStartGoalMinCutWidth(bool[,] blocked, List<Vector2Int> starts, List<Vector2Int> goals)
    {
        int w = blocked.GetLength(0);
        int h = blocked.GetLength(1);

        var isStart = new bool[w, h];
        var isGoal = new bool[w, h];
        var isEndpoint = new bool[w, h];

        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            if (!IsInBounds(s.x, s.y, w, h)) continue;
            if (blocked[s.x, s.y]) continue;
            isStart[s.x, s.y] = true;
            isEndpoint[s.x, s.y] = true;
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (!IsInBounds(g.x, g.y, w, h)) continue;
            if (blocked[g.x, g.y]) continue;
            isGoal[g.x, g.y] = true;
            isEndpoint[g.x, g.y] = true;
        }

        var passableIds = new int[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                passableIds[x, y] = -1;
            }
        }

        int passableCount = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (blocked[x, y]) continue;
                passableIds[x, y] = passableCount;
                passableCount++;
            }
        }

        if (passableCount == 0) return 0;

        int source = passableCount * 2;
        int sink = source + 1;
        var flow = new DinicMaxFlow(sink + 1);
        int inf = int.MaxValue / 4;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int id = passableIds[x, y];
                if (id < 0) continue;

                int nodeIn = id * 2;
                int nodeOut = nodeIn + 1;
                int nodeCapacity = isEndpoint[x, y] ? inf : 1;
                flow.AddEdge(nodeIn, nodeOut, nodeCapacity);

                if (isStart[x, y]) flow.AddEdge(source, nodeIn, inf);
                if (isGoal[x, y]) flow.AddEdge(nodeOut, sink, inf);

                var from = new Vector2Int(x, y);
                AppendWalkableNeighbors(from, blocked, w, h, _neighborScratch);
                for (int i = 0; i < _neighborScratch.Count; i++)
                {
                    var n = _neighborScratch[i];
                    int nid = passableIds[n.x, n.y];
                    if (nid < 0) continue;
                    flow.AddEdge(nodeOut, nid * 2, inf);
                }
            }
        }

        return flow.MaxFlow(source, sink, inf);
    }

    private sealed class DinicMaxFlow
    {
        private struct Edge
        {
            public int to;
            public int rev;
            public int capacity;
        }

        private readonly List<Edge>[] _graph;
        private readonly int[] _level;
        private readonly int[] _iter;

        public DinicMaxFlow(int nodeCount)
        {
            _graph = new List<Edge>[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                _graph[i] = new List<Edge>(8);
            }

            _level = new int[nodeCount];
            _iter = new int[nodeCount];
        }

        public void AddEdge(int from, int to, int capacity)
        {
            var forward = new Edge { to = to, rev = _graph[to].Count, capacity = capacity };
            var reverse = new Edge { to = from, rev = _graph[from].Count, capacity = 0 };
            _graph[from].Add(forward);
            _graph[to].Add(reverse);
        }

        public int MaxFlow(int source, int sink, int flowCap)
        {
            int total = 0;
            while (BuildLevelGraph(source, sink))
            {
                Array.Clear(_iter, 0, _iter.Length);
                while (total < flowCap)
                {
                    int pushed = SendFlow(source, sink, flowCap - total);
                    if (pushed <= 0) break;
                    total += pushed;
                }

                if (total >= flowCap) break;
            }

            return total;
        }

        private bool BuildLevelGraph(int source, int sink)
        {
            for (int i = 0; i < _level.Length; i++)
            {
                _level[i] = -1;
            }

            var queue = new Queue<int>();
            _level[source] = 0;
            queue.Enqueue(source);

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                var edges = _graph[v];
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    if (e.capacity <= 0) continue;
                    if (_level[e.to] >= 0) continue;

                    _level[e.to] = _level[v] + 1;
                    if (e.to == sink) return true;
                    queue.Enqueue(e.to);
                }
            }

            return _level[sink] >= 0;
        }

        private int SendFlow(int v, int sink, int flow)
        {
            if (v == sink) return flow;

            var edges = _graph[v];
            for (; _iter[v] < edges.Count; _iter[v]++)
            {
                int ei = _iter[v];
                var e = edges[ei];
                if (e.capacity <= 0) continue;
                if (_level[e.to] != _level[v] + 1) continue;

                int pushed = SendFlow(e.to, sink, Mathf.Min(flow, e.capacity));
                if (pushed <= 0) continue;

                e.capacity -= pushed;
                edges[ei] = e;
                var rev = _graph[e.to][e.rev];
                rev.capacity += pushed;
                _graph[e.to][e.rev] = rev;
                return pushed;
            }

            return 0;
        }
    }

    // Minimal binary heap priority queue (index + float priority).
    private sealed class MinHeap
    {
        public struct Node
        {
            public int index;
            public float priority;
        }

        private readonly List<Node> _data;

        public int Count => _data.Count;

        public MinHeap(int capacity)
        {
            _data = new List<Node>(Mathf.Max(4, capacity));
        }

        public void Push(int index, float priority)
        {
            _data.Add(new Node { index = index, priority = priority });
            SiftUp(_data.Count -1);
        }

        public Node Pop()
        {
            int last = _data.Count -1;
            Node root = _data[0];
            _data[0] = _data[last];
            _data.RemoveAt(last);
            if (_data.Count >0) SiftDown(0);
            return root;
        }

        private void SiftUp(int i)
        {
            while (i >0)
            {
                int p = (i -1) /2;
                if (_data[p].priority <= _data[i].priority) break;
                (_data[p], _data[i]) = (_data[i], _data[p]);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            int count = _data.Count;
            while (true)
            {
                int l = i *2 +1;
                if (l >= count) break;
                int r = l +1;

                int smallest = (r < count && _data[r].priority < _data[l].priority) ? r : l;
                if (_data[i].priority <= _data[smallest].priority) break;

                (_data[i], _data[smallest]) = (_data[smallest], _data[i]);
                i = smallest;
            }
        }
    }
}
