using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private PIC pic;
    [SerializeField] private GridViz gridViz;
    [SerializeField] private GameObject waveLineRendererPrefab;
    [Min(0f)] public float stepDuration = 0.05f;
    [SerializeField, Min(0f)] private float waveAlphaIncreasePerCell = 0.15f;
    [SerializeField, Min(0)] private int waveMaxTravelDistanceInCells = 8;

    private Coroutine _waveRoutine;
    private readonly List<LineRenderer> _activeWaveLines = new List<LineRenderer>(16);

    private static readonly Vector2Int[] NeighborOffsets8 =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
    };

    private static readonly Vector2Int[] NeighborOffsets4 =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    public void Update()
    {
        if (Input.GetMouseButtonDown(0)) {
            Wave(pic.GetMousePosition());
        }
    }
    public void Wave(Vector3 position)
    {
        ResolveReferences();
        if (grid == null || gridViz == null) return;

        if (!grid.TryWorldToCell(position, out var startCellIdx)) return;
        if (!grid.TryGetCell(startCellIdx.x, startCellIdx.y, out var startCell)) return;
        if (startCell.IsBlocked) return;

        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }

        ClearWaveFrontierLines();

        _waveRoutine = StartCoroutine(WaveRoutine(startCellIdx));
    }

    private IEnumerator WaveRoutine(Vector2Int startCellIdx)
    {
        var visited = new HashSet<Vector2Int>(256);
        var frontier = new Queue<Vector2Int>(64);
        int maxTravelDistance = Mathf.Max(0, waveMaxTravelDistanceInCells);
        int currentTravelDistance = 0;

        visited.Add(startCellIdx);
        frontier.Enqueue(startCellIdx);

        float waitSeconds = Mathf.Max(0f, stepDuration);

        while (frontier.Count > 0 && currentTravelDistance <= maxTravelDistance)
        {
            int layerCount = frontier.Count;
            var layerCells = new List<Vector2Int>(layerCount);

            for (int i = 0; i < layerCount; i++)
            {
                Vector2Int current = frontier.Dequeue();
                if (!grid.TryGetCell(current.x, current.y, out var currentCell)) continue;
                if (currentCell.IsBlocked) continue;

                layerCells.Add(current);

                gridViz.AlphaCell(waveAlphaIncreasePerCell, current);

                if (currentTravelDistance >= maxTravelDistance) continue;

                if (currentCell.Neighbors != null && currentCell.Neighbors.Length > 0)
                {
                    for (int n = 0; n < currentCell.Neighbors.Length; n++)
                    {
                        Vector2Int neighbor = currentCell.Neighbors[n];
                        TryEnqueueNeighbor(neighbor, visited, frontier);
                    }
                }
                else
                {
                    for (int n = 0; n < NeighborOffsets8.Length; n++)
                    {
                        Vector2Int neighbor = current + NeighborOffsets8[n];
                        TryEnqueueNeighbor(neighbor, visited, frontier);
                    }
                }
            }

            yield return AnimateWaveFrontierLayer(layerCells, waitSeconds);

            currentTravelDistance++;
        }

        ClearWaveFrontierLines();
        _waveRoutine = null;
    }

    private void TryEnqueueNeighbor(Vector2Int neighbor, HashSet<Vector2Int> visited, Queue<Vector2Int> frontier)
    {
        if (visited.Contains(neighbor)) return;
        if (!grid.TryGetCell(neighbor.x, neighbor.y, out var neighborCell)) return;
        if (neighborCell.IsBlocked) return;

        visited.Add(neighbor);
        frontier.Enqueue(neighbor);
    }

    private void ResolveReferences()
    {
        if (grid == null)
        {
            grid = GridManager.instance != null ? GridManager.instance : FindFirstObjectByType<GridManager>();
        }

        if (pic == null)
        {
            pic = PIC.instance != null ? PIC.instance : FindFirstObjectByType<PIC>();
        }

        if (gridViz == null)
        {
            gridViz = FindFirstObjectByType<GridViz>();
            if (gridViz == null && pic != null)
            {
                gridViz = pic.GetComponent<GridViz>();
            }
        }
    }

    private IEnumerator AnimateWaveFrontierLayer(List<Vector2Int> layerCells, float duration)
    {
        if (waveLineRendererPrefab == null)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return null;
            }
            yield break;
        }

        List<List<Vector3>> targetPaths = BuildFrontierCenterLines(layerCells);
        SyncWaveLineCount(targetPaths.Count);

        if (_activeWaveLines.Count == 0)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return null;
            }
            yield break;
        }

        var fromPaths = new List<List<Vector3>>(_activeWaveLines.Count);
        for (int i = 0; i < _activeWaveLines.Count; i++)
        {
            fromPaths.Add(GetLinePoints(_activeWaveLines[i]));
        }

        if (duration <= 0f)
        {
            for (int i = 0; i < _activeWaveLines.Count; i++)
            {
                SetLinePoints(_activeWaveLines[i], targetPaths[i]);
            }

            yield return null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < _activeWaveLines.Count; i++)
            {
                SetInterpolatedLinePoints(_activeWaveLines[i], fromPaths[i], targetPaths[i], t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < _activeWaveLines.Count; i++)
        {
            SetLinePoints(_activeWaveLines[i], targetPaths[i]);
        }
    }

    private List<List<Vector3>> BuildFrontierCenterLines(List<Vector2Int> layerCells)
    {
        var result = new List<List<Vector3>>();
        if (layerCells == null || layerCells.Count == 0) return result;

        var frontier = new HashSet<Vector2Int>(layerCells);
        var components = BuildConnectedComponents(frontier);
        for (int i = 0; i < components.Count; i++)
        {
            var pathCells = BuildPolylineForComponent(components[i]);
            if (pathCells.Count == 0) continue;

            var worldPath = new List<Vector3>(pathCells.Count);
            for (int p = 0; p < pathCells.Count; p++)
            {
                Vector2Int idx = pathCells[p];
                worldPath.Add(grid.GetCellWorldCenter(idx.x, idx.y));
            }

            result.Add(worldPath);
        }

        result.Sort((a, b) =>
        {
            Vector3 ac = ComputeCentroid(a);
            Vector3 bc = ComputeCentroid(b);
            int byY = ac.y.CompareTo(bc.y);
            return byY != 0 ? byY : ac.x.CompareTo(bc.x);
        });

        return result;
    }

    private List<HashSet<Vector2Int>> BuildConnectedComponents(HashSet<Vector2Int> cells)
    {
        var result = new List<HashSet<Vector2Int>>();
        var unvisited = new HashSet<Vector2Int>(cells);
        var queue = new Queue<Vector2Int>(Mathf.Max(4, cells.Count));

        while (unvisited.Count > 0)
        {
            Vector2Int seed = default;
            foreach (var idx in unvisited)
            {
                seed = idx;
                break;
            }

            var component = new HashSet<Vector2Int>();
            queue.Enqueue(seed);
            unvisited.Remove(seed);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                component.Add(current);

                for (int i = 0; i < NeighborOffsets4.Length; i++)
                {
                    TryVisit(current + NeighborOffsets4[i], unvisited, queue);
                }
            }

            result.Add(component);
        }

        return result;

        static void TryVisit(Vector2Int cell, HashSet<Vector2Int> unvisited, Queue<Vector2Int> queue)
        {
            if (!unvisited.Remove(cell)) return;
            queue.Enqueue(cell);
        }
    }

    private List<Vector2Int> BuildPolylineForComponent(HashSet<Vector2Int> component)
    {
        var path = new List<Vector2Int>(component.Count);
        if (component == null || component.Count == 0) return path;

        var unvisited = new HashSet<Vector2Int>(component);
        Vector2Int current = ChooseStartCell(component);

        while (unvisited.Count > 0)
        {
            if (unvisited.Remove(current))
            {
                path.Add(current);
            }

            if (unvisited.Count == 0) break;

            if (TryGetUnvisitedNeighbor(current, unvisited, out var nextNeighbor))
            {
                current = nextNeighbor;
                continue;
            }

            current = FindNearestCell(current, unvisited);
        }

        return path;
    }

    private Vector2Int ChooseStartCell(HashSet<Vector2Int> component)
    {
        Vector2Int best = default;
        bool hasBest = false;
        int bestDegree = int.MaxValue;

        foreach (var cell in component)
        {
            int degree = 0;
            for (int i = 0; i < NeighborOffsets8.Length; i++)
            {
                if (component.Contains(cell + NeighborOffsets8[i])) degree++;
            }

            if (!hasBest
                || degree < bestDegree
                || (degree == bestDegree && (cell.y < best.y || (cell.y == best.y && cell.x < best.x))))
            {
                hasBest = true;
                best = cell;
                bestDegree = degree;
            }
        }

        return hasBest ? best : default;
    }

    private bool TryGetUnvisitedNeighbor(Vector2Int current, HashSet<Vector2Int> unvisited, out Vector2Int next)
    {
        for (int i = 0; i < NeighborOffsets8.Length; i++)
        {
            Vector2Int candidate = current + NeighborOffsets8[i];
            if (!unvisited.Contains(candidate)) continue;
            next = candidate;
            return true;
        }

        next = default;
        return false;
    }

    private Vector2Int FindNearestCell(Vector2Int from, HashSet<Vector2Int> candidates)
    {
        Vector2Int best = default;
        int bestDist = int.MaxValue;

        foreach (var cell in candidates)
        {
            int dx = cell.x - from.x;
            int dy = cell.y - from.y;
            int dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                best = cell;
                bestDist = dist;
            }
        }

        return best;
    }

    private void SyncWaveLineCount(int targetCount)
    {
        targetCount = Mathf.Max(0, targetCount);

        while (_activeWaveLines.Count > targetCount)
        {
            int last = _activeWaveLines.Count - 1;
            LineRenderer lr = _activeWaveLines[last];
            _activeWaveLines.RemoveAt(last);
            if (lr != null)
            {
                Destroy(lr.gameObject);
            }
        }

        while (_activeWaveLines.Count < targetCount)
        {
            var created = CreateWaveLineRenderer();
            if (created == null) break;

            created.useWorldSpace = true;
            created.loop = false;
            created.positionCount = 0;
        }
    }

    private List<Vector3> GetLinePoints(LineRenderer lr)
    {
        var points = new List<Vector3>();
        if (lr == null || lr.positionCount <= 0) return points;

        points.Capacity = lr.positionCount;
        for (int i = 0; i < lr.positionCount; i++)
        {
            points.Add(lr.GetPosition(i));
        }

        return points;
    }

    private void SetLinePoints(LineRenderer lr, List<Vector3> points)
    {
        if (lr == null) return;
        if (points == null || points.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lr.SetPosition(i, points[i]);
        }
    }

    private void SetInterpolatedLinePoints(LineRenderer lr, List<Vector3> from, List<Vector3> to, float t)
    {
        if (lr == null) return;

        int count = Mathf.Max(1, Mathf.Max(from != null ? from.Count : 0, to != null ? to.Count : 0));
        var a = ResamplePath(from, count);
        var b = ResamplePath(to, count);

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            lr.SetPosition(i, Vector3.LerpUnclamped(a[i], b[i], t));
        }
    }

    private List<Vector3> ResamplePath(List<Vector3> path, int count)
    {
        count = Mathf.Max(1, count);
        var result = new List<Vector3>(count);

        if (path == null || path.Count == 0)
        {
            for (int i = 0; i < count; i++) result.Add(Vector3.zero);
            return result;
        }

        if (path.Count == 1)
        {
            Vector3 single = path[0];
            for (int i = 0; i < count; i++) result.Add(single);
            return result;
        }

        float[] cumulative = new float[path.Count];
        cumulative[0] = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }

        float total = cumulative[path.Count - 1];
        if (total <= 0.00001f)
        {
            Vector3 p = path[0];
            for (int i = 0; i < count; i++) result.Add(p);
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            float u = count == 1 ? 0f : (float)i / (count - 1);
            float target = total * u;
            int seg = 0;
            while (seg < cumulative.Length - 1 && cumulative[seg + 1] < target)
            {
                seg++;
            }

            int next = Mathf.Min(seg + 1, path.Count - 1);
            float segStart = cumulative[seg];
            float segEnd = cumulative[next];
            float denom = Mathf.Max(0.00001f, segEnd - segStart);
            float localT = Mathf.Clamp01((target - segStart) / denom);
            result.Add(Vector3.LerpUnclamped(path[seg], path[next], localT));
        }

        return result;
    }

    private static Vector3 ComputeCentroid(List<Vector3> points)
    {
        if (points == null || points.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
        {
            sum += points[i];
        }

        return sum / points.Count;
    }

    private LineRenderer CreateWaveLineRenderer()
    {
        GameObject go = Instantiate(waveLineRendererPrefab, transform);
        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.GetComponentInChildren<LineRenderer>();
        if (lr == null)
        {
            Destroy(go);
            return null;
        }

        _activeWaveLines.Add(lr);
        return lr;
    }

    private void ClearWaveFrontierLines()
    {
        for (int i = 0; i < _activeWaveLines.Count; i++)
        {
            LineRenderer lr = _activeWaveLines[i];
            if (lr == null) continue;
            Destroy(lr.gameObject);
        }

        _activeWaveLines.Clear();
    }

    private void OnDisable()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }

        ClearWaveFrontierLines();
    }
}
