using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class WaveManager : MonoBehaviour
{
    public WaveInfo currentWaveInfo;
    public WaveInfo nextWaveInfo;
    public static WaveManager instance;

    [Header("References")]
    [SerializeField] private Pathfinding pathfinding;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Button startWaveButton;

    [Header("Wave Data")]
    public WaveDataSO waveDataSO;

    [Header("Wave Generation")]
    public int waveCount = 20;
    public float dangerScoreA = 0.05f;
    public float dangerScoreB = 2f;
    public float dangerScoreC = 4f;
    public float enemyDiversityB = 0.15f;
    public float enemyDiversityC = 1f;
    [Header("Wave Value")]
    public float waveValueA = 0.03f;
    public float waveValueB = 1.2f;
    public float waveValueC = 2f;
    [Header("Health Multiplier")]
    public float healthMultiplierA = 0f;
    public float healthMultiplierB = 0f;
    public float healthMultiplierC = 1f;
    [Header("Spawning Duration")]
    public float spawningDurationA = 0f;
    public float spawningDurationB = 0f;
    public float spawningDurationC = 5f;
    [Tooltip("If enabled, spawn delay between enemies is skipped whenever there are no living enemies.")]
    [SerializeField] private bool sendIfAllDead = false;
    [Header("Goal Reach Currency")]
    [Tooltip("If enabled, enemies that reach the goal will still spawn their currency reward as if killed.")]
    public bool CurrencySpawnOnKill = false;
    [Min(0)] public int minimumEnemiesPerWave = 5;
    [Min(0)] public int minimumEnemyCountIncrease = 1;

    [Serializable]
    public struct WaveData
    {
        public List<EnemyData> enemyDatas;
        public int totalWaveValue;
    }

    private struct SpawnPlan
    {
        public EnemyData data;
        public List<int> individualValues;
    }

    public enum SpawnGroup
    {
        A, B, C, D, E, F, G, H
    }

    [Serializable]
    public class EnemyData
    {
        public Enemy.ID enemyTag;
        public int count;

        [Header("Spawn")]
        public SpawnGroup spawnGroup;

        [Tooltip("Random radius (world units) around the spawn point to offset enemy spawn position.")]
        [Min(0f)]
        public float spawnRadius = 1;

        [Tooltip("Delay before this group starts spawning (seconds).")]
        [Min(0f)]
        public float startDelay = 0;

        [Tooltip("Delay between each enemy in this group (seconds).")]
        [Min(0f)]
        public float intervalDelay = 1;
    }

    private List<WaveData> _cachedWaves;

    private int currentWaveIndex = 0;
    private Coroutine _waveRoutine;
    private bool _isSpawningWave;
    private bool _awaitingWaveCompletion;

    // Added for UI (1-based wave number, clamped).
    public void ShowWaveData()
    {
        var list = GetWaveList();
        if (list == null || list.Count == 0) return;

        int currentIdx = Mathf.Clamp(currentWaveIndex, 0, list.Count - 1);
        int nextIdx = Mathf.Clamp(currentWaveIndex + 1, 0, list.Count - 1);

        if (currentWaveInfo != null) currentWaveInfo.DisplayWaveInfo(list[currentIdx]);
        if (nextWaveInfo != null) nextWaveInfo.DisplayWaveInfo(list[nextIdx]);

        // Notify UI.
        if (GameInfoDisplay.instance != null) GameInfoDisplay.instance.RefreshWaveText();
    }

    public void Update()
    {
        TryResolveWaveCompletion();
        RefreshStartWaveButtonState();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SendWave();
        }
    }

    public bool IsWaveActive()
    {
        if (_isSpawningWave) return true;

        var aliveEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        return aliveEnemies != null && aliveEnemies.Length > 0;
    }

    public bool IsWaveInProgress()
    {
        return IsWaveActive();
    }
    public int GetCurrentWaveNumber()
    {
        var list = GetWaveList();
        if (list == null || list.Count == 0) return 0;
        int idx = Mathf.Clamp(currentWaveIndex, 0, list.Count - 1);
        return idx + 1;
    }

    private List<WaveData> GetWaveList()
    {
        if (waveDataSO == null || waveDataSO.waves == null) return null;

        // Convert SO data to runtime WaveData list view.
        // We cache the converted list to avoid allocations.
        if (_cachedWaves == null) _cachedWaves = new List<WaveData>(waveDataSO.waves.Count);
        _cachedWaves.Clear();

        for (int i = 0; i < waveDataSO.waves.Count; i++)
        {
            int totalValue = waveDataSO.waves[i].totalWaveValue;
            if (totalValue <= 0)
            {
                totalValue = EvaluateWaveValueTarget(i + 1);
            }

            _cachedWaves.Add(new WaveData
            {
                enemyDatas = waveDataSO.waves[i].enemyDatas,
                totalWaveValue = totalValue
            });
        }

        return _cachedWaves;
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        if (startWaveButton != null)
        {
            startWaveButton.onClick.RemoveListener(SendWave);
            startWaveButton.onClick.AddListener(SendWave);
        }
    }

    private void OnDisable()
    {
        if (startWaveButton != null)
        {
            startWaveButton.onClick.RemoveListener(SendWave);
        }
    }

    private void Start()
    {
        if (pathfinding == null) pathfinding = Pathfinding.instance != null ? Pathfinding.instance : FindFirstObjectByType<Pathfinding>();
        if (enemyManager == null) enemyManager = EnemyManager.instance != null ? EnemyManager.instance : FindFirstObjectByType<EnemyManager>();

        ShowWaveData();
        RefreshStartWaveButtonState();
    }

    private void RefreshStartWaveButtonState()
    {
        if (startWaveButton == null) return;
        startWaveButton.interactable = !IsWaveInProgress();
    }

    public void SendWave()
    {
        var list = GetWaveList();
        if (list == null || list.Count == 0) return;

        if (IsWaveInProgress())
        {
            return;
        }

        StartWaveAtCurrentIndex(list);
    }

    private void StartWaveAtCurrentIndex(List<WaveData> list)
    {
        if (list == null || list.Count == 0) return;

        ApplyRoundStartRelicEffects();

        // If we run out of waves, keep resending the last wave.
        int idx = Mathf.Clamp(currentWaveIndex, 0, list.Count - 1);
        _isSpawningWave = true;
        _awaitingWaveCompletion = false;
        _waveRoutine = StartCoroutine(SendWaveRoutine(list[idx]));
    }

    private void ApplyRoundStartRelicEffects()
    {
        if (RM.i == null || !RM.i.Active(RM.ID.devilsPact)) return;

        if (GameController.instance != null && GameController.instance.GetLives() > 1)
        {
            GameController.instance.LoseLifes(1);
        }

        if (CurrencyManager.instance != null)
        {
            int gold = Mathf.Max(0, RM.devilsPactRoundGold);
            if (gold > 0)
            {
                CurrencyManager.instance.AddCurrency(gold);
            }
        }
    }

    private IEnumerator SendWaveRoutine(WaveData wave)
    {
        if (pathfinding == null) pathfinding = Pathfinding.instance != null ? Pathfinding.instance : FindFirstObjectByType<Pathfinding>();
        if (enemyManager == null) enemyManager = EnemyManager.instance != null ? EnemyManager.instance : FindFirstObjectByType<EnemyManager>();

        if (pathfinding == null)
        {
            Debug.LogError("WaveManager requires a Pathfinding reference to get spawn points.", this);
            _isSpawningWave = false;
            _waveRoutine = null;
            yield break;
        }
        if (enemyManager == null)
        {
            Debug.LogError("WaveManager requires an EnemyManager reference.", this);
            _isSpawningWave = false;
            _waveRoutine = null;
            yield break;
        }

        var spawns = pathfinding.GetPathStarts();
        if (spawns == null || spawns.Count == 0)
        {
            Debug.LogError("Pathfinding has no path starts assigned.", this);
            _isSpawningWave = false;
            _waveRoutine = null;
            yield break;
        }

        if (wave.enemyDatas == null || wave.enemyDatas.Count == 0)
        {
            _isSpawningWave = false;
            _waveRoutine = null;
            _awaitingWaveCompletion = true;
            TryResolveWaveCompletion();
            yield break;
        }

        var spawnPlans = BuildSpawnPlans(wave);

        float healthMultiplier = EvaluateHealthMultiplier(currentWaveIndex + 1);
        float spawningDuration = EvaluateSpawningDuration(currentWaveIndex + 1);

        // Start one coroutine per EnemyData so different spawn groups can overlap.
        var routines = new List<Coroutine>(spawnPlans.Count);
        for (int i = 0; i < spawnPlans.Count; i++)
        {
            routines.Add(StartCoroutine(SpawnEnemyDataRoutine(spawnPlans[i], spawns, healthMultiplier, spawningDuration)));
        }

        // Wait for all to finish.
        for (int i = 0; i < routines.Count; i++)
        {
            yield return routines[i];
        }

        _isSpawningWave = false;
        _awaitingWaveCompletion = true;
        _waveRoutine = null;
        TryResolveWaveCompletion();
    }

    private void TryResolveWaveCompletion()
    {
        if (!_awaitingWaveCompletion) return;
        if (_isSpawningWave) return;
        if (IsWaveActive()) return;

        OnWaveComplete();
    }

    public void OnWaveComplete()
    {
        if (!_awaitingWaveCompletion) return;

        _awaitingWaveCompletion = false;
        ResetMineModeProjectileTowerCooldowns();
        IndicateSpawnIndicators();
        currentWaveIndex++;
        ShowWaveData();
    }

    private void ResetMineModeProjectileTowerCooldowns()
    {
        if (TowerManager.instance == null) return;

        foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower is not ProjectileTower projectileTower) continue;
            if (!projectileTower.IsMineModeActive()) continue;

            float remainingCooldown = projectileTower.GetRemainingCooldown();
            if (remainingCooldown <= 0f) continue;

            projectileTower.FlatReduceCooldown(remainingCooldown);
        }
    }

    private void IndicateSpawnIndicators()
    {
        if (pathfinding == null) pathfinding = Pathfinding.instance != null ? Pathfinding.instance : FindFirstObjectByType<Pathfinding>();
        if (pathfinding == null) return;

        var spawns = pathfinding.GetPathStarts();
        if (spawns == null) return;

        var seen = new HashSet<SRC>();
        for (int i = 0; i < spawns.Count; i++)
        {
            Transform spawn = spawns[i];
            if (spawn == null) continue;

            SRC src = spawn.GetComponent<SRC>();
            if (src == null) src = spawn.GetComponentInChildren<SRC>(true);
            if (src == null || !seen.Add(src)) continue;

            src.Indicate();
        }
    }

    private IEnumerator SpawnEnemyDataRoutine(SpawnPlan plan, IReadOnlyList<Transform> spawns, float healthMultiplier = 1f, float spawningDuration = 0f)
    {
        var data = plan.data;
        var individualValues = plan.individualValues;

        if (data == null) yield break;

        if (data.startDelay > 0f) yield return new WaitForSeconds(data.startDelay);

        int spawnIndex = (int)data.spawnGroup;
        if (spawnIndex < 0 || spawnIndex >= spawns.Count)
        {
            spawnIndex = Mathf.Clamp(spawnIndex, 0, spawns.Count - 1);
        }

        Transform spawnT = spawns[spawnIndex];
        if (spawnT == null) yield break;

        if (!enemyManager.enemyInfoDict.TryGetValue(data.enemyTag, out var info) || info.prefab == null)
        {
            Debug.LogWarning($"No prefab configured for enemy tag {data.enemyTag}.", this);
            yield break;
        }

        int c = Mathf.Max(0, data.count);

        // If a spawning duration is set, distribute this group's enemies evenly across it.
        // Otherwise fall back to the per-EnemyData intervalDelay.
        float interval = spawningDuration > 0f
            ? (c > 1 ? spawningDuration / (c - 1) : 0f)
            : Mathf.Max(0f, data.intervalDelay);

        for (int i = 0; i < c; i++)
        {
            Vector3 pos = spawnT.position;
            float r = Mathf.Max(0f, data.spawnRadius);
            if (r > 0.0001f)
            {
                Vector2 off = UnityEngine.Random.insideUnitCircle * r;
                pos += new Vector3(off.x, off.y, 0f);
            }

            GameObject go = Instantiate(info.prefab, pos, Quaternion.identity);
            if (go != null)
            {
                var movement = go.GetComponent<Movement>();
                if (movement != null)
                {
                    var goals = pathfinding.GetPathGoals();
                    if (goals != null && goals.Count > 0)
                        movement.Goal = goals[0];
                }

                var enemy = go.GetComponent<Enemy>();
                if (enemy != null)
                {
                    int assigned = (individualValues != null && i < individualValues.Count)
                        ? Mathf.Max(0, individualValues[i])
                        : 0;

                    enemy.SetRuntimeValue(assigned);
                    if (healthMultiplier != 1f && enemy.health != null)
                    {
                        enemy.health.SetMaxHealth(enemy.health.GetMaxHealth() * healthMultiplier);
                    }
                }
            }
            if (interval > 0f && i < c - 1)
            {
                if (!sendIfAllDead || HasLivingEnemies())
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }
    }

    private bool HasLivingEnemies()
    {
        if (enemyManager == null) return false;
        if (enemyManager.enemiesByCell == null || enemyManager.enemiesByCell.Count == 0) return false;

        foreach (var pair in enemyManager.enemiesByCell)
        {
            var set = pair.Value;
            if (set == null || set.Count == 0) continue;

            foreach (var enemy in set)
            {
                if (enemy == null) continue;
                if (!enemy.gameObject.activeInHierarchy) continue;

                Health h = enemy.health != null ? enemy.health : enemy.GetComponent<Health>();
                if (h == null || h.GetCurrentHealth() > 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [ContextMenu("PrintFirst10WaveValues")]
    public void PrintFirst10WaveValues()
    {
        const int maxToPrint = 10;
        var list = GetWaveList();

        for (int waveNumber = 1; waveNumber <= maxToPrint; waveNumber++)
        {
            int runtimeWaveValue = EvaluateWaveValueTarget(waveNumber);
            if (list != null && list.Count >= waveNumber)
            {
                runtimeWaveValue = Mathf.Max(0, list[waveNumber - 1].totalWaveValue);
            }

            float healthMultiplier = EvaluateHealthMultiplier(waveNumber);
            float spawningDuration = EvaluateSpawningDuration(waveNumber);

            Debug.Log(
                $"Wave {waveNumber}: Value={runtimeWaveValue}, HealthMultiplier={healthMultiplier:0.###}, SpawningDuration={spawningDuration:0.###}",
                this);
        }
    }

    [ContextMenu("GenerateWaveSO")]
    public void GenerateWaveSO()
    {
#if UNITY_EDITOR
        EnemyManager resolvedEnemyManager = enemyManager != null ? enemyManager : FindFirstObjectByType<EnemyManager>();
        if (resolvedEnemyManager == null)
        {
            Debug.LogError("GenerateWaveSO failed: no EnemyManager found.", this);
            return;
        }

        var enemyCatalog = BuildEnemyCatalog(resolvedEnemyManager);
        if (enemyCatalog.Count == 0)
        {
            Debug.LogError("GenerateWaveSO failed: EnemyManager has no valid enemy prefabs with Enemy components.", this);
            return;
        }

        WaveDataSO generated = ScriptableObject.CreateInstance<WaveDataSO>();
        generated.waves = new List<WaveDataSO.WaveData>(Mathf.Max(0, waveCount));

        int spawnGroupCount = Enum.GetValues(typeof(SpawnGroup)).Length;
        int totalGeneratedWaves = Mathf.Max(0, waveCount);
        for (int waveNumber = 1; waveNumber <= totalGeneratedWaves; waveNumber++)
        {
            int targetDanger = Mathf.Max(1, Mathf.RoundToInt((dangerScoreA * waveNumber * waveNumber) + (dangerScoreB * waveNumber) + dangerScoreC));
            int targetWaveValue = EvaluateWaveValueTarget(waveNumber);
            float waveProgress01 = EvaluateWaveProgress01(waveNumber, Mathf.Max(1, totalGeneratedWaves));
            var eligibleCatalog = FilterCatalogByProgress(enemyCatalog, waveProgress01);

            if (eligibleCatalog.Count == 0)
            {
                Debug.LogWarning($"Wave {waveNumber}: no eligible enemies at progress {waveProgress01:0.###}. Check EnemyManager enemy balance progressionMinimum values.", this);
                generated.waves.Add(new WaveDataSO.WaveData
                {
                    enemyDatas = new List<EnemyData>(),
                    totalWaveValue = targetWaveValue
                });
                continue;
            }

            int targetDiversity = Mathf.RoundToInt((enemyDiversityB * waveNumber) + enemyDiversityC);
            targetDiversity = Mathf.Clamp(targetDiversity, 1, eligibleCatalog.Count);

            var selected = SelectUniqueEnemiesForWave(eligibleCatalog, targetDiversity, waveNumber);
            EnemyCatalogEntry lowestDangerEnemy = selected[0];
            for (int i = 1; i < selected.Count; i++)
            {
                if (selected[i].danger < lowestDangerEnemy.danger)
                {
                    lowestDangerEnemy = selected[i];
                }
            }

            var counts = new Dictionary<Enemy.ID, int>(selected.Count);
            int accumulatedDanger = 0;

            // Seed each selected enemy with one unit so the wave reaches target diversity.
            for (int i = 0; i < selected.Count; i++)
            {
                var e = selected[i];
                counts[e.tag] = 1;
                accumulatedDanger += e.danger;
            }

            var rng = new System.Random(unchecked(9187 + (waveNumber * 131)));
            while (accumulatedDanger < targetDanger)
            {
                int pick = rng.Next(0, selected.Count);
                var e = selected[pick];
                counts[e.tag] = counts[e.tag] + 1;
                accumulatedDanger += e.danger;
            }

            int totalEnemyCount = 0;
            foreach (var kvp in counts)
            {
                totalEnemyCount += Mathf.Max(0, kvp.Value);
            }

            int minimumForWave = Mathf.Max(0, minimumEnemiesPerWave + (minimumEnemyCountIncrease * (waveNumber - 1)));
            int neededForMinimum = Mathf.Max(0, minimumForWave - totalEnemyCount);
            if (neededForMinimum > 0)
            {
                if (!counts.TryGetValue(lowestDangerEnemy.tag, out int currentLowest))
                {
                    currentLowest = 0;
                }

                counts[lowestDangerEnemy.tag] = currentLowest + neededForMinimum;
                totalEnemyCount += neededForMinimum;
            }

            // Rule: at least 50% of enemies in each generated wave are the lowest-danger enemy.
            if (!counts.TryGetValue(lowestDangerEnemy.tag, out int lowestCount))
            {
                lowestCount = 0;
            }

            // 2 * lowestCount >= totalEnemyCount  => add x where x >= totalEnemyCount - 2*lowestCount
            int neededLowest = Mathf.Max(0, totalEnemyCount - (2 * lowestCount));
            if (neededLowest > 0)
            {
                counts[lowestDangerEnemy.tag] = lowestCount + neededLowest;
            }

            var enemyDatas = new List<EnemyData>(counts.Count);
            int spawnIndex = 0;
            foreach (var kvp in counts)
            {
                enemyDatas.Add(new EnemyData
                {
                    enemyTag = kvp.Key,
                    count = kvp.Value,
                    spawnGroup = (SpawnGroup)(spawnIndex % spawnGroupCount),
                    spawnRadius = .3f,
                    startDelay = 0,
                    intervalDelay = 1,
                });
                spawnIndex++;
            }

            generated.waves.Add(new WaveDataSO.WaveData
            {
                enemyDatas = enemyDatas,
                totalWaveValue = targetWaveValue
            });
        }

        string folderPath = "Assets/Generated";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }

        string basePath = folderPath + "/WaveDataSO_Generated.asset";
        string path = AssetDatabase.GenerateUniqueAssetPath(basePath);
        AssetDatabase.CreateAsset(generated, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        waveDataSO = generated;
        _cachedWaves = null;
        EditorUtility.SetDirty(this);

        Debug.Log($"Generated {generated.waves.Count} waves at {path} and assigned it to WaveManager.", this);

        if (Application.isPlaying)
        {
            ShowWaveData();
        }
#else
        Debug.LogWarning("GenerateWaveSO is only available in the Unity Editor.", this);
#endif
    }

    private struct EnemyCatalogEntry
    {
        public Enemy.ID tag;
        public int danger;
        public float progressionMinumum;
    }

    private List<EnemyCatalogEntry> BuildEnemyCatalog(EnemyManager manager)
    {
        var catalog = new List<EnemyCatalogEntry>();
        if (manager.enemyInfo == null) return catalog;

        for (int i = 0; i < manager.enemyInfo.Count; i++)
        {
            var info = manager.enemyInfo[i];
            if (info.prefab == null) continue;

            var enemy = info.prefab.GetComponent<Enemy>();
            if (enemy == null) continue;

            int danger = manager.GetDangerScore(enemy.id);
            catalog.Add(new EnemyCatalogEntry
            {
                tag = enemy.id,
                danger = danger,
                progressionMinumum = manager.GetProgressionMinimum(enemy.id)
            });
        }

        return catalog;
    }

    private static float EvaluateWaveProgress01(int waveNumber, int totalWaves)
    {
        if (totalWaves <= 1) return 1f;
        int clampedWave = Mathf.Clamp(waveNumber, 1, totalWaves);
        return (clampedWave - 1f) / (totalWaves - 1f);
    }

    private static List<EnemyCatalogEntry> FilterCatalogByProgress(List<EnemyCatalogEntry> source, float waveProgress01)
    {
        var filtered = new List<EnemyCatalogEntry>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry.progressionMinumum <= 0f || waveProgress01 > entry.progressionMinumum)
            {
                filtered.Add(entry);
            }
        }
        return filtered;
    }

    private List<EnemyCatalogEntry> SelectUniqueEnemiesForWave(List<EnemyCatalogEntry> source, int count, int waveNumber)
    {
        var working = new List<EnemyCatalogEntry>(source);
        var selected = new List<EnemyCatalogEntry>(Mathf.Clamp(count, 0, source.Count));
        var rng = new System.Random(unchecked(3121 + (waveNumber * 97)));

        int toTake = Mathf.Clamp(count, 0, working.Count);
        for (int i = 0; i < toTake; i++)
        {
            int idx = rng.Next(0, working.Count);
            selected.Add(working[idx]);
            working.RemoveAt(idx);
        }

        return selected;
    }

    private int EvaluateWaveValueTarget(int waveNumber)
    {
        return Mathf.Max(0, Mathf.RoundToInt((waveValueA * waveNumber * waveNumber) + (waveValueB * waveNumber) + waveValueC));
    }

    private float EvaluateHealthMultiplier(int waveNumber)
    {
        return (healthMultiplierA * waveNumber * waveNumber) + (healthMultiplierB * waveNumber) + healthMultiplierC;
    }

    private float EvaluateSpawningDuration(int waveNumber)
    {
        return Mathf.Max(0f, (spawningDurationA * waveNumber * waveNumber) + (spawningDurationB * waveNumber) + spawningDurationC);
    }

    private List<SpawnPlan> BuildSpawnPlans(WaveData wave)
    {
        var plans = new List<SpawnPlan>();
        if (wave.enemyDatas == null || wave.enemyDatas.Count == 0) return plans;

        int totalValue = Mathf.Max(0, wave.totalWaveValue);
        var entries = new List<(int groupIndex, int danger)>(64);
        var countsPerGroup = new List<int>(wave.enemyDatas.Count);

        for (int i = 0; i < wave.enemyDatas.Count; i++)
        {
            var data = wave.enemyDatas[i];
            int count = Mathf.Max(0, data != null ? data.count : 0);
            countsPerGroup.Add(count);

            int danger = GetDangerForEnemyData(data);
            for (int n = 0; n < count; n++)
            {
                entries.Add((i, danger));
            }
        }

        var flatAllocations = AllocateValuesByDanger(entries, totalValue);
        var perGroupAllocations = new List<int>[wave.enemyDatas.Count];
        for (int i = 0; i < perGroupAllocations.Length; i++)
        {
            perGroupAllocations[i] = new List<int>(countsPerGroup[i]);
        }

        int assignedSum = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            int groupIndex = entries[i].groupIndex;
            int value = i < flatAllocations.Count ? flatAllocations[i] : 0;
            perGroupAllocations[groupIndex].Add(value);
            assignedSum += value;
        }

        if (assignedSum != totalValue)
        {
            Debug.LogWarning($"Wave value allocation mismatch: assigned={assignedSum} target={totalValue}", this);
        }

        for (int i = 0; i < wave.enemyDatas.Count; i++)
        {
            plans.Add(new SpawnPlan
            {
                data = wave.enemyDatas[i],
                individualValues = perGroupAllocations[i]
            });
        }

        return plans;
    }

    private int GetDangerForEnemyData(EnemyData data)
    {
        if (data == null || enemyManager == null)
            return 1;

        return enemyManager.GetDangerScore(data.enemyTag);
    }

    private List<int> AllocateValuesByDanger(List<(int groupIndex, int danger)> entries, int totalValue)
    {
        var allocations = new List<int>(entries.Count);
        if (entries.Count == 0)
            return allocations;

        for (int i = 0; i < entries.Count; i++) allocations.Add(0);
        if (totalValue <= 0)
            return allocations;

        long totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            totalWeight += Mathf.Max(1, entries[i].danger);
        }

        if (totalWeight <= 0)
            return allocations;

        int allocated = 0;
        var remainders = new float[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            float exact = (Mathf.Max(1, entries[i].danger) / (float)totalWeight) * totalValue;
            int floor = Mathf.FloorToInt(exact);
            allocations[i] = floor;
            remainders[i] = exact - floor;
            allocated += floor;
        }

        int remaining = totalValue - allocated;
        for (int r = 0; r < remaining; r++)
        {
            int bestIdx = 0;
            float bestRem = float.MinValue;
            for (int i = 0; i < remainders.Length; i++)
            {
                if (remainders[i] > bestRem)
                {
                    bestRem = remainders[i];
                    bestIdx = i;
                }
            }

            allocations[bestIdx] += 1;
            remainders[bestIdx] = -1f;
        }

        return allocations;
    }
}
