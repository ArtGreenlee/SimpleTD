using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class PIC : MonoBehaviour
{
    public static PIC instance;
    [SerializeField] private GridManager grid;

    public GridManager.Cell hoveredCell;
    public ParticleSystem pickupDropVfx;
    public UnityEngine.UI.Image placementRadialTimer;
    [Header("Pickup Times")]
    [Min(0f)] public float towerPickupTime = 0.3f;
    [Min(0f)] public float relicPickupTime = 0.5f;
    public enum PICState
    {
        Idle,
        PlacingTower,
        PlacingUpgrade,
        PlacingWall
    }

    public List<Transform> towerInventorySlots;

    public Interactable hoveredInteractable;
    public PICState currentState = PICState.Idle;

    [Header("Pickup/Drop")]
    [SerializeField] private KeyCode interactKey = KeyCode.Mouse0;
    public KeyCode InteractKey => interactKey;

    [Header("Hover UI")]
    [Tooltip("If true, hovering a tower will show its TowerInformationDisplay and hide it when no longer hovering the tower/UI.")]
    [SerializeField] private bool showTowerInformationOnHover = false;
    [Tooltip("If true, hovering a relic will show its RelicInformationDisplay and hide it when no longer hovering the relic/UI.")]
    [SerializeField] private bool showRelicInformationOnHover = true;

    [Header("Placement Search")]
    [Min(0)]
    [SerializeField] private int placementCellRadius = 2;

    [Header("Rotation")]
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.E;
    [SerializeField] private float rotationStepDegrees = 15f;

    [Header("Held Tower Tilt")]
    [SerializeField, Min(0f)] private float heldTowerTiltDegreesPerUnitPerSecond = 4f;
    [SerializeField, Min(0f)] private float heldTowerMaxTiltDegrees = 12f;
    [SerializeField, Min(0f)] private float heldTowerTiltLerpSpeed = 12f;

    [Header("Placement")]
    [Tooltip("If enabled, towers can be placed on any empty cell; the cell will be converted into a wall automatically.")]
    [SerializeField] private bool allowPlaceAnywhereByAutoWall = false;

    [Tooltip("If true, towers may only be placed on wall cells.")]
    [SerializeField] private bool towersCanOnlyBePlacedOnWalls = true;

    [Header("Placement Indicators")]
    [SerializeField] private GameObject wallIndicatorLineRendererPrefab;
    [Tooltip("Adds to half-size of wall indicator square in world units. Positive grows, negative shrinks.")]
    [SerializeField] private float wallIndicatorShapeSizeOffset = 0f;
    [SerializeField, Range(0f, 1f)] private float placementIndicatorAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float placementIndicatorHoveredAlpha = 0.4f;
    [SerializeField, Min(0f)] private float placementIndicatorAlphaLerpSpeed = 12f;
    [SerializeField] private Color placementIndicatorValidColor = Color.white;
    [SerializeField] private Color placementIndicatorInventoryRedirectColor = Color.red;

    [Header("Player Wall Placement Indicators")]
    [SerializeField] private CM.ColorType wallPlacementIndicatorColorType = CM.ColorType.Cyan;
    [SerializeField] private CM.ColorType wallPlacementPreviewColorType = CM.ColorType.Green;
    [Tooltip("When enabled, wall start indicators are hidden if the player cannot afford the minimum one-cell wall placement cost.")]
    [SerializeField] private bool hideWallStartIndicatorsWhenUnaffordable = false;
    [Tooltip("Very low alpha used to show precomputed wall extents before the player starts placing a wall.")]
    [SerializeField, Range(0f, 1f)] private float wallPlacementPrecomputedExtentAlpha = 0.03f;

    [Header("Player Wall Placement Cost")]
    [SerializeField, Min(0)] private int wallPlacementInitialCost = 0;
    [SerializeField, Min(0)] private int wallPlacementPerCellCost = 0;
    [SerializeField, Range(0f, 90f)] private float wallPlacementCancelAngleDegrees = 35f;
    [SerializeField] private Vector3 wallPlacementCostTextOffset = new Vector3(0.6f, 0.6f, 0f);

    [Header("Player Wall Placement Precompute")]
    [Tooltip("How many async wall-start precompute steps are executed per frame while not placing a wall.")]
    [SerializeField, Min(1)] private int wallPlacementPrecomputeStepsPerFrame = 1;

    [Header("Target Override")]
    [Tooltip("If true, every tower will target the highlighted enemy (the enemy info panel target) if it is in range.")]
    [SerializeField] private bool forceAllTowersTargetHighlightedEnemyOption = false;
    [Tooltip("World-space indicator shown on the highlighted enemy while target override is active.")]
    [SerializeField] private Transform playerTargetOverrideIndicator;
    [SerializeField, Min(0f)] private float playerTargetOverrideIndicatorLerpSpeed = 12f;
    [SerializeField, Min(0f)] private float playerTargetOverrideIndicatorNearDistance = 0.2f;
    [SerializeField] private float playerTargetOverrideIndicatorRotateDegreesPerSecond = 180f;
    [SerializeField, Min(0f)] private float playerTargetOverrideIndicatorSwellAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float playerTargetOverrideIndicatorSwellFrequency = 2f;
    public static bool forceAllTowersTargetHighlightedEnemy;

    [Tooltip("Maximum distance (world units) at which the held tower will snap to a free inventory slot.")]
    [Min(0f)]
    [SerializeField] private float maxInventorySnapDistance = 1.5f;

    private class PlacementIndicatorState
    {
        public GameObject root;
        public LineRenderer lineRenderer;
        public float currentAlpha;
        public float desiredAlpha;
    }

    private readonly Dictionary<Vector2Int, PlacementIndicatorState> _placementIndicators = new Dictionary<Vector2Int, PlacementIndicatorState>(256);
    private readonly HashSet<Vector2Int> _placementIndicatorsTouched = new HashSet<Vector2Int>();
    private bool _wallPlacementCancelRequested;
    private bool _wasWaveActiveLastFrame;
    private int _wallPlacementChargedCost;

    private TowerInteractable _heldTower;
    private UpgradeInteractable _heldUpgrade;

    private bool _towerInfoPinned;
    private TowerInteractable _hoverTowerInfo;
    private Relic _hoverRelicInfo;

    // Track which inventory slot we came from / should return to.
    private int _heldTowerOriginalInventorySlot = -1;
    private int _heldUpgradeOriginalInventorySlot = -1;

    // Hold-to-pickup
    private TowerInteractable _pickupCandidate;
    private float _pickupHoldStartTime;
    private bool _isHoldingForPickup;

    private Relic _relicPickupCandidate;
    private float _relicPickupHoldStartTime;
    private bool _isHoldingForRelicPurchase;

    private UpgradeInteractable _upgradePickupCandidate;
    private float _upgradePickupHoldStartTime;
    private bool _isHoldingForUpgradePickup;

    private Vector2Int _heldTowerOriginalCell = new Vector2Int(int.MinValue, int.MinValue);
    private bool _hasLastValidCell;
    private Vector2Int _lastValidCell;
    private Vector3 _lastValidCellCenter;
    public Camera rayCastCamera;
    public RenderTexture renderTexture;

    private float _heldRangeRotationDeg;
    private float _heldTowerTiltAngle;
    private Vector3 _heldTowerPrevPosition;
    private Vector3 _playerTargetOverrideIndicatorBaseScale = Vector3.one;
    private bool _hasPlayerTargetOverrideIndicatorBaseScale;
    private SRC _playerTargetOverrideIndicatorSrc;
    private readonly List<float> _playerTargetOverrideIndicatorBaseAlphaOverrides = new List<float>(8);

    private Vector2Int _heldTowerLastMeshCell = new Vector2Int(int.MinValue, int.MinValue);
    private TowerInteractable _swapPreviewTower;
    private Vector2Int _swapPreviewCell = new Vector2Int(int.MinValue, int.MinValue);
    public Transform debugTransform;

    // ---- Player wall placement ----
    public bool placingWallMode { get; private set; }
    private Vector2Int _wallPlacementOrigin;
    private GridManager.Direction _wallPlacementDir;
    private bool _hasWallPlacementDir;
    private readonly List<Vector2Int> _wallPlacementPreview = new List<Vector2Int>(8);
    private readonly List<Vector2Int> _wallPlacementValidationScratch = new List<Vector2Int>(8);
    private readonly List<Vector2Int> _minePlacementPreview = new List<Vector2Int>(16);
    private readonly List<Vector2Int> _wallPlacementAllowedRight = new List<Vector2Int>(16);
    private readonly List<Vector2Int> _wallPlacementAllowedLeft = new List<Vector2Int>(16);
    private readonly List<Vector2Int> _wallPlacementAllowedUp = new List<Vector2Int>(16);
    private readonly List<Vector2Int> _wallPlacementAllowedDown = new List<Vector2Int>(16);
    private readonly Dictionary<Vector2Int, WallPlacementRunCounts> _wallPlacementRunCountCache = new Dictionary<Vector2Int, WallPlacementRunCounts>(128);
    private CancellationTokenSource _wallPlacementPrecomputeCts;
    private Task<WallPlacementPrecomputeTaskResult> _wallPlacementPrecomputeTask;
    private readonly ConcurrentQueue<WallPlacementPrecomputeResultEntry> _wallPlacementPrecomputeResults = new ConcurrentQueue<WallPlacementPrecomputeResultEntry>();
    private int _wallPlacementRunCountCacheWalkabilityVersion = -1;
    private bool _hasWallPlacementPrecomputeTaskPriorityOrigin;
    private Vector2Int _wallPlacementPrecomputeTaskPriorityOrigin;

    private static readonly GridManager.Direction[] WallPlacementDirectionOrder =
    {
        GridManager.Direction.Right,
        GridManager.Direction.Left,
        GridManager.Direction.Up,
        GridManager.Direction.Down,
    };

    private struct WallPlacementRunCounts
    {
        public bool canStart;
        public int right;
        public int left;
        public int up;
        public int down;
    }

    private class WallPlacementPrecomputeSnapshot
    {
        public int width;
        public int height;
        public int minimumForcedGapWidth;
        public int minimumPlacementCellCount;
        public int walkabilityVersion;
        public bool[,] blocked;
        public bool[,] isWall;
        public bool[,] inMazeBounds;
        public bool[,,] allowedByDirection;
        public List<Vector2Int> starts;
        public List<Vector2Int> goals;
        public List<Vector2Int> candidateOrigins;
        public List<int> candidateDirectionIndices;
    }

    private class WallPlacementPrecomputeTaskResult
    {
        public int walkabilityVersion;
    }

    private struct WallPlacementPrecomputeResultEntry
    {
        public int walkabilityVersion;
        public Vector2Int origin;
        public WallPlacementRunCounts counts;
    }

    public RectTransform renderTextureRectTransform; // Assign in inspector if using UI

    public GameObject WallIndicatorLineRendererPrefab => wallIndicatorLineRendererPrefab;
    public float WallIndicatorShapeSizeOffset => wallIndicatorShapeSizeOffset;

    public void SetHoveredCell()
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        if (grid.TryWorldToCell(mouseWorldPos, out var cellIdx) && grid.TryGetCell(cellIdx.x, cellIdx.y, out var cell))
        {
            hoveredCell = cell;
        }
    }

    public bool IsHighlightingEnemy()
    {
        return GetHighlightedEnemy() != null;
    }

    public Enemy GetHighlightedEnemy()
    {
        var display = EnemyInformationDisplay.instance;
        if (display == null) return null;

        var e = display.displayedEnemy;
        if (e == null)
        {
            // Handle the highlighted enemy being destroyed while the panel is up.
            if (display.enemyInformationPanel != null && display.enemyInformationPanel.activeSelf)
            {
                display.HideEnemyInformation();
            }
            return null;
        }

        return e;
    }

    private float GetPickupTimeForInteractable(Interactable interactable)
    {
        if (interactable is TowerInteractable) return towerPickupTime;
        if (interactable is Relic) return relicPickupTime;
        return towerPickupTime; // default fallback
    }

    public bool isHoldingTower()
    {
        return _heldTower != null;
    }

    public bool IsHoldingTower(Tower tower)
    {
        if (tower == null) return false;
        if (_heldTower == null) return false;
        return _heldTower.GetTower() == tower;
    }

    public bool IsValidTowerPlacementCell(Vector2Int cellIdx)
    {
        return IsCellValidForTower(cellIdx);
    }

    public Vector3 GetMousePosition()
    {
        Camera cam = rayCastCamera != null ? rayCastCamera : Camera.main;
        Vector3 mousePos = Input.mousePosition;

        if (renderTextureRectTransform != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                renderTextureRectTransform, mousePos, null, out localPoint);

            Rect rect = renderTextureRectTransform.rect;
            float normX = (localPoint.x - rect.x) / rect.width;
            float normY = (localPoint.y - rect.y) / rect.height;

            // Clamp to [0,1] to avoid out-of-bounds
            normX = Mathf.Clamp01(normX);
            normY = Mathf.Clamp01(normY);

            if (cam != null)
            {
                Rect pixelRect = cam.pixelRect;
                mousePos.x = pixelRect.x + normX * pixelRect.width;
                mousePos.y = pixelRect.y + normY * pixelRect.height;
            }
        }
        else if (cam != null && cam.targetTexture != null)
        {
            Rect pixelRect = cam.pixelRect;
            float normX = mousePos.x / Screen.width;
            float normY = mousePos.y / Screen.height;
            mousePos.x = pixelRect.x + normX * pixelRect.width;
            mousePos.y = pixelRect.y + normY * pixelRect.height;
        }

        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mousePos);
        mouseWorldPos.z = 0f;
        return mouseWorldPos;
    }

    private PlacementIndicatorState GetOrCreatePlacementIndicator(Vector2Int cellIdx)
    {
        if (_placementIndicators.TryGetValue(cellIdx, out var existing))
        {
            return existing;
        }

        if (wallIndicatorLineRendererPrefab == null) return null;

        GameObject go = Instantiate(wallIndicatorLineRendererPrefab, transform);
        go.name = $"WallIndicator_{cellIdx.x}_{cellIdx.y}";

        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.GetComponentInChildren<LineRenderer>();
        if (lr == null)
        {
            Destroy(go);
            return null;
        }

        var created = new PlacementIndicatorState
        {
            root = go,
            lineRenderer = lr,
            currentAlpha = 0f,
            desiredAlpha = 0f,
        };

        ConfigurePlacementIndicatorShape(created.lineRenderer, cellIdx);
        SetPlacementIndicatorAlpha(created, 0f);
        if (created.root != null) created.root.SetActive(false);

        _placementIndicators[cellIdx] = created;
        return created;
    }

    private void ConfigurePlacementIndicatorShape(LineRenderer lr, Vector2Int cellIdx)
    {
        GridViz.ConfigureWallIndicatorShape(lr, grid, cellIdx, wallIndicatorShapeSizeOffset);
    }

    private static void SetPlacementIndicatorAlpha(PlacementIndicatorState state, float alpha)
    {
        if (state == null || state.lineRenderer == null) return;

        float a = Mathf.Clamp01(alpha);
        Color start = state.lineRenderer.startColor;
        Color end = state.lineRenderer.endColor;
        start.a = a;
        end.a = a;
        state.lineRenderer.startColor = start;
        state.lineRenderer.endColor = end;
    }

    private static void SetPlacementIndicatorColor(PlacementIndicatorState state, Color color)
    {
        if (state == null || state.lineRenderer == null) return;

        Color start = color;
        Color end = color;
        start.a = state.lineRenderer.startColor.a;
        end.a = state.lineRenderer.endColor.a;
        state.lineRenderer.startColor = start;
        state.lineRenderer.endColor = end;
    }

    private Color GetCMColor(CM.ColorType colorType, Color fallback)
    {
        return CM.i != null ? CM.i.ColorTypeToColor(colorType) : fallback;
    }

    private Color GetWallPlacementIndicatorColor(bool cancelRequested)
    {
        return cancelRequested
            ? GetCMColor(CM.ColorType.Red, Color.red)
            : GetCMColor(wallPlacementIndicatorColorType, new Color(0.4f, 0.8f, 1f, 1f));
    }

    private Color GetWallPlacementPreviewColor(bool cancelRequested)
    {
        return cancelRequested
            ? GetCMColor(CM.ColorType.Red, Color.red)
            : GetCMColor(wallPlacementPreviewColorType, new Color(0.2f, 1f, 0.4f, 1f));
    }

    private bool ShouldShowInventoryRedirectPlacementIndicators()
    {
        if (_heldTower == null || currentState != PICState.PlacingTower) return false;
        if (TowerManager.instance == null) return false;

        if (TowerManager.instance.GetCurrentPlacedTowers() < TowerManager.instance.GetMaximumPlacedTowers()) return false;

        int invSlot = -1;
        return TryGetNearestFreeInventorySlot(_heldTower.transform.position, out invSlot, _heldTower, _heldUpgrade);
    }

    private void ClearPlacementIndicatorDesiredAlpha()
    {
        foreach (var kvp in _placementIndicators)
        {
            if (kvp.Value == null) continue;
            kvp.Value.desiredAlpha = 0f;
        }
    }

    private void UpdatePlacementIndicatorsDesiredAlpha(Vector3 hoverWorldPos)
    {
        if (_heldTower == null || currentState != PICState.PlacingTower)
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        if (_heldTower.GetTower() is ProjectileTower projectileTower && projectileTower.IsMineModeActive())
        {
            UpdateMinePlacementIndicatorsDesiredAlpha(hoverWorldPos, projectileTower);
            return;
        }

        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        _placementIndicatorsTouched.Clear();

        Vector2Int hoveredIdx = default;
        bool hasHoveredValidCell = false;
        if (grid.TryWorldToCell(hoverWorldPos, out var hoveredCellIdx) && IsCellValidForTower(hoveredCellIdx))
        {
            hoveredIdx = hoveredCellIdx;
            hasHoveredValidCell = true;
        }

        bool hasSwapPreviewCell = TryGetSwapPreviewAtWorldPosition(hoverWorldPos, out var swapPreviewCellIdx, out _);

        int xMin = 0;
        int yMin = 0;
        int xMax = grid.CellsX;
        int yMax = grid.CellsY;
        bool restrictToMazeBounds = !towersCanOnlyBePlacedOnWalls || allowPlaceAnywhereByAutoWall;
        if (restrictToMazeBounds && grid.TryGetMazeBounds(out var bounds))
        {
            xMin = Mathf.Clamp(bounds.xMin, 0, grid.CellsX);
            yMin = Mathf.Clamp(bounds.yMin, 0, grid.CellsY);
            xMax = Mathf.Clamp(bounds.xMax, 0, grid.CellsX);
            yMax = Mathf.Clamp(bounds.yMax, 0, grid.CellsY);
        }

        float baseAlpha = Mathf.Clamp01(placementIndicatorAlpha);
        float hoveredAlpha = Mathf.Clamp01(placementIndicatorHoveredAlpha);
        if (hoveredAlpha < baseAlpha) hoveredAlpha = baseAlpha;
        Color indicatorColor = ShouldShowInventoryRedirectPlacementIndicators()
            ? placementIndicatorInventoryRedirectColor
            : placementIndicatorValidColor;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var idx = new Vector2Int(x, y);
                if (!IsCellValidForTower(idx)) continue;

                var indicator = GetOrCreatePlacementIndicator(idx);
                if (indicator == null) continue;

                ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
                SetPlacementIndicatorColor(indicator, indicatorColor);
                indicator.desiredAlpha = hasHoveredValidCell && idx == hoveredIdx ? hoveredAlpha : baseAlpha;
                _placementIndicatorsTouched.Add(idx);
            }
        }

        if (hasSwapPreviewCell)
        {
            var indicator = GetOrCreatePlacementIndicator(swapPreviewCellIdx);
            if (indicator != null)
            {
                ConfigurePlacementIndicatorShape(indicator.lineRenderer, swapPreviewCellIdx);
                SetPlacementIndicatorColor(indicator, placementIndicatorValidColor);
                indicator.desiredAlpha = hoveredAlpha;
                _placementIndicatorsTouched.Add(swapPreviewCellIdx);
            }
        }

        foreach (var kvp in _placementIndicators)
        {
            if (_placementIndicatorsTouched.Contains(kvp.Key)) continue;
            if (kvp.Value == null) continue;
            kvp.Value.desiredAlpha = 0f;
        }
    }

    private void UpdateMinePlacementIndicatorsDesiredAlpha(Vector3 hoverWorldPos, ProjectileTower projectileTower)
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null || projectileTower == null)
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        if (!grid.TryWorldToCell(hoverWorldPos, out var hoveredCellIdx) || !IsCellValidForTower(hoveredCellIdx))
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        Vector3 validHoverCenter = grid.GetCellWorldCenter(hoveredCellIdx.x, hoveredCellIdx.y);

        _placementIndicatorsTouched.Clear();
        _minePlacementPreview.Clear();

        if (!projectileTower.TryGetMineSpawnPreviewCells(validHoverCenter, _minePlacementPreview))
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        float baseAlpha = Mathf.Clamp01(placementIndicatorAlpha);
        float hoveredAlpha = Mathf.Clamp01(placementIndicatorHoveredAlpha);
        if (hoveredAlpha < baseAlpha) hoveredAlpha = baseAlpha;
        Color indicatorColor = projectileTower.GetMinePreviewColor();
        Vector2Int startCell = hoveredCellIdx;

        for (int i = 0; i < _minePlacementPreview.Count; i++)
        {
            Vector2Int idx = _minePlacementPreview[i];
            var indicator = GetOrCreatePlacementIndicator(idx);
            if (indicator == null) continue;

            ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
            SetPlacementIndicatorColor(indicator, indicatorColor);
            bool isFirstRing = Mathf.Abs(idx.x - startCell.x) + Mathf.Abs(idx.y - startCell.y) == 1;
            indicator.desiredAlpha = isFirstRing ? hoveredAlpha : baseAlpha;
            _placementIndicatorsTouched.Add(idx);
        }

        foreach (var kvp in _placementIndicators)
        {
            if (_placementIndicatorsTouched.Contains(kvp.Key)) continue;
            if (kvp.Value == null) continue;
            kvp.Value.desiredAlpha = 0f;
        }
    }

    private void UpdateWallPlacementIndicatorsDesiredAlpha(Vector3 hoverWorldPos)
    {
        if (grid == null) return;

        if (IsWaveCurrentlyActive())
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        if (hideWallStartIndicatorsWhenUnaffordable && !CanAffordAnyWallPlacement())
        {
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        _placementIndicatorsTouched.Clear();

        var cells = grid.GetWallPlacementIndicatorCells();
        if (cells == null) return;

        bool hasHovered = grid.TryWorldToCell(hoverWorldPos, out var hoveredIdx);
        bool hoveredPotentialStart = hasHovered && IsWallPlacementStartCellAvailable(hoveredIdx);

        float baseAlpha = Mathf.Clamp01(placementIndicatorAlpha);
        float hoveredAlpha = Mathf.Clamp01(placementIndicatorHoveredAlpha);
        if (hoveredAlpha < baseAlpha) hoveredAlpha = baseAlpha;
        float precomputedAlpha = Mathf.Clamp01(wallPlacementPrecomputedExtentAlpha);

        // Preemptive overlay: only show for the currently hovered potential start cell.
        if (precomputedAlpha > 0f && hoveredPotentialStart && TryGetCachedWallPlacementRunCounts(hoveredIdx, out var hoveredCounts))
        {
            AddPrecomputedWallPlacementExtentIndicatorsForOrigin(hoveredIdx, hoveredCounts, precomputedAlpha, GetWallPlacementIndicatorColor(false));
        }

        foreach (var idx in cells)
        {
            if (!IsWallPlacementStartCellAvailable(idx)) continue;

            var indicator = GetOrCreatePlacementIndicator(idx);
            if (indicator == null) continue;

            ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
            SetPlacementIndicatorColor(indicator, GetWallPlacementIndicatorColor(false));
            float desired = hasHovered && idx == hoveredIdx ? hoveredAlpha : baseAlpha;
            indicator.desiredAlpha = Mathf.Max(indicator.desiredAlpha, desired);
            _placementIndicatorsTouched.Add(idx);
        }

        foreach (var kvp in _placementIndicators)
        {
            if (_placementIndicatorsTouched.Contains(kvp.Key)) continue;
            if (kvp.Value == null) continue;
            kvp.Value.desiredAlpha = 0f;
        }
    }

    private void AddPrecomputedWallPlacementExtentIndicators(float alpha, Color color)
    {
        if (_wallPlacementRunCountCache.Count == 0) return;
        if (grid == null) return;

        foreach (var kvp in _wallPlacementRunCountCache)
        {
            var origin = kvp.Key;
            if (!grid.IsWallPlacementIndicatorCell(origin)) continue;

            var counts = kvp.Value;
            if (!counts.canStart) continue;
            AddPrecomputedRunIndicators(origin, Vector2Int.right, counts.right, alpha, color);
            AddPrecomputedRunIndicators(origin, Vector2Int.left, counts.left, alpha, color);
            AddPrecomputedRunIndicators(origin, Vector2Int.up, counts.up, alpha, color);
            AddPrecomputedRunIndicators(origin, Vector2Int.down, counts.down, alpha, color);
        }
    }

    private void AddPrecomputedWallPlacementExtentIndicatorsForOrigin(Vector2Int origin, WallPlacementRunCounts counts, float alpha, Color color)
    {
        if (grid == null) return;
        if (!grid.IsWallPlacementIndicatorCell(origin)) return;

        AddPrecomputedRunIndicators(origin, Vector2Int.right, counts.right, alpha, color);
        AddPrecomputedRunIndicators(origin, Vector2Int.left, counts.left, alpha, color);
        AddPrecomputedRunIndicators(origin, Vector2Int.up, counts.up, alpha, color);
        AddPrecomputedRunIndicators(origin, Vector2Int.down, counts.down, alpha, color);
    }

    private void AddPrecomputedRunIndicators(Vector2Int origin, Vector2Int step, int count, float alpha, Color color)
    {
        if (count <= 0) return;

        Vector2Int cur = origin + step;
        for (int i = 0; i < count; i++)
        {
            var indicator = GetOrCreatePlacementIndicator(cur);
            if (indicator != null)
            {
                ConfigurePlacementIndicatorShape(indicator.lineRenderer, cur);
                SetPlacementIndicatorColor(indicator, color);
                indicator.desiredAlpha = Mathf.Max(indicator.desiredAlpha, alpha);
                _placementIndicatorsTouched.Add(cur);
            }

            cur += step;
        }
    }

    private void UpdateWallPlacementPreviewIndicators(Vector3 hoverWorldPos)
    {
        if (grid == null) return;

        _placementIndicatorsTouched.Clear();

        UpdateWallPlacementDragFromMouse(hoverWorldPos);

        float baseAlpha = Mathf.Clamp01(placementIndicatorAlpha);
        float hoveredAlpha = Mathf.Clamp01(placementIndicatorHoveredAlpha);
        if (hoveredAlpha < baseAlpha) hoveredAlpha = baseAlpha;
        bool unaffordable = IsCurrentWallPlacementUnaffordable();
        bool invalidPlacement = !IsCurrentWallPlacementValid();
        bool showCancelVisuals = _wallPlacementCancelRequested || unaffordable || invalidPlacement;
        Color wallPlacementIndicatorColor = GetWallPlacementIndicatorColor(showCancelVisuals);
        Color wallPlacementPreviewColor = GetWallPlacementPreviewColor(showCancelVisuals);

        // Origin always shown brightly.
        var originIndicator = GetOrCreatePlacementIndicator(_wallPlacementOrigin);
        if (originIndicator != null)
        {
            ConfigurePlacementIndicatorShape(originIndicator.lineRenderer, _wallPlacementOrigin);
            SetPlacementIndicatorColor(originIndicator, wallPlacementPreviewColor);
            originIndicator.desiredAlpha = hoveredAlpha;
            _placementIndicatorsTouched.Add(_wallPlacementOrigin);
        }

        bool hasForcedDir = grid.TryGetWallPlacementContinuationDirection(_wallPlacementOrigin, out var forcedDir);

        if (hasForcedDir)
        {
            var run = GetCachedWallPlacementAllowedRun(forcedDir);
            for (int i = 0; i < run.Count; i++)
            {
                var idx = run[i];
                var indicator = GetOrCreatePlacementIndicator(idx);
                if (indicator == null) continue;

                ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
                SetPlacementIndicatorColor(indicator, wallPlacementIndicatorColor);
                indicator.desiredAlpha = baseAlpha;
                _placementIndicatorsTouched.Add(idx);
            }
        }
        else
        {
            // Show available extents in all four directions at base alpha.
            for (int d = 0; d < 4; d++)
            {
                var dir = (GridManager.Direction)d;
                var run = GetCachedWallPlacementAllowedRun(dir);
                for (int i = 0; i < run.Count; i++)
                {
                    var idx = run[i];
                    var indicator = GetOrCreatePlacementIndicator(idx);
                    if (indicator == null) continue;

                    ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
                    SetPlacementIndicatorColor(indicator, wallPlacementIndicatorColor);
                    indicator.desiredAlpha = baseAlpha;
                    _placementIndicatorsTouched.Add(idx);
                }
            }
        }

        // Brighten preview cells along the active drag direction.
        for (int i = 0; i < _wallPlacementPreview.Count; i++)
        {
            var idx = _wallPlacementPreview[i];
            var indicator = GetOrCreatePlacementIndicator(idx);
            if (indicator == null) continue;

            ConfigurePlacementIndicatorShape(indicator.lineRenderer, idx);
            SetPlacementIndicatorColor(indicator, wallPlacementPreviewColor);
            indicator.desiredAlpha = hoveredAlpha;
            _placementIndicatorsTouched.Add(idx);
        }

        foreach (var kvp in _placementIndicators)
        {
            if (_placementIndicatorsTouched.Contains(kvp.Key)) continue;
            if (kvp.Value == null) continue;
            kvp.Value.desiredAlpha = 0f;
        }
    }

    private int GetWallPlacementCost(int cellCount)
    {
        cellCount = Mathf.Max(0, cellCount);
        return Mathf.Max(0, wallPlacementInitialCost) + Mathf.Max(0, wallPlacementPerCellCost) * cellCount;
    }

    private bool IsCurrentWallPlacementUnaffordable()
    {
        int cellCount = 1 + _wallPlacementPreview.Count;
        int cost = GetWallPlacementCost(cellCount);
        return !CanAffordWallPlacementCost(cost);
    }

    private bool IsCurrentWallPlacementValid()
    {
        if (grid == null) return false;

        _wallPlacementValidationScratch.Clear();
        _wallPlacementValidationScratch.Add(_wallPlacementOrigin);
        for (int i = 0; i < _wallPlacementPreview.Count; i++)
        {
            _wallPlacementValidationScratch.Add(_wallPlacementPreview[i]);
        }

        return grid.CanFinalizeWallPlacement(_wallPlacementValidationScratch);
    }

    private string GetCurrentWallPlacementValidationMessage()
    {
        if (grid == null) return "invalid wall placement";

        _wallPlacementValidationScratch.Clear();
        _wallPlacementValidationScratch.Add(_wallPlacementOrigin);
        for (int i = 0; i < _wallPlacementPreview.Count; i++)
        {
            _wallPlacementValidationScratch.Add(_wallPlacementPreview[i]);
        }

        return grid.GetWallPlacementValidationMessage(_wallPlacementValidationScratch);
    }

    private bool CanAffordWallPlacementCost(int cost)
    {
        if (cost <= 0) return true;
        if (CurrencyManager.instance == null) return true;

        int availableCurrency = CurrencyManager.instance.GetCurrency();
        if (placingWallMode || currentState == PICState.PlacingWall)
        {
            availableCurrency += Mathf.Max(0, _wallPlacementChargedCost);
        }

        return availableCurrency >= cost;
    }

    private bool CanAffordAnyWallPlacement()
    {
        int minimumCost = GetWallPlacementCost(1);
        return CanAffordWallPlacementCost(minimumCost);
    }

    private static bool IsWaveCurrentlyActive()
    {
        return WaveManager.instance != null && WaveManager.instance.IsWaveActive();
    }

    private void UpdateWallPlacementCostText(Vector3 hoverWorldPos)
    {
        if (CursorToolTipController.instance == null) return;

        int cellCount = 1 + _wallPlacementPreview.Count;
        int cost = GetWallPlacementCost(cellCount);
        Vector3 textPosition = hoverWorldPos + wallPlacementCostTextOffset;
        bool cancelRequested = _wallPlacementCancelRequested;
        bool unaffordable = !CanAffordWallPlacementCost(cost);
        string invalidMessage = cancelRequested ? string.Empty : GetCurrentWallPlacementValidationMessage();
        bool invalidPlacement = !string.IsNullOrEmpty(invalidMessage);
        CM.ColorType textColorType = (cancelRequested || unaffordable || invalidPlacement)
            ? CM.ColorType.Red
            : CM.ColorType.Gold;
        string text = cancelRequested
            ? "cancel?"
            : invalidPlacement
                ? invalidMessage
                : $"${cost}";

        if (CM.i != null)
        {
            text = CM.i.RTC(textColorType, text);
        }

        CursorToolTipController.instance.ShowCustomToolTip(textPosition, text);
    }

    private void ReleaseWallPlacementCostText()
    {
        if (CursorToolTipController.instance == null) return;
        CursorToolTipController.instance.HideCustomToolTip();
    }

    private void UpdateWallPlacementDragFromMouse(Vector3 hoverWorldPos)
    {
        _wallPlacementPreview.Clear();
        _wallPlacementCancelRequested = false;
        if (grid == null) return;

        Vector3 originCenter = grid.GetCellWorldCenter(_wallPlacementOrigin.x, _wallPlacementOrigin.y);
        Vector2 delta = new Vector2(hoverWorldPos.x - originCenter.x, hoverWorldPos.y - originCenter.y);

        float spacing = Mathf.Max(0.0001f, grid.GetSpacing());
        if (delta.magnitude < spacing * 0.5f)
        {
            _hasWallPlacementDir = false;
            return;
        }

        bool hasForcedDir = grid.TryGetWallPlacementContinuationDirection(_wallPlacementOrigin, out var forcedDir);
        GridManager.Direction dir;
        if (hasForcedDir)
        {
            dir = forcedDir;
            _wallPlacementDir = dir;
            _hasWallPlacementDir = true;
        }
        else if (_hasWallPlacementDir)
        {
            dir = _wallPlacementDir;
        }
        else
        {
            dir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? GridManager.Direction.Right : GridManager.Direction.Left)
                : (delta.y >= 0f ? GridManager.Direction.Up : GridManager.Direction.Down);
            _wallPlacementDir = dir;
            _hasWallPlacementDir = true;
        }

        Vector2 dirVector = dir switch
        {
            GridManager.Direction.Right => Vector2.right,
            GridManager.Direction.Left => Vector2.left,
            GridManager.Direction.Up => Vector2.up,
            GridManager.Direction.Down => Vector2.down,
            _ => Vector2.right
        };

        float cancelAngle = Mathf.Clamp(wallPlacementCancelAngleDegrees, 0f, 90f);
        float angleFromDirection = Vector2.Angle(delta, dirVector);
        if (angleFromDirection > cancelAngle)
        {
            _wallPlacementCancelRequested = true;
            return;
        }

        var available = GetCachedWallPlacementAllowedRun(dir);
        if (available.Count == 0) return;

        float axis;
        if (hasForcedDir)
        {
            axis = dir switch
            {
                GridManager.Direction.Right => Mathf.Max(0f, delta.x),
                GridManager.Direction.Left => Mathf.Max(0f, -delta.x),
                GridManager.Direction.Up => Mathf.Max(0f, delta.y),
                GridManager.Direction.Down => Mathf.Max(0f, -delta.y),
                _ => 0f
            };

            if (axis <= 0f) return;
        }
        else
        {
            axis = (dir == GridManager.Direction.Left || dir == GridManager.Direction.Right)
                ? Mathf.Abs(delta.x)
                : Mathf.Abs(delta.y);
        }

        int desired = Mathf.Clamp(Mathf.RoundToInt(axis / spacing), 1, available.Count);
        for (int i = 0; i < desired; i++)
        {
            _wallPlacementPreview.Add(available[i]);
        }
    }

    private void BeginWallPlacement(Vector2Int originCell)
    {
        _wallPlacementOrigin = originCell;
        _hasWallPlacementDir = false;
        _wallPlacementCancelRequested = false;
        _wallPlacementChargedCost = 0;
        _wallPlacementPreview.Clear();
        RebuildWallPlacementAllowedRunCache();
        SyncWallPlacementReservedCurrency();
        UpdateWallPlacementCostText(GetMousePosition());
        placingWallMode = true;
        currentState = PICState.PlacingWall;
    }

    private void CancelWallPlacement()
    {
        _wallPlacementPreview.Clear();
        ClearWallPlacementAllowedRunCache();
        _hasWallPlacementDir = false;
        _wallPlacementCancelRequested = false;
        ReleaseWallPlacementCostText();
        placingWallMode = false;
        if (currentState == PICState.PlacingWall) currentState = PICState.Idle;
    }

    private void CancelWallPlacementAndRefund()
    {
        if (_wallPlacementChargedCost > 0 && CurrencyManager.instance != null)
        {
            CurrencyManager.instance.AddCurrency(_wallPlacementChargedCost);
        }

        _wallPlacementChargedCost = 0;
        CancelWallPlacement();
    }

    private void SyncWallPlacementReservedCurrency()
    {
        int targetReservedCost = 0;
        if (!_wallPlacementCancelRequested)
        {
            int cellCount = 1 + _wallPlacementPreview.Count;
            targetReservedCost = GetWallPlacementCost(cellCount);
        }

        targetReservedCost = Mathf.Max(0, targetReservedCost);
        if (targetReservedCost > _wallPlacementChargedCost && !CanAffordWallPlacementCost(targetReservedCost))
        {
            return;
        }

        int delta = targetReservedCost - _wallPlacementChargedCost;
        if (delta > 0)
        {
            if (CurrencyManager.instance != null)
            {
                CurrencyManager.instance.RemoveCurrency(delta);
            }
        }
        else if (delta < 0)
        {
            if (CurrencyManager.instance != null)
            {
                CurrencyManager.instance.AddCurrency(-delta);
            }
        }

        _wallPlacementChargedCost = targetReservedCost;
    }

    private List<Vector2Int> GetCachedWallPlacementAllowedRun(GridManager.Direction dir)
    {
        return dir switch
        {
            GridManager.Direction.Right => _wallPlacementAllowedRight,
            GridManager.Direction.Left => _wallPlacementAllowedLeft,
            GridManager.Direction.Up => _wallPlacementAllowedUp,
            GridManager.Direction.Down => _wallPlacementAllowedDown,
            _ => _wallPlacementAllowedRight,
        };
    }

    private void ClearWallPlacementAllowedRunCache()
    {
        _wallPlacementAllowedRight.Clear();
        _wallPlacementAllowedLeft.Clear();
        _wallPlacementAllowedUp.Clear();
        _wallPlacementAllowedDown.Clear();
    }

    private void RebuildWallPlacementAllowedRunCache()
    {
        ClearWallPlacementAllowedRunCache();
        if (grid == null) return;

        if (TryGetCachedWallPlacementRunCounts(_wallPlacementOrigin, out var counts))
        {
            FillRunFromCount(_wallPlacementOrigin, GridManager.Direction.Right, counts.right, _wallPlacementAllowedRight);
            FillRunFromCount(_wallPlacementOrigin, GridManager.Direction.Left, counts.left, _wallPlacementAllowedLeft);
            FillRunFromCount(_wallPlacementOrigin, GridManager.Direction.Up, counts.up, _wallPlacementAllowedUp);
            FillRunFromCount(_wallPlacementOrigin, GridManager.Direction.Down, counts.down, _wallPlacementAllowedDown);
            return;
        }

        var right = grid.GetWallPlacementBuildCellsWithinCommitRules(_wallPlacementOrigin, GridManager.Direction.Right);
        var left = grid.GetWallPlacementBuildCellsWithinCommitRules(_wallPlacementOrigin, GridManager.Direction.Left);
        var up = grid.GetWallPlacementBuildCellsWithinCommitRules(_wallPlacementOrigin, GridManager.Direction.Up);
        var down = grid.GetWallPlacementBuildCellsWithinCommitRules(_wallPlacementOrigin, GridManager.Direction.Down);

        _wallPlacementAllowedRight.AddRange(right);
        _wallPlacementAllowedLeft.AddRange(left);
        _wallPlacementAllowedUp.AddRange(up);
        _wallPlacementAllowedDown.AddRange(down);

        var computedCounts = new WallPlacementRunCounts
        {
            right = right.Count,
            left = left.Count,
            up = up.Count,
            down = down.Count,
        };
        _wallPlacementRunCountCache[_wallPlacementOrigin] = computedCounts;
    }

    private bool TryGetCachedWallPlacementRunCounts(Vector2Int origin, out WallPlacementRunCounts counts)
    {
        counts = default;
        if (grid == null) return false;

        int walkabilityVersion = grid.WalkabilityVersion;
        if (_wallPlacementRunCountCacheWalkabilityVersion != walkabilityVersion)
        {
            ResetWallPlacementPrecomputeCaches(walkabilityVersion);
            return false;
        }

        return _wallPlacementRunCountCache.TryGetValue(origin, out counts);
    }

    private bool IsWallPlacementStartCellAvailable(Vector2Int idx)
    {
        if (grid == null) return false;
        if (!grid.IsWallPlacementIndicatorCell(idx)) return false;
        if (!TryGetCachedWallPlacementRunCounts(idx, out var counts)) return false;
        return counts.canStart;
    }

    private void ResetWallPlacementPrecomputeCaches(int walkabilityVersion)
    {
        _wallPlacementRunCountCacheWalkabilityVersion = walkabilityVersion;
        _wallPlacementRunCountCache.Clear();
        CancelWallPlacementPrecomputeTask();
        ClearWallPlacementPrecomputeResults();
    }

    private static void FillRunFromCount(Vector2Int origin, GridManager.Direction dir, int count, List<Vector2Int> target)
    {
        target.Clear();
        if (count <= 0) return;

        Vector2Int step = dir switch
        {
            GridManager.Direction.Right => Vector2Int.right,
            GridManager.Direction.Left => Vector2Int.left,
            GridManager.Direction.Up => Vector2Int.up,
            GridManager.Direction.Down => Vector2Int.down,
            _ => Vector2Int.zero,
        };

        if (step == Vector2Int.zero) return;

        Vector2Int cur = origin + step;
        for (int i = 0; i < count; i++)
        {
            target.Add(cur);
            cur += step;
        }
    }

    private void UpdateWallPlacementPrecomputeAsync()
    {
        if (grid == null) return;
        if (placingWallMode || currentState == PICState.PlacingWall) return;
        if (!grid.wallPlacementEnabled) return;

        int walkabilityVersion = grid.WalkabilityVersion;
        if (_wallPlacementRunCountCacheWalkabilityVersion != walkabilityVersion)
        {
            ResetWallPlacementPrecomputeCaches(walkabilityVersion);
        }

        DrainWallPlacementPrecomputeResults();
        TryApplyCompletedWallPlacementPrecomputeTask();

        bool hasPriorityOrigin = TryGetHoveredPotentialWallPlacementStartCell(out var priorityOrigin);
        bool priorityNeedsCompute = hasPriorityOrigin && !_wallPlacementRunCountCache.ContainsKey(priorityOrigin);

        if (_wallPlacementPrecomputeTask != null)
        {
            if (priorityNeedsCompute && (!_hasWallPlacementPrecomputeTaskPriorityOrigin || _wallPlacementPrecomputeTaskPriorityOrigin != priorityOrigin))
            {
                CancelWallPlacementPrecomputeTask();
                ClearWallPlacementPrecomputeResults();
            }
            else
            {
                return;
            }
        }

        var snapshot = BuildWallPlacementPrecomputeSnapshot(walkabilityVersion, priorityNeedsCompute, priorityOrigin);
        if (snapshot == null || snapshot.candidateOrigins == null || snapshot.candidateOrigins.Count == 0) return;

        bool hasAllCached = true;
        for (int i = 0; i < snapshot.candidateOrigins.Count; i++)
        {
            if (_wallPlacementRunCountCache.ContainsKey(snapshot.candidateOrigins[i])) continue;
            hasAllCached = false;
            break;
        }

        if (hasAllCached) return;

        _wallPlacementPrecomputeCts = new CancellationTokenSource();
        var ct = _wallPlacementPrecomputeCts.Token;
        _hasWallPlacementPrecomputeTaskPriorityOrigin = priorityNeedsCompute;
        _wallPlacementPrecomputeTaskPriorityOrigin = priorityOrigin;
        _wallPlacementPrecomputeTask = Task.Run(() => ComputeWallPlacementRunCountsThreaded(snapshot, _wallPlacementPrecomputeResults, ct), ct);
    }

    private void TryApplyCompletedWallPlacementPrecomputeTask()
    {
        if (_wallPlacementPrecomputeTask == null) return;
        if (!_wallPlacementPrecomputeTask.IsCompleted) return;

        DrainWallPlacementPrecomputeResults();

        _wallPlacementPrecomputeTask = null;
        _hasWallPlacementPrecomputeTaskPriorityOrigin = false;
        if (_wallPlacementPrecomputeCts != null)
        {
            _wallPlacementPrecomputeCts.Dispose();
            _wallPlacementPrecomputeCts = null;
        }
    }

    private void DrainWallPlacementPrecomputeResults()
    {
        while (_wallPlacementPrecomputeResults.TryDequeue(out var entry))
        {
            if (entry.walkabilityVersion != _wallPlacementRunCountCacheWalkabilityVersion) continue;
            _wallPlacementRunCountCache[entry.origin] = entry.counts;
        }
    }

    private void ClearWallPlacementPrecomputeResults()
    {
        while (_wallPlacementPrecomputeResults.TryDequeue(out _)) { }
    }

    private void CancelWallPlacementPrecomputeTask()
    {
        if (_wallPlacementPrecomputeCts != null)
        {
            _wallPlacementPrecomputeCts.Cancel();
            _wallPlacementPrecomputeCts.Dispose();
            _wallPlacementPrecomputeCts = null;
        }

        _wallPlacementPrecomputeTask = null;
        _hasWallPlacementPrecomputeTaskPriorityOrigin = false;
    }

    private bool TryGetHoveredPotentialWallPlacementStartCell(out Vector2Int origin)
    {
        origin = default;
        if (grid == null) return false;
        if (currentState != PICState.Idle) return false;
        Vector3 mousePos = GetMousePosition();
        if (!grid.TryWorldToCell(mousePos, out var idx)) return false;
        if (!grid.IsWallPlacementIndicatorCell(idx)) return false;
        origin = idx;
        return true;
    }

    private bool TryGetHoveredWallPlacementStartCell(out Vector2Int origin)
    {
        origin = default;
        if (!TryGetHoveredPotentialWallPlacementStartCell(out var idx)) return false;
        if (!IsWallPlacementStartCellAvailable(idx)) return false;
        origin = idx;
        return true;
    }

    private WallPlacementPrecomputeSnapshot BuildWallPlacementPrecomputeSnapshot(int walkabilityVersion, bool hasPriorityOrigin, Vector2Int priorityOrigin)
    {
        if (grid == null) return null;

        int w = grid.CellsX;
        int h = grid.CellsY;
        if (w <= 0 || h <= 0) return null;

        var blocked = new bool[w, h];
        var isWall = new bool[w, h];
        var inMazeBounds = new bool[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = new Vector2Int(x, y);
                inMazeBounds[x, y] = grid.IsInMazeBounds(idx);
                if (grid.TryGetCell(x, y, out var cell))
                {
                    blocked[x, y] = cell.IsBlocked;
                    isWall[x, y] = cell.IsWall;
                }
            }
        }

        Pathfinding pf = Pathfinding.instance;
        int minimumForcedGapWidth = pf != null ? pf.MinimumForcedGapWidth : 0;
    float spacing = Mathf.Max(0.0001f, grid.GetSpacing());
    int minimumPlacementCellCount = Mathf.Max(0, Mathf.CeilToInt(grid.MinimumWallPlacementLengthUnits / spacing));

        var allowedByDirection = new bool[4, w, h];
        for (int d = 0; d < WallPlacementDirectionOrder.Length; d++)
        {
            var dir = WallPlacementDirectionOrder[d];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool baseAvailable = inMazeBounds[x, y] && !blocked[x, y];
                    if (!baseAvailable)
                    {
                        allowedByDirection[d, x, y] = false;
                        continue;
                    }

                    bool allowed = pf == null || pf.IsWallPlacementCellAllowed(new Vector2Int(x, y), dir);
                    allowedByDirection[d, x, y] = allowed;
                }
            }
        }

        var starts = new List<Vector2Int>(4);
        var goals = new List<Vector2Int>(4);
        if (pf != null)
        {
            AddEndpointCellsToList(pf.GetPathStarts(), starts, grid);
            AddEndpointCellsToList(pf.GetPathGoals(), goals, grid);
        }

        var candidateOrigins = new List<Vector2Int>(64);
        var candidateDirectionIndices = new List<int>(64);
        var candidates = grid.GetWallPlacementIndicatorCells();
        if (candidates != null)
        {
            if (hasPriorityOrigin && grid.IsWallPlacementIndicatorCell(priorityOrigin) &&
                !_wallPlacementRunCountCache.ContainsKey(priorityOrigin) &&
                grid.TryGetWallPlacementContinuationDirection(priorityOrigin, out var priorityDir))
            {
                candidateOrigins.Add(priorityOrigin);
                candidateDirectionIndices.Add(DirectionToIndex(priorityDir));
            }

            foreach (var origin in candidates)
            {
                if (hasPriorityOrigin && origin == priorityOrigin) continue;
                if (!grid.TryGetWallPlacementContinuationDirection(origin, out var dir)) continue;
                candidateOrigins.Add(origin);
                candidateDirectionIndices.Add(DirectionToIndex(dir));
            }
        }

        return new WallPlacementPrecomputeSnapshot
        {
            width = w,
            height = h,
            minimumForcedGapWidth = minimumForcedGapWidth,
            minimumPlacementCellCount = minimumPlacementCellCount,
            walkabilityVersion = walkabilityVersion,
            blocked = blocked,
            isWall = isWall,
            inMazeBounds = inMazeBounds,
            allowedByDirection = allowedByDirection,
            starts = starts,
            goals = goals,
            candidateOrigins = candidateOrigins,
            candidateDirectionIndices = candidateDirectionIndices,
        };
    }

    private static int DirectionToIndex(GridManager.Direction dir)
    {
        return dir switch
        {
            GridManager.Direction.Right => 0,
            GridManager.Direction.Left => 1,
            GridManager.Direction.Up => 2,
            GridManager.Direction.Down => 3,
            _ => -1,
        };
    }

    private static void AddEndpointCellsToList(IReadOnlyList<Transform> endpoints, List<Vector2Int> target, GridManager gridManager)
    {
        if (endpoints == null || target == null || gridManager == null) return;

        for (int i = 0; i < endpoints.Count; i++)
        {
            var t = endpoints[i];
            if (t == null) continue;
            if (!gridManager.TryWorldToCell(t.position, out var cell)) continue;
            if (!target.Contains(cell)) target.Add(cell);
        }
    }

    private static WallPlacementPrecomputeTaskResult ComputeWallPlacementRunCountsThreaded(WallPlacementPrecomputeSnapshot snapshot, ConcurrentQueue<WallPlacementPrecomputeResultEntry> outputQueue, CancellationToken ct)
    {
        for (int i = 0; i < snapshot.candidateOrigins.Count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var origin = snapshot.candidateOrigins[i];
            int dirIndex = (snapshot.candidateDirectionIndices != null && i < snapshot.candidateDirectionIndices.Count)
                ? snapshot.candidateDirectionIndices[i]
                : -1;

            var counts = new WallPlacementRunCounts();
            if (dirIndex == 0)
            {
                counts.right = ComputeMaxRunForDirection(snapshot, origin, GridManager.Direction.Right, 0, ct);
            }
            else if (dirIndex == 1)
            {
                counts.left = ComputeMaxRunForDirection(snapshot, origin, GridManager.Direction.Left, 1, ct);
            }
            else if (dirIndex == 2)
            {
                counts.up = ComputeMaxRunForDirection(snapshot, origin, GridManager.Direction.Up, 2, ct);
            }
            else if (dirIndex == 3)
            {
                counts.down = ComputeMaxRunForDirection(snapshot, origin, GridManager.Direction.Down, 3, ct);
            }

            counts.canStart = CanStartWallPlacementFromCachedCounts(snapshot, origin, counts, dirIndex);

            outputQueue.Enqueue(new WallPlacementPrecomputeResultEntry
            {
                walkabilityVersion = snapshot.walkabilityVersion,
                origin = origin,
                counts = counts,
            });
        }

        return new WallPlacementPrecomputeTaskResult
        {
            walkabilityVersion = snapshot.walkabilityVersion,
        };
    }

    private static int ComputeMaxRunForDirection(WallPlacementPrecomputeSnapshot snapshot, Vector2Int origin, GridManager.Direction dir, int dirIndex, CancellationToken ct)
    {
        if (!IsPlacementCellAllowedOnSnapshot(snapshot, origin, dirIndex)) return 0;

        var rawRun = new List<Vector2Int>(16);
        Vector2Int step = DirectionToStep(dir);
        Vector2Int cur = origin + step;

        while (IsPlacementCellAllowedOnSnapshot(snapshot, cur, dirIndex))
        {
            rawRun.Add(cur);
            cur += step;
        }

        if (rawRun.Count == 0) return 0;

        var blocked = (bool[,])snapshot.blocked.Clone();
        blocked[origin.x, origin.y] = true;

        if (!ValidateBlockedSnapshot(blocked, snapshot.starts, snapshot.goals, snapshot.minimumForcedGapWidth)) return 0;

        int count = 0;
        for (int i = 0; i < rawRun.Count; i++)
        {
            if (ct.IsCancellationRequested) break;
            var idx = rawRun[i];
            blocked[idx.x, idx.y] = true;
            if (!ValidateBlockedSnapshot(blocked, snapshot.starts, snapshot.goals, snapshot.minimumForcedGapWidth)) break;
            count++;
        }

        return count;
    }

    private static bool CanStartWallPlacementFromCachedCounts(WallPlacementPrecomputeSnapshot snapshot, Vector2Int origin, WallPlacementRunCounts counts, int dirIndex)
    {
        if (dirIndex < 0) return false;
        if (!IsPlacementCellAllowedOnSnapshot(snapshot, origin, dirIndex)) return false;

        int maxAdditionalCells = dirIndex switch
        {
            0 => counts.right,
            1 => counts.left,
            2 => counts.up,
            3 => counts.down,
            _ => 0,
        };

        if (PlacementMeetsLengthRuleOnSnapshot(snapshot, origin, dirIndex, 0)) return true;

        for (int additionalCells = 1; additionalCells <= maxAdditionalCells; additionalCells++)
        {
            if (PlacementMeetsLengthRuleOnSnapshot(snapshot, origin, dirIndex, additionalCells)) return true;
        }

        return false;
    }

    private static bool PlacementMeetsLengthRuleOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int origin, int dirIndex, int additionalCells)
    {
        int totalCells = 1 + Mathf.Max(0, additionalCells);
        if (snapshot.minimumPlacementCellCount <= 0 || totalCells >= snapshot.minimumPlacementCellCount) return true;
        return ConnectsSeparateWallGroupsOnSnapshot(snapshot, origin, dirIndex, additionalCells);
    }

    private static bool ConnectsSeparateWallGroupsOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int origin, int dirIndex, int additionalCells)
    {
        var proposedCells = new HashSet<Vector2Int>(1 + Mathf.Max(0, additionalCells));
        Vector2Int step = dirIndex switch
        {
            0 => Vector2Int.right,
            1 => Vector2Int.left,
            2 => Vector2Int.up,
            3 => Vector2Int.down,
            _ => Vector2Int.zero,
        };

        if (step == Vector2Int.zero) return false;

        proposedCells.Add(origin);
        Vector2Int cur = origin;
        for (int i = 0; i < additionalCells; i++)
        {
            cur += step;
            proposedCells.Add(cur);
        }

        var touchedWallSeeds = new List<Vector2Int>(8);
        var touchedWallSeedSet = new HashSet<Vector2Int>();
        foreach (var cell in proposedCells)
        {
            TryAddTouchedWallSeedOnSnapshot(snapshot, new Vector2Int(cell.x + 1, cell.y), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeedOnSnapshot(snapshot, new Vector2Int(cell.x - 1, cell.y), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeedOnSnapshot(snapshot, new Vector2Int(cell.x, cell.y + 1), proposedCells, touchedWallSeeds, touchedWallSeedSet);
            TryAddTouchedWallSeedOnSnapshot(snapshot, new Vector2Int(cell.x, cell.y - 1), proposedCells, touchedWallSeeds, touchedWallSeedSet);
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
                TryVisitConnectedWallOnSnapshot(snapshot, new Vector2Int(current.x + 1, current.y), proposedCells, visited, queue);
                TryVisitConnectedWallOnSnapshot(snapshot, new Vector2Int(current.x - 1, current.y), proposedCells, visited, queue);
                TryVisitConnectedWallOnSnapshot(snapshot, new Vector2Int(current.x, current.y + 1), proposedCells, visited, queue);
                TryVisitConnectedWallOnSnapshot(snapshot, new Vector2Int(current.x, current.y - 1), proposedCells, visited, queue);
            }
        }

        return false;
    }

    private static void TryAddTouchedWallSeedOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int idx, HashSet<Vector2Int> proposedCells, List<Vector2Int> touchedWallSeeds, HashSet<Vector2Int> touchedWallSeedSet)
    {
        if (proposedCells.Contains(idx)) return;
        if (!IsWallOnSnapshot(snapshot, idx)) return;
        if (!touchedWallSeedSet.Add(idx)) return;
        touchedWallSeeds.Add(idx);
    }

    private static void TryVisitConnectedWallOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int idx, HashSet<Vector2Int> proposedCells, HashSet<Vector2Int> visited, Queue<Vector2Int> queue)
    {
        if (proposedCells.Contains(idx)) return;
        if (visited.Contains(idx)) return;
        if (!IsWallOnSnapshot(snapshot, idx)) return;

        visited.Add(idx);
        queue.Enqueue(idx);
    }

    private static bool IsWallOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int idx)
    {
        if (idx.x < 0 || idx.y < 0 || idx.x >= snapshot.width || idx.y >= snapshot.height) return false;
        return snapshot.isWall[idx.x, idx.y];
    }

    private static bool IsPlacementCellAllowedOnSnapshot(WallPlacementPrecomputeSnapshot snapshot, Vector2Int idx, int dirIndex)
    {
        if (idx.x < 0 || idx.y < 0 || idx.x >= snapshot.width || idx.y >= snapshot.height) return false;
        if (!snapshot.inMazeBounds[idx.x, idx.y]) return false;
        if (snapshot.blocked[idx.x, idx.y]) return false;
        return snapshot.allowedByDirection[dirIndex, idx.x, idx.y];
    }

    private static Vector2Int DirectionToStep(GridManager.Direction dir)
    {
        return dir switch
        {
            GridManager.Direction.Right => Vector2Int.right,
            GridManager.Direction.Left => Vector2Int.left,
            GridManager.Direction.Up => Vector2Int.up,
            GridManager.Direction.Down => Vector2Int.down,
            _ => Vector2Int.zero,
        };
    }

    private static readonly Vector2Int[] PathCardinalSteps =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    private static readonly Vector2Int[] PathDiagonalSteps =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
    };

    private static bool ValidateBlockedSnapshot(bool[,] blocked, List<Vector2Int> starts, List<Vector2Int> goals, int minimumForcedGapWidth)
    {
        int w = blocked.GetLength(0);
        int h = blocked.GetLength(1);

        if (starts == null || goals == null || starts.Count == 0 || goals.Count == 0) return true;

        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            if (!IsInBounds(s.x, s.y, w, h) || blocked[s.x, s.y]) return false;
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (!IsInBounds(g.x, g.y, w, h) || blocked[g.x, g.y]) return false;
        }

        var visited = new bool[w, h];
        var queue = new Queue<Vector2Int>(starts.Count * 2);
        var scratch = new List<Vector2Int>(8);

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
            AppendWalkableNeighbors(cur, blocked, w, h, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var n = scratch[i];
                if (visited[n.x, n.y]) continue;
                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (!visited[g.x, g.y]) return false;
        }

        if (minimumForcedGapWidth > 0)
        {
            int minCutWidth = ComputeStartGoalMinCutWidth(blocked, starts, goals);
            if (minCutWidth <= minimumForcedGapWidth) return false;
        }

        return true;
    }

    private static int ComputeStartGoalMinCutWidth(bool[,] blocked, List<Vector2Int> starts, List<Vector2Int> goals)
    {
        int w = blocked.GetLength(0);
        int h = blocked.GetLength(1);

        var isStart = new bool[w, h];
        var isGoal = new bool[w, h];
        var isEndpoint = new bool[w, h];

        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            if (!IsInBounds(s.x, s.y, w, h) || blocked[s.x, s.y]) continue;
            isStart[s.x, s.y] = true;
            isEndpoint[s.x, s.y] = true;
        }

        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i];
            if (!IsInBounds(g.x, g.y, w, h) || blocked[g.x, g.y]) continue;
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
        int inf = int.MaxValue / 4;
        var flow = new DinicMaxFlow(sink + 1);
        var scratch = new List<Vector2Int>(8);

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

                AppendWalkableNeighbors(new Vector2Int(x, y), blocked, w, h, scratch);
                for (int i = 0; i < scratch.Count; i++)
                {
                    var n = scratch[i];
                    int nid = passableIds[n.x, n.y];
                    if (nid < 0) continue;
                    flow.AddEdge(nodeOut, nid * 2, inf);
                }
            }
        }

        return flow.MaxFlow(source, sink, inf);
    }

    private static void AppendWalkableNeighbors(Vector2Int from, bool[,] blocked, int w, int h, List<Vector2Int> result)
    {
        result.Clear();

        for (int i = 0; i < PathCardinalSteps.Length; i++)
        {
            int nx = from.x + PathCardinalSteps[i].x;
            int ny = from.y + PathCardinalSteps[i].y;
            if (!IsInBounds(nx, ny, w, h)) continue;
            if (blocked[nx, ny]) continue;
            result.Add(new Vector2Int(nx, ny));
        }

        for (int i = 0; i < PathDiagonalSteps.Length; i++)
        {
            int dx = PathDiagonalSteps[i].x;
            int dy = PathDiagonalSteps[i].y;
            int nx = from.x + dx;
            int ny = from.y + dy;
            if (!IsInBounds(nx, ny, w, h) || blocked[nx, ny]) continue;

            int ax = from.x + dx;
            int ay = from.y;
            int bx = from.x;
            int by = from.y + dy;
            if (!IsInBounds(ax, ay, w, h) || blocked[ax, ay]) continue;
            if (!IsInBounds(bx, by, w, h) || blocked[bx, by]) continue;

            result.Add(new Vector2Int(nx, ny));
        }
    }

    private static bool IsInBounds(int x, int y, int w, int h)
    {
        return x >= 0 && y >= 0 && x < w && y < h;
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

        public int MaxFlow(int source, int sink, int cap)
        {
            int total = 0;
            while (BuildLevelGraph(source, sink))
            {
                System.Array.Clear(_iter, 0, _iter.Length);
                while (total < cap)
                {
                    int pushed = SendFlow(source, sink, cap - total);
                    if (pushed <= 0) break;
                    total += pushed;
                }
                if (total >= cap) break;
            }

            return total;
        }

        private bool BuildLevelGraph(int source, int sink)
        {
            for (int i = 0; i < _level.Length; i++) _level[i] = -1;

            var q = new Queue<int>();
            _level[source] = 0;
            q.Enqueue(source);

            while (q.Count > 0)
            {
                int v = q.Dequeue();
                var edges = _graph[v];
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    if (e.capacity <= 0 || _level[e.to] >= 0) continue;
                    _level[e.to] = _level[v] + 1;
                    if (e.to == sink) return true;
                    q.Enqueue(e.to);
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
                if (e.capacity <= 0 || _level[e.to] != _level[v] + 1) continue;

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

    private void CommitWallPlacementIfAny()
    {
        if (grid == null)
        {
            CancelWallPlacementAndRefund();
            return;
        }

        var cellsToCommit = new List<Vector2Int>(_wallPlacementPreview.Count + 1)
        {
            _wallPlacementOrigin
        };

        for (int i = 0; i < _wallPlacementPreview.Count; i++)
        {
            cellsToCommit.Add(_wallPlacementPreview[i]);
        }

        if (_wallPlacementCancelRequested)
        {
            CancelWallPlacementAndRefund();
            return;
        }

        if (!grid.CanFinalizeWallPlacement(cellsToCommit))
        {
            CancelWallPlacementAndRefund();
            return;
        }

        int wallCost = GetWallPlacementCost(cellsToCommit.Count);
        if (!CanAffordWallPlacementCost(wallCost))
        {
            CancelWallPlacementAndRefund();
            return;
        }

        if (grid.TryCommitWallPlacement(cellsToCommit))
        {
            _wallPlacementChargedCost = 0;
            CancelWallPlacement();
            return;
        }

        CancelWallPlacementAndRefund();
    }

    private void WallPlacementIdleHover(Vector3 mousePos)
    {
        if (grid == null) return;
        UpdateWallPlacementIndicatorsDesiredAlpha(mousePos);

        if (IsWaveCurrentlyActive()) return;
        if (hideWallStartIndicatorsWhenUnaffordable && !CanAffordAnyWallPlacement()) return;

        if (!Input.GetKeyDown(interactKey)) return;
        if (!grid.TryWorldToCell(mousePos, out var idx)) return;
        if (!IsWallPlacementStartCellAvailable(idx)) return;

        BeginWallPlacement(idx);
    }

    private void PlacingWallUpdate()
    {
        if (grid == null || !grid.wallPlacementEnabled)
        {
            CancelWallPlacementAndRefund();
            return;
        }

        Vector3 mousePos = GetMousePosition();
        UpdateWallPlacementPreviewIndicators(mousePos);
        SyncWallPlacementReservedCurrency();
        UpdateWallPlacementCostText(mousePos);

        if (Input.GetKeyUp(interactKey))
        {
            CommitWallPlacementIfAny();
        }
    }

    private void UpdatePlacementIndicatorsVisuals()
    {
        if (_placementIndicators.Count == 0) return;

        float t = 1f - Mathf.Exp(-Mathf.Max(0f, placementIndicatorAlphaLerpSpeed) * Time.deltaTime);

        foreach (var kvp in _placementIndicators)
        {
            var state = kvp.Value;
            if (state == null || state.root == null || state.lineRenderer == null) continue;

            state.currentAlpha = Mathf.Lerp(state.currentAlpha, state.desiredAlpha, t);
            SetPlacementIndicatorAlpha(state, state.currentAlpha);

            bool visible = state.currentAlpha > 0.001f || state.desiredAlpha > 0.001f;
            if (state.root.activeSelf != visible)
            {
                state.root.SetActive(visible);
            }
        }
    }

    private void DestroyPlacementIndicators()
    {
        foreach (var kvp in _placementIndicators)
        {
            var state = kvp.Value;
            if (state != null && state.root != null)
            {
                Destroy(state.root);
            }
        }

        _placementIndicators.Clear();
        _placementIndicatorsTouched.Clear();
    }

    private bool IsCellValidForTower(Vector2Int cellIdx)
    {
        if (grid == null) return false;

        if (!towersCanOnlyBePlacedOnWalls || allowPlaceAnywhereByAutoWall)
        {
            // Allow any in-bounds non-occupied cell.
            if (!grid.IsInMazeBounds(cellIdx)) return false;
            if (cellIdx.x < 0 || cellIdx.y < 0 || cellIdx.x >= grid.CellsX || cellIdx.y >= grid.CellsY) return false;
            if (grid.TryGetTowerAtCell(cellIdx, out var existing) && existing != null) return false;
            return true;
        }

        if (!grid.IsWallAtCell(cellIdx)) return false;

        if (grid.TryGetCell(cellIdx.x, cellIdx.y, out var wallCell) && !wallCell.TowerPlacementEnabled) return false;

        if (grid.TryGetTowerAtCell(cellIdx, out var existing2) && existing2 != null) return false;

        return true;
    }

    private bool TryGetRandomValidCell(out Vector2Int cellIdx)
    {
        cellIdx = default;
        if (grid == null) return false;

        int xMin = 0;
        int yMin = 0;
        int xMax = grid.CellsX;
        int yMax = grid.CellsY;

        if (grid.TryGetMazeBounds(out var bounds))
        {
            xMin = Mathf.Clamp(bounds.xMin, 0, grid.CellsX);
            yMin = Mathf.Clamp(bounds.yMin, 0, grid.CellsY);
            xMax = Mathf.Clamp(bounds.xMax, 0, grid.CellsX);
            yMax = Mathf.Clamp(bounds.yMax, 0, grid.CellsY);
        }

        bool found = false;
        int candidates = 0;
        Vector2Int chosen = default;

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                var idx = new Vector2Int(x, y);
                if (!IsCellValidForTower(idx)) continue;

                candidates++;
                if (!found || Random.Range(0, candidates) == 0)
                {
                    chosen = idx;
                    found = true;
                }
            }
        }

        if (!found) return false;
        cellIdx = chosen;
        return true;
    }

    private void PlaceHeldTowerIntoCell(Vector2Int cell)
    {
        if (_heldTower == null) return;
        if (grid == null) return;

        // If place-anywhere is enabled, ensure the underlying cell is a wall.
        if (!towersCanOnlyBePlacedOnWalls || allowPlaceAnywhereByAutoWall)
        {
            grid.TrySetWallAtCell(cell, isWall: true);
        }

        Vector3 center = grid.GetCellWorldCenter(cell.x, cell.y);
        _heldTower.transform.position = center;
        grid.SetTowerAtCell(cell, _heldTower.GetTower());

        var t = _heldTower.GetTower();
        if (t != null) t.CurrentState = Tower.State.Placed;

        // Keep TowerManager registry in sync (TagManager derives from it).
        if (t != null && TowerManager.instance != null)
        {
            TowerManager.instance.OnTowerPlaced(t);
        }

        if (pickupDropVfx != null)
        {
            pickupDropVfx.transform.position = _heldTower.transform.position;
            pickupDropVfx.Play();
        }

        var rm = _heldTower.GetComponentInChildren<RangeManager>();
        if (rm != null) rm.HideRangeVisualization();

        _heldTower = null;
        currentState = PICState.Idle;
    }

    private bool TryFindNearestValidCell(Vector2Int centerCell, out Vector2Int best)
    {
        best = default;
        if (grid == null) return false;

        int r = Mathf.Max(0, placementCellRadius);
        bool found = false;
        float bestDist2 = float.MaxValue;

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                var idx = new Vector2Int(centerCell.x + dx, centerCell.y + dy);
                if (idx.x < 0 || idx.y < 0 || idx.x >= grid.CellsX || idx.y >= grid.CellsY) continue;
                if (!IsCellValidForTower(idx)) continue;

                float d2 = dx * dx + dy * dy;
                if (!found || d2 < bestDist2)
                {
                    found = true;
                    bestDist2 = d2;
                    best = idx;
                }
            }
        }

        return found;
    }

    private void UpdateLastValidCellUnderMouse()
    {
        _hasLastValidCell = false;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null) return;

        Vector3 mousePos = GetMousePosition();

        // If the mouse is off-grid, choose the nearest in-bounds cell as the search center.
        Vector2Int centerIdx;
        if (!grid.TryWorldToCell(mousePos, out centerIdx))
        {
            float spacing = Mathf.Max(0.0001f, grid.GetSpacing());

            // Convert to approximate grid coordinate and ROUND to nearest cell (not floor).
            Vector2 world = new Vector2(mousePos.x, mousePos.y);
            int approxX = Mathf.RoundToInt(world.x / spacing);
            int approxY = Mathf.RoundToInt(world.y / spacing);

            centerIdx = new Vector2Int(
                Mathf.Clamp(approxX,0, grid.CellsX -1),
                Mathf.Clamp(approxY,0, grid.CellsY -1));
        }

        if (!TryFindNearestValidCell(centerIdx, out var bestIdx)) return;

        _hasLastValidCell = true;
        _lastValidCell = bestIdx;
        _lastValidCellCenter = grid.GetCellWorldCenter(bestIdx.x, bestIdx.y);
    }

    private static TowerInteractable AsTowerInteractable(Interactable i)
    {
        return i != null ? i.GetComponent<TowerInteractable>() : null;
    }

    private static Enemy AsEnemy(Interactable i)
    {
        return i != null ? i.GetComponent<Enemy>() : null;
    }

    private static Relic AsRelicInteractable(Interactable i)
    {
        return i != null ? i.GetComponent<Relic>() : null;
    }

    private static UpgradeInteractable AsUpgradeInteractable(Interactable i)
    {
        return i != null ? i.GetComponent<UpgradeInteractable>() : null;
    }

    private static void UpdateWolfPackRelicHoverVisualization(Interactable hovered)
    {
        if (RM.i == null)
        {
            return;
        }

        var relic = AsRelicInteractable(hovered);
        bool shouldShow = relic != null
            && relic.id == RM.ID.wolfPack
            && RM.i != null
            && RM.i.Active(RM.ID.wolfPack);

        RM.i.SetWolfPackHoverVisualizationVisible(shouldShow);
    }

    private TowerInteractable GetPlacedTowerUnderMouse(Vector3 mousePos)
    {
        int mask = LayerMaskManager.instance != null ? LayerMaskManager.instance.PICLayermask : ~0;
        var hits = Physics2D.OverlapPointAll(mousePos, mask);
        if (hits == null || hits.Length == 0) return null;

        TowerInteractable best = null;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            var ti = h.GetComponentInParent<TowerInteractable>();
            if (ti == null) continue;

            var tower = ti.GetTower();
            if (tower == null || tower.CurrentState != Tower.State.Placed) continue;

            float d2 = ((Vector2)h.ClosestPoint(mousePos) - (Vector2)mousePos).sqrMagnitude;
            if (best == null || d2 < bestD2)
            {
                best = ti;
                bestD2 = d2;
            }
        }

        return best;
    }

    // TowerInformationDisplay (TID) rules:
    // 1) If TID is hidden and no tower is clicked, hovering a tower shows TID for that tower.
    // 2) Clicking a tower pins TID to that tower (and replaces any previously clicked tower).
    // 3) While a tower is clicked/pinned, hovering other towers does not change TID.
    // 4) While hovering the TID UI, all other PIC interactions are disabled.
    // 5) A "click" is an interact press+release that does NOT result in picking up the tower.

    private static TowerInformationDisplay GetTID() => TowerInformationDisplay.instance;
    private static RelicInformationDisplay GetRID() => RelicInformationDisplay.instance;
    private static EnemyInformationDisplay GetEID() => EnemyInformationDisplay.instance;

    private RelicShopManager GetRelicShopManager()
    {
        return RelicShopManager.instance;
    }

    private bool CanPickUpTowerInteractable(TowerInteractable ti)
    {
        if (ti == null) return false;

        // Explicit per-tower pickup flag.
        if (!ti.pickupable) return false;

        var t = ti.GetTower();
        if (t == null) return false;

        var tm = TowerManager.instance;
        if (tm == null) return true;

        // Shop tower: block purchase only when both inventory slots and placed slots are all full.
        if (t.CurrentState == Tower.State.Shop)
            return tm.TowerCanBePurchasedFromShop();

        // Already placed towers can always be repositioned.
        if (t.CurrentState == Tower.State.Placed)
            return true;

        // Inventory tower: allow pickup when it can still be returned to inventory or placed.
        return tm.TowerCanBePickedUpFromInventory();
    }

    public void IdlePICUpdate()
    {
        // Disable world interactions while hovering the TowerInformationDisplay UI.
        var tid = GetTID();
        var rid = GetRID();
        var eid = GetEID();
        if (tid != null && tid.PlayerMouseHovering())
        {
            UpdateWolfPackRelicHoverVisualization(null);

            if (_isHoldingForPickup) CancelPickupHold();
            if (_isHoldingForRelicPurchase) CancelRelicPurchaseHold();
            if (_isHoldingForUpgradePickup) CancelUpgradePickupHold();

            if (hoveredInteractable != null)
            {
                hoveredInteractable.InteractableOnMouseExit();
                hoveredInteractable = null;

            }

            return;
        }

        // Disable world interactions while hovering the RelicInformationDisplay UI.
        if (rid != null && rid.PlayerMouseHovering())
        {
            UpdateWolfPackRelicHoverVisualization(null);

            if (_isHoldingForPickup) CancelPickupHold();
            if (_isHoldingForRelicPurchase) CancelRelicPurchaseHold();
            if (_isHoldingForUpgradePickup) CancelUpgradePickupHold();

            if (hoveredInteractable != null)
            {
                hoveredInteractable.InteractableOnMouseExit();
                hoveredInteractable = null;
            }

            return;
        }

        // Resolve hovered interactable under mouse.
        Vector3 mousePos = GetMousePosition();
        int mask = LayerMaskManager.instance != null ? LayerMaskManager.instance.PICLayermask : ~0;
        var hits = Physics2D.OverlapPointAll(mousePos, mask);

        Interactable nextHovered = null;
        if (hits != null && hits.Length > 0)
        {
            // Prefer the closest interactable collider (handles overlaps reliably).
            float bestD2 = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                var inter = h.GetComponent<Interactable>();
                if (inter == null) continue;
                float d2 = ((Vector2)h.ClosestPoint(mousePos) - (Vector2)mousePos).sqrMagnitude;
                if (nextHovered == null || d2 < bestD2)
                {
                    nextHovered = inter;
                    bestD2 = d2;
                }
            }
        }

        if (hoveredInteractable != nextHovered)
        {
            if (hoveredInteractable != null)
            {
                hoveredInteractable.InteractableOnMouseExit();
            }
            hoveredInteractable = nextHovered;
            if (hoveredInteractable != null)
            {
                hoveredInteractable.InteractableOnMouseEnter();
            }
        }

        if (hoveredInteractable != null)
        {
            hoveredInteractable.OnMouseStay();
        }

        UpdateWolfPackRelicHoverVisualization(hoveredInteractable);

        // Hover-to-show tower info (only when not pinned).
        if (showTowerInformationOnHover && !_towerInfoPinned && tid != null)
        {
            var ti = AsTowerInteractable(hoveredInteractable);
            if (ti != null)
            {
                if (_hoverTowerInfo != ti)
                {
                    tid.DisplayTowerInformation(ti);
                    _hoverTowerInfo = ti;
                }
            }
            else
            {
                if (_hoverTowerInfo != null)
                {
                    tid.HideTowerInformation();
                    _hoverTowerInfo = null;
                }
            }
        }

        // Hover-to-show relic info.
        if (showRelicInformationOnHover && rid != null)
        {
            var ri = AsRelicInteractable(hoveredInteractable);
            if (ri != null)
            {
                if (_hoverRelicInfo != ri)
                {
                    rid.DisplayRelicInformation(ri);
                    _hoverRelicInfo = ri;
                }
            }
            else
            {
                if (_hoverRelicInfo != null)
                {
                    rid.HideRelicInformation();
                    _hoverRelicInfo = null;
                }
            }
        }

        if (_isHoldingForRelicPurchase)
        {
            var hoveredRelic = AsRelicInteractable(hoveredInteractable);
            var shop = GetRelicShopManager();

            if (_relicPickupCandidate == null || hoveredRelic != _relicPickupCandidate || shop == null || !shop.CanPurchaseRelic(_relicPickupCandidate))
            {
                CancelRelicPurchaseHold();
                return;
            }

            UpdateRelicPurchaseRadial();

            if (Time.time - _relicPickupHoldStartTime >= Mathf.Max(0.001f, GetPickupTimeForInteractable(_relicPickupCandidate)))
            {
                PerformRelicPurchase(_relicPickupCandidate);
                return;
            }

            if (Input.GetKeyUp(interactKey))
            {
                CancelRelicPurchaseHold();
            }

            return;
        }

        if (_isHoldingForUpgradePickup)
        {
            if (_upgradePickupCandidate == null || hoveredInteractable != _upgradePickupCandidate)
            {
                CancelUpgradePickupHold();
            }
            else
            {
                UpdateUpgradePickupRadial();

                if (Time.time - _upgradePickupHoldStartTime >= Mathf.Max(0.001f, GetPickupTimeForInteractable(_upgradePickupCandidate)))
                {
                    PerformUpgradePickup(_upgradePickupCandidate);
                    return;
                }

                if (Input.GetKeyUp(interactKey))
                {
                    CancelUpgradePickupHold();
                }
            }

            return;
        }

        if (_isHoldingForPickup)
        {
            if (_pickupCandidate == null || hoveredInteractable != _pickupCandidate)
            {
                CancelPickupHold();
            }
            else
            {
                UpdatePickupRadial();

                if (Time.time - _pickupHoldStartTime >= Mathf.Max(0.001f, GetPickupTimeForInteractable(_pickupCandidate)))
                {
                    PerformPickup(_pickupCandidate);
                    return;
                }

                if (Input.GetKeyUp(interactKey))
                {
                    var ti = _pickupCandidate;
                    CancelPickupHold();

                    if (ti != null)
                    {
                        ti.ClickSelect();
                        if (tid != null)
                        {
                            tid.DisplayTowerInformation(ti);
                            _towerInfoPinned = true;
                            _hoverTowerInfo = ti;
                        }
                    }
                }
            }
        }
        else
        {
            // Begin hold-to-pickup on press.
            if (Input.GetKeyDown(interactKey))
            {
                var ti = AsTowerInteractable(hoveredInteractable);

                if (ti != null)
                {
                     ti.GetTower().OnClick();
                    if (!CanPickUpTowerInteractable(ti))
                    {
                        var tower = ti.GetTower();
                        if (tower != null) tower.Indicate(Color.red);

                        // Still allow regular click behavior even when pickup is blocked.
                        ti.ClickSelect();
                        if (tid != null)
                        {
                            tid.DisplayTowerInformation(ti);
                            _towerInfoPinned = true;
                            _hoverTowerInfo = ti;
                        }
                        return;
                    }

                    BeginPickupHold(ti);
                }
                else
                {
                    var ui = AsUpgradeInteractable(hoveredInteractable);
                    if (ui != null)
                    {
                        var upgrade = ui.GetUpgradeItem();
                        if (upgrade != null) upgrade.OnClick();

                        if (!CanPickUpUpgradeInteractable(ui))
                        {
                            if (upgrade != null) upgrade.Indicate(Color.red);
                            return;
                        }

                        BeginUpgradePickupHold(ui);
                        return;
                    }

                    var relic = AsRelicInteractable(hoveredInteractable);
                    if (relic != null)
                    {
                        relic.Flash();
                    }

                    var shop = GetRelicShopManager();
                    if (relic != null && shop != null && shop.IsShopRelic(relic))
                    {
                        if (!shop.CanPurchaseRelic(relic))
                        {
                            relic.Indicate();
                            return;
                        }

                        BeginRelicPurchaseHold(relic);
                        return;
                    }

                    var enemy = GetEnemyUnderMouse();
                    if (enemy != null)
                    {
                        enemy.OnClicked();
                        enemy.Flash();
                        if (eid != null)
                        {
                            eid.DisplayEnemyInformation(enemy);
                        }

                        _towerInfoPinned = false;
                        _hoverTowerInfo = null;
                        if (tid != null && tid.displayed) tid.HideTowerInformation();
                        if (rid != null) rid.HideRelicInformation();
                        _hoverRelicInfo = null;
                        return;
                    }

                    // Clicked empty space: unpin info panel.
                    _towerInfoPinned = false;
                    _hoverTowerInfo = null;
                    if (tid != null && tid.displayed) tid.HideTowerInformation();
                    if (rid != null) rid.HideRelicInformation();
                    if (eid != null) eid.HideEnemyInformation();
                    _hoverRelicInfo = null;
                }
            }
        }
    }

    private static float Dist2(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        return d.sqrMagnitude;
    }

    private int GetNearestInventorySlotIndex(Vector3 worldPos)
    {
        if (towerInventorySlots == null || towerInventorySlots.Count ==0) return -1;

        int best = -1;
        float bestD2 = float.MaxValue;

        for (int i =0; i < towerInventorySlots.Count; i++)
        {
            var t = towerInventorySlots[i];
            if (t == null) continue;
            float d2 = Dist2(worldPos, t.position);
            if (best <0 || d2 < bestD2)
            {
                best = i;
                bestD2 = d2;
            }
        }

        return best;
    }

    private bool IsInventorySlotOccupied(int slotIndex, TowerInteractable ignoreTower = null, UpgradeInteractable ignoreUpgrade = null, TowerInteractable ignoreTowerSecondary = null)
    {
        if (slotIndex <0 || towerInventorySlots == null || slotIndex >= towerInventorySlots.Count) return false;
        var slot = towerInventorySlots[slotIndex];
        if (slot == null) return false;

        const float r =0.15f;
        int mask = LayerMaskManager.instance != null ? LayerMaskManager.instance.PICLayermask : ~0;
        var hits = Physics2D.OverlapCircleAll(slot.position, r, mask);
        for (int i =0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            var ti = h.GetComponentInParent<TowerInteractable>();
            if (ti != null)
            {
                if (ignoreTower != null && ti == ignoreTower) continue;
                if (ignoreTowerSecondary != null && ti == ignoreTowerSecondary) continue;
                return true;
            }

            var ui = h.GetComponentInParent<UpgradeInteractable>();
            if (ui != null)
            {
                if (ignoreUpgrade != null && ui == ignoreUpgrade) continue;
                return true;
            }
        }

        return false;
    }

    private bool TryGetNearestFreeInventorySlot(Vector3 worldPos, out int slotIndex, TowerInteractable ignoreTower = null, UpgradeInteractable ignoreUpgrade = null, TowerInteractable ignoreTowerSecondary = null)
    {
        slotIndex = -1;
        if (towerInventorySlots == null || towerInventorySlots.Count ==0) return false;

        float bestD2 = float.MaxValue;
        for (int i =0; i < towerInventorySlots.Count; i++)
        {
            if (towerInventorySlots[i] == null) continue;
            if (IsInventorySlotOccupied(i, ignoreTower, ignoreUpgrade, ignoreTowerSecondary)) continue;

            float d2 = Dist2(worldPos, towerInventorySlots[i].position);
            if (slotIndex <0 || d2 < bestD2)
            {
                slotIndex = i;
                bestD2 = d2;
            }
        }

        return slotIndex >=0;
    }

    private void PlaceHeldTowerIntoInventorySlot(int slotIndex)
    {
        if (_heldTower == null) return;
        if (towerInventorySlots == null || slotIndex <0 || slotIndex >= towerInventorySlots.Count) return;
        var slot = towerInventorySlots[slotIndex];
        if (slot == null) return;

        var towerToInventory = _heldTower;

        // Clear grid occupancy if somehow still registered.
        if (grid != null && grid.TryWorldToCell(towerToInventory.transform.position, out var idx))
        {
            grid.ClearTowerAtCell(idx, towerToInventory.GetTower());
        }

        var t = towerToInventory.GetTower();
        if (t != null) t.CurrentState = Tower.State.Inventory;

        // Keep TowerManager registry in sync (TagManager derives from it).
        if (t != null && TowerManager.instance != null)
        {
            TowerManager.instance.OnTowerMovedToInventory(t);
        }

        // Hide range viz.
        var rm = towerToInventory.GetComponent<RangeManager>();
        if (rm == null) rm = towerToInventory.GetComponentInChildren<RangeManager>();
        if (rm != null) rm.HideRangeVisualization();

        LerpTowerToInventorySlot(towerToInventory, slot);

        _heldTower = null;
        currentState = PICState.Idle;
    }

    private void LerpTowerToInventorySlot(TowerInteractable tower, Transform slot)
    {
        if (tower == null || slot == null) return;
        StartCoroutine(LerpTowerToPositionRoutine(tower.transform, slot.position));
    }

    private IEnumerator LerpTowerToPositionRoutine(Transform movingTransform, Vector3 destination)
    {
        if (movingTransform == null) yield break;

        while (movingTransform != null && Dist2(movingTransform.position, destination) > 0.0001f)
        {
            float t = 1f - Mathf.Exp(-20f * Time.deltaTime);
            movingTransform.position = Vector3.Lerp(movingTransform.position, destination, t);
            yield return null;
        }

        if (movingTransform != null)
        {
            movingTransform.position = destination;
            if (pickupDropVfx != null)
            {
                pickupDropVfx.transform.position = destination;
                pickupDropVfx.Play();
            }
        }
    }

    private static bool TryGetTowerInteractableForTower(Tower tower, out TowerInteractable towerInteractable)
    {
        towerInteractable = null;
        if (tower == null) return false;

        towerInteractable = tower.GetComponent<TowerInteractable>();
        if (towerInteractable == null) towerInteractable = tower.GetComponentInParent<TowerInteractable>();
        if (towerInteractable == null) towerInteractable = tower.GetComponentInChildren<TowerInteractable>();
        return towerInteractable != null;
    }

    private void ClearSwapPreview()
    {
        _swapPreviewTower = null;
        _swapPreviewCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    private bool TryGetSwapPreviewAtWorldPosition(Vector3 worldPos, out Vector2Int cellIdx, out TowerInteractable towerInteractable)
    {
        cellIdx = new Vector2Int(int.MinValue, int.MinValue);
        towerInteractable = null;

        if (_heldTower == null || currentState != PICState.PlacingTower || grid == null) return false;

        TowerManager tm = TowerManager.instance;
        if (tm == null) return false;
        if (tm.GetCurrentPlacedTowers() != tm.GetMaximumPlacedTowers()) return false;

        if (!grid.TryWorldToCell(worldPos, out cellIdx)) return false;
        if (!grid.TryGetTowerAtCell(cellIdx, out var towerInCell) || towerInCell == null) return false;

        Tower heldTower = _heldTower.GetTower();
        if (towerInCell == heldTower) return false;
        if (towerInCell.CurrentState != Tower.State.Placed) return false;
        if (!TryGetTowerInteractableForTower(towerInCell, out towerInteractable)) return false;

        int invSlot = -1;
        if (!TryGetNearestFreeInventorySlot(towerInCell.transform.position, out invSlot, _heldTower, _heldUpgrade, towerInteractable)) return false;
        if (towerInventorySlots == null || invSlot < 0 || invSlot >= towerInventorySlots.Count) return false;
        if (towerInventorySlots[invSlot] == null) return false;

        return true;
    }

    private void UpdateSwapPreviewVisual(Vector3 mousePos)
    {
        if (!TryGetSwapPreviewAtWorldPosition(mousePos, out var hoverCellIdx, out var towerInCellInteractable))
        {
            ClearSwapPreview();
            return;
        }

        _swapPreviewTower = towerInCellInteractable;
        _swapPreviewCell = hoverCellIdx;
    }

    private bool TrySwapHeldTowerWithPlacedTowerAtCell(Vector2Int cellIdx)
    {
        if (_heldTower == null) return false;
        if (grid == null) return false;

        var tm = TowerManager.instance;
        if (tm == null) return false;
        if (tm.GetCurrentPlacedTowers() != tm.GetMaximumPlacedTowers()) return false;

        if (!grid.TryGetTowerAtCell(cellIdx, out var towerInCell) || towerInCell == null) return false;

        var heldTowerComponent = _heldTower.GetTower();
        if (towerInCell == heldTowerComponent) return false;
        if (towerInCell.CurrentState != Tower.State.Placed) return false;

        if (!TryGetTowerInteractableForTower(towerInCell, out var towerInCellInteractable)) return false;

        int invSlot = -1;
        if (!TryGetNearestFreeInventorySlot(towerInCell.transform.position, out invSlot, _heldTower, _heldUpgrade, towerInCellInteractable)) return false;
        if (towerInventorySlots == null || invSlot < 0 || invSlot >= towerInventorySlots.Count) return false;
        var slot = towerInventorySlots[invSlot];
        if (slot == null) return false;

        grid.ClearTowerAtCell(cellIdx, towerInCell);

        towerInCell.OnPickedUp();
        towerInCell.CurrentState = Tower.State.Inventory;
        tm.OnTowerMovedToInventory(towerInCell);

        var rm = towerInCellInteractable.GetComponent<RangeManager>();
        if (rm == null) rm = towerInCellInteractable.GetComponentInChildren<RangeManager>();
        if (rm != null) rm.HideRangeVisualization();

        LerpTowerToInventorySlot(towerInCellInteractable, slot);

        PlaceHeldTowerIntoCell(cellIdx);
        return true;
    }

    private bool TryMoveHeldTowerToInventoryIfNoPlacementCapacity()
    {
        if (_heldTower == null) return false;
        if (TowerManager.instance == null) return false;
        if (TowerManager.instance.GetCurrentPlacedTowers() < TowerManager.instance.GetMaximumPlacedTowers()) return false;

        int invSlot = -1;
        if (!TryGetNearestFreeInventorySlot(_heldTower.transform.position, out invSlot, _heldTower, _heldUpgrade)) return false;

        PlaceHeldTowerIntoInventorySlot(invSlot);
        return true;
    }

    private void PlacingTowerUpdate()
    {
        if (_isHoldingForPickup) CancelPickupHold();
        if (_isHoldingForRelicPurchase) CancelRelicPurchaseHold();
        if (_isHoldingForUpgradePickup) CancelUpgradePickupHold();

        if (_heldTower == null)
        {
            currentState = PICState.Idle;
            ClearPlacementIndicatorDesiredAlpha();
            return;
        }

        Vector3 mousePos = GetMousePosition();
        UpdateLastValidCellUnderMouse();

        int snapInvSlot = -1;
        bool hasFreeSnapInv = TryGetNearestFreeInventorySlot(mousePos, out snapInvSlot, _heldTower, _heldUpgrade);
        float snapInvD2 = hasFreeSnapInv && towerInventorySlots != null && snapInvSlot >=0 && snapInvSlot < towerInventorySlots.Count && towerInventorySlots[snapInvSlot] != null
            ? Dist2(mousePos, towerInventorySlots[snapInvSlot].position)
            : float.MaxValue;
        float maxSnapD2 = Mathf.Max(0f, maxInventorySnapDistance);
        maxSnapD2 *= maxSnapD2;

        if (!hasFreeSnapInv || snapInvD2 > maxSnapD2)
        {
            hasFreeSnapInv = false;
            snapInvD2 = float.MaxValue;
        }
        float snapCellD2 = _hasLastValidCell ? Dist2(mousePos, _lastValidCellCenter) : float.MaxValue;

        Vector3 targetPos;
        if (hasFreeSnapInv && snapInvD2 < snapCellD2)
        {
            targetPos = towerInventorySlots[snapInvSlot].position;
        }
        else if (_hasLastValidCell)
        {
            // If the mouse is outside the grid bounds, follow the mouse; otherwise snap to cell.
            if (grid != null && grid.TryWorldToCell(mousePos, out _))
            {
                targetPos = _lastValidCellCenter;
            }
            else
            {
                targetPos = mousePos;
            }
        }
        else
        {
            // No valid cell found: still follow the mouse.
            targetPos = mousePos;
        }

        UpdatePlacementIndicatorsDesiredAlpha(targetPos);
        UpdateSwapPreviewVisual(targetPos);

        float lerpT = 1f - Mathf.Exp(-20f * Time.deltaTime);
        _heldTower.transform.position = Vector3.Lerp(_heldTower.transform.position, targetPos, lerpT);

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float xSpeed = (_heldTower.transform.position.x - _heldTowerPrevPosition.x) / dt;
        float targetTilt = Mathf.Clamp(-xSpeed * heldTowerTiltDegreesPerUnitPerSecond, -heldTowerMaxTiltDegrees, heldTowerMaxTiltDegrees);
        float tiltLerpT = 1f - Mathf.Exp(-Mathf.Max(0f, heldTowerTiltLerpSpeed) * dt);
        _heldTowerTiltAngle = Mathf.Lerp(_heldTowerTiltAngle, targetTilt, tiltLerpT);
        var dragEuler = _heldTower.transform.eulerAngles;
        _heldTower.transform.eulerAngles = new Vector3(dragEuler.x, dragEuler.y, _heldTowerTiltAngle);
        _heldTowerPrevPosition = _heldTower.transform.position;

        // Rotate range manager only.
        if (Input.GetKeyDown(rotateLeftKey))
        {
            _heldRangeRotationDeg -= rotationStepDegrees;
            _heldTower.GetTower().OnRotate(-1);
            ApplyHeldRangeRotation();
        }
        if (Input.GetKeyDown(rotateRightKey))
        {
            _heldRangeRotationDeg += rotationStepDegrees;
            ApplyHeldRangeRotation();
            _heldTower.GetTower().OnRotate(1);
        }

        // Drop
        if (Input.GetKeyUp(interactKey))
        {
            // Re-evaluate placement at drop time.
            UpdateLastValidCellUnderMouse();
            Vector3 dropMousePos = GetMousePosition();

            // If max placed towers are reached, prefer inventory placement first.
            if (TowerManager.instance != null && TowerManager.instance.GetCurrentPlacedTowers() >= TowerManager.instance.GetMaximumPlacedTowers())
            {
                if (grid != null && grid.TryWorldToCell(dropMousePos, out var dropCellIdx))
                {
                    if (TrySwapHeldTowerWithPlacedTowerAtCell(dropCellIdx))
                    {
                        return;
                    }
                }

                int invSlotAtDrop = -1;
                if (TryGetNearestFreeInventorySlot(dropMousePos, out invSlotAtDrop, _heldTower, _heldUpgrade))
                {
                    PlaceHeldTowerIntoInventorySlot(invSlotAtDrop);
                    return;
                }

                // No inventory room either: don't buy/place a new tower.
                if (_heldTower.GetTower() != null && _heldTower.GetTower().CurrentState == Tower.State.Shop)
                {
                    var tower = _heldTower.GetTower();
                    if (tower != null) tower.Indicate(Color.red);
                }
                return;
            }

            // Decide between inventory vs grid cell based on distance.
            int invSlot = -1;
            bool hasFreeInv = TryGetNearestFreeInventorySlot(dropMousePos, out invSlot, _heldTower, _heldUpgrade);
            float invD2 = hasFreeInv && towerInventorySlots != null && invSlot >= 0 && invSlot < towerInventorySlots.Count && towerInventorySlots[invSlot] != null
                ? Dist2(dropMousePos, towerInventorySlots[invSlot].position)
                : float.MaxValue;

            // Only consider inventory placement-by-proximity if we're within snap range.
            if (!hasFreeInv || invD2 > maxSnapD2)
            {
                hasFreeInv = false;
                invD2 = float.MaxValue;
            }
            float cellD2 = _hasLastValidCell ? Dist2(dropMousePos, _lastValidCellCenter) : float.MaxValue;

            // If inventory slot is closer than a valid cell, place into inventory.
            if (hasFreeInv && invD2 < cellD2)
            {
                PlaceHeldTowerIntoInventorySlot(invSlot);
                return;
            }

            bool mouseInBounds = grid != null && grid.TryWorldToCell(dropMousePos, out _);

            // If released off-grid but we have a valid cell, force snap/place.
            if (!mouseInBounds && _hasLastValidCell)
            {
                _heldTower.transform.position = _lastValidCellCenter;
            }

            if (_hasLastValidCell)
            {
                PlaceHeldTowerIntoCell(_lastValidCell);
            }
            else
            {
                // No valid cell at drop time:
                // 1) Try place into any available inventory slot.
                // 2) If no inventory space, place on a random valid cell.
                if (TryGetNearestFreeInventorySlot(dropMousePos, out int freeSlot, _heldTower, _heldUpgrade))
                {
                    PlaceHeldTowerIntoInventorySlot(freeSlot);
                    return;
                }

                if (TryGetRandomValidCell(out var randomCell))
                {
                    PlaceHeldTowerIntoCell(randomCell);
                    return;
                }

                // Ultimate fallback: return to original position if we have it.
                if (_heldTowerOriginalInventorySlot >= 0 && towerInventorySlots != null && _heldTowerOriginalInventorySlot < towerInventorySlots.Count && towerInventorySlots[_heldTowerOriginalInventorySlot] != null)
                {
                    PlaceHeldTowerIntoInventorySlot(_heldTowerOriginalInventorySlot);
                    return;
                }

                if (_heldTowerOriginalCell.x != int.MinValue)
                {
                    PlaceHeldTowerIntoCell(_heldTowerOriginalCell);
                    return;
                }

                // Nothing we can do; drop it where it is.
                var rm = _heldTower.GetComponent<RangeManager>();
                if (rm == null) rm = _heldTower.GetComponentInChildren<RangeManager>();
                if (rm != null) rm.HideRangeVisualization();
                _heldTower = null;
                currentState = PICState.Idle;
            }
        }
    }

    private void ApplyHeldRangeRotation()
    {
        if (_heldTower == null) return;
        var rm = _heldTower.GetComponent<RangeManager>();
        if (rm == null) rm = _heldTower.GetComponentInChildren<RangeManager>();
        if (rm == null) return;
        rm.SetRotation(_heldRangeRotationDeg);
        rm.VisualizeRange();
    }

    private void FixedUpdate()
    {
        if (_heldTower == null) return;

        var rm = _heldTower.GetComponent<RangeManager>();
        if (rm == null) rm = _heldTower.GetComponentInChildren<RangeManager>();
        if (rm != null)
        {
            rm.MeshVisualization();
        }
        
    }

    private void Update()
    {
        debugTransform.position = GetMousePosition();
        forceAllTowersTargetHighlightedEnemy = forceAllTowersTargetHighlightedEnemyOption;
        UpdatePlayerTargetOverrideIndicator();

        bool waveActive = IsWaveCurrentlyActive();
        bool waveStartedThisFrame = waveActive && !_wasWaveActiveLastFrame;
        if (waveStartedThisFrame && (placingWallMode || currentState == PICState.PlacingWall))
        {
            CancelWallPlacementAndRefund();
        }

        UpdateWallPlacementPrecomputeAsync();

        HandleCurrencyHoverPickup();

        switch (currentState)
        {
            case PICState.Idle:
                ClearPlacementIndicatorDesiredAlpha();
                ClearSwapPreview();
                IdlePICUpdate();
                if (grid != null && grid.wallPlacementEnabled && currentState == PICState.Idle)
                {
                    WallPlacementIdleHover(GetMousePosition());
                }
                break;
            case PICState.PlacingTower:
                PlacingTowerUpdate();
                break;
            case PICState.PlacingUpgrade:
                ClearPlacementIndicatorDesiredAlpha();
                ClearSwapPreview();
                PlacingUpgradeUpdate();
                break;
            case PICState.PlacingWall:
                ClearSwapPreview();
                PlacingWallUpdate();
                break;
        }

        UpdatePlacementIndicatorsVisuals();
        _wasWaveActiveLastFrame = waveActive;
    }

    private void UpdatePlayerTargetOverrideIndicator()
    {
        if (playerTargetOverrideIndicator == null) return;
        EnsurePlayerTargetOverrideIndicatorDefaults();

        Enemy highlightedEnemy = forceAllTowersTargetHighlightedEnemy ? GetHighlightedEnemy() : null;
        if (highlightedEnemy == null)
        {
            if (_hasPlayerTargetOverrideIndicatorBaseScale)
            {
                playerTargetOverrideIndicator.localScale = _playerTargetOverrideIndicatorBaseScale;
            }
            ApplyPlayerTargetOverrideIndicatorAlphaSwell(1f);
            if (playerTargetOverrideIndicator.gameObject.activeSelf)
                playerTargetOverrideIndicator.gameObject.SetActive(false);
            return;
        }

        Vector3 targetPos = highlightedEnemy.transform.position;
        targetPos.z = playerTargetOverrideIndicator.position.z;

        if (!playerTargetOverrideIndicator.gameObject.activeSelf)
        {
            playerTargetOverrideIndicator.position = targetPos;
            if (_hasPlayerTargetOverrideIndicatorBaseScale)
            {
                playerTargetOverrideIndicator.localScale = _playerTargetOverrideIndicatorBaseScale;
            }
            ApplyPlayerTargetOverrideIndicatorAlphaSwell(1f);
            playerTargetOverrideIndicator.gameObject.SetActive(true);
            return;
        }

        float t = 1f - Mathf.Exp(-Mathf.Max(0f, playerTargetOverrideIndicatorLerpSpeed) * Time.deltaTime);
        playerTargetOverrideIndicator.position = Vector3.Lerp(playerTargetOverrideIndicator.position, targetPos, t);

        float nearDistance = Mathf.Max(0f, playerTargetOverrideIndicatorNearDistance);
        float distToTarget = Vector2.Distance(playerTargetOverrideIndicator.position, targetPos);
        bool isNearTarget = distToTarget <= nearDistance;

        if (isNearTarget)
        {
            float rotateSpeed = playerTargetOverrideIndicatorRotateDegreesPerSecond;
            if (Mathf.Abs(rotateSpeed) > 0.0001f)
            {
                playerTargetOverrideIndicator.Rotate(0f, 0f, rotateSpeed * Time.deltaTime, Space.Self);
            }

            if (_hasPlayerTargetOverrideIndicatorBaseScale)
            {
                float swellAmp = Mathf.Max(0f, playerTargetOverrideIndicatorSwellAmplitude);
                float swellFreq = Mathf.Max(0f, playerTargetOverrideIndicatorSwellFrequency);
                float swell = 1f + Mathf.Sin(Time.time * swellFreq * Mathf.PI * 2f) * swellAmp;
                float clampedSwell = Mathf.Max(0f, swell);
                playerTargetOverrideIndicator.localScale = _playerTargetOverrideIndicatorBaseScale * clampedSwell;
                ApplyPlayerTargetOverrideIndicatorAlphaSwell(clampedSwell);
            }
        }
        else if (_hasPlayerTargetOverrideIndicatorBaseScale)
        {
            float scaleResetT = 1f - Mathf.Exp(-10f * Time.deltaTime);
            playerTargetOverrideIndicator.localScale = Vector3.Lerp(playerTargetOverrideIndicator.localScale, _playerTargetOverrideIndicatorBaseScale, scaleResetT);
            ApplyPlayerTargetOverrideIndicatorAlphaSwell(1f);
        }
    }

    private void EnsurePlayerTargetOverrideIndicatorDefaults()
    {
        if (playerTargetOverrideIndicator == null) return;
        if (_hasPlayerTargetOverrideIndicatorBaseScale) return;

        _playerTargetOverrideIndicatorBaseScale = playerTargetOverrideIndicator.localScale;
        _hasPlayerTargetOverrideIndicatorBaseScale = true;

        _playerTargetOverrideIndicatorSrc = playerTargetOverrideIndicator.GetComponent<SRC>();
        if (_playerTargetOverrideIndicatorSrc == null)
            _playerTargetOverrideIndicatorSrc = playerTargetOverrideIndicator.GetComponentInChildren<SRC>(true);

        _playerTargetOverrideIndicatorBaseAlphaOverrides.Clear();
        if (_playerTargetOverrideIndicatorSrc != null && _playerTargetOverrideIndicatorSrc.srColorInfos != null)
        {
            for (int i = 0; i < _playerTargetOverrideIndicatorSrc.srColorInfos.Count; i++)
            {
                var info = _playerTargetOverrideIndicatorSrc.srColorInfos[i];
                _playerTargetOverrideIndicatorBaseAlphaOverrides.Add(info.alphaOverride ? info.alphaOverrideValue : -1f);
            }
        }
    }

    private void ApplyPlayerTargetOverrideIndicatorAlphaSwell(float swellScale)
    {
        if (_playerTargetOverrideIndicatorSrc == null) return;
        if (_playerTargetOverrideIndicatorSrc.srColorInfos == null) return;

        int count = Mathf.Min(_playerTargetOverrideIndicatorSrc.srColorInfos.Count, _playerTargetOverrideIndicatorBaseAlphaOverrides.Count);
        if (count <= 0) return;

        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            float baseAlpha = _playerTargetOverrideIndicatorBaseAlphaOverrides[i];
            if (baseAlpha < 0f) continue;

            var info = _playerTargetOverrideIndicatorSrc.srColorInfos[i];
            float targetAlpha = Mathf.Clamp01(baseAlpha * Mathf.Max(0f, swellScale));
            if (Mathf.Abs(info.alphaOverrideValue - targetAlpha) <= 0.0001f) continue;

            info.alphaOverride = true;
            info.alphaOverrideValue = targetAlpha;
            _playerTargetOverrideIndicatorSrc.srColorInfos[i] = info;
            changed = true;
        }

        if (changed)
        {
            _playerTargetOverrideIndicatorSrc.ApplySpriteRendererColors();
        }
    }

    private void BeginPickupHold(TowerInteractable ti)
    {
        _pickupCandidate = ti;
        _pickupHoldStartTime = Time.time;
        _isHoldingForPickup = true;
        UpdatePickupRadial();
    }

    private void CancelPickupHold()
    {
        _pickupCandidate = null;
        _isHoldingForPickup = false;
        _pickupHoldStartTime = 0f;
        UpdatePickupRadial(clear: true);
    }

    private void BeginRelicPurchaseHold(Relic relic)
    {
        _relicPickupCandidate = relic;
        _relicPickupHoldStartTime = Time.time;
        _isHoldingForRelicPurchase = true;
        UpdateRelicPurchaseRadial();
    }

    private void CancelRelicPurchaseHold()
    {
        _relicPickupCandidate = null;
        _relicPickupHoldStartTime = 0f;
        _isHoldingForRelicPurchase = false;
        UpdateRelicPurchaseRadial(clear: true);
    }

    private void BeginUpgradePickupHold(UpgradeInteractable ui)
    {
        _upgradePickupCandidate = ui;
        _upgradePickupHoldStartTime = Time.time;
        _isHoldingForUpgradePickup = true;
        UpdateUpgradePickupRadial();
    }

    private void CancelUpgradePickupHold()
    {
        _upgradePickupCandidate = null;
        _upgradePickupHoldStartTime = 0f;
        _isHoldingForUpgradePickup = false;
        UpdateUpgradePickupRadial(clear: true);
    }

    private void UpdatePickupRadial(bool clear = false)
    {
        if (placementRadialTimer == null) return;

        if (clear || !_isHoldingForPickup || _pickupCandidate == null)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        float req = Mathf.Max(0.001f, GetPickupTimeForInteractable(_pickupCandidate));
        float elapsed = Time.time - _pickupHoldStartTime;
        float showDelay = req * 0.5f;

        if (elapsed < showDelay)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        placementRadialTimer.transform.position = _pickupCandidate.transform.position;
        placementRadialTimer.enabled = true;
        float t = Mathf.Clamp01((elapsed - showDelay) / Mathf.Max(0.0001f, req - showDelay));
        placementRadialTimer.fillAmount = t;
    }

    private void UpdateRelicPurchaseRadial(bool clear = false)
    {
        if (placementRadialTimer == null) return;

        if (clear || !_isHoldingForRelicPurchase || _relicPickupCandidate == null)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        float req = Mathf.Max(0.001f, GetPickupTimeForInteractable(_relicPickupCandidate));
        float elapsed = Time.time - _relicPickupHoldStartTime;
        float showDelay = req * 0.5f;

        if (elapsed < showDelay)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        placementRadialTimer.transform.position = _relicPickupCandidate.transform.position;
        placementRadialTimer.enabled = true;
        float t = Mathf.Clamp01((elapsed - showDelay) / Mathf.Max(0.0001f, req - showDelay));
        placementRadialTimer.fillAmount = t;
    }

    private void UpdateUpgradePickupRadial(bool clear = false)
    {
        if (placementRadialTimer == null) return;

        if (clear || !_isHoldingForUpgradePickup || _upgradePickupCandidate == null)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        float req = Mathf.Max(0.001f, GetPickupTimeForInteractable(_upgradePickupCandidate));
        float elapsed = Time.time - _upgradePickupHoldStartTime;
        float showDelay = req * 0.5f;

        if (elapsed < showDelay)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
            return;
        }

        placementRadialTimer.transform.position = _upgradePickupCandidate.transform.position;
        placementRadialTimer.enabled = true;
        float t = Mathf.Clamp01((elapsed - showDelay) / Mathf.Max(0.0001f, req - showDelay));
        placementRadialTimer.fillAmount = t;
    }

    private void PerformPickup(TowerInteractable ti)
    {
        if (!CanPickUpTowerInteractable(ti))
        {
            var tower = ti != null ? ti.GetTower() : null;
            if (tower != null) tower.Indicate(Color.red);
            CancelPickupHold();
            return;
        }

        CancelPickupHold();

        var tid = GetTID();
        if (tid != null && tid.displayed)
        {
            tid.HideTowerInformation();
        }
        _towerInfoPinned = false;
        _hoverTowerInfo = null;

        if (ti == null) return;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null) return;

        _heldTower = ti;
        _heldTowerLastMeshCell = new Vector2Int(int.MinValue, int.MinValue);

        var heldTowerComp = _heldTower.GetTower();
        bool isRecipeResult = heldTowerComp != null && heldTowerComp.HasCraftingRequirements;

        if (isRecipeResult)
        {
            RecipeDisplay.UntrackPickedUpTower(_heldTower.gameObject);

            var required = heldTowerComp.CraftingRequiredTowers;
            var ingredients = CollectIngredientTowers(required);
            if (RecipeManager.instance != null) RecipeManager.instance.CombineTowers(ingredients, heldTowerComp);

            heldTowerComp.ClearCraftingRequiredTowers();
            heldTowerComp.CurrentState = Tower.State.Inventory;
            if (TowerManager.instance != null) TowerManager.instance.OnTowerMovedToInventory(heldTowerComp);
        }
        else if (heldTowerComp != null && heldTowerComp.CurrentState == Tower.State.Shop)
        {
            int cost = Mathf.Max(0, heldTowerComp.GetCost());
            if (cost > 0 && CurrencyManager.instance != null)
            {
                CurrencyManager.instance.RemoveCurrency(cost);
            }
            TowerShopManager.instance.OnTowerPurchased(heldTowerComp);

            if (TowerManager.instance != null) TowerManager.instance.OnTowerPurchasedFromShop(heldTowerComp);
        }

        _heldTowerOriginalInventorySlot = -1;
        if (!isRecipeResult && towerInventorySlots != null && towerInventorySlots.Count > 0)
        {
            int nearest = GetNearestInventorySlotIndex(_heldTower.transform.position);
            if (nearest >= 0 && towerInventorySlots[nearest] != null)
            {
                const float within = 0.2f;
                if (Vector2.Distance(_heldTower.transform.position, towerInventorySlots[nearest].position) <= within)
                {
                    _heldTowerOriginalInventorySlot = nearest;
                }
            }
        }

        if (pickupDropVfx != null)
        {
            pickupDropVfx.transform.position = _heldTower.transform.position;
            pickupDropVfx.Play();
        }

        if (isRecipeResult)
        {
            _heldTowerOriginalCell = new Vector2Int(int.MinValue, int.MinValue);
        }
        else if (_heldTowerOriginalInventorySlot >= 0)
        {
            _heldTowerOriginalCell = new Vector2Int(int.MinValue, int.MinValue);

            var t = _heldTower.GetTower();
            if (t != null) t.CurrentState = Tower.State.Inventory;
        }
        else if (grid.TryWorldToCell(_heldTower.transform.position, out var origIdx))
        {
            _heldTowerOriginalCell = origIdx;
            grid.ClearTowerAtCell(origIdx, _heldTower.GetTower());

            var t = _heldTower.GetTower();
            if (t != null)
            {
                t.OnPickedUp();
                t.CurrentState = Tower.State.Inventory;
                if (TowerManager.instance != null) TowerManager.instance.OnTowerMovedToInventory(t);
            }
        }
        else
        {
            _heldTowerOriginalCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        var rm = _heldTower.GetComponent<RangeManager>();
        if (rm == null) rm = _heldTower.GetComponentInChildren<RangeManager>();
        if (rm != null)
        {
            _heldRangeRotationDeg = rm.transform.eulerAngles.z;
            rm.VisualizeRange();
        }
        else
        {
            _heldRangeRotationDeg = 0f;
        }

        _hasLastValidCell = false;
        currentState = PICState.PlacingTower;
    }

    private void PerformRelicPurchase(Relic relic)
    {
        var shop = GetRelicShopManager();
        CancelRelicPurchaseHold();

        if (shop == null || relic == null) return;

        if (!shop.TryPurchaseRelic(relic))
        {
            relic.Indicate();
            return;
        }

        var rid = GetRID();
        if (rid != null && rid.displayedRelic == relic)
        {
            rid.HideRelicInformation();
            _hoverRelicInfo = null;
        }
    }

    private void PerformUpgradePickup(UpgradeInteractable ui)
    {
        if (!CanPickUpUpgradeInteractable(ui))
        {
            var upgrade = ui != null ? ui.GetUpgradeItem() : null;
            if (upgrade != null) upgrade.Indicate(Color.red);
            CancelUpgradePickupHold();
            return;
        }

        CancelUpgradePickupHold();

        if (ui == null) return;
        _heldUpgrade = ui;

        _heldUpgradeOriginalInventorySlot = -1;
        if (towerInventorySlots != null && towerInventorySlots.Count > 0)
        {
            int nearest = GetNearestInventorySlotIndex(_heldUpgrade.transform.position);
            if (nearest >= 0 && towerInventorySlots[nearest] != null)
            {
                const float within = 0.2f;
                if (Vector2.Distance(_heldUpgrade.transform.position, towerInventorySlots[nearest].position) <= within)
                {
                    _heldUpgradeOriginalInventorySlot = nearest;
                }
            }
        }

        if (pickupDropVfx != null)
        {
            pickupDropVfx.transform.position = _heldUpgrade.transform.position;
            pickupDropVfx.Play();
        }

        currentState = PICState.PlacingUpgrade;
    }

    private void PlaceHeldUpgradeIntoInventorySlot(int slotIndex)
    {
        if (_heldUpgrade == null) return;
        if (towerInventorySlots == null || slotIndex < 0 || slotIndex >= towerInventorySlots.Count) return;
        var slot = towerInventorySlots[slotIndex];
        if (slot == null) return;

        _heldUpgrade.transform.position = slot.position;

        if (pickupDropVfx != null)
        {
            pickupDropVfx.transform.position = _heldUpgrade.transform.position;
            pickupDropVfx.Play();
        }

        _heldUpgrade = null;
        currentState = PICState.Idle;
    }

    private bool TryApplyHeldUpgradeToTower(TowerInteractable target)
    {
        if (_heldUpgrade == null || target == null) return false;

        var tower = target.GetTower();
        var upgrade = _heldUpgrade.GetUpgradeItem();
        if (tower == null || upgrade == null) return false;
        if (tower.CurrentState != Tower.State.Placed) return false;
        if (tower.IsMaxLevel()) return false;
        if (tower.UpgradeActive(upgrade.uid)) return false;

        tower.ApplyUpgrade(upgrade.uid);

        if (pickupDropVfx != null)
        {
            pickupDropVfx.transform.position = tower.transform.position;
            pickupDropVfx.Play();
        }

        Destroy(_heldUpgrade.gameObject);
        _heldUpgrade = null;
        currentState = PICState.Idle;
        return true;
    }

    private void PlacingUpgradeUpdate()
    {
        if (_isHoldingForPickup) CancelPickupHold();
        if (_isHoldingForRelicPurchase) CancelRelicPurchaseHold();
        if (_isHoldingForUpgradePickup) CancelUpgradePickupHold();

        if (_heldUpgrade == null)
        {
            currentState = PICState.Idle;
            return;
        }

        Vector3 mousePos = GetMousePosition();

        int snapInvSlot = -1;
        bool hasFreeSnapInv = TryGetNearestFreeInventorySlot(mousePos, out snapInvSlot, _heldTower, _heldUpgrade);
        float snapInvD2 = hasFreeSnapInv && towerInventorySlots != null && snapInvSlot >= 0 && snapInvSlot < towerInventorySlots.Count && towerInventorySlots[snapInvSlot] != null
            ? Dist2(mousePos, towerInventorySlots[snapInvSlot].position)
            : float.MaxValue;

        float maxSnapD2 = Mathf.Max(0f, maxInventorySnapDistance);
        maxSnapD2 *= maxSnapD2;

        Vector3 targetPos = mousePos;
        if (hasFreeSnapInv && snapInvD2 <= maxSnapD2)
        {
            targetPos = towerInventorySlots[snapInvSlot].position;
        }

        float lerpT = 1f - Mathf.Exp(-20f * Time.deltaTime);
        _heldUpgrade.transform.position = Vector3.Lerp(_heldUpgrade.transform.position, targetPos, lerpT);

        if (Input.GetKeyUp(interactKey))
        {
            Vector3 dropMousePos = GetMousePosition();

            var placedTower = GetPlacedTowerUnderMouse(dropMousePos);
            if (placedTower != null)
            {
                if (TryApplyHeldUpgradeToTower(placedTower))
                {
                    return;
                }

                var badUpgrade = _heldUpgrade != null ? _heldUpgrade.GetUpgradeItem() : null;
                if (badUpgrade != null) badUpgrade.Indicate(Color.red);
            }

            int invSlot = -1;
            bool hasFreeInv = TryGetNearestFreeInventorySlot(dropMousePos, out invSlot, _heldTower, _heldUpgrade);
            float invD2 = hasFreeInv && towerInventorySlots != null && invSlot >= 0 && invSlot < towerInventorySlots.Count && towerInventorySlots[invSlot] != null
                ? Dist2(dropMousePos, towerInventorySlots[invSlot].position)
                : float.MaxValue;

            if (hasFreeInv && invD2 <= maxSnapD2)
            {
                PlaceHeldUpgradeIntoInventorySlot(invSlot);
                return;
            }

            if (TryGetNearestFreeInventorySlot(dropMousePos, out int freeSlot, _heldTower, _heldUpgrade))
            {
                PlaceHeldUpgradeIntoInventorySlot(freeSlot);
                return;
            }

            if (_heldUpgradeOriginalInventorySlot >= 0 && towerInventorySlots != null && _heldUpgradeOriginalInventorySlot < towerInventorySlots.Count && towerInventorySlots[_heldUpgradeOriginalInventorySlot] != null)
            {
                PlaceHeldUpgradeIntoInventorySlot(_heldUpgradeOriginalInventorySlot);
                return;
            }

            _heldUpgrade = null;
            currentState = PICState.Idle;
        }
    }

    private bool CanPickUpUpgradeInteractable(UpgradeInteractable ui)
    {
        if (ui == null) return false;
        if (!ui.pickupable) return false;
        return ui.GetUpgradeItem() != null;
    }

    private Enemy GetEnemyUnderMouse()
    {
        Vector3 mousePos = GetMousePosition();
        int mask = LayerMaskManager.instance != null ? LayerMaskManager.instance.PICLayermask : ~0;
        var hits = Physics2D.OverlapPointAll(mousePos, mask);
        if (hits == null || hits.Length == 0) return null;

        Enemy best = null;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            float d2 = ((Vector2)h.ClosestPoint(mousePos) - (Vector2)mousePos).sqrMagnitude;
            if (best == null || d2 < bestD2)
            {
                best = enemy;
                bestD2 = d2;
            }
        }

        return best;
    }

    private void HandleCurrencyHoverPickup()
    {
        int currencyMask = LayerMaskManager.instance != null ? LayerMaskManager.instance.currencyLayerMask : 0;
        if (currencyMask == 0) return;
        if (CurrencyManager.instance == null) return;

        Vector3 mousePos = GetMousePosition();
        var hits = Physics2D.OverlapPointAll(mousePos, currencyMask);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            var currencyObj = h.GetComponentInParent<Currency>();
            if (currencyObj == null) continue;

            CurrencyManager.instance.TryPickupCurrency(currencyObj);
        }
    }

    private static List<Tower> CollectIngredientTowers(IReadOnlyList<Tower.ID> required)
    {
        var ingredients = new List<Tower>();
        if (required == null || required.Count == 0) return ingredients;
        if (TowerManager.instance == null) return ingredients;

        var inventory = new List<Tower>(TowerManager.instance.EnumerateTowersInState(Tower.State.Inventory));
        var placed = new List<Tower>(TowerManager.instance.EnumeratePlacedTowers());

        for (int i = 0; i < required.Count; i++)
        {
            var id = required[i];
            var found = FindAndRemoveFirstById(inventory, id) ?? FindAndRemoveFirstById(placed, id);
            if (found != null) ingredients.Add(found);
        }

        return ingredients;
    }

    private static Tower FindAndRemoveFirstById(List<Tower> towers, Tower.ID id)
    {
        if (towers == null) return null;
        for (int i = 0; i < towers.Count; i++)
        {
            var t = towers[i];
            if (t == null) continue;
            if (t.id != id) continue;
            towers.RemoveAt(i);
            return t;
        }
        return null;
    }

    private void Awake()
    {
        instance = this;
        forceAllTowersTargetHighlightedEnemy = forceAllTowersTargetHighlightedEnemyOption;
        EnsurePlayerTargetOverrideIndicatorDefaults();
        if (playerTargetOverrideIndicator != null)
        {
            if (_hasPlayerTargetOverrideIndicatorBaseScale)
            {
                playerTargetOverrideIndicator.localScale = _playerTargetOverrideIndicatorBaseScale;
            }
            ApplyPlayerTargetOverrideIndicatorAlphaSwell(1f);
            playerTargetOverrideIndicator.gameObject.SetActive(false);
        }
        if (placementRadialTimer != null)
        {
            placementRadialTimer.enabled = false;
            placementRadialTimer.fillAmount = 0f;
        }
    }

    private void OnDestroy()
    {
        CancelWallPlacementPrecomputeTask();
        if (instance == this) instance = null;
        if (playerTargetOverrideIndicator != null)
        {
            if (_hasPlayerTargetOverrideIndicatorBaseScale)
            {
                playerTargetOverrideIndicator.localScale = _playerTargetOverrideIndicatorBaseScale;
            }
            playerTargetOverrideIndicator.gameObject.SetActive(false);
        }
        DestroyPlacementIndicators();
        ReleaseWallPlacementCostText();
    }
}
