using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public List<GameObject> prefabs;
    public int amount;
    public KeyCode spawnkey;

    public enum Mode
    {
        Tower,
        Enemy,
    }

    public Mode spawnMode;

    [Header("Grid")]
    [SerializeField] private GridManager grid;
    [SerializeField] private int maxAttemptsPerSpawn = 200;

    private void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(spawnkey)) return;
        if (prefabs == null || prefabs.Count == 0) return;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (grid == null) return;

        for (int i = 0; i < amount; i++)
        {
            if (!TryGetSpawnCell(out var cellIdx))
                continue;

            Vector3 pos = grid.GetCellWorldCenter(cellIdx.x, cellIdx.y);
            pos.z = 0f;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            var go = Instantiate(prefab, pos, Quaternion.identity);

            // If we spawned a tower, register it to the grid.
            if (spawnMode == Mode.Tower && go != null)
            {
                var tower = go.GetComponent<Tower>();
                if (tower != null)
                {
                    grid.SetTowerAtCell(cellIdx, tower);
                }
            }
        }
    }

    private bool TryGetSpawnCell(out Vector2Int cellIdx)
    {
        cellIdx = default;

        int attempts = Mathf.Max(1, maxAttemptsPerSpawn);
        for (int a = 0; a < attempts; a++)
        {
            int x = Random.Range(0, grid.CellsX);
            int y = Random.Range(0, grid.CellsY);
            var idx = new Vector2Int(x, y);

            if (!grid.TryGetCell(x, y, out var cell))
                continue;

            if (spawnMode == Mode.Enemy)
            {
                // Enemies may only spawn on unblocked cells.
                if (cell.IsBlocked) continue;

                cellIdx = idx;
                return true;
            }

            // Towers may only spawn on valid tower cells.
            if (!cell.IsWall) continue;
            if (grid.TryGetTowerAtCell(idx, out var existing) && existing != null) continue;

            cellIdx = idx;
            return true;
        }

        return false;
    }
}
