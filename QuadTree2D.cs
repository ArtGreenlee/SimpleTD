using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple dynamic quadtree for2D point queries (XY plane).
/// Used to find nearby boid neighbors without relying on grid cells.
/// </summary>
public class QuadTree2D : MonoBehaviour
{
    public static QuadTree2D instance;

    [Header("Bounds")]
    [Tooltip("If enabled, quadtree bounds will be derived from the GridManager at runtime.")]
    [SerializeField] private bool autoSizeFromGrid = true;

    [Tooltip("Optional explicit grid reference; if null, uses GridManager.instance.")]
    [SerializeField] private GridManager grid;

    [Tooltip("Extra world-space padding added on each axis when auto sizing from grid.")]
    [Min(0f)]
    [SerializeField] private float boundsPadding = 0.5f;

    [Tooltip("World-space center of the quadtree bounds.")]
    [SerializeField] private Vector2 worldCenter = Vector2.zero;
    [Tooltip("World-space size of the quadtree bounds.")]
    [SerializeField] private Vector2 worldSize = new Vector2(50f, 50f);

    [Header("Tuning")]
    [Min(1)]
    [SerializeField] private int nodeCapacity = 16;
    [Min(1)]
    [SerializeField] private int maxDepth = 8;
    [Tooltip("If enabled, draws quadtree bounds in gizmos (selected).")]
    [SerializeField] private bool visualize = false;

    private Node _root;
    private Rect _lastBuiltBounds;
    private readonly Dictionary<int, Enemy> _byId = new Dictionary<int, Enemy>(512);
    private readonly Dictionary<int, Node> _nodeById = new Dictionary<int, Node>(512);

    private void Awake()
    {
        instance = this;
        RebuildTree();
    }

    private void OnEnable()
    {
        if (_root == null) RebuildTree();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        boundsPadding = Mathf.Max(0f, boundsPadding);

        // Keep preview bounds in sync in the editor.
        if (!Application.isPlaying && autoSizeFromGrid)
        {
            TrySyncBoundsFromGrid();
        }
    }
#endif

    private void TrySyncBoundsFromGrid()
    {
        if (!autoSizeFromGrid) return;

        GridManager gm = grid != null ? grid : GridManager.instance;
        if (gm == null) return;

        // Grid is centered around gm.transform, defined in gm local space.
        Vector2 size = new Vector2(gm.CellsX * gm.GetSpacing(), gm.CellsY * gm.GetSpacing());
        Vector2 center = (Vector2)gm.transform.position;

        // Handle scaled grids (common in Unity).
        Vector3 lossy = gm.transform.lossyScale;
        size = new Vector2(size.x * Mathf.Abs(lossy.x), size.y * Mathf.Abs(lossy.y));

        size += Vector2.one * (boundsPadding * 2f);

        worldCenter = center;
        worldSize = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
    }

    public void RebuildTree()
    {
        TrySyncBoundsFromGrid();

        _byId.Clear();
        _nodeById.Clear();
        _lastBuiltBounds = new Rect(worldCenter - worldSize * 0.5f, worldSize);
        _root = new Node(_lastBuiltBounds, nodeCapacity, maxDepth, 0);
    }

    /// <summary>Call once per frame/FixedUpdate before queries to ensure tree exists.</summary>
    public void Ensure()
    {
        if (_root == null)
        {
            RebuildTree();
            return;
        }

        // If grid size changes during play, rebuild the tree to match.
        if (autoSizeFromGrid)
        {
            TrySyncBoundsFromGrid();
            var desired = new Rect(worldCenter - worldSize * 0.5f, worldSize);
            if (desired.size != _lastBuiltBounds.size || desired.center != _lastBuiltBounds.center)
            {
                RebuildTree();
            }
        }
    }

    public void Register(Enemy e)
    {
        if (e == null) return;
        Ensure();

        int id = e.GetInstanceID();
        _byId[id] = e;
        InsertInternal(e);
    }

    public void Unregister(Enemy e)
    {
        if (e == null) return;
        int id = e.GetInstanceID();

        _byId.Remove(id);

        if (_nodeById.TryGetValue(id, out var node) && node != null)
        {
            node.Remove(id);
        }
        _nodeById.Remove(id);
    }

    public void UpdateEnemy(Enemy e)
    {
        if (e == null) return;
        Ensure();

        int id = e.GetInstanceID();

        // If not registered yet, register.
        if (!_byId.ContainsKey(id))
        {
            Register(e);
            return;
        }

        // If node still contains point, keep; else reinsert.
        if (_nodeById.TryGetValue(id, out var node) && node != null)
        {
            Vector2 p = GetEnemyPos(e);
            if (node.Bounds.Contains(p))
            {
                node.TryUpdatePosition(id, p);
                return;
            }

            node.Remove(id);
            _nodeById.Remove(id);
        }

        InsertInternal(e);
    }

    private void InsertInternal(Enemy e)
    {
        Vector2 p = GetEnemyPos(e);
        int id = e.GetInstanceID();

        if (!_root.Bounds.Contains(p))
        {
            // Outside root bounds: clamp into bounds so we can still query neighbors.
            p.x = Mathf.Clamp(p.x, _root.Bounds.xMin, _root.Bounds.xMax);
            p.y = Mathf.Clamp(p.y, _root.Bounds.yMin, _root.Bounds.yMax);
        }

        var insertedNode = _root.Insert(id, p);
        if (insertedNode != null)
        {
            _nodeById[id] = insertedNode;
        }
    }

