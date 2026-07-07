using System.Collections.Generic;
using UnityEngine;

public class GridEditor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager grid;
    [Tooltip("Camera used for raycasting. If null, will use Camera.main.")]
    [SerializeField] private Camera raycastCamera;

    [Header("Input")]
    [SerializeField] private KeyCode blockKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode unblockKey = KeyCode.Mouse1;

    [Header("Prefabs")]
    [Tooltip("Prefab with a SpriteRenderer (square) used to visualize blocked cells.")]
    [SerializeField] private GameObject blockedCellPrefab;

    [Header("Overlay Rendering")]
    [SerializeField] private Color hoverColor = new Color(0f,1f,1f,0.25f);
    [Tooltip("Small reduction so the overlay doesn't overlap exact cell bounds.")]
    [SerializeField] private float overlayPadding =0.05f;
    [Tooltip("Offset along grid forward to prevent Z-fighting.")]
    [SerializeField] private float overlayDepthOffset =0.001f;

    [Header("Debug")]
    [SerializeField] private bool logHoveredCell = true;

    public bool wallPlacingEnabled = true;

    private Vector2Int _hoverCell = new Vector2Int(-1, -1);
    private Vector2Int _lastLoggedHoverCell = new Vector2Int(int.MinValue, int.MinValue);
    private bool _hasHover;

    // Blocked visualization instances keyed by cell index.
    private readonly Dictionary<Vector2Int, GameObject> _blockedVisuals = new Dictionary<Vector2Int, GameObject>();

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (raycastCamera == null) raycastCamera = Camera.main;

        RefreshBlockedVisuals();
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (raycastCamera == null) raycastCamera = Camera.main;

        RefreshBlockedVisuals();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        _hasHover = TryGetHoverCell(out _hoverCell);

        if (logHoveredCell)
        {
            if (_hasHover)
            {
                if (_hoverCell != _lastLoggedHoverCell)
                {
                    Debug.Log($"Hover Cell: ({_hoverCell.x}, {_hoverCell.y})", this);
                    _lastLoggedHoverCell = _hoverCell;
                }
            }
            else
            {
                if (_lastLoggedHoverCell.x != int.MinValue)
                {
                    Debug.Log("Hover Cell: (none)", this);
                    _lastLoggedHoverCell = new Vector2Int(int.MinValue, int.MinValue);
                }
            }
        }
        if (_hasHover)
        {
            if (wallPlacingEnabled)
            {
                
                if (Input.GetKey(blockKey))
                {
                    TrySetBlocked(_hoverCell, true);
                }
                else if (Input.GetKeyDown(unblockKey))
                {
                    TrySetBlocked(_hoverCell, false);
                }
            }
            
        }
    }

    private bool TryGetHoverCell(out Vector2Int cell)
    {
        cell = default;
        if (grid == null) return false;

        Camera cam = GetRaycastCamera();
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Grid is defined on its local XY plane, so plane normal is grid.transform.forward.
        Plane plane = new Plane(grid.transform.forward, grid.transform.position);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        return TryWorldToCell(hit, out cell);
    }

    private Camera GetRaycastCamera()
    {
        if (raycastCamera != null) return raycastCamera;
        return Camera.main;
    }

    private bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        cell = default;
        if (grid == null) return false;

        Vector3 local = grid.transform.InverseTransformPoint(world);

        float spacing = GetSpacing();
        float width = grid.CellsX * spacing;
        float height = grid.CellsY * spacing;
        float x0 = -width *0.5f;
        float y0 = -height *0.5f;

        int x = Mathf.FloorToInt((local.x - x0) / spacing);
        int y = Mathf.FloorToInt((local.y - y0) / spacing);

        if (x <0 || y <0 || x >= grid.CellsX || y >= grid.CellsY) return false;

        cell = new Vector2Int(x, y);
        return true;
    }

    private float GetSpacing()
    {
        // GridManager.spacing is private; infer spacing from adjacent cell centers.
        // This assumes uniform spacing.
        if (grid == null) return 1f;

        if (grid.CellsX >=2)
        {
            Vector3 a = grid.GetCellWorldCenter(0,0);
            Vector3 b = grid.GetCellWorldCenter(1,0);
            float d = Vector3.Distance(a, b);
            return d >0.0001f ? d :1f;
        }

        if (grid.CellsY >=2)
        {
            Vector3 a = grid.GetCellWorldCenter(0,0);
            Vector3 b = grid.GetCellWorldCenter(0,1);
            float d = Vector3.Distance(a, b);
            return d >0.0001f ? d :1f;
        }

        return 1f;
    }

    private void TrySetBlocked(Vector2Int cell, bool blocked)
    {
        if (grid == null) return;
        if (!grid.TryGetCell(cell.x, cell.y, out var c)) return;

        if (c.IsBlocked == blocked) return;

        c.IsBlocked = blocked;
        grid.TrySetCell(c);

        // Reset any cached flow maps because walkability changed.
        var pf = FindFirstObjectByType<Pathfinding>();
        if (pf != null) pf.ResetFlowMaps();

        if (blocked) EnsureBlockedVisual(cell);
        else RemoveBlockedVisual(cell);
    }

    private void EnsureBlockedVisual(Vector2Int cell)
    {
        if (blockedCellPrefab == null) return;
        if (_blockedVisuals.ContainsKey(cell)) return;
        if (grid == null || !grid.TryGetCell(cell.x, cell.y, out var c)) return;

        var go = Instantiate(blockedCellPrefab, transform);
        go.name = $"BlockedCell ({cell.x},{cell.y})";

        // Place it at the cell center and orient with the grid.
        go.transform.position = c.WorldCenter + grid.transform.forward * overlayDepthOffset;
        go.transform.rotation = grid.transform.rotation;

        // Scale so a1x1 sprite fits the cell.
        float spacing = GetSpacing();
        float size = Mathf.Max(0.001f, spacing - overlayPadding);
        go.transform.localScale = new Vector3(size, size,1f);

        _blockedVisuals[cell] = go;
    }

    private void RemoveBlockedVisual(Vector2Int cell)
    {
        if (!_blockedVisuals.TryGetValue(cell, out var go) || go == null)
        {
            _blockedVisuals.Remove(cell);
            return;
        }

        _blockedVisuals.Remove(cell);
        Destroy(go);
    }

    private void RefreshBlockedVisuals()
    {
        if (grid == null) return;

        // Clean up any visuals that no longer correspond to a blocked cell.
        // (Also handles grid resize.)
        var toRemove = new List<Vector2Int>();
        foreach (var kvp in _blockedVisuals)
        {
            var cell = kvp.Key;
            if (!grid.TryGetCell(cell.x, cell.y, out var c) || !c.IsBlocked)
                toRemove.Add(cell);
        }
        for (int i =0; i < toRemove.Count; i++) RemoveBlockedVisual(toRemove[i]);

        // Ensure visuals exist for all blocked cells.
        for (int y =0; y < grid.CellsY; y++)
        {
            for (int x =0; x < grid.CellsX; x++)
            {
                if (!grid.TryGetCell(x, y, out var c)) continue;
                if (!c.IsBlocked) continue;

                EnsureBlockedVisual(new Vector2Int(x, y));
            }
        }
    }

}
