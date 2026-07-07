using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PathViz : MonoBehaviour
{
    public LineRenderer lr;

    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Pathfinding pathfinding;

    [Header("Path Build")]
    [SerializeField, Min(1)] private int maxPathSteps = 4096;
    [SerializeField] private float zOffset = -0.05f;

    [Header("Path Smoothing")]
    [Tooltip("Enables cubic Bezier smoothing over cached path points before rendering.")]
    [SerializeField] private bool smoothWithBezier = true;
    [Tooltip("How many points to sample per path segment when smoothing.")]
    [SerializeField, Min(1)] private int bezierSamplesPerSegment = 6;
    [Tooltip("0 = straight segments, 1 = full smooth tangents.")]
    [Range(0f, 1f)]
    [SerializeField] private float bezierSmoothStrength = 1f;

    private readonly List<CachedPath> _cachedPaths = new List<CachedPath>(32);
    private readonly List<LineRenderer> _lineRenderers = new List<LineRenderer>(8);
    private readonly List<Vector3> _smoothedPointsBuffer = new List<Vector3>(512);
    private readonly List<LineRendererColorState> _alphaAnimationStates = new List<LineRendererColorState>(8);
    private bool _hasCachedPaths;
    private Coroutine _alphaAnimationRoutine;

    private sealed class CachedPath
    {
        public readonly List<Vector3> points = new List<Vector3>(256);
    }

    private sealed class LineRendererColorState
    {
        public LineRenderer lineRenderer;
        public Color startColor;
        public Color endColor;
    }

    private void Awake()
    {
        ResolveRefs();
        EnsureLineRenderer();
    }

    private void OnEnable()
    {
        ResolveRefs();
        EnsureLineRenderer();
    }

    private void Update()
    {
        bool waveActive = waveManager != null && waveManager.IsWaveInProgress();
        if (waveActive)
        {
            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] != null)
                {
                    _lineRenderers[i].enabled = false;
                }
            }
            return;
        }

        BuildAndCachePathsIfNeeded();
        RenderCachedPaths();
    }

    [ContextMenu("Rebuild Cached Path")]
    public void RebuildCachedPath()
    {
        _hasCachedPaths = false;
        _cachedPaths.Clear();
        BuildAndCachePathsIfNeeded(force: true);
        RenderCachedPaths();
    }

    [ContextMenu("Rerender PathViz")]
    public void RerenderPathViz()
    {
        BuildAndCachePathsIfNeeded();
        RenderCachedPaths();
    }

    public void ResetRenderedPath()
    {
        _hasCachedPaths = false;
        _cachedPaths.Clear();

        StopAlphaAnimation(restoreOriginalAlpha: false);

        for (int i = 0; i < _lineRenderers.Count; i++)
        {
            if (_lineRenderers[i] == null) continue;
            _lineRenderers[i].positionCount = 0;
            _lineRenderers[i].enabled = false;
        }
    }

    public void AnimateLineRendererAlphaSine(float durationSeconds, float frequency = 2f, float minAlpha = 0.2f, float maxAlpha = 1f)
    {
        EnsureLineRenderer();

        StopAlphaAnimation();

        if (durationSeconds <= 0f || _lineRenderers.Count == 0)
        {
            return;
        }

        _alphaAnimationRoutine = StartCoroutine(AnimateLineRendererAlphaSineRoutine(durationSeconds, frequency, minAlpha, maxAlpha));
    }

    public void StopAlphaAnimation(bool restoreOriginalAlpha = true)
    {
        if (_alphaAnimationRoutine == null)
        {
            return;
        }

        StopCoroutine(_alphaAnimationRoutine);
        _alphaAnimationRoutine = null;

        if (!restoreOriginalAlpha)
        {
            _alphaAnimationStates.Clear();
            return;
        }

        for (int i = 0; i < _alphaAnimationStates.Count; i++)
        {
            LineRendererColorState state = _alphaAnimationStates[i];
            if (state.lineRenderer == null) continue;

            SetLineRendererColors(state.lineRenderer, state.startColor, state.endColor);
        }

        _alphaAnimationStates.Clear();
    }

    private void BuildAndCachePathsIfNeeded(bool force = false)
    {
        if (!force && _hasCachedPaths) return;

        ResolveRefs();

        _cachedPaths.Clear();
        _hasCachedPaths = false;

        if (gridManager == null || pathfinding == null)
        {
            return;
        }

        IReadOnlyList<Transform> starts = pathfinding.GetPathStarts();
        IReadOnlyList<Transform> goals = pathfinding.GetPathGoals();
        if (starts == null || goals == null || starts.Count == 0 || goals.Count == 0)
        {
            return;
        }

        for (int si = 0; si < starts.Count; si++)
        {
            Transform startTransform = starts[si];
            if (startTransform == null) continue;

            for (int gi = 0; gi < goals.Count; gi++)
            {
                Transform goalTransform = goals[gi];
                if (goalTransform == null) continue;

                if (TryBuildPath(startTransform.position, goalTransform, out CachedPath cachedPath))
                {
                    _cachedPaths.Add(cachedPath);
                }
            }
        }

        _hasCachedPaths = _cachedPaths.Count > 0;
    }

    private bool TryBuildPath(Vector3 start, Transform goalTransform, out CachedPath cachedPath)
    {
        cachedPath = null;

        if (!gridManager.TryWorldToCell(start, out Vector2Int startCell)) return false;
        if (!gridManager.TryWorldToCell(goalTransform.position, out Vector2Int goalCell)) return false;

        if (!gridManager.TryGetCell(startCell.x, startCell.y, out var startCellData) || startCellData.IsBlocked) return false;
        if (!gridManager.TryGetCell(goalCell.x, goalCell.y, out var goalCellData) || goalCellData.IsBlocked) return false;

        cachedPath = new CachedPath();

        var visited = new HashSet<Vector2Int>();
        Vector2Int current = startCell;

        cachedPath.points.Add(ToVizPoint(startCellData.WorldCenter));
        visited.Add(current);

        int safety = Mathf.Max(1, maxPathSteps);
        while (current != goalCell && safety-- > 0)
        {
            if (!gridManager.TryGetCell(current.x, current.y, out var currentCell)) break;
            if (currentCell.Neighbors == null || currentCell.Neighbors.Length == 0) break;

            Vector3 desired3 = pathfinding.GetDirection(currentCell.WorldCenter, goalTransform);
            Vector2 desiredDir = new Vector2(desired3.x, desired3.y);
            if (desiredDir.sqrMagnitude <= 0.000001f)
            {
                Vector3 toGoal = goalCellData.WorldCenter - currentCell.WorldCenter;
                desiredDir = new Vector2(toGoal.x, toGoal.y);
            }
            if (desiredDir.sqrMagnitude > 0.000001f)
            {
                desiredDir.Normalize();
            }

            bool found = false;
            Vector2Int bestNext = current;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < currentCell.Neighbors.Length; i++)
            {
                Vector2Int n = currentCell.Neighbors[i];
                if (!gridManager.TryGetCell(n.x, n.y, out var nCell)) continue;
                if (nCell.IsBlocked) continue;
                if (visited.Contains(n)) continue;

                Vector3 step3 = nCell.WorldCenter - currentCell.WorldCenter;
                Vector2 stepDir = new Vector2(step3.x, step3.y);
                if (stepDir.sqrMagnitude <= 0.000001f) continue;
                stepDir.Normalize();

                float align = Vector2.Dot(stepDir, desiredDir);
                float distToGoal = (goalCellData.WorldCenter - nCell.WorldCenter).sqrMagnitude;
                float score = align * 1000f - distToGoal;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestNext = n;
                    found = true;
                }
            }

            if (!found)
            {
                break;
            }

            current = bestNext;
            visited.Add(current);

            if (gridManager.TryGetCell(current.x, current.y, out var nextCellData))
            {
                cachedPath.points.Add(ToVizPoint(nextCellData.WorldCenter));
            }
            else
            {
                break;
            }
        }

        return cachedPath.points.Count >= 2;
    }

    private void RenderCachedPaths()
    {
        EnsureLineRenderer();
        if (lr == null) return;

        if (!_hasCachedPaths || _cachedPaths.Count == 0)
        {
            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] == null) continue;
                _lineRenderers[i].positionCount = 0;
                _lineRenderers[i].enabled = false;
            }
            return;
        }

        EnsureRendererPool(_cachedPaths.Count);

        for (int i = 0; i < _cachedPaths.Count; i++)
        {
            LineRenderer renderer = _lineRenderers[i];
            if (renderer == null) continue;

            var points = _cachedPaths[i].points;
            var renderPoints = GetRenderPoints(points);
            renderer.enabled = true;
            renderer.positionCount = renderPoints.Count;
            for (int p = 0; p < renderPoints.Count; p++)
            {
                renderer.SetPosition(p, renderPoints[p]);
            }
        }

        for (int i = _cachedPaths.Count; i < _lineRenderers.Count; i++)
        {
            if (_lineRenderers[i] == null) continue;
            _lineRenderers[i].positionCount = 0;
            _lineRenderers[i].enabled = false;
        }
    }

    private void EnsureRendererPool(int needed)
    {
        EnsureLineRenderer();
        if (lr == null) return;

        if (_lineRenderers.Count == 0)
        {
            _lineRenderers.Add(lr);
        }

        while (_lineRenderers.Count < needed)
        {
            LineRenderer clone = Instantiate(lr, lr.transform.parent);
            clone.gameObject.name = lr.gameObject.name + "_Path_" + _lineRenderers.Count;
            clone.enabled = false;
            _lineRenderers.Add(clone);
        }
    }

    private void ResolveRefs()
    {
        if (waveManager == null) waveManager = WaveManager.instance != null ? WaveManager.instance : FindFirstObjectByType<WaveManager>();
        if (gridManager == null) gridManager = GridManager.instance != null ? GridManager.instance : FindFirstObjectByType<GridManager>();
        if (pathfinding == null) pathfinding = Pathfinding.instance != null ? Pathfinding.instance : FindFirstObjectByType<Pathfinding>();
    }

    private void EnsureLineRenderer()
    {
        if (lr == null)
        {
            lr = GetComponent<LineRenderer>();
        }

        if (lr != null && _lineRenderers.Count == 0)
        {
            _lineRenderers.Add(lr);
        }
    }

    private IEnumerator AnimateLineRendererAlphaSineRoutine(float durationSeconds, float frequency, float minAlpha, float maxAlpha)
    {
        float clampedMinAlpha = Mathf.Clamp01(minAlpha);
        float clampedMaxAlpha = Mathf.Clamp01(maxAlpha);
        if (clampedMaxAlpha < clampedMinAlpha)
        {
            float temp = clampedMinAlpha;
            clampedMinAlpha = clampedMaxAlpha;
            clampedMaxAlpha = temp;
        }

        float clampedFrequency = Mathf.Max(0f, frequency);
        float elapsed = 0f;
        _alphaAnimationStates.Clear();

        for (int i = 0; i < _lineRenderers.Count; i++)
        {
            LineRenderer renderer = _lineRenderers[i];
            if (renderer == null) continue;

            _alphaAnimationStates.Add(new LineRendererColorState
            {
                lineRenderer = renderer,
                startColor = renderer.startColor,
                endColor = renderer.endColor
            });
        }

        while (elapsed < durationSeconds)
        {
            float phase = elapsed * clampedFrequency * Mathf.PI * 2f;
            float wave = 0.5f + 0.5f * Mathf.Sin(phase);
            float alpha = Mathf.Lerp(clampedMinAlpha, clampedMaxAlpha, wave);

            for (int i = 0; i < _alphaAnimationStates.Count; i++)
            {
                LineRendererColorState state = _alphaAnimationStates[i];
                if (state.lineRenderer == null) continue;

                SetLineRendererAlpha(state.lineRenderer, state.startColor, state.endColor, alpha);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < _alphaAnimationStates.Count; i++)
        {
            LineRendererColorState state = _alphaAnimationStates[i];
            if (state.lineRenderer == null) continue;

            SetLineRendererColors(state.lineRenderer, state.startColor, state.endColor);
        }

        _alphaAnimationStates.Clear();
        _alphaAnimationRoutine = null;
    }

    private static void SetLineRendererAlpha(LineRenderer renderer, Color startColor, Color endColor, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color nextStart = startColor;
        Color nextEnd = endColor;
        float clampedAlpha = Mathf.Clamp01(alpha);
        nextStart.a = clampedAlpha;
        nextEnd.a = clampedAlpha;
        SetLineRendererColors(renderer, nextStart, nextEnd);
    }

    private static void SetLineRendererColors(LineRenderer renderer, Color startColor, Color endColor)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.startColor = startColor;
        renderer.endColor = endColor;
    }

    private Vector3 ToVizPoint(Vector3 p)
    {
        p.z += zOffset;
        return p;
    }

    private List<Vector3> GetRenderPoints(List<Vector3> rawPoints)
    {
        if (!smoothWithBezier || rawPoints == null || rawPoints.Count < 3)
        {
            return rawPoints;
        }

        BuildBezierSmoothedPath(rawPoints, _smoothedPointsBuffer);
        if (_smoothedPointsBuffer.Count < 2)
        {
            return rawPoints;
        }

        return _smoothedPointsBuffer;
    }

    private void BuildBezierSmoothedPath(List<Vector3> rawPoints, List<Vector3> output)
    {
        output.Clear();
        if (rawPoints == null || rawPoints.Count == 0)
        {
            return;
        }

        if (rawPoints.Count < 3)
        {
            output.AddRange(rawPoints);
            return;
        }

        int segmentSamples = Mathf.Max(1, bezierSamplesPerSegment);
        float strength = Mathf.Clamp01(bezierSmoothStrength);

        output.Add(rawPoints[0]);

        for (int i = 0; i < rawPoints.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? rawPoints[i - 1] : rawPoints[i];
            Vector3 p1 = rawPoints[i];
            Vector3 p2 = rawPoints[i + 1];
            Vector3 p3 = i + 2 < rawPoints.Count ? rawPoints[i + 2] : rawPoints[i + 1];

            Vector3 c1 = p1 + (p2 - p0) * (strength / 6f);
            Vector3 c2 = p2 - (p3 - p1) * (strength / 6f);

            for (int s = 1; s <= segmentSamples; s++)
            {
                float t = (float)s / segmentSamples;
                output.Add(EvaluateCubicBezier(p1, c1, c2, p2, t));
            }
        }
    }

    private static Vector3 EvaluateCubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * a) + (3f * uu * t * b) + (3f * u * tt * c) + (ttt * d);
    }
}
