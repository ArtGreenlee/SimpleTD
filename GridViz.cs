using System.Collections.Generic;
using UnityEngine;

public class GridViz : MonoBehaviour
{
    public enum AlphaCircleDistanceFalloffMode
    {
        DistanceFromCenter,
        Constant,
    }

    private static readonly Vector2Int[] CardinalOffsets =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    private struct OpenCellData
    {
        public Vector2Int index;
        public Vector3 center;
    }
    private struct WeightedColorMix
    {
        public float r;
        public float g;
        public float b;
        public float weight;

        public void Add(Color color, float contributionWeight)
        {
            if (contributionWeight <= 0f) return;
            r += color.r * contributionWeight;
            g += color.g * contributionWeight;
            b += color.b * contributionWeight;
            weight += contributionWeight;
        }

        public Color ToColor()
        {
            if (weight <= 0f) return Color.white;
            float inv = 1f / weight;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }
    }

    private struct CellColorOverride
    {
        public Color color;
        public float expiresAt;
    }

    private struct CellColorBoost
    {
        public Color color;
        public float strength;
    }

    private struct ColorMix
    {
        public float r;
        public float g;
        public float b;
        public int count;

        public void Add(Color color)
        {
            r += color.r;
            g += color.g;
            b += color.b;
            count++;
        }

        public Color ToColor()
        {
            if (count <= 0) return Color.white;
            float inv = 1f / count;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }
    }

    private struct CircleQueryKey : System.IEquatable<CircleQueryKey>
    {
        public Vector2Int centerCell;
        public int radius;

        public bool Equals(CircleQueryKey other) => centerCell == other.centerCell && radius == other.radius;
        public override bool Equals(object obj) => obj is CircleQueryKey k && Equals(k);
        public override int GetHashCode() => (centerCell.GetHashCode() * 397) ^ radius;
    }

    private struct LineQueryKey : System.IEquatable<LineQueryKey>
    {
        public Vector2Int startCell;
        public Vector2Int endCell;
        public int widthInCells;

        public bool Equals(LineQueryKey other) => startCell == other.startCell && endCell == other.endCell && widthInCells == other.widthInCells;
        public override bool Equals(object obj) => obj is LineQueryKey k && Equals(k);
        public override int GetHashCode()
        {
            int h = startCell.GetHashCode();
            h = (h * 397) ^ endCell.GetHashCode();
            h = (h * 397) ^ widthInCells;
            return h;
        }
    }

    [SerializeField] private GridManager grid;
    [SerializeField] private GameObject wallIndicatorPrefab;
    [SerializeField] private bool usePICWallIndicatorSettings = true;
    [Tooltip("Adds to half-size of wall indicator square in world units. Positive grows, negative shrinks.")]
    [SerializeField] private float wallIndicatorShapeSizeOffset = 0f;
    [SerializeField, Range(0f, 1f)]
    private float restingAlpha = 0.12f;
    [SerializeField, Min(0f)] private float cellColorLerpSpeed = 10f;
    [SerializeField, Min(0.01f)] private float desiredColorRefreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float visualColorEpsilon = 0.0025f;
    [Header("Color Spreading")]
    [SerializeField] private bool colorSpreading = false;
    [SerializeField, Range(0f, 1f)] private float colorSpreadAmount = 0.25f;
    [SerializeField, Min(1)] private int colorSpreadIterations = 1;
    [Header("Alpha Circle")]
    [SerializeField] private bool allowAOEPoolAlphaCircle = true;
    [SerializeField] private bool allowLaserPoolAlphaLine = true;
    [SerializeField] private AlphaCircleDistanceFalloffMode alphaCircleDistanceFalloffMode = AlphaCircleDistanceFalloffMode.DistanceFromCenter;
    [SerializeField, Min(0f)] private float alphaCircleDecayPerSecond = 2f;
    [Header("Lens Visualization")]
    [SerializeField] private bool showLensTowerAlphaBoost = true;
    [SerializeField, Range(0f, 1f)] private float lensTowerCellAlphaIncrease = 0.2f;
    [Header("Mine Tower Visualization")]
    [SerializeField] private bool showMineTowerCellHighlight = true;
    [SerializeField, Range(0f, 1f)] private float mineTowerCellAlphaIncrease = 0.2f;
    [SerializeField, Range(0f, 1f)] private float mineTowerCellColorStrength = 0.18f;
    [SerializeField, Range(0f, 1f)] private float mineTowerCellColorBrightenAmount = 0.15f;
    [Header("FPS Throttling")]
    [Tooltip("When smoothed FPS falls below this value, the refresh interval is increased to reduce CPU cost.")]
    [SerializeField, Min(1f)] private float fpsDropThreshold = 30f;
    [Tooltip("Refresh interval used when FPS is below the drop threshold. Should be >= desiredColorRefreshInterval.")]
    [SerializeField, Min(0.01f)] private float lowFpsRefreshInterval = 0.25f;
    [Header("Outside Grid")]
    [SerializeField] private bool renderOutsideGrid = false;
    [SerializeField, Min(1)] private int outsideGridSize = 1;

