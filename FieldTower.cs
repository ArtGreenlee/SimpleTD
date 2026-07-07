using System.Collections.Generic;
using UnityEngine;

public class FieldTower : Tower
{
    public GameObject fieldPrefab;
    public enum Direction
    {
        up,
        down,
        left,
        right
    }

    [SerializeField] private List<Direction> enabledDirections = new List<Direction>();
    [SerializeField, Min(0.001f)] private float maxFieldDistance = 100f;
    [SerializeField, Min(0f)] private float spawnWallIgnoreDistance = 0.05f;

    private readonly Dictionary<Direction, Field> fields = new Dictionary<Direction, Field>();
    private readonly Dictionary<Direction, Vector3> _baseFieldScales = new Dictionary<Direction, Vector3>();

    private Tower _tower;

    public List<Effect> fieldIntersectionEffects;

    public override void Awake()
    {
        base.Awake();
        CacheRefs();
        EnsureFields();
        UpdateFieldTransforms();
        UpdateFieldVisibilityAndActivity();
    }

    private void OnEnable()
    {
        CacheRefs();
        EnsureFields();
    }

    private void LateUpdate()
    {
        CacheRefs();
        EnsureFields();
        UpdateFieldTransforms();
        UpdateFieldVisibilityAndActivity();
    }

    private void OnDisable()
    {
        foreach (var kv in fields)
        {
            if (kv.Value != null)
            {
                kv.Value.gameObject.SetActive(false);
            }
        }
    }

    public Direction RotateDirection(Direction dir, bool clockwise)
    {
        switch (dir)
        {
            case Direction.up: return clockwise ? Direction.right : Direction.left;
            case Direction.down: return clockwise ? Direction.left : Direction.right;
            case Direction.left: return clockwise ? Direction.up : Direction.down;
            case Direction.right: return clockwise ? Direction.down : Direction.up;
            default: return dir;
        }
    }

    public override void OnRotate(int direction)
    {
        base.OnRotate(direction);   
        List<Direction> newDirs = new List<Direction>();
        foreach (var dir in enabledDirections)
        {
            newDirs.Add(RotateDirection(dir, direction == -1));
        }
        enabledDirections = newDirs;

    }

    public void ApplyFieldToProjectile(Projectile projectile)
    {
        if (projectile == null) return;

        var tower = _tower != null ? _tower : GetComponent<Tower>();
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        if (tower != null)
        {
            projectile.AddFieldEffect(tower);
        }
        projectile.SetOutlineColor(CM.ColorType.Orange);
    }

    private void CacheRefs()
    {
        if (_tower == null) _tower = GetComponent<Tower>() ?? GetComponentInParent<Tower>();
        if (rangeManager == null) rangeManager = GetComponent<RangeManager>() ?? GetComponentInChildren<RangeManager>();
    }

