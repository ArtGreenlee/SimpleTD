using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UID = UpgradeData.UID;

public class ProjectileTower : Tower
{
    public static int goldTowerProjectileCost = 2;
    private static readonly GridManager.Direction[] MineDirections =
    {
        GridManager.Direction.Up,
        GridManager.Direction.Right,
        GridManager.Direction.Down,
        GridManager.Direction.Left,
    };

    public enum ProjectileTowerMode
    {
        Standard,
        Mine,
    }

    public enum ProjectileSpeedMode
    {
        Constant,
        MinMax,
    }

    public enum ProjectileLifetimeMode
    {
        DistanceBased,
        Fixed,
    }

    public enum ValueMode
    {
        Constant,
        RandomBetweenConstants,
    }

    public enum ProjectileColorMode
    {
        Constant,
        RandomBetweenConstants,
    }

    private GameObject bulletPrefab;
    [SerializeField] private ProjectileTowerMode projectileTowerMode = ProjectileTowerMode.Standard;
    [SerializeField] private ProjectileSpeedMode projectileSpeedMode = ProjectileSpeedMode.Constant;
    [Min(0f)]
    [SerializeField] private float bulletSpeed = 10f;
    [Min(0f)]
    [SerializeField] private float projectileMinSpeed = 8f;
    [Min(0f)]
    [SerializeField] private float projectileMaxSpeed = 12f;

    [Header("Projectile Acceleration")]
    [FormerlySerializedAs("speedDecayMode")]
    [SerializeField] private ValueMode projectileAccelerationMode = ValueMode.Constant;
    [FormerlySerializedAs("speedDecay")]
    [SerializeField] private float projectileAcceleration = 0f;
    [FormerlySerializedAs("speedDecayMin")]
    [SerializeField] private float projectileAccelerationMin = 0f;
    [FormerlySerializedAs("speedDecayMax")]
    [SerializeField] private float projectileAccelerationMax = 0f;

    public Projectile.ProjectileType projectileType = Projectile.ProjectileType.Constant;

    [Header("Projectile")]
    [Tooltip("How many enemies each bullet can damage before despawning.1 = default (no pierce).")]
    [SerializeField] private ValueMode pierceCountMode = ValueMode.Constant;
    [Min(1)]
    [SerializeField] private int pierceCount = 1;
    [Min(1)]
    [SerializeField] private int pierceCountMin = 1;
    [Min(1)]
    [SerializeField] private int pierceCountMax = 1;

    [Tooltip("If false, projectiles fired by this tower will not play OnHit VFX.")]
    [SerializeField] private bool triggerOnHitVfx = true;

    [Tooltip("If true, projectiles fired by this tower enable attached particle-system VFX.")]
    public bool enableProjectileParticleVfx = false;

    [Tooltip("Multiplier applied to the projectile's travel distance (and therefore lifetime). 1 = default range.")]
    [Min(0f)]
    [SerializeField] private float projectileLifetimeModifier = 1f;

    [Tooltip("DistanceBased: lifetime is derived from range and speed. Fixed: projectile lives for a set number of seconds.")]
    [SerializeField] private ProjectileLifetimeMode projectileLifetimeMode = ProjectileLifetimeMode.DistanceBased;

    [Tooltip("Lifetime in seconds when ProjectileLifetimeMode is set to Fixed.")]
    [Min(0f)]
    [SerializeField] private float fixedProjectileLifetime = 1f;

    [Header("Projectile Size")]
    [Tooltip("Uniform scale multiplier applied to each fired projectile. 1 = default size.")]
    [SerializeField] private ValueMode projectileSizeMultiplierMode = ValueMode.Constant;
    [Min(0f)]
    [SerializeField] private float projectileSizeMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float projectileSizeMultiplierMin = 1f;
    [Min(0f)]
    [SerializeField] private float projectileSizeMultiplierMax = 1f;

    [Header("Projectile Color")]
    [Tooltip("When true, projectiles use custom color settings instead of the tower damage color.")]
    [SerializeField] private bool customColor = false;
    [SerializeField] private ProjectileColorMode projectileColorMode = ProjectileColorMode.Constant;
    [SerializeField] private CM.ColorType projectileConstantColorType = CM.ColorType.White;
    [SerializeField] private Color projectileColorMin = Color.white;
    [SerializeField] private Color projectileColorMax = Color.white;
    [Tooltip("Overrides the alpha of every projectile fired by this tower. 1 = fully opaque, 0 = fully transparent.")]
    [Range(0f, 1f)]
    [SerializeField] private float projectileAlphaOverride = 1f;

