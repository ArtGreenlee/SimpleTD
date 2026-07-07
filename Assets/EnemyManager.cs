using UnityEngine;
using System.Collections.Generic;
using System;
public class EnemyManager : MonoBehaviour
{
    private const int DefaultDangerScore = 1;
    private const float DefaultProgressionMinimum = 0f;

    // Authoritative runtime danger scores for wave generation and value allocation.
    private static readonly Dictionary<Enemy.ID, int> EnemyDangerScoreById = new Dictionary<Enemy.ID, int>
    {
        { Enemy.ID.Grunt, 1 },
        { Enemy.ID.Speck, 5 },
        { Enemy.ID.Cluster, 2 },
        { Enemy.ID.Ghost, 5 },
        { Enemy.ID.Stun, 2 },
        { Enemy.ID.Heal, 5 },
        { Enemy.ID.Tank, 3 },
        { Enemy.ID.EliteGrunt, 3 },
        { Enemy.ID.Teleport, 4 },
        { Enemy.ID.Scythe, 6 },
    };

    // Authoritative runtime progression minimums for wave generation.
    private static readonly Dictionary<Enemy.ID, float> EnemyProgressionMinimumById = new Dictionary<Enemy.ID, float>
    {
        { Enemy.ID.Grunt, 0f },
        { Enemy.ID.Speck, 0.2f },
        { Enemy.ID.Cluster, 0.15f },
        { Enemy.ID.Ghost, 0.2f },
        { Enemy.ID.Stun, 0.25f },
        { Enemy.ID.Heal, 0.3f },
        { Enemy.ID.Tank, 0.35f },
        { Enemy.ID.EliteGrunt, 0.45f },
        { Enemy.ID.Teleport, 0.55f },
        { Enemy.ID.Scythe, 0.7f },
    };

    public Dictionary<Vector2Int, HashSet<Enemy>> enemiesByCell = new Dictionary<Vector2Int, HashSet<Enemy>>();

    public int GetDangerScore(Enemy.ID id)
    {
        if (!EnemyDangerScoreById.TryGetValue(id, out var dangerScore))
        {
            return DefaultDangerScore;
        }

        return Mathf.Max(1, dangerScore);
    }

    public float GetProgressionMinimum(Enemy.ID id)
    {
        if (!EnemyProgressionMinimumById.TryGetValue(id, out var progressionMinimum))
        {
            return DefaultProgressionMinimum;
        }

        return Mathf.Clamp01(progressionMinimum);
    }

    public void UpdateEnemyGridCell(Enemy enemy, bool hasPreviousCell, Vector2Int previousCell, Vector2Int currentCell)
    {
        if (enemy == null) return;

        if (hasPreviousCell && previousCell != currentCell)
        {
            RemoveEnemyFromCell(enemy, previousCell);
        }

        AddEnemyToCell(enemy, currentCell);
    }

    public void RemoveEnemyFromGrid(Enemy enemy, bool hasKnownCell = false, Vector2Int knownCell = default)
    {
        if (enemy == null) return;

        if (hasKnownCell)
        {
            RemoveEnemyFromCell(enemy, knownCell);
            return;
        }

        RemoveEnemyFromAllCells(enemy);
    }

    private void AddEnemyToCell(Enemy enemy, Vector2Int cell)
    {
        if (!enemiesByCell.TryGetValue(cell, out var set) || set == null)
        {
            set = new HashSet<Enemy>();
            enemiesByCell[cell] = set;
        }

        set.Add(enemy);
    }

    private void RemoveEnemyFromCell(Enemy enemy, Vector2Int cell)
    {
        if (!enemiesByCell.TryGetValue(cell, out var set) || set == null) return;

        set.Remove(enemy);
        if (set.Count == 0)
        {
            enemiesByCell.Remove(cell);
        }
    }