    private void EnsureFields()
    {
        if (fieldPrefab == null || enabledDirections == null) return;

        for (int i = enabledDirections.Count - 1; i >= 0; i--)
        {
            Direction dir = enabledDirections[i];
            if (fields.ContainsKey(dir) && fields[dir] != null) continue;

            GameObject go = Instantiate(fieldPrefab, transform.position, Quaternion.identity, transform);
            var field = go.GetComponent<Field>();
            if (field == null) field = go.AddComponent<Field>();
            field.SetFieldTower(this);

            fields[dir] = field;
            _baseFieldScales[dir] = go.transform.localScale;
        }

        var toRemove = new List<Direction>();
        foreach (var kv in fields)
        {
            if (!enabledDirections.Contains(kv.Key))
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value.gameObject);
                }
                toRemove.Add(kv.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _baseFieldScales.Remove(toRemove[i]);
            fields.Remove(toRemove[i]);
        }
    }

    private void UpdateFieldTransforms()
    {
        if (fields.Count == 0) return;

        Vector2 origin = transform.position;
        GridManager gm = GridManager.instance;
        float cellSize = gm != null ? Mathf.Max(0.001f, gm.GetSpacing()) : 1f;

        foreach (var kv in fields)
        {
            Field field = kv.Value;
            if (field == null) continue;

            Direction dirEnum = kv.Key;
            Vector2 dir = DirectionToVector(dirEnum);
            Vector2 end = GetFieldEndPosition(origin, dirEnum, dir);
            Vector2 mid = (origin + end) * 0.5f;

            Transform t = field.transform;
            t.position = new Vector3(mid.x, mid.y, t.position.z);
            t.rotation = Quaternion.FromToRotation(Vector2.up, dir);

            Vector3 baseScale = _baseFieldScales.TryGetValue(dirEnum, out var b) ? b : t.localScale;
            float length = Mathf.Max(0.001f, Vector2.Distance(origin, end));
            t.localScale = new Vector3(cellSize, length, baseScale.z);
        }
    }

    private Vector2 GetFieldEndPosition(Vector2 origin, Direction dirEnum, Vector2 dir)
    {
        GridManager gm = GridManager.instance;
        if (gm != null && gm.TryWorldToCell(origin, out var startCell))
        {
            if (!gm.IsInMazeBounds(startCell))
            {
                return origin;
            }

            Vector2Int step = DirectionToGridVector(dirEnum);
            Vector2Int current = startCell + step;
            Vector2Int lastValid = startCell;

            while (gm.TryGetCell(current.x, current.y, out var cell))
            {
                if (!gm.IsInMazeBounds(current))
                {
                    break;
                }

                if (cell.IsWall)
                {
                    return cell.WorldCenter;
                }

                lastValid = current;
                current += step;
            }

            if (gm.TryGetCell(lastValid.x, lastValid.y, out var edgeCell))
            {
                return edgeCell.WorldCenter;
            }
        }

        int wallMask = LayerMaskManager.instance != null ? LayerMaskManager.instance.wallLayerMask : 0;
        float dist = GetDistanceToNearestWall(origin, dir, wallMask, Mathf.Max(0.001f, maxFieldDistance));
        return origin + dir * dist;
    }

    private static Vector2Int DirectionToGridVector(Direction dir)
    {
        switch (dir)
        {
            case Direction.up: return Vector2Int.up;
            case Direction.down: return Vector2Int.down;
            case Direction.left: return Vector2Int.left;
            case Direction.right: return Vector2Int.right;
            default: return Vector2Int.right;
        }
    }

    private float GetDistanceToNearestWall(Vector2 origin, Vector2 dir, int wallMask, float maxDist)
    {
        if (wallMask == 0) return maxDist;

        var hits = Physics2D.RaycastAll(origin, dir, maxDist, wallMask);
        float best = maxDist;

        float ignore = Mathf.Max(0f, spawnWallIgnoreDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if (h.distance <= ignore) continue;
            if (h.distance < best) best = h.distance;
        }

        return best;
    }

    private void UpdateFieldVisibilityAndActivity()
    {
        bool show = ShouldShowFieldVisualization();
        bool active = _tower != null && _tower.CurrentState == Tower.State.Placed;

        foreach (var kv in fields)
        {
            Field f = kv.Value;
            if (f == null) continue;

            if (f.gameObject.activeSelf != show)
            {
                f.gameObject.SetActive(show);
            }

            var c = f.GetComponent<Collider2D>();
            if (c != null) c.enabled = active;
        }
    }

    public override void Attack()
    {
        if (fieldIntersectionEffects.Count == 0)
        {
            base.Attack();
            return;
        }
        List<Enemy> removeList = new List<Enemy>();
        foreach (Field field in fields.Values)
        {
            removeList.Clear();
            foreach (var enemy in field.intersectedEnemies)
            {
                if (enemy == null)
                {
                    removeList.Add(enemy);
                }
                else
                {
                    foreach (Effect effect in fieldIntersectionEffects)
                    {
                        effect.ApplyEffect(enemy);
                    }
                }
            }
            foreach (var e in removeList)
            {
                field.intersectedEnemies.Remove(e);
            }
            if (field.intersectedEnemies.Count > 0)
            {
            }
        }
        
        base.Attack();
    }

    private bool ShouldShowFieldVisualization()
    {
        if (_tower == null) return false;
        if (_tower.IsStunned()) return false;

        if (_tower.CurrentState == Tower.State.Placed)
        {
            return true;
        }

        return IsFieldTowerHeldByPlayer();
    }

    private bool IsFieldTowerHeldByPlayer()
    {
        if (_tower == null) return false;

        var pic = PIC.instance;
        if (pic == null) return false;
        if (pic.currentState != PIC.PICState.PlacingTower) return false;

        return pic.IsHoldingTower(_tower);
    }

    private static Vector2 DirectionToVector(Direction dir)
    {
        switch (dir)
        {
            case Direction.up: return Vector2.up;
            case Direction.down: return Vector2.down;
            case Direction.left: return Vector2.left;
            case Direction.right: return Vector2.right;
            default: return Vector2.right;
        }
    }
}