    [Header("Multi-Shot")]
    [Tooltip("Number of projectiles fired per attack.")]
    [Min(1)]
    [SerializeField] private int shotsPerAttack = 1;

    [Tooltip("If true, each projectile is fired at a random angle within the arc defined by degreesPerShot * shotsPerAttack.")]
    [SerializeField] private bool randomArc = false;

    [Tooltip("Degrees between each projectile in a multishot spread.")]
    [Min(0f)]
    [SerializeField] private float degreesPerShot = 10f;

    [Header("Random Fire")]
    [Tooltip("When true, each shot aims at a random valid point inside this tower's RangeManager bounds.")]
    [SerializeField] private bool random = false;

    [Header("Manual Target Decay")]
    [Tooltip("If enabled while RangeManager is in Manual mode, each projectile gets speed decay so it stops within a radius around the manual target indicator.")]
    [SerializeField] private bool manualTargetDecayEnabled = false;
    [Tooltip("Radius around the manual target indicator where projectiles should stop when Manual Target Decay is enabled.")]
    [SerializeField, Min(0f)] private float manualTargetDecayRadius = 0.5f;

    [Header("Homing")]
    [Tooltip("Degrees per second a homing projectile can steer toward its target.")]
    [Min(0f)]
    [SerializeField] private float homingSteeringSpeed = 360f;
    [Tooltip("If enabled, homing projectiles retarget to the nearest enemy when their current target dies.")]
    [SerializeField] private bool homingRetargetting = false;

    [Header("Body Rotation")]
    [SerializeField, Min(0f)] private float bodyRotationLerpSpeed = 30f;
    [SerializeField, Min(0f)] private float idleBodySweepAngle = 12f;
    [SerializeField, Min(0f)] private float idleBodySweepSpeed = 2f;

    private bool _nextAttackHoming;
    private float _desiredBodyAngle;
    private bool _hasDesiredBodyAngle;
    private float _idleBaseBodyAngle;
    private bool _idleBaseBodyAngleInitialized;
    private readonly Dictionary<Vector2Int, Projectile> _activeMineProjectilesByCell = new Dictionary<Vector2Int, Projectile>();
    private readonly Dictionary<Projectile, Vector2Int> _activeMineProjectileCellsByProjectile = new Dictionary<Projectile, Vector2Int>();

    private bool IsMineMode()
    {
        return projectileTowerMode == ProjectileTowerMode.Mine || id == ID.Mine;
    }

    public bool IsMineModeActive()
    {
        return IsMineMode();
    }

    private void SyncMineModeSettings()
    {
        if (IsMineMode())
        {
            attackWithoutTarget = true;
        }
    }

    public override void Awake()
    {
        base.Awake();
        SyncMineModeSettings();
    }

    public override void Start()
    {
        base.Start();
        SyncMineModeSettings();
    }

    private void OnDisable()
    {
        ClearMineProjectiles();
    }

    private void OnDestroy()
    {
        ClearMineProjectiles();
    }

    public override void OnProjectileReturned(Projectile projectile)
    {
        if (projectile == null) return;
        if (!_activeMineProjectileCellsByProjectile.TryGetValue(projectile, out Vector2Int cell)) return;

        _activeMineProjectileCellsByProjectile.Remove(projectile);
        if (_activeMineProjectilesByCell.TryGetValue(cell, out Projectile tracked) && tracked == projectile)
        {
            _activeMineProjectilesByCell.Remove(cell);
        }
    }

    public static void ReleaseMineProjectilesForPlacedWalls(IList<Vector2Int> wallCells)
    {
        if (wallCells == null || wallCells.Count == 0) return;
        if (TowerManager.instance == null) return;

        foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower is not ProjectileTower projectileTower) continue;
            if (!projectileTower.IsMineMode()) continue;