    public void QueryCircle(Vector2 center, float radius, List<Enemy> results, Enemy ignore = null)
    {
        if (results == null) return;
        results.Clear();
        Ensure();

        float r = Mathf.Max(0f, radius);
        if (r <= 0.0001f) return;

        float rSqr = r * r;
        _root.QueryCircle(center, r, rSqr, results, _byId, ignore);
    }

    private static Vector2 GetEnemyPos(Enemy e)
    {
        if (e.rb != null) return e.rb.position;
        return (Vector2)e.transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!visualize) return;

        if (autoSizeFromGrid) TrySyncBoundsFromGrid();

        var r = new Rect(worldCenter - worldSize * 0.5f, worldSize);
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(r.center, r.size);

        if (_root != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            _root.DrawGizmos();
        }
    }

    private class Node
    {
        public Rect Bounds { get; }

        private readonly int _capacity;
        private readonly int _maxDepth;
        private readonly int _depth;

        private readonly List<int> _ids;
        private readonly List<Vector2> _positions;

        private Node _nw, _ne, _sw, _se;

        public Node(Rect bounds, int capacity, int maxDepth, int depth)
        {
            Bounds = bounds;
            _capacity = Mathf.Max(1, capacity);
            _maxDepth = Mathf.Max(1, maxDepth);
            _depth = depth;

            _ids = new List<int>(_capacity);
            _positions = new List<Vector2>(_capacity);
        }

        public Node Insert(int id, Vector2 pos)
        {
            if (!Bounds.Contains(pos)) return null;

            if (_nw == null)
            {
                if (_ids.Count < _capacity || _depth >= _maxDepth)
                {
                    _ids.Add(id);
                    _positions.Add(pos);
                    return this;
                }

                Subdivide();
                // Reinsert existing into children.
                for (int i = _ids.Count - 1; i >= 0; i--)
                {
                    int eid = _ids[i];
                    Vector2 ep = _positions[i];
                    _ids.RemoveAt(i);
                    _positions.RemoveAt(i);
                    InsertIntoChild(eid, ep);
                }
            }

            return InsertIntoChild(id, pos);
        }

        private Node InsertIntoChild(int id, Vector2 pos)
        {
            // Order doesn't matter.
            if (_nw.Bounds.Contains(pos)) return _nw.Insert(id, pos);
            if (_ne.Bounds.Contains(pos)) return _ne.Insert(id, pos);
            if (_sw.Bounds.Contains(pos)) return _sw.Insert(id, pos);
            if (_se.Bounds.Contains(pos)) return _se.Insert(id, pos);

            // Shouldn't happen due to parent bounds check, but fallback.
            _ids.Add(id);
            _positions.Add(pos);
            return this;
        }

        public void Remove(int id)
        {
            for (int i = _ids.Count - 1; i >= 0; i--)
            {
                if (_ids[i] == id)
                {
                    _ids.RemoveAt(i);
                    _positions.RemoveAt(i);
                    return;
                }
            }
        }

        public void TryUpdatePosition(int id, Vector2 pos)
        {
            for (int i = _ids.Count - 1; i >= 0; i--)
            {
                if (_ids[i] == id)
                {
                    _positions[i] = pos;
                    return;
                }
            }
        }

        private void Subdivide()
        {
            Vector2 half = Bounds.size * 0.5f;
            Vector2 min = Bounds.min;

            var nw = new Rect(new Vector2(min.x, min.y + half.y), half);
            var ne = new Rect(new Vector2(min.x + half.x, min.y + half.y), half);
            var sw = new Rect(new Vector2(min.x, min.y), half);
            var se = new Rect(new Vector2(min.x + half.x, min.y), half);

            _nw = new Node(nw, _capacity, _maxDepth, _depth + 1);
            _ne = new Node(ne, _capacity, _maxDepth, _depth + 1);
            _sw = new Node(sw, _capacity, _maxDepth, _depth + 1);
            _se = new Node(se, _capacity, _maxDepth, _depth + 1);
        }

        public void QueryCircle(Vector2 center, float radius, float rSqr, List<Enemy> results, Dictionary<int, Enemy> byId, Enemy ignore)
        {
            if (!IntersectsCircle(Bounds, center, radius)) return;

            for (int i = 0; i < _ids.Count; i++)
            {
                Vector2 p = _positions[i];
                if ((p - center).sqrMagnitude > rSqr) continue;

                int id = _ids[i];
                if (!byId.TryGetValue(id, out var e) || e == null) continue;
                if (ignore != null && e == ignore) continue;
                results.Add(e);
            }

            if (_nw == null) return;
            _nw.QueryCircle(center, radius, rSqr, results, byId, ignore);
            _ne.QueryCircle(center, radius, rSqr, results, byId, ignore);
            _sw.QueryCircle(center, radius, rSqr, results, byId, ignore);
            _se.QueryCircle(center, radius, rSqr, results, byId, ignore);
        }

        private static bool IntersectsCircle(Rect rect, Vector2 center, float radius)
        {
            // Clamp point to rect, compute distance to circle center.
            float cx = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            float dx = center.x - cx;
            float dy = center.y - cy;
            return (dx * dx + dy * dy) <= radius * radius;
        }

        public void DrawGizmos()
        {
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            if (_nw == null) return;
            _nw.DrawGizmos();
            _ne.DrawGizmos();
            _sw.DrawGizmos();
            _se.DrawGizmos();
        }
    }
}