    private void RemoveEnemyFromAllCells(Enemy enemy)
    {
        if (enemiesByCell.Count == 0) return;

        var emptyCells = new List<Vector2Int>(8);
        foreach (var pair in enemiesByCell)
        {
            var set = pair.Value;
            if (set == null)
            {
                emptyCells.Add(pair.Key);
                continue;
            }

            set.Remove(enemy);
            if (set.Count == 0)
            {
                emptyCells.Add(pair.Key);
            }
        }

        for (int i = 0; i < emptyCells.Count; i++)
        {
            enemiesByCell.Remove(emptyCells[i]);
        }
    }

    [Serializable]
    public struct EnemyInfo
    {
        public GameObject prefab;
    }

    public List<EnemyInfo> enemyInfo;
    public Dictionary<Enemy.ID, EnemyInfo> enemyInfoDict = new Dictionary<Enemy.ID, EnemyInfo>();
    public static EnemyManager instance;

    public ParticleSystem markPs;
    private ParticleSystem.MainModule mainMarkPs;

    [Header("Grid Viz Projectile Hit")]
    [SerializeField] private bool gridVizProjectileHitCellBoost = true;
    [SerializeField, Min(0f)] private float gridVizProjectileHitAlphaIncrease = 0.2f;
    [SerializeField, Min(0f)] private float gridVizProjectileHitColorIncrease = 0.2f;
    private GridViz _gridViz;

    private void Awake()
    {
        instance = this;
        enemiesByCell.Clear();
        enemyInfoDict.Clear();
        foreach (EnemyInfo info in enemyInfo)
        {
            if (info.prefab == null) continue;

            var enemy = info.prefab.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogWarning("EnemyManager enemyInfo entry has no Enemy component on prefab.", info.prefab);
                continue;
            }

            var tag = enemy.id;
            if (!enemyInfoDict.ContainsKey(tag))
            {
                enemyInfoDict.Add(tag, info);
            }
        }
        mainMarkPs = markPs.main;
    }

    public void PlayMarkVfx(Vector3 position, CM.ColorType colorType)
    {
        markPs.transform.position = position;
        mainMarkPs.startColor = CM.i.ColorTypeToColor(colorType);
    }

    public void OnProjectileHitEnemyCell(Enemy enemy, CM.ColorType colorType)
    {
        if (!gridVizProjectileHitCellBoost) return;
        if (enemy == null) return;

        if (_gridViz == null)
        {
            _gridViz = FindFirstObjectByType<GridViz>();
        }

        if (_gridViz == null) return;

        Vector3 hitPosition = enemy.transform.position;

        if (gridVizProjectileHitAlphaIncrease > 0f)
        {
            _gridViz.AlphaCell(gridVizProjectileHitAlphaIncrease, hitPosition);
        }

        if (gridVizProjectileHitColorIncrease > 0f)
        {
            Color hitColor = Color.white;
            if (colorType != CM.ColorType.None && CM.i != null)
            {
                hitColor = CM.i.ColorTypeToColor(colorType);
            }

            _gridViz.ColorCell(hitColor, gridVizProjectileHitColorIncrease, hitPosition);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(KeyCode.K))
        {
            DebugKillAllActiveEnemiesEditorOnly();
        }
    }

    [ContextMenu("DEBUG/Kill All Active Enemies (Editor Only)")]
    private void DebugKillAllActiveEnemiesEditorOnly()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("EnemyManager debug kill only works while in Play Mode.", this);
            return;
        }

        Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        int killedCount = 0;

        for (int i = 0; i < allEnemies.Length; i++)
        {
            Enemy enemy = allEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            Health enemyHealth = enemy.health != null ? enemy.health : enemy.GetComponent<Health>();
            if (enemyHealth == null)
            {
                Destroy(enemy.gameObject);
                killedCount++;
                continue;
            }

            float currentHealth = enemyHealth.GetCurrentHealth();
            if (currentHealth <= 0f) continue;

            enemyHealth.TakeDamage(currentHealth + 1f, null, CM.ColorType.None, null);
            killedCount++;
        }

        Debug.Log("EnemyManager DEBUG: Killed " + killedCount + " active enemies.", this);
    }
#endif

}