    private PIC pic;
    private readonly Dictionary<Vector2Int, LineRenderer> _wallIndicators = new Dictionary<Vector2Int, LineRenderer>(256);
    private readonly List<Vector2Int> _removeBuffer = new List<Vector2Int>(64);
    private readonly Dictionary<Vector2Int, ColorMix> _desiredColorMixes = new Dictionary<Vector2Int, ColorMix>(256);
    private readonly Dictionary<Vector2Int, CellColorOverride> _cellColorOverrides = new Dictionary<Vector2Int, CellColorOverride>(64);
    private readonly Dictionary<Vector2Int, Vector3> _openCellCenters = new Dictionary<Vector2Int, Vector3>(256);
    private readonly Dictionary<Vector2Int, float> _alphaBoostByCell = new Dictionary<Vector2Int, float>(256);
    private readonly List<KeyValuePair<Vector2Int, float>> _alphaBoostSnapshot = new List<KeyValuePair<Vector2Int, float>>(256);
    private readonly Dictionary<Vector2Int, CellColorBoost> _colorBoostByCell = new Dictionary<Vector2Int, CellColorBoost>(256);
    private readonly List<KeyValuePair<Vector2Int, CellColorBoost>> _colorBoostSnapshot = new List<KeyValuePair<Vector2Int, CellColorBoost>>(256);
    private readonly Dictionary<Vector2Int, float> _lensAlphaBoostByCell = new Dictionary<Vector2Int, float>(256);
    private readonly Dictionary<Vector2Int, float> _mineAlphaBoostByCell = new Dictionary<Vector2Int, float>(256);
    private readonly Dictionary<Vector2Int, CellColorBoost> _mineColorBoostByCell = new Dictionary<Vector2Int, CellColorBoost>(256);
    private readonly List<Vector2Int> _minePreviewCellsScratch = new List<Vector2Int>(64);
    private readonly Dictionary<Enemy, SRC> _enemySrcCache = new Dictionary<Enemy, SRC>(64);
    private readonly List<OpenCellData> _openCells = new List<OpenCellData>(256);
    private readonly Dictionary<Vector2Int, WeightedColorMix> _spreadBuffer = new Dictionary<Vector2Int, WeightedColorMix>(256);
    private readonly List<Vector2Int> _neighborScratch = new List<Vector2Int>(4);
    private readonly Dictionary<CircleQueryKey, List<Vector2Int>> _circleQueryCache = new Dictionary<CircleQueryKey, List<Vector2Int>>(32);
    private readonly Dictionary<LineQueryKey, List<Vector2Int>> _lineQueryCache = new Dictionary<LineQueryKey, List<Vector2Int>>(32);

    private int _lastWalkabilityVersion = -1;
    private int _lastCellsX = -1;
    private int _lastCellsY = -1;
    private float _nextDesiredColorRefreshTime;
    private bool _desiredColorsDirty = true;
    private float _smoothedFps = 60f;
    private float _effectiveRefreshInterval;

    [SerializeField, Min(0f)] public float cursorCellAlphaIncrease;
    private readonly Dictionary<Vector2Int, Color> desiredColor = new Dictionary<Vector2Int, Color>(256);

    public bool AllowAOEPoolAlphaCircle => allowAOEPoolAlphaCircle;
    public bool AllowLaserPoolAlphaLine => allowLaserPoolAlphaLine;

    void Awake()
    {
        pic = GetComponent<PIC>();
        if (grid == null) grid = GridManager.instance;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        SyncSettingsFromPIC();
        _effectiveRefreshInterval = desiredColorRefreshInterval;
    }

    private void OnEnable()
    {
        TryResolveGrid();
        SyncSettingsFromPIC();
        SyncIndicators(forceFullRebuild: true);
    }

    private void OnDisable()
    {
        ClearAllIndicators();
    }

    private void Update()
    {
        if (!TryResolveGrid()) return;

        bool gridSizeChanged = _lastCellsX != grid.CellsX || _lastCellsY != grid.CellsY;
        bool walkabilityChanged = _lastWalkabilityVersion != grid.WalkabilityVersion;
        if (gridSizeChanged || walkabilityChanged)
        {
            SyncIndicators(forceFullRebuild: gridSizeChanged);
        }

        _smoothedFps = Mathf.Lerp(_smoothedFps, 1f / Mathf.Max(0.0001f, Time.deltaTime), 5f * Time.deltaTime);
        _effectiveRefreshInterval = _smoothedFps < fpsDropThreshold
            ? Mathf.Max(desiredColorRefreshInterval, lowFpsRefreshInterval)
            : desiredColorRefreshInterval;

        if (_desiredColorsDirty || Time.time >= _nextDesiredColorRefreshTime)
        {
            UpdateDesiredColors();
            _desiredColorsDirty = false;
            _nextDesiredColorRefreshTime = Time.time + Mathf.Max(0.01f, _effectiveRefreshInterval);
        }

        UpdateCellVisuals();
    }

    private bool TryResolveGrid()
    {
        if (grid != null) return true;
        grid = GridManager.instance;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        return grid != null;
    }

    private void SyncSettingsFromPIC()
    {
        if (!usePICWallIndicatorSettings) return;

        if (pic == null) pic = GetComponent<PIC>();
        if (pic == null) return;

        if (pic.WallIndicatorLineRendererPrefab != null)
        {
            wallIndicatorPrefab = pic.WallIndicatorLineRendererPrefab;
        }

        wallIndicatorShapeSizeOffset = pic.WallIndicatorShapeSizeOffset;
    }

    private void SyncIndicators(bool forceFullRebuild)
    {
        if (grid == null || wallIndicatorPrefab == null)
        {
            _lastWalkabilityVersion = grid != null ? grid.WalkabilityVersion : -1;
            _lastCellsX = grid != null ? grid.CellsX : -1;
            _lastCellsY = grid != null ? grid.CellsY : -1;
            return;
        }

        if (forceFullRebuild)
        {
            ClearAllIndicators();
        }
        else
        {
            RemoveIndicatorsOnNewWalls();
        }

        EnsureIndicatorsForAllOpenMazeCells();
        RebuildOpenCellCache();
        _desiredColorsDirty = true;

        _lastWalkabilityVersion = grid.WalkabilityVersion;
        _lastCellsX = grid.CellsX;
        _lastCellsY = grid.CellsY;
    }

    public void OverrideCellColor(Vector2Int cellIdx, Color color, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            ClearCellColorOverride(cellIdx);
            return;
        }

