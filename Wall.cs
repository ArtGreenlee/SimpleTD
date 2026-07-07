using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Wall : MonoBehaviour
{
    public SpriteRenderer sr;
    public Collider2D col;

    public bool towerPlacementEnabled = false;
    public Color placementEnabledColor = Color.white;
    public Color placementDisabledColor = Color.red;

    private GridManager _grid;
    private Vector2Int _cell;
    private bool _hasCell;

    private void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (col == null) col = GetComponent<Collider2D>();

        _grid = FindFirstObjectByType<GridManager>();
        TryMarkCell(isWall: true);
        UpdatePlacementDisplay();
    }

    private void OnDestroy()
    {
        TryMarkCell(isWall: false);
    }

    private void TryMarkCell(bool isWall)
    {
        if (_grid == null) return;

        // Use wall's world position to find grid cell.
        if (!_grid.TryWorldToCell(transform.position, out var idx)) return;
        _cell = idx;
        _hasCell = true;

        _grid.TrySetWallAtCell(idx, isWall);
        if (isWall)
        {
            _grid.TrySetTowerPlacementEnabledAtCell(idx, towerPlacementEnabled);
        }
    }

    public void SetTowerPlacementEnabled(bool enabled)
    {
        towerPlacementEnabled = enabled;
        if (_hasCell && _grid != null)
        {
            _grid.TrySetTowerPlacementEnabledAtCell(_cell, enabled);
        }
        UpdatePlacementDisplay();
    }

    private void UpdatePlacementDisplay()
    {
        if (sr == null) return;
        sr.color = towerPlacementEnabled ? placementEnabledColor : placementDisabledColor;
    }
}