            projectileTower.ReleaseMineProjectilesIntersectingWalls(wallCells);
        }
    }

    private void ReleaseMineProjectilesIntersectingWalls(IList<Vector2Int> wallCells)
    {
        if (_activeMineProjectilesByCell.Count == 0) return;

        List<Projectile> projectilesToDeactivate = null;
        for (int i = 0; i < wallCells.Count; i++)
        {
            Vector2Int wallCell = wallCells[i];
            if (!_activeMineProjectilesByCell.TryGetValue(wallCell, out Projectile projectile)) continue;

            _activeMineProjectilesByCell.Remove(wallCell);
            if (projectile == null)
            {
                continue;
            }

            _activeMineProjectileCellsByProjectile.Remove(projectile);
            if (projectilesToDeactivate == null)
            {
                projectilesToDeactivate = new List<Projectile>(wallCells.Count);
            }

            projectilesToDeactivate.Add(projectile);
        }

        if (projectilesToDeactivate == null) return;

        for (int i = 0; i < projectilesToDeactivate.Count; i++)
        {
            Projectile projectile = projectilesToDeactivate[i];
            if (projectile != null)
            {
                projectile.Deactivate();
            }
        }
    }

    private void ClearMineProjectiles()
    {
        if (_activeMineProjectileCellsByProjectile.Count == 0)
        {
            _activeMineProjectilesByCell.Clear();
            return;
        }

        List<Projectile> projectilesToRemove = new List<Projectile>(_activeMineProjectileCellsByProjectile.Keys);
        _activeMineProjectileCellsByProjectile.Clear();
        _activeMineProjectilesByCell.Clear();

        for (int i = 0; i < projectilesToRemove.Count; i++)
        {
            Projectile projectile = projectilesToRemove[i];
            if (projectile != null)
            {
                projectile.Deactivate();
            }
        }
    }

    public void ClearPlacedMineProjectiles()
    {
        ClearMineProjectiles();
    }

    public override void OnPickedUp()
    {
        base.OnPickedUp();

        if (IsMineMode())
        {
            ClearMineProjectiles();
        }
    }

    private bool TryGetMineDirection(out GridManager.Direction direction)
    {
        direction = GridManager.Direction.Up;

        switch (towerRotation)
        {
            case CardinalRotation.North:
                direction = GridManager.Direction.Up;
                return true;
            case CardinalRotation.East:
                direction = GridManager.Direction.Right;
                return true;
            case CardinalRotation.South:
                direction = GridManager.Direction.Down;
                return true;
            case CardinalRotation.West:
                direction = GridManager.Direction.Left;
                return true;
            default:
                return false;
        }
    }

    private bool IsMineCellBlocked(Vector2Int cellIndex)
    {
        if (GridManager.instance == null) return true;
        if (!GridManager.instance.TryGetCell(cellIndex.x, cellIndex.y, out var cell)) return true;
        if (cell.IsBlocked || cell.IsWall) return true;
        if (GridManager.instance.TryGetTowerAtCell(cellIndex, out var tower) && tower != null) return true;
        return false;
    }

    private bool TryGetMineSpawnCell(out Vector2Int cellIndex, out Vector3 worldPosition)
    {
        for (int i = 0; i < MineDirections.Length; i++)
        {
            if (TryGetMineSpawnCellInDirection(MineDirections[i], out cellIndex, out worldPosition))
            {
                return true;
            }
        }

        cellIndex = default;
        worldPosition = transform.position;
        return false;
    }

    private bool TryGetMineSpawnCellInDirection(GridManager.Direction direction, out Vector2Int cellIndex, out Vector3 worldPosition)
    {
        cellIndex = default;
        worldPosition = transform.position;

        GridManager grid = GridManager.instance;
        if (grid == null) return false;

        Vector2Int step = grid.DirectionToVector2Int(direction);
        if (step == Vector2Int.zero) return false;

        Vector2Int startCell = grid.WorldToGrid(transform.position);
        if (startCell.x < 0 || startCell.y < 0) return false;

        Vector2Int current = startCell + step;
        while (grid.TryGetCell(current.x, current.y, out var cell))
        {
            if (!grid.IsInMazeBounds(current)) break;
            if (cell.IsBlocked || cell.IsWall) break;

            bool occupiedByMineProjectile = _activeMineProjectilesByCell.TryGetValue(current, out var activeProjectile)
                && activeProjectile != null;
            if (!occupiedByMineProjectile)
            {
                cellIndex = current;
                worldPosition = cell.WorldCenter;
                return true;
            }
            current += step;
        }

        return false;
    }

    public bool TryGetMineSpawnPreviewCells(Vector3 towerWorldPosition, List<Vector2Int> previewCells)
    {
        if (previewCells == null) return false;
        previewCells.Clear();

        if (!IsMineMode()) return false;

        GridManager grid = GridManager.instance;
        if (grid == null) return false;
        if (!grid.TryWorldToCell(towerWorldPosition, out var startCell)) return false;

        for (int i = 0; i < MineDirections.Length; i++)
        {
            AddMineSpawnPreviewCellsInDirection(grid, startCell, MineDirections[i], previewCells);
        }

        return previewCells.Count > 0;
    }

    private static void AddMineSpawnPreviewCellsInDirection(
        GridManager grid,
        Vector2Int startCell,
        GridManager.Direction direction,
        List<Vector2Int> previewCells)
    {
        Vector2Int step = grid.DirectionToVector2Int(direction);
        if (step == Vector2Int.zero) return;

        Vector2Int current = startCell + step;
        while (grid.TryGetCell(current.x, current.y, out var cell))
        {
            if (!grid.IsInMazeBounds(current)) break;
            if (cell.IsBlocked || cell.IsWall) break;
            if (grid.TryGetTowerAtCell(current, out var towerAtCell) && towerAtCell != null) break;

            previewCells.Add(current);
            current += step;
        }
    }

    public Color GetMinePreviewColor()
    {
        CM.ColorType configuredType = GetConfiguredDamageType();
        if (CM.i != null && CM.i.colorDictionary != null && CM.i.colorDictionary.TryGetValue(configuredType, out var color))
        {
            return color;
        }

        return GetColor();
    }

    private void SpawnMineProjectile()
    {
        GridManager grid = GridManager.instance;
        if (grid == null) return;

        for (int i = 0; i < MineDirections.Length; i++)
        {
            GridManager.Direction direction = MineDirections[i];
            if (!TryGetMineSpawnCellInDirection(direction, out var cellIndex, out var spawnPosition))
            {
                continue;
            }

            SpawnMineProjectileAtCell(grid, direction, cellIndex, spawnPosition);
        }
    }

    private void SpawnMineProjectileAtCell(
        GridManager grid,
        GridManager.Direction direction,
        Vector2Int cellIndex,
        Vector3 spawnPosition)
    {
        Vector2Int step = grid.DirectionToVector2Int(direction);
        if (step == Vector2Int.zero) return;

        Color shotColor = GetProjectileColorForShot();
        shotColor.a = projectileAlphaOverride;
        float shotSize = GetProjectileSizeMultiplierForShot();

        var helper = ProjectileHelper.instance;
        if (helper == null)
        {
            return;
        }

        Projectile projectile = helper.FireProjectile(
            start: spawnPosition,
            direction: new Vector3(step.x, step.y, 0f),
            mode: Projectile.ProjectileType.Constant,
            speed: 0f,
            source: this,
            target: null,
            size: shotSize,
            color: shotColor,
            projectileAcceleration: 0f,
            lifetime: -1f,
            pierceCount: 1,
            baseDamageRatio: towerDamageData.baseDamageRatio,
            triggerOnHitVfx: triggerOnHitVfx,
            enableParticleVfx: enableProjectileParticleVfx,
            steeringSpeed: 0f,
            homingRetargetting: false,
            lerpScaleToIntendedScaleOnSpawn: true);

        if (projectile == null) return;

        _activeMineProjectilesByCell[cellIndex] = projectile;
        _activeMineProjectileCellsByProjectile[projectile] = cellIndex;
        projectile.rb.linearVelocity = Vector2.zero;
    }

    public override float GetCooldown(CustomDamageData data = null)
    {
        float cooldown = base.GetCooldown(data);
        if (IsMineMode() && (WaveManager.instance == null || !WaveManager.instance.IsWaveActive()))
        {
            cooldown *= 0.01f;
        }

        return Mathf.Max(0f, cooldown);
    }

    public void EnableNextAttackHoming()
    {
        _nextAttackHoming = true;
    }

    public override int GetPierceCount() => pierceCount;
    public void SetPierceCount(int value)
    {
        pierceCount = Mathf.Max(1, value);
        pierceCountMin = Mathf.Max(1, pierceCountMin);
        pierceCountMax = Mathf.Max(1, pierceCountMax);
    }
    public void DoubleShotsPerAttack() => shotsPerAttack *= 2;

    private int GetPierceCountForShot()
    {
        if (pierceCountMode == ValueMode.RandomBetweenConstants)
        {
            int min = Mathf.Min(pierceCountMin, pierceCountMax);
            int max = Mathf.Max(pierceCountMin, pierceCountMax);
            return Random.Range(min, max + 1);
        }

        return Mathf.Max(1, pierceCount);
    }

    private float GetProjectileSizeMultiplierForShot()
    {
        if (projectileSizeMultiplierMode == ValueMode.RandomBetweenConstants)
        {
            float min = Mathf.Min(projectileSizeMultiplierMin, projectileSizeMultiplierMax);
            float max = Mathf.Max(projectileSizeMultiplierMin, projectileSizeMultiplierMax);
            return Random.Range(min, max);
        }

        return Mathf.Max(0f, projectileSizeMultiplier);
    }

    private float GetProjectileAccelerationForShot()
    {
        if (projectileAccelerationMode == ValueMode.RandomBetweenConstants)
        {
            float min = Mathf.Min(projectileAccelerationMin, projectileAccelerationMax);
            float max = Mathf.Max(projectileAccelerationMin, projectileAccelerationMax);
            return Random.Range(min, max);
        }

        return projectileAcceleration;
    }

    private Color GetProjectileColorForShot()
    {
        if (!customColor) return GetColor();

        if (projectileColorMode == ProjectileColorMode.RandomBetweenConstants)
        {
            return Color.Lerp(projectileColorMin, projectileColorMax, Random.value);
        }

        if (CM.i != null)
        {
            return CM.i.ColorTypeToColor(projectileConstantColorType);
        }

        return Color.white;
    }

    protected override float GetRangeMultiplier(float multiplier, CustomDamageData data = null)
    {
        if (UpgradeActive(UID.ProjectileSpeedToRange))
        {
            multiplier += UpgradeData.ProjectileSpeedToRangeRangeIncreasePercent / 100f;
        }
        return base.GetRangeMultiplier(multiplier, data);
    }

    protected override float GetDamageMultiplier(float multiplier, CustomDamageData data = null)
    {
        if (id == ID.Sniper && data != null)
        {
            float halfRangeSqr = (GetRange() * GetRange() / 2f);
            float distanceSqr = (data.enemyHit.transform.position - transform.position).sqrMagnitude;

            if (UpgradeActive(UID.LongRangeDamageBonus) && distanceSqr > halfRangeSqr)
            {
                multiplier += 1f;
            }

            if (UpgradeActive(UID.ShortRangeDamageBonus) && distanceSqr < halfRangeSqr)
            {
                multiplier += 1f;
            }
        }

        return base.GetDamageMultiplier(multiplier, data);
    }

    public override void OnConsumeMark(Enemy enemy)
    {
        if (UpgradeActive(UID.GoldOnConsumeMark))
        {
            CurrencyManager.instance.AddCurrency(1, transform.position);
        }
    }
    public override string GetUpgradeDescription(UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }

    public override void ActivateUpgrade(UID uid)
    {
        base.ActivateUpgrade(uid);

        if (uid == UID.SetProjectileHoming)
        {
            projectileType = Projectile.ProjectileType.Homing;
        }
    }

    private int GetShotsPerAttackForCurrentState()
    {
        int effectiveShots = Mathf.Max(1, shotsPerAttack);
        if (!UpgradeActive(UID.GatlingConditionExtraProjectile)) return effectiveShots;
        if (TowerManager.instance == null) return effectiveShots;

        int requiredCount = Mathf.Max(1, UpgradeData.ConditionalBonusProjectileRequiredPlacedCount);
        int placedCount = TowerManager.instance.GetPlacedTowerCountById(UpgradeData.ConditionalBonusProjectileRequiredTowerId);
        if (placedCount < requiredCount) return effectiveShots;

        return effectiveShots + UpgradeData.ConditionalBonusProjectileAdditionalShots;
    }

    protected override void OnUpgrade(int level)
    {
        base.OnUpgrade(level);
    }

    public override void Update()
    {
        base.Update();
        UpdateTowerBodyRotation();
    }

    private void UpdateTowerBodyRotation()
    {
        if (!rotateBodyWhenAttacking || bulletSpawnTransform == null) return;

        if (ShouldPlayIdleBodyAnimation())
        {
            if (!_idleBaseBodyAngleInitialized)
            {
                _idleBaseBodyAngle = bulletSpawnTransform.eulerAngles.z;
                _idleBaseBodyAngleInitialized = true;
            }

            _desiredBodyAngle = _idleBaseBodyAngle + Mathf.Sin(Time.time * idleBodySweepSpeed) * idleBodySweepAngle;
            _hasDesiredBodyAngle = true;
        }
        else
        {
            _idleBaseBodyAngleInitialized = false;
        }

        if (!_hasDesiredBodyAngle) return;

        float currentAngle = bulletSpawnTransform.eulerAngles.z;
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, bodyRotationLerpSpeed) * Time.deltaTime);
        float nextAngle = Mathf.LerpAngle(currentAngle, _desiredBodyAngle, t);
        bulletSpawnTransform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    private bool ShouldPlayIdleBodyAnimation()
    {
        return CurrentState == State.Placed
            && WaveManager.instance != null
            && !WaveManager.instance.IsWaveActive();
    }

    private void SetDesiredBodyAngle(float angleDeg)
    {
        _desiredBodyAngle = angleDeg;
        _hasDesiredBodyAngle = true;
        _idleBaseBodyAngleInitialized = false;
    }

    public void ShootProjectile(Vector3 spawn, Vector3 direction, float projectileSpeed, float? projectileAccelerationOverride = null, Projectile.ProjectileType? projectileTypeOverride = null, bool chargedShot = false)
    {
        Projectile.ProjectileType shotProjectileType = projectileTypeOverride ?? projectileType;
        float configuredProjectileAcceleration = projectileAccelerationOverride ?? GetProjectileAccelerationForShot();
        int shotPierce = GetPierceCountForShot();
        float shotSize = GetProjectileSizeMultiplierForShot();
        if (UpgradeActive(UID.IncreasePierce))
        {
            shotPierce++;
        }

        CM.ColorType shotDamageType = GetDamageType(towerDamageData, true);
        Color shotColor = customColor ? GetProjectileColorForShot() : CM.i.ColorTypeToColor(shotDamageType);
        shotColor.a = projectileAlphaOverride;

        float lifetimeSeconds = -1f;
        if (projectileLifetimeMode == ProjectileLifetimeMode.Fixed)
        {
            lifetimeSeconds = fixedProjectileLifetime;
        }
        else if (shotProjectileType == Projectile.ProjectileType.Constant && rangeManager.GetLos())
        {
            float dist = rangeManager.GetDistance(direction) * projectileLifetimeModifier;
            lifetimeSeconds = projectileSpeed > 0.0001f ? dist / projectileSpeed : 0f;
        }
        else if (shotProjectileType == Projectile.ProjectileType.Homing)
        {
            float dist = rangeManager.GetDistance(direction) * 1.5f * projectileLifetimeModifier;
            lifetimeSeconds = projectileSpeed > 0.0001f ? dist / projectileSpeed : 0f;
        }
        else
        {
            float dist = GetRange() * projectileLifetimeModifier;
            lifetimeSeconds = projectileSpeed > 0.0001f ? dist / projectileSpeed : 0f;
        }

        var helper = ProjectileHelper.instance;
        if (helper != null)
        {
            var proj = helper.FireProjectile(
                start: spawn,
                direction: direction,
                mode: shotProjectileType,
                speed: projectileSpeed,
                source: this,
                target: targettedEnemy,
                size: shotSize,
                color: shotColor,
                projectileAcceleration: configuredProjectileAcceleration,
                lifetime: lifetimeSeconds,
                pierceCount: shotPierce,
                baseDamageRatio: towerDamageData.baseDamageRatio,
                triggerOnHitVfx: triggerOnHitVfx,
                enableParticleVfx: enableProjectileParticleVfx,
                steeringSpeed: homingSteeringSpeed,
                homingRetargetting: homingRetargetting);

            if (proj != null)
            {
                proj.data.damageType = shotDamageType;
                if (chargedShot)
                {
                    proj.SetOutlineColor(CM.ColorType.White);
                }
                return;
            }
        }

        // Fallback path if helper/pool are unavailable.
        ProjectileObjectPool pool = ProjectileObjectPool.instance;
        if (pool == null || pool.projectilePrefab == null) return;

        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);
        GameObject bulletObj = pool.Get(spawn, rotation);
        if (bulletObj == null)
        {
            bulletObj = Instantiate(pool.projectilePrefab, spawn, rotation);
        }
        if (bulletObj == null) return;

        var fallbackProj = bulletObj.GetComponent<Projectile>();
        if (fallbackProj == null) return;

        fallbackProj.sourceTower = this;
        fallbackProj.pool = pool;
        fallbackProj.triggerOnHitVfx = triggerOnHitVfx;
        fallbackProj.data.enemyHit = null;
        fallbackProj.data.hitColliders = null;
        fallbackProj.data.numHit = 1;
        fallbackProj.data.critCount = RollCritCount();
        fallbackProj.data.crit = fallbackProj.data.critCount > 0;
        fallbackProj.data.baseDamageRatio = towerDamageData.baseDamageRatio;
        fallbackProj.data.damageType = shotDamageType;
        fallbackProj.SetPierceCount(shotPierce);
        fallbackProj.ApplyColor(shotColor);
        fallbackProj.ConfigureParticleVfx(enableProjectileParticleVfx, shotColor);
        pool.RefreshAllIn1Properties(bulletObj);
        if (chargedShot)
        {
            fallbackProj.SetOutlineColor(CM.ColorType.White);
        }
        fallbackProj.Configure(shotProjectileType, targettedEnemy, projectileSpeed, this, configuredProjectileAcceleration, homingRetargetting);
        fallbackProj.SetSteeringSpeed(homingSteeringSpeed);
        fallbackProj.SetSizeMultiplier(shotSize, IsMineMode());
        if (lifetimeSeconds >= 0f)
        {
            fallbackProj.ArmLifetimeDirect(lifetimeSeconds);
        }
        if (fallbackProj.rb != null)
        {
            fallbackProj.rb.linearVelocity = direction.normalized * projectileSpeed;
        }
    }

    public void AddProjectile()
    {
        shotsPerAttack = Mathf.Max(1, shotsPerAttack + 1);
    }

    private bool IsNearBank()
    {
        if (rangeManager == null) return false;

        var towersInsideRange = rangeManager.GetAllActiveTowersInRange();
        foreach (Tower tower in towersInsideRange)
        {
            if (tower is BankTower)
                return true;
        }

        return false;
    }

    public override void OnHitEnemy(Enemy enemy)
    {
        if (UpgradeActive(UID.MagicExposedExtraShot) && enemy != null && enemy.HasStatusEffect(Enemy.StatusEffect.Exposed))
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            ShootProjectile(bulletSpawnTransform.position, new Vector3(randomDir.x, randomDir.y, 0f), GetProjectileSpeedForShot());
        }
    }

    protected override void OnCriticalHit(CustomDamageData data)
    {

        base.OnCriticalHit(data);
    }

    public float GetProjectileSpeedForShot()
    {
        float speed;
        if (projectileSpeedMode == ProjectileSpeedMode.MinMax)
        {
            float min = Mathf.Min(projectileMinSpeed, projectileMaxSpeed);
            float max = Mathf.Max(projectileMinSpeed, projectileMaxSpeed);
            speed = Random.Range(min, max);
        }
        else
        {
            speed = bulletSpeed;
        }

        if (UpgradeActive(UID.ProjectileSpeedToRange))
        {
            speed *= 1f - UpgradeData.ProjectileSpeedToRangeSpeedReductionPercent / 100f;
        }

        return speed;
    }

    private bool TryGetRandomShotDirection(out Vector3 direction, out Vector3 sampledPoint)
    {
        direction = Vector3.up;
        sampledPoint = bulletSpawnTransform != null ? bulletSpawnTransform.position : transform.position;

        if (rangeManager == null)
        {
            Vector2 fallback = Random.insideUnitCircle;
            if (fallback.sqrMagnitude <= 0.000001f) return false;
            fallback.Normalize();
            direction = new Vector3(fallback.x, fallback.y, 0f);
            sampledPoint = sampledPoint + direction;
            return true;
        }

        Vector3 center = rangeManager.transform.position;
        center.z = 0f;
        float baseRange = Mathf.Max(0.001f, GetRange());

        const int maxAttempts = 24;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 sampleOffset = Random.insideUnitCircle * baseRange;
            Vector3 candidatePoint = center + new Vector3(sampleOffset.x, sampleOffset.y, 0f);

            if (!rangeManager.PointInsideRange(candidatePoint)) continue;

            Vector3 fromSpawn = candidatePoint - bulletSpawnTransform.position;
            fromSpawn.z = 0f;
            if (fromSpawn.sqrMagnitude <= 0.000001f) continue;

            direction = fromSpawn.normalized;
            sampledPoint = candidatePoint;
            return true;
        }

        return false;
    }

    private static float ComputeAccelerationToStopAtDistance(float initialSpeed, float distance)
    {
        float v = Mathf.Max(0f, initialSpeed);
        float d = Mathf.Max(0.0001f, distance);
        if (v <= 0.000001f) return 0f;

        return -(v * v) / (2f * d);
    }

    private Vector3 GetManualTargetStopPoint(Vector3 indicatorPosition)
    {
        float radius = Mathf.Max(0f, manualTargetDecayRadius);
        if (radius <= 0.0001f)
        {
            return indicatorPosition;
        }

        Vector2 offset = Random.insideUnitCircle * radius;
        return indicatorPosition + new Vector3(offset.x, offset.y, 0f);
    }

    private void TryMoveManualTargetIndicatorToRandomEnemy()
    {
        if (!UpgradeActive(UID.ManualTargetMover)) return;
        if (rangeManager == null || towerTool == null) return;

        var enemies = rangeManager.GetAllEnemiesInRange();
        if (enemies == null || enemies.Count == 0) return;

        Enemy chosen = enemies[Random.Range(0, enemies.Count)];
        if (chosen == null) return;

        Vector3 p = chosen.transform.position;
        p.z = towerTool.transform.position.z;
        towerTool.transform.position = p;
    }

    public override void Attack()
    {
        if (IsMineMode())
        {
            SpawnMineProjectile();
            _nextAttackHoming = false;
            return;
        }

        if (targettedEnemy == null && !attackWithoutTarget) return;
        Vector3 targetPosition = Vector3.zero;
        bool isManualTargetting = rangeManager.GetTargettingMode() == RangeManager.TargettingMode.Manual;
        if (isManualTargetting)
        {
            TryMoveManualTargetIndicatorToRandomEnemy();
            targetPosition = towerTool.transform.position;
        }
        else if (targettedEnemy == null)
        {
            targetPosition = transform.position + Random.insideUnitSphere;
            targetPosition.z = 0;
        }
        else
        {
            targetPosition = targettedEnemy.transform.position;
        }
        base.Attack();
        Vector3 toTarget = targetPosition - bulletSpawnTransform.position;
        toTarget.z = 0f;
		if (toTarget.sqrMagnitude > 0.000001f && !rangeManager.IsSingleRay() && rotateBodyWhenAttacking)
        {
            float angleDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
            SetDesiredBodyAngle(angleDeg);
        }
        else if (rangeManager.IsSingleRay() && rotateBodyWhenAttacking)
        {
			Vector2 rayDir = rangeManager.GetSingleRayDirectionWorld();
			if (rayDir.sqrMagnitude > 0.000001f)
			{
				float angleDeg = Mathf.Atan2(rayDir.y, rayDir.x) * Mathf.Rad2Deg - 90f;
               SetDesiredBodyAngle(angleDeg);
				toTarget = new Vector3(rayDir.x, rayDir.y, 0f);
                
			}
            
        }

        Projectile.ProjectileType attackProjectileType = _nextAttackHoming ? Projectile.ProjectileType.Homing : projectileType;
        bool chargedShot = UsedChargeForLastAttack();
        int effectiveShotsPerAttack = GetShotsPerAttackForCurrentState();

      float arc = degreesPerShot * (effectiveShotsPerAttack - 1);
        float halfArc = arc * 0.5f;

        for (int i = 0; i < effectiveShotsPerAttack; i++)
        {
            if (id == ID.GoldProjectile)
            {
                int cost = goldTowerProjectileCost;
                if (UpgradeActive(UID.AddAOEIncreaseCost))
                {
                    cost += Mathf.Max(0, UpgradeData.AddAOEIncreaseCostProjectileCostIncrease);
                }
                if (UpgradeActive(UID.BankDiscount) && IsNearBank())
                {
                    cost = Mathf.Max(0, Mathf.RoundToInt(cost * (1f - UpgradeData.BankDiscountPercent / 100f)));
                }
                if (CurrencyManager.instance.GetCurrency() < cost) continue;
                CurrencyManager.instance.RemoveCurrency(cost);
                CurrencyManager.instance.ShowCurrencyText(-cost, transform.position + (Vector3)(Random.insideUnitCircle / 4));
            }
            Vector3 shotDirection = toTarget;
            float? shotProjectileAccelerationOverride = null;
            Vector3 randomStopPoint = Vector3.zero;
            bool hasRandomStopPoint = false;
            if (random && TryGetRandomShotDirection(out var randomDirection, out var randomPoint))
            {
                shotDirection = randomDirection;
                randomStopPoint = randomPoint;
                hasRandomStopPoint = true;
            }
            else if (!randomArc && effectiveShotsPerAttack > 1)
            {
              float angle = Mathf.Lerp(-halfArc, halfArc, effectiveShotsPerAttack == 1 ? 0.5f : (float)i / (effectiveShotsPerAttack - 1));
                shotDirection = Quaternion.Euler(0f, 0f, angle) * toTarget;
            }
            else if (randomArc && effectiveShotsPerAttack > 1)
            {
                float angle = Random.Range(-halfArc, halfArc);
                shotDirection = Quaternion.Euler(0f, 0f, angle) * toTarget;
            }
            float shotSpeed = GetProjectileSpeedForShot();
            if (random && hasRandomStopPoint)
            {
                float stopDistance = Vector2.Distance(bulletSpawnTransform.position, randomStopPoint);
                shotProjectileAccelerationOverride = ComputeAccelerationToStopAtDistance(shotSpeed, stopDistance);
            }

            if (manualTargetDecayEnabled && isManualTargetting)
            {
                Vector3 stopPoint = GetManualTargetStopPoint(targetPosition);
                float stopDistance = Vector2.Distance(bulletSpawnTransform.position, stopPoint);
                shotProjectileAccelerationOverride = ComputeAccelerationToStopAtDistance(shotSpeed, stopDistance);
            }

            ShootProjectile(bulletSpawnTransform.position, shotDirection, shotSpeed, shotProjectileAccelerationOverride, attackProjectileType, chargedShot);
        }

        _nextAttackHoming = false;
    }
}
