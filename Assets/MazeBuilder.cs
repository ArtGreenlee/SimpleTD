using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MazeBuilder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private Pathfinding pathfinding;
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private GameObject gridOverlayPrefab;

    [Header("Wall Output")]
    [SerializeField] private MeshRenderer wallRenderer;
    [SerializeField] private MeshFilter wallMeshFilter;
    [SerializeField] private PolygonCollider2D wallCollider;

    [Header("Input")]
    [SerializeField] private bool active = true;
    [SerializeField] private KeyCode buildMouseButton = KeyCode.Mouse0;

    [Header("Hover Outline")]
    [SerializeField] private Color hoverOutlineColor = new Color(0f, 1f, 1f, 0.9f);
    [SerializeField, Min(0.001f)] private float hoverOutlineWidth = 0.04f;
    [SerializeField, Min(0f)] private float hoverOutlineInset = 0.06f;
    [SerializeField, Min(0f)] private float hoverDepthOffset = 0.002f;

    [Header("Drag Preview")]
    [SerializeField, Min(0f)] private float overlayPadding = 0.06f;
    [SerializeField, Min(0f)] private float overlayDepthOffset = 0.001f;

    private readonly Dictionary<Vector2Int, GameObject> _previewOverlays = new Dictionary<Vector2Int, GameObject>();
    private readonly List<Vector2Int> _previewCells = new List<Vector2Int>(64);

    private LineRenderer _lineRenderer;
    private Mesh _wallMesh;
    private bool _isDragging;
    private bool _hasHoverCell;
    private Vector2Int _hoverCell;
    private Vector2Int _dragStartCell;
    private Vector2Int _dragCurrentCell;

    private void Awake()
    {
        EnsureRefs();
        EnsureLineRenderer();
        HideHoverOutline();
    }

    private void OnEnable()
    {
        EnsureRefs();
        EnsureLineRenderer();
        RefreshWallMeshFromGrid();
    }

    private void OnDisable()
    {
        _isDragging = false;
        ClearPreviewOverlays();
        HideHoverOutline();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        EnsureRefs();

        if (!active || grid == null)
        {
            _isDragging = false;
            ClearPreviewOverlays();
            HideHoverOutline();
            return;
        }

        _hasHoverCell = TryGetHoverCell(out _hoverCell);
        if (_hasHoverCell)
            DrawHoverOutline(_hoverCell);
        else
            HideHoverOutline();

        if (Input.GetKeyDown(buildMouseButton) && _hasHoverCell && IsBuildableCell(_hoverCell))
        {
            _isDragging = true;
            _dragStartCell = _hoverCell;
            _dragCurrentCell = _hoverCell;
            UpdatePreviewLine();
        }

        if (_isDragging)
        {
            if (_hasHoverCell)
            {
                _dragCurrentCell = _hoverCell;
                UpdatePreviewLine();
            }

            if (Input.GetKeyUp(buildMouseButton))
            {
                CommitPreviewLine();
                _isDragging = false;
            }
        }
    }

    private void EnsureRefs()
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (pathfinding == null) pathfinding = FindFirstObjectByType<Pathfinding>();
        if (raycastCamera == null) raycastCamera = Camera.main;
        if (wallMeshFilter == null) wallMeshFilter = GetComponent<MeshFilter>();
        if (wallRenderer == null) wallRenderer = GetComponent<MeshRenderer>();
        if (wallCollider == null) wallCollider = GetComponent<PolygonCollider2D>();
    }

    private void EnsureLineRenderer()
    {
        if (_lineRenderer != null) return;

        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();

        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 4;
        _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.alignment = LineAlignment.TransformZ;
        _lineRenderer.textureMode = LineTextureMode.Stretch;
        _lineRenderer.sharedMaterial = null;
        _lineRenderer.startColor = hoverOutlineColor;
        _lineRenderer.endColor = hoverOutlineColor;
        _lineRenderer.startWidth = hoverOutlineWidth;
        _lineRenderer.endWidth = hoverOutlineWidth;
        _lineRenderer.enabled = false;
    }

    private bool TryGetHoverCell(out Vector2Int cell)
    {
        cell = default;
        if (grid == null) return false;

        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(grid.transform.forward, grid.transform.position);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        if (!grid.TryWorldToCell(hit, out cell)) return false;
        return grid.IsInMazeBounds(cell);
    }

    private void DrawHoverOutline(Vector2Int cell)
    {
        if (_lineRenderer == null || grid == null) return;

        float spacing = Mathf.Max(0.001f, grid.GetSpacing());
        float half = Mathf.Max(0.001f, spacing * 0.5f - hoverOutlineInset);
        Vector3 center = grid.GetCellWorldCenter(cell.x, cell.y) + grid.transform.forward * hoverDepthOffset;
        Vector3 right = grid.transform.right * half;
        Vector3 up = grid.transform.up * half;

        _lineRenderer.startColor = hoverOutlineColor;
        _lineRenderer.endColor = hoverOutlineColor;
        _lineRenderer.startWidth = hoverOutlineWidth;
        _lineRenderer.endWidth = hoverOutlineWidth;
        _lineRenderer.SetPosition(0, center - right - up);
        _lineRenderer.SetPosition(1, center - right + up);
        _lineRenderer.SetPosition(2, center + right + up);
        _lineRenderer.SetPosition(3, center + right - up);
        _lineRenderer.enabled = true;
    }

    private void HideHoverOutline()
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
    }

    private void UpdatePreviewLine()
    {
        RebuildPreviewCells();

        var activeSet = new HashSet<Vector2Int>(_previewCells);
        var toRemove = new List<Vector2Int>();
        foreach (var pair in _previewOverlays)
        {
            if (!activeSet.Contains(pair.Key))
                toRemove.Add(pair.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            RemovePreviewOverlay(toRemove[i]);

        for (int i = 0; i < _previewCells.Count; i++)
            EnsurePreviewOverlay(_previewCells[i]);
    }

    private void RebuildPreviewCells()
    {
        _previewCells.Clear();

        if (grid == null)
            return;

        int dx = _dragCurrentCell.x - _dragStartCell.x;
        int dy = _dragCurrentCell.y - _dragStartCell.y;

        bool horizontal = Mathf.Abs(dx) >= Mathf.Abs(dy);
        if (horizontal)
        {
            int minX = Mathf.Min(_dragStartCell.x, _dragCurrentCell.x);
            int maxX = Mathf.Max(_dragStartCell.x, _dragCurrentCell.x);
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, _dragStartCell.y);
                if (IsBuildableCell(cell))
                    _previewCells.Add(cell);
            }
        }
        else
        {
            int minY = Mathf.Min(_dragStartCell.y, _dragCurrentCell.y);
            int maxY = Mathf.Max(_dragStartCell.y, _dragCurrentCell.y);
            for (int y = minY; y <= maxY; y++)
            {
                var cell = new Vector2Int(_dragStartCell.x, y);
                if (IsBuildableCell(cell))
                    _previewCells.Add(cell);
            }
        }
    }

    private bool IsBuildableCell(Vector2Int cell)
    {
        if (grid == null) return false;
        if (cell.x < 0 || cell.y < 0 || cell.x >= grid.CellsX || cell.y >= grid.CellsY) return false;
        if (!grid.IsInMazeBounds(cell)) return false;
        if (!grid.TryGetCell(cell.x, cell.y, out var current)) return false;
        return !current.IsBlocked || current.IsWall;
    }

    private void EnsurePreviewOverlay(Vector2Int cell)
    {
        if (gridOverlayPrefab == null || grid == null) return;
        if (_previewOverlays.ContainsKey(cell)) return;

        var go = Instantiate(gridOverlayPrefab, transform);
        go.name = $"WallPreview ({cell.x},{cell.y})";

        Vector3 center = grid.GetCellWorldCenter(cell.x, cell.y) + grid.transform.forward * overlayDepthOffset;
        go.transform.position = center;
        go.transform.rotation = grid.transform.rotation;

        float spacing = Mathf.Max(0.001f, grid.GetSpacing());
        float size = Mathf.Max(0.001f, spacing - overlayPadding);
        go.transform.localScale = new Vector3(size, size, 1f);

        _previewOverlays[cell] = go;
    }

    private void RemovePreviewOverlay(Vector2Int cell)
    {
        if (!_previewOverlays.TryGetValue(cell, out var go))
            return;

        _previewOverlays.Remove(cell);
        if (go != null)
            Destroy(go);
    }

    private void ClearPreviewOverlays()
    {
        foreach (var pair in _previewOverlays)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }

        _previewOverlays.Clear();
        _previewCells.Clear();
    }

    private void CommitPreviewLine()
    {
        if (grid == null)
        {
            ClearPreviewOverlays();
            return;
        }

        bool changed = false;
        for (int i = 0; i < _previewCells.Count; i++)
        {
            Vector2Int cell = _previewCells[i];
            if (!IsBuildableCell(cell))
                continue;

            if (!grid.IsWallAtCell(cell))
                changed |= grid.TrySetWallAtCell(cell, isWall: true);
        }

        ClearPreviewOverlays();

        if (!changed)
            return;

        RefreshWallMeshFromGrid();
        grid.Rebuild();
        if (pathfinding != null) pathfinding.ResetFlowMaps();
    }

    private void RefreshWallMeshFromGrid()
    {
        if (grid == null || wallMeshFilter == null)
            return;

        int width = grid.CellsX;
        int height = grid.CellsY;
        float cellSize = Mathf.Max(0.001f, grid.GetSpacing());
        float half = cellSize * 0.5f;

        var vertices = new List<Vector3>(256);
        var triangles = new List<int>(384);
        var uvs = new List<Vector2>(256);
        var normals = new List<Vector3>(256);
        var colliderPaths = wallCollider != null ? new List<Vector2[]>(256) : null;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!grid.IsWallAtCell(cell))
                    continue;

                Vector3 worldCenter = grid.GetCellWorldCenter(x, y);
                Vector3 meshCenter = wallMeshFilter.transform.InverseTransformPoint(worldCenter);

                int baseIndex = vertices.Count;
                vertices.Add(new Vector3(meshCenter.x - half, meshCenter.y - half, 0f));
                vertices.Add(new Vector3(meshCenter.x - half, meshCenter.y + half, 0f));
                vertices.Add(new Vector3(meshCenter.x + half, meshCenter.y + half, 0f));
                vertices.Add(new Vector3(meshCenter.x + half, meshCenter.y - half, 0f));

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));

                normals.Add(Vector3.back);
                normals.Add(Vector3.back);
                normals.Add(Vector3.back);
                normals.Add(Vector3.back);

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);

                if (colliderPaths != null)
                {
                    Vector3 colliderCenter = wallCollider.transform.InverseTransformPoint(worldCenter);
                    colliderPaths.Add(new[]
                    {
                        new Vector2(colliderCenter.x - half, colliderCenter.y - half),
                        new Vector2(colliderCenter.x - half, colliderCenter.y + half),
                        new Vector2(colliderCenter.x + half, colliderCenter.y + half),
                        new Vector2(colliderCenter.x + half, colliderCenter.y - half),
                    });
                }
            }
        }

        if (_wallMesh == null)
        {
            _wallMesh = new Mesh();
            _wallMesh.name = "MazeBuilder Walls";
        }

        _wallMesh.Clear();
        _wallMesh.indexFormat = IndexFormat.UInt32;
        _wallMesh.SetVertices(vertices);
        _wallMesh.SetTriangles(triangles, 0);
        _wallMesh.SetUVs(0, uvs);
        _wallMesh.SetNormals(normals);
        _wallMesh.RecalculateBounds();
        wallMeshFilter.sharedMesh = _wallMesh;

        if (wallRenderer != null)
            wallRenderer.enabled = vertices.Count > 0;

        if (wallCollider != null)
        {
            wallCollider.pathCount = colliderPaths != null ? colliderPaths.Count : 0;
            if (colliderPaths != null)
            {
                for (int i = 0; i < colliderPaths.Count; i++)
                    wallCollider.SetPath(i, colliderPaths[i]);
            }

            wallCollider.enabled = wallCollider.pathCount > 0;
        }
    }
}