        _cellColorOverrides[cellIdx] = new CellColorOverride
        {
            color = color,
            expiresAt = Time.time + durationSeconds,
        };
        _desiredColorsDirty = true;
    }

    public void ClearCellColorOverride(Vector2Int cellIdx)
    {
        _cellColorOverrides.Remove(cellIdx);
    }

    private void UpdateDesiredColors()
    {
        desiredColor.Clear();
        _desiredColorMixes.Clear();

        if (grid == null)
        {
            return;
        }

        foreach (var pair in _wallIndicators)
        {
            desiredColor[pair.Key] = Color.white;
        }

        TowerManager towerManager = TowerManager.instance;
        if (towerManager != null)
        {
            foreach (Tower tower in towerManager.EnumeratePlacedTowers())
            {
                if (tower == null || !tower.isActiveAndEnabled) continue;
                if (tower.CurrentState != Tower.State.Placed) continue;

                RangeManager rangeManager = tower.GetRangeManager();
                if (rangeManager == null || !rangeManager.isActiveAndEnabled || !rangeManager.IsShowRangeEnabled()) continue;

                Color towerColor = rangeManager.GetRangeVisualizerBaseColor();

                if (!grid.TryWorldToCell(rangeManager.transform.position, out var towerCellIdx))
                {
                    continue;
                }

                float rangeRadius = rangeManager.GetRangeRadius();
                float spacing = Mathf.Max(0.0001f, grid.GetSpacing());
                int cellRadius = Mathf.Max(1, Mathf.CeilToInt(rangeRadius / spacing) + 1);

                int minX = Mathf.Max(0, towerCellIdx.x - cellRadius);
                int maxX = Mathf.Min(grid.CellsX - 1, towerCellIdx.x + cellRadius);
                int minY = Mathf.Max(0, towerCellIdx.y - cellRadius);
                int maxY = Mathf.Min(grid.CellsY - 1, towerCellIdx.y + cellRadius);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2Int cellIdx = new Vector2Int(x, y);
                        if (!_openCellCenters.TryGetValue(cellIdx, out var cellCenter)) continue;
                        if (!rangeManager.PointInsideRange(cellCenter)) continue;

                        if (!_desiredColorMixes.TryGetValue(cellIdx, out var mix))
                        {
                            mix = default;
                        }

                        mix.Add(towerColor);
                        _desiredColorMixes[cellIdx] = mix;
                    }
                }
            }
        }

        for (int i = 0; i < _openCells.Count; i++)
        {
            Vector2Int cellIdx = _openCells[i].index;
            Color finalColor = Color.white;
            if (_desiredColorMixes.TryGetValue(cellIdx, out var mix) && mix.count > 0)
            {
                finalColor = mix.ToColor();
            }

            desiredColor[cellIdx] = finalColor;
        }

        ApplyEnemyCellHighlights();
        RebuildMineTowerCellHighlights();
        if (colorSpreading)
        {
            ApplyColorSpreading();
        }
    }

    private void RebuildMineTowerCellHighlights()
    {
        _mineAlphaBoostByCell.Clear();
        _mineColorBoostByCell.Clear();

        if (!showMineTowerCellHighlight) return;
        if (mineTowerCellAlphaIncrease <= 0f && mineTowerCellColorStrength <= 0f) return;
        if (grid == null) return;
        if (pic == null) pic = GetComponent<PIC>();
        if (pic == null) return;
        if (pic.currentState != PIC.PICState.PlacingTower || !pic.isHoldingTower()) return;

        TowerManager towerManager = TowerManager.instance;
        if (towerManager == null) return;

        float alphaIncrease = Mathf.Max(0f, mineTowerCellAlphaIncrease);
        float colorStrength = Mathf.Max(0f, mineTowerCellColorStrength);
        float brightenAmount = Mathf.Clamp01(mineTowerCellColorBrightenAmount);

        ProjectileTower heldMineTower = FindHeldMineTower(towerManager);
        if (heldMineTower == null) return;

    Vector3 towerWorldPos = heldMineTower.transform.position;
    if (!grid.TryWorldToCell(towerWorldPos, out var hoveredCellIdx)) return;
    if (!pic.IsValidTowerPlacementCell(hoveredCellIdx)) return;

        _minePreviewCellsScratch.Clear();
        Vector3 validHoverCenter = grid.GetCellWorldCenter(hoveredCellIdx.x, hoveredCellIdx.y);
        if (!heldMineTower.TryGetMineSpawnPreviewCells(validHoverCenter, _minePreviewCellsScratch)) return;

        Color shiftedColor = Color.Lerp(heldMineTower.GetMinePreviewColor(), Color.white, brightenAmount);
        shiftedColor.a = 1f;

        for (int i = 0; i < _minePreviewCellsScratch.Count; i++)
        {
            Vector2Int cellIdx = _minePreviewCellsScratch[i];
            if (!_wallIndicators.ContainsKey(cellIdx)) continue;

            if (alphaIncrease > 0f)
            {
                if (!_mineAlphaBoostByCell.TryGetValue(cellIdx, out var currentAlpha)) currentAlpha = 0f;
                _mineAlphaBoostByCell[cellIdx] = Mathf.Clamp01(currentAlpha + alphaIncrease);
            }

            if (colorStrength > 0f)
            {
                AddMineColorBoost(cellIdx, shiftedColor, colorStrength);
            }
        }
    }

    private ProjectileTower FindHeldMineTower(TowerManager towerManager)
    {
        if (towerManager == null || pic == null) return null;

        ProjectileTower held = FindHeldMineTowerInState(towerManager, Tower.State.Shop);
        if (held != null) return held;

        held = FindHeldMineTowerInState(towerManager, Tower.State.Inventory);
        if (held != null) return held;

        return FindHeldMineTowerInState(towerManager, Tower.State.Placed);
    }

    private ProjectileTower FindHeldMineTowerInState(TowerManager towerManager, Tower.State state)
    {
        foreach (Tower tower in towerManager.EnumerateTowersInState(state))
        {
            if (tower is not ProjectileTower projectileTower) continue;
            if (!projectileTower.isActiveAndEnabled) continue;
            if (!projectileTower.IsMineModeActive()) continue;
            if (!pic.IsHoldingTower(projectileTower)) continue;
            return projectileTower;
        }

        return null;
    }

    private void AddMineColorBoost(Vector2Int cellIdx, Color color, float increase)
    {
        if (increase <= 0f) return;

        color.a = 1f;

        if (!_mineColorBoostByCell.TryGetValue(cellIdx, out var existing))
        {
            _mineColorBoostByCell[cellIdx] = new CellColorBoost
            {
                color = color,
                strength = Mathf.Clamp01(increase),
            };
            return;
        }

        float totalStrength = existing.strength + increase;
        float blendT = totalStrength > 0.0001f ? increase / totalStrength : 1f;

        existing.color = Color.Lerp(existing.color, color, Mathf.Clamp01(blendT));
        existing.color.a = 1f;
        existing.strength = Mathf.Clamp01(totalStrength);
        _mineColorBoostByCell[cellIdx] = existing;
    }

    private void ApplyEnemyCellHighlights()
    {
        EnemyManager enemyManager = EnemyManager.instance;
        if (enemyManager == null || enemyManager.enemiesByCell == null) return;

        foreach (var pair in enemyManager.enemiesByCell)
        {
            Vector2Int cellIdx = pair.Key;
            if (!_wallIndicators.ContainsKey(cellIdx)) continue;

            if (TryGetFirstEnemyCellColor(pair.Value, out var enemyColor))
            {
                desiredColor[cellIdx] = enemyColor;
            }
        }
    }

    private bool TryGetFirstEnemyCellColor(HashSet<Enemy> enemies, out Color color)
    {
        color = Color.white;
        if (enemies == null || enemies.Count == 0) return false;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            SRC src = null;
            _enemySrcCache.TryGetValue(enemy, out src);

            if (src == null)
            {
                src = enemy.GetComponent<SRC>();
                if (src != null)
                {
                    _enemySrcCache[enemy] = src;
                }
            }

            if (src == null) continue;

            color = src.GetPrimaryColor();
            color.a = 1f;
            return true;
        }

        return false;
    }

    private void ApplyColorSpreading()
    {
        if (_openCells.Count == 0 || desiredColor.Count == 0) return;

        float spreadAmount = Mathf.Clamp01(colorSpreadAmount);
        int iterations = Mathf.Max(1, colorSpreadIterations);

        Dictionary<Vector2Int, Color> current = desiredColor;
        Dictionary<Vector2Int, Color> next = new Dictionary<Vector2Int, Color>(desiredColor.Count);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            _spreadBuffer.Clear();

            foreach (var cellData in _openCells)
            {
                Vector2Int cellIdx = cellData.index;
                if (!current.TryGetValue(cellIdx, out var sourceColor)) continue;
                if (IsNearWhite(sourceColor)) continue;

                List<Vector2Int> neighbors = GetOpenCardinalNeighbors(cellIdx);
                if (neighbors.Count == 0 || spreadAmount <= 0f)
                {
                    AddSpreadContribution(cellIdx, sourceColor, 1f);
                    continue;
                }

                float keepWeight = 1f - spreadAmount;
                float shareWeight = spreadAmount / neighbors.Count;

                AddSpreadContribution(cellIdx, sourceColor, keepWeight);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    AddSpreadContribution(neighbors[i], sourceColor, shareWeight);
                }
            }

            next.Clear();
            foreach (var pair in _spreadBuffer)
            {
                next[pair.Key] = pair.Value.ToColor();
            }

            current = next;
            next = new Dictionary<Vector2Int, Color>(current.Count + 8);
        }

        desiredColor.Clear();
        foreach (var pair in current)
        {
            desiredColor[pair.Key] = pair.Value;
        }
    }

    private void AddSpreadContribution(Vector2Int cellIdx, Color sourceColor, float weight)
    {
        if (weight <= 0f) return;

        if (!_spreadBuffer.TryGetValue(cellIdx, out var mix))
        {
            mix = default;
        }

        mix.Add(sourceColor, weight);
        _spreadBuffer[cellIdx] = mix;
    }

    private List<Vector2Int> GetOpenCardinalNeighbors(Vector2Int cellIdx)
    {
        _neighborScratch.Clear();

        for (int i = 0; i < CardinalOffsets.Length; i++)
        {
            Vector2Int idx = cellIdx + CardinalOffsets[i];
            if (_openCellCenters.ContainsKey(idx))
            {
                _neighborScratch.Add(idx);
            }
        }

        return _neighborScratch;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r >= 0.999f && color.g >= 0.999f && color.b >= 0.999f;
    }

    private void UpdateCellVisuals()
    {
        if (_wallIndicators.Count == 0) return;

        ExpireCellColorOverrides();
        DecayVisualBoosts();
        RebuildLensTowerAlphaBoosts();

        Vector2Int hoveredCellIdx;
        bool hasHoveredCell = TryGetHoveredCell(out hoveredCellIdx);

        float lerpT = Mathf.Clamp01(Time.deltaTime * Mathf.Max(0f, cellColorLerpSpeed));

        foreach (var pair in _wallIndicators)
        {
            Vector2Int cellIdx = pair.Key;
            LineRenderer lr = pair.Value;
            if (lr == null) continue;

            Color targetColor = GetCellTargetColor(cellIdx);
            if (_mineColorBoostByCell.TryGetValue(cellIdx, out var mineColorBoost))
            {
                targetColor = Color.Lerp(targetColor, mineColorBoost.color, Mathf.Clamp01(mineColorBoost.strength));
            }
            if (_colorBoostByCell.TryGetValue(cellIdx, out var colorBoost))
            {
                targetColor = Color.Lerp(targetColor, colorBoost.color, Mathf.Clamp01(colorBoost.strength));
            }

            float targetAlpha = restingAlpha;
            if (hasHoveredCell && hoveredCellIdx == cellIdx)
            {
                targetAlpha = Mathf.Clamp01(restingAlpha + cursorCellAlphaIncrease);
            }

            if (_alphaBoostByCell.TryGetValue(cellIdx, out var alphaBoost))
            {
                targetAlpha = Mathf.Clamp01(targetAlpha + alphaBoost);
            }

            if (_mineAlphaBoostByCell.TryGetValue(cellIdx, out var mineAlphaBoost))
            {
                targetAlpha = Mathf.Clamp01(targetAlpha + mineAlphaBoost);
            }

            if (_lensAlphaBoostByCell.TryGetValue(cellIdx, out var lensAlphaBoost))
            {
                targetAlpha = Mathf.Clamp01(targetAlpha + lensAlphaBoost);
            }

            targetColor.a = targetAlpha;

            Color current = lr.startColor;
            if (ApproximatelyColor(current, targetColor, visualColorEpsilon))
            {
                continue;
            }

            Color next = Color.Lerp(current, targetColor, lerpT);
            lr.startColor = next;
            lr.endColor = next;
        }
    }

    private void RebuildLensTowerAlphaBoosts()
    {
        _lensAlphaBoostByCell.Clear();

        if (!showLensTowerAlphaBoost) return;
        if (lensTowerCellAlphaIncrease <= 0f) return;
        if (_openCells.Count == 0) return;

        TowerManager towerManager = TowerManager.instance;
        if (towerManager == null) return;

        foreach (Tower tower in towerManager.EnumeratePlacedTowers())
        {
            LensTower lensTower = tower as LensTower;
            if (lensTower == null || !lensTower.isActiveAndEnabled) continue;

            Transform lensTransform = lensTower.lensTransform;
            if (lensTransform == null || !lensTransform.gameObject.activeInHierarchy) continue;

            Vector3 center = lensTransform.position;
            float radius = GetLensWorldRadius(lensTower);
            if (radius <= 0.0001f) continue;

            int radiusInt = Mathf.CeilToInt(radius);
            if (radiusInt < 0) radiusInt = 0;

            Vector2Int centerCell = default;
            if (grid != null) grid.TryWorldToCell(center, out centerCell);

            var key = new CircleQueryKey { centerCell = centerCell, radius = radiusInt };
            if (!_circleQueryCache.TryGetValue(key, out var cells))
            {
                cells = BuildCircleCells(radiusInt, center);
                _circleQueryCache[key] = cells;
            }

            float radiusSqr = radius * radius;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int idx = cells[i];
                if (!_openCellCenters.TryGetValue(idx, out var cellCenter)) continue;

                Vector2 delta = new Vector2(cellCenter.x - center.x, cellCenter.y - center.y);
                if (delta.sqrMagnitude > radiusSqr) continue;

                if (!_lensAlphaBoostByCell.TryGetValue(idx, out var current)) current = 0f;
                _lensAlphaBoostByCell[idx] = Mathf.Clamp01(current + lensTowerCellAlphaIncrease);
            }
        }
    }

    private static float GetLensWorldRadius(LensTower lensTower)
    {
        if (lensTower == null) return 0f;

        Transform lensTransform = lensTower.lensTransform;
        if (lensTransform != null)
        {
            Vector3 lossyScale = lensTransform.lossyScale;
            float diameter = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
            if (diameter > 0.0001f)
            {
                return diameter * 0.5f;
            }
        }

        return Mathf.Max(0f, lensTower.lensRadius * 0.5f);
    }

    public void AlphaCell(float increase, Vector2Int cellIdx)
    {
        if (increase <= 0f) return;
        if (!_openCellCenters.ContainsKey(cellIdx)) return;

        if (!_alphaBoostByCell.TryGetValue(cellIdx, out var current)) current = 0f;
        _alphaBoostByCell[cellIdx] = Mathf.Clamp01(current + increase);
    }

    public void AlphaCell(float increase, Vector3 worldPosition)
    {
        if (increase <= 0f) return;
        if (grid == null) return;
        if (!grid.TryWorldToCell(worldPosition, out var cellIdx)) return;

        AlphaCell(increase, cellIdx);
    }

    public void ColorCell(Color color, float increase, Vector2Int cellIdx)
    {
        if (increase <= 0f) return;
        if (!_openCellCenters.ContainsKey(cellIdx)) return;

        color.a = 1f;
        AddColorBoost(cellIdx, color, increase);
    }

    public void HighlightMinePreviewCells(
        IList<Vector2Int> cellIndices,
        Color towerColor,
        float alphaIncrease,
        float colorStrength,
        float colorBrightenAmount)
    {
        if (cellIndices == null || cellIndices.Count == 0) return;

        float resolvedAlphaIncrease = Mathf.Max(0f, alphaIncrease);
        float resolvedColorStrength = Mathf.Max(0f, colorStrength);
        float resolvedBrightenAmount = Mathf.Clamp01(colorBrightenAmount);

        Color shiftedColor = Color.Lerp(towerColor, Color.white, resolvedBrightenAmount);
        shiftedColor.a = 1f;

        for (int i = 0; i < cellIndices.Count; i++)
        {
            Vector2Int cellIdx = cellIndices[i];
            if (!_openCellCenters.ContainsKey(cellIdx)) continue;

            if (resolvedAlphaIncrease > 0f)
            {
                if (!_alphaBoostByCell.TryGetValue(cellIdx, out var currentAlphaBoost)) currentAlphaBoost = 0f;
                _alphaBoostByCell[cellIdx] = Mathf.Clamp01(currentAlphaBoost + resolvedAlphaIncrease);
            }

            if (resolvedColorStrength > 0f)
            {
                AddColorBoost(cellIdx, shiftedColor, resolvedColorStrength);
            }
        }
    }

    public void ColorCell(Color color, float increase, Vector3 worldPosition)
    {
        if (increase <= 0f) return;
        if (grid == null) return;
        if (!grid.TryWorldToCell(worldPosition, out var cellIdx)) return;

        ColorCell(color, increase, cellIdx);
    }

    public void AlphaCircle(float increase, float radius, Vector3 position)
    {
        bool scaleByDistanceFromCenter = alphaCircleDistanceFalloffMode == AlphaCircleDistanceFalloffMode.DistanceFromCenter;
        AlphaCircle(increase, radius, position, scaleByDistanceFromCenter);
    }

    public void AlphaCircle(float increase, float radius, Vector3 position, bool scaleByDistanceFromCenter)
    {
        if (increase <= 0f || radius <= 0f) return;
        if (_openCells.Count == 0) return;

        int radiusInt = Mathf.RoundToInt(radius);
        if (radiusInt < 0) radiusInt = 0;

        Vector2Int centerCell = default;
        if (grid != null) grid.TryWorldToCell(position, out centerCell);

        var key = new CircleQueryKey { centerCell = centerCell, radius = radiusInt };
        if (!_circleQueryCache.TryGetValue(key, out var cells))
        {
            cells = BuildCircleCells(radiusInt, position);
            _circleQueryCache[key] = cells;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int idx = cells[i];
            float appliedIncrease = increase;
            if (scaleByDistanceFromCenter && _openCellCenters.TryGetValue(idx, out var cellCenter))
            {
                float distance = Vector2.Distance(new Vector2(cellCenter.x, cellCenter.y), new Vector2(position.x, position.y));
                float t = Mathf.Clamp01(1f - (distance / Mathf.Max(0.0001f, radius)));
                appliedIncrease *= t;
            }

            if (appliedIncrease <= 0f) continue;

            if (!_alphaBoostByCell.TryGetValue(idx, out var current)) current = 0f;
            _alphaBoostByCell[idx] = Mathf.Clamp01(current + appliedIncrease);
        }
    }

    private List<Vector2Int> BuildCircleCells(int radius, Vector3 position)
    {
        float radiusSqr = (float)radius * radius;
        Vector2 center = new Vector2(position.x, position.y);
        var result = new List<Vector2Int>();

        for (int i = 0; i < _openCells.Count; i++)
        {
            Vector2Int idx = _openCells[i].index;
            Vector3 cellCenter = _openCells[i].center;
            Vector2 toCell = new Vector2(cellCenter.x, cellCenter.y) - center;
            if (toCell.sqrMagnitude <= radiusSqr)
            {
                result.Add(idx);
            }
        }

        return result;
    }

    public void AlphaLine(float increase, Vector3 start, Vector3 end, int widthInCells)
    {
        if (increase <= 0f) return;
        if (widthInCells < 1) return;
        if (_openCells.Count == 0) return;

        Vector2Int startCell = default;
        Vector2Int endCell = default;
        if (grid != null)
        {
            grid.TryWorldToCell(start, out startCell);
            grid.TryWorldToCell(end, out endCell);
        }

        var key = new LineQueryKey { startCell = startCell, endCell = endCell, widthInCells = widthInCells };
        if (!_lineQueryCache.TryGetValue(key, out var cells))
        {
            cells = BuildLineCells(startCell, endCell, widthInCells);
            _lineQueryCache[key] = cells;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int idx = cells[i];
            if (!_alphaBoostByCell.TryGetValue(idx, out var current)) current = 0f;
            _alphaBoostByCell[idx] = Mathf.Clamp01(current + increase);
        }
    }

    public void ColorCircle(Color color, float increase, float radius, Vector3 position)
    {
        if (increase <= 0f || radius <= 0f) return;
        if (_openCells.Count == 0) return;

        color.a = 1f;

        int radiusInt = Mathf.RoundToInt(radius);
        if (radiusInt < 0) radiusInt = 0;

        Vector2Int centerCell = default;
        if (grid != null) grid.TryWorldToCell(position, out centerCell);

        var key = new CircleQueryKey { centerCell = centerCell, radius = radiusInt };
        if (!_circleQueryCache.TryGetValue(key, out var cells))
        {
            cells = BuildCircleCells(radiusInt, position);
            _circleQueryCache[key] = cells;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            AddColorBoost(cells[i], color, increase);
        }
    }

    public void ColorLine(Color color, float increase, Vector3 start, Vector3 end, int widthInCells)
    {
        if (increase <= 0f) return;
        if (widthInCells < 1) return;
        if (_openCells.Count == 0) return;

        color.a = 1f;

        Vector2Int startCell = default;
        Vector2Int endCell = default;
        if (grid != null)
        {
            grid.TryWorldToCell(start, out startCell);
            grid.TryWorldToCell(end, out endCell);
        }

        var key = new LineQueryKey { startCell = startCell, endCell = endCell, widthInCells = widthInCells };
        if (!_lineQueryCache.TryGetValue(key, out var cells))
        {
            cells = BuildLineCells(startCell, endCell, widthInCells);
            _lineQueryCache[key] = cells;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            AddColorBoost(cells[i], color, increase);
        }
    }

    private void AddColorBoost(Vector2Int cellIdx, Color color, float increase)
    {
        if (increase <= 0f) return;

        color.a = 1f;

        if (!_colorBoostByCell.TryGetValue(cellIdx, out var existing))
        {
            _colorBoostByCell[cellIdx] = new CellColorBoost
            {
                color = color,
                strength = Mathf.Clamp01(increase),
            };
            return;
        }

        float totalStrength = existing.strength + increase;
        float blendT = totalStrength > 0.0001f ? increase / totalStrength : 1f;

        existing.color = Color.Lerp(existing.color, color, Mathf.Clamp01(blendT));
        existing.color.a = 1f;
        existing.strength = Mathf.Clamp01(totalStrength);
        _colorBoostByCell[cellIdx] = existing;
    }

    private List<Vector2Int> BuildLineCells(Vector2Int startCell, Vector2Int endCell, int widthInCells)
    {
        float spacing = grid != null ? Mathf.Max(0.0001f, grid.GetSpacing()) : 1f;
        float halfWidthWorld = Mathf.Max(0.0001f, widthInCells * spacing * 0.5f);
        float halfWidthWorldSqr = halfWidthWorld * halfWidthWorld;

        // Use snapped cell centers as line endpoints for deterministic cache results.
        Vector3 startWorld = grid != null ? grid.GetCellWorldCenter(startCell.x, startCell.y) : Vector3.zero;
        Vector3 endWorld   = grid != null ? grid.GetCellWorldCenter(endCell.x,   endCell.y)   : Vector3.zero;

        Vector2 a = new Vector2(startWorld.x, startWorld.y);
        Vector2 b = new Vector2(endWorld.x,   endWorld.y);

        var result = new List<Vector2Int>();
        for (int i = 0; i < _openCells.Count; i++)
        {
            Vector2Int idx = _openCells[i].index;
            Vector3 cellCenter = _openCells[i].center;
            Vector2 p = new Vector2(cellCenter.x, cellCenter.y);

            if (DistancePointToSegmentSqr(p, a, b) <= halfWidthWorldSqr)
            {
                result.Add(idx);
            }
        }

        return result;
    }

    private void ClearQueryCaches()
    {
        _circleQueryCache.Clear();
        _lineQueryCache.Clear();
    }

    private static float DistancePointToSegmentSqr(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abLenSqr = ab.sqrMagnitude;
        if (abLenSqr <= 0.000001f)
        {
            return (p - a).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSqr);
        Vector2 closest = a + (ab * t);
        return (p - closest).sqrMagnitude;
    }

    private void DecayVisualBoosts()
    {
        float normalInterval = Mathf.Max(0.0001f, desiredColorRefreshInterval);
        float effectiveInterval = Mathf.Max(normalInterval, _effectiveRefreshInterval);
        float intervalScale = normalInterval / effectiveInterval;
        float decay = Mathf.Max(0f, alphaCircleDecayPerSecond) * Time.deltaTime * intervalScale;
        if (decay <= 0f) return;

        if (_alphaBoostByCell.Count > 0)
        {
            _alphaBoostSnapshot.Clear();
            foreach (var pair in _alphaBoostByCell)
            {
                _alphaBoostSnapshot.Add(pair);
            }

            _removeBuffer.Clear();
            for (int i = 0; i < _alphaBoostSnapshot.Count; i++)
            {
                var pair = _alphaBoostSnapshot[i];
                float next = pair.Value - decay;
                if (next <= 0.0001f)
                {
                    _removeBuffer.Add(pair.Key);
                }
                else
                {
                    _alphaBoostByCell[pair.Key] = next;
                }
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                _alphaBoostByCell.Remove(_removeBuffer[i]);
            }

            _removeBuffer.Clear();
            _alphaBoostSnapshot.Clear();
        }

        if (_colorBoostByCell.Count > 0)
        {
            _colorBoostSnapshot.Clear();
            foreach (var pair in _colorBoostByCell)
            {
                _colorBoostSnapshot.Add(pair);
            }

            _removeBuffer.Clear();
            for (int i = 0; i < _colorBoostSnapshot.Count; i++)
            {
                var pair = _colorBoostSnapshot[i];
                float next = pair.Value.strength - decay;
                if (next <= 0.0001f)
                {
                    _removeBuffer.Add(pair.Key);
                }
                else
                {
                    var updated = pair.Value;
                    updated.strength = next;
                    _colorBoostByCell[pair.Key] = updated;
                }
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                _colorBoostByCell.Remove(_removeBuffer[i]);
            }

            _removeBuffer.Clear();
            _colorBoostSnapshot.Clear();
        }
    }

    private void ExpireCellColorOverrides()
    {
        if (_cellColorOverrides.Count == 0) return;

        _removeBuffer.Clear();
        foreach (var pair in _cellColorOverrides)
        {
            if (Time.time > pair.Value.expiresAt)
            {
                _removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            _cellColorOverrides.Remove(_removeBuffer[i]);
        }

        _removeBuffer.Clear();
        _desiredColorsDirty = true;
    }

    private Color GetCellTargetColor(Vector2Int cellIdx)
    {
        if (_cellColorOverrides.TryGetValue(cellIdx, out var cellOverride))
        {
            return cellOverride.color;
        }

        if (desiredColor.TryGetValue(cellIdx, out var color))
        {
            return color;
        }

        return Color.white;
    }

    private bool TryGetHoveredCell(out Vector2Int cellIdx)
    {
        cellIdx = default;
        if (grid == null) return false;

        Vector3 mouseWorld = pic != null ? pic.GetMousePosition() : Input.mousePosition;
        if (pic == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
        }

        if (!grid.TryWorldToCell(mouseWorld, out cellIdx)) return false;
        return grid.IsInMazeBounds(cellIdx) && !grid.IsWallAtCell(cellIdx) && _wallIndicators.ContainsKey(cellIdx);
    }

    private void EnsureIndicatorsForAllOpenMazeCells()
    {
        if (grid == null) return;

        // Determine the iteration bounds based on whether we're rendering outside grid
        int minX = grid.CellsX;
        int maxX = -1;
        int minY = grid.CellsY;
        int maxY = -1;
        
        if (renderOutsideGrid)
        {
            minX = -grid.CellsX;
            maxX = grid.CellsX * 2 - 1;
            minY = -grid.CellsY;
            maxY = grid.CellsY * 2 - 1;
        }
        else
        {
            minX = 0;
            maxX = grid.CellsX - 1;
            minY = 0;
            maxY = grid.CellsY - 1;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int idx = new Vector2Int(x, y);
                if (!ShouldHaveIndicatorAtCell(idx)) continue;
                GetOrCreateWallIndicator(idx);
            }
        }
    }

    private void RebuildOpenCellCache()
    {
        _openCellCenters.Clear();
        _openCells.Clear();
        ClearQueryCaches();

        foreach (var pair in _wallIndicators)
        {
            Vector2Int idx = pair.Key;
            Vector3 center = grid.GetCellWorldCenter(idx.x, idx.y);
            _openCellCenters[idx] = center;
            _openCells.Add(new OpenCellData
            {
                index = idx,
                center = center,
            });
        }
    }

    private bool ShouldHaveIndicatorAtCell(Vector2Int idx)
    {
        if (grid == null) return false;
        if (grid.IsWallAtCell(idx)) return false;
        
        // Inside maze bounds always gets an indicator
        if (grid.IsInMazeBounds(idx)) return true;
        
        // Outside maze bounds only if renderOutsideGrid is enabled
        if (renderOutsideGrid) return true;
        
        return false;
    }

    private void RemoveIndicatorsOnNewWalls()
    {
        _removeBuffer.Clear();

        foreach (var pair in _wallIndicators)
        {
            Vector2Int idx = pair.Key;
            if (!ShouldHaveIndicatorAtCell(idx))
            {
                _removeBuffer.Add(idx);
            }
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            RemoveIndicatorAtCell(_removeBuffer[i]);
        }
    }

    private LineRenderer GetOrCreateWallIndicator(Vector2Int cellIdx)
    {
        if (_wallIndicators.TryGetValue(cellIdx, out var existing) && existing != null)
        {
            return existing;
        }

        if (wallIndicatorPrefab == null || grid == null) return null;

        GameObject go = Instantiate(wallIndicatorPrefab, transform);
        bool isOutsideGrid = !grid.IsInMazeBounds(cellIdx);
        go.name = isOutsideGrid ? $"OutsideGridIndicator_{cellIdx.x}_{cellIdx.y}" : $"WallIndicator_{cellIdx.x}_{cellIdx.y}";

        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.GetComponentInChildren<LineRenderer>();
        if (lr == null)
        {
            Destroy(go);
            return null;
        }

        ConfigureIndicatorShape(lr, cellIdx);
        SetIndicatorAlpha(lr, restingAlpha);

        _wallIndicators[cellIdx] = lr;
        return lr;
    }

    private void ConfigureWallIndicatorShapeInternal(LineRenderer lr, GridManager grid, Vector2Int cellIdx, float shapeSizeOffset)
    {
        if (lr == null || grid == null) return;

        Vector3 center = grid.GetCellWorldCenter(cellIdx.x, cellIdx.y);
        
        // Determine if this cell is outside grid and apply scaling
        bool isOutsideGrid = !grid.IsInMazeBounds(cellIdx);
        float sizeMultiplier = isOutsideGrid ? outsideGridSize : 1f;
        
        float half = Mathf.Max(0.0001f, ((grid.GetSpacing() * 0.5f) + shapeSizeOffset) * sizeMultiplier);

        Vector3 p0 = new Vector3(center.x - half, center.y - half, center.z);
        Vector3 p1 = new Vector3(center.x + half, center.y - half, center.z);
        Vector3 p2 = new Vector3(center.x + half, center.y + half, center.z);
        Vector3 p3 = new Vector3(center.x - half, center.y + half, center.z);

        // Draw as an open path and inset the start/end by half width so cap extension lands on the corner cleanly.
        float halfLineWidth = Mathf.Max(0f, Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f);
        float inset = Mathf.Min(halfLineWidth, half * 0.95f);

        Vector3 pStart = new Vector3(p0.x + inset, p0.y, p0.z);
        Vector3 pEnd = new Vector3(p0.x, p0.y + inset, p0.z);

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = 5;
        lr.SetPosition(0, pStart - Vector3.right * inset * 2);
        lr.SetPosition(1, p1);
        lr.SetPosition(2, p2);
        lr.SetPosition(3, p3);
        lr.SetPosition(4, pEnd);
    }

    private void ConfigureIndicatorShape(LineRenderer lr, Vector2Int cellIdx)
    {
        ConfigureWallIndicatorShapeInternal(lr, grid, cellIdx, wallIndicatorShapeSizeOffset);
    }
    
    public static void ConfigureWallIndicatorShape(LineRenderer lr, GridManager grid, Vector2Int cellIdx, float shapeSizeOffset)
    {
        // Static version for backwards compatibility - uses default scaling (1.0)
        if (lr == null || grid == null) return;

        Vector3 center = grid.GetCellWorldCenter(cellIdx.x, cellIdx.y);
        float half = Mathf.Max(0.0001f, (grid.GetSpacing() * 0.5f) + shapeSizeOffset);

        Vector3 p0 = new Vector3(center.x - half, center.y - half, center.z);
        Vector3 p1 = new Vector3(center.x + half, center.y - half, center.z);
        Vector3 p2 = new Vector3(center.x + half, center.y + half, center.z);
        Vector3 p3 = new Vector3(center.x - half, center.y + half, center.z);

        float halfLineWidth = Mathf.Max(0f, Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f);
        float inset = Mathf.Min(halfLineWidth, half * 0.95f);

        Vector3 pStart = new Vector3(p0.x + inset, p0.y, p0.z);
        Vector3 pEnd = new Vector3(p0.x, p0.y + inset, p0.z);

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = 5;
        lr.SetPosition(0, pStart - Vector3.right * inset * 2);
        lr.SetPosition(1, p1);
        lr.SetPosition(2, p2);
        lr.SetPosition(3, p3);
        lr.SetPosition(4, pEnd);
    }

    private static void SetIndicatorAlpha(LineRenderer lr, float alpha)
    {
        if (lr == null) return;

        float a = Mathf.Clamp01(alpha);
        Color start = lr.startColor;
        Color end = lr.endColor;
        start.a = a;
        end.a = a;
        lr.startColor = start;
        lr.endColor = end;
    }

    private static bool ApproximatelyColor(Color a, Color b, float epsilon)
    {
        return Mathf.Abs(a.r - b.r) <= epsilon
            && Mathf.Abs(a.g - b.g) <= epsilon
            && Mathf.Abs(a.b - b.b) <= epsilon
            && Mathf.Abs(a.a - b.a) <= epsilon;
    }

    private void RemoveIndicatorAtCell(Vector2Int idx)
    {
        if (!_wallIndicators.TryGetValue(idx, out var lr)) return;

        if (lr != null)
        {
            Destroy(lr.gameObject);
        }

        _wallIndicators.Remove(idx);
        _openCellCenters.Remove(idx);
        _alphaBoostByCell.Remove(idx);
        _lensAlphaBoostByCell.Remove(idx);
        _colorBoostByCell.Remove(idx);
        desiredColor.Remove(idx);
        _desiredColorMixes.Remove(idx);
        _cellColorOverrides.Remove(idx);
        _desiredColorsDirty = true;
    }

    private void ClearAllIndicators()
    {
        foreach (var pair in _wallIndicators)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        _wallIndicators.Clear();
        _removeBuffer.Clear();
        _openCellCenters.Clear();
        _alphaBoostByCell.Clear();
        _lensAlphaBoostByCell.Clear();
        _colorBoostByCell.Clear();
        _colorBoostSnapshot.Clear();
        _openCells.Clear();
        _enemySrcCache.Clear();
        desiredColor.Clear();
        _desiredColorMixes.Clear();
        _cellColorOverrides.Clear();
        ClearQueryCaches();
        _desiredColorsDirty = true;
    }
}
