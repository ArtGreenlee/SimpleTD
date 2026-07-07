using UnityEngine;

public class LaserTower : Tower
{
    public enum LaserMode
    {
        Normal,
        Cardinal
    }

    // public bool canShootThroughWalls = false;
    // public bool tryGetOptimalTarget = true;

    [Header("Attack Mode")]
    [SerializeField] private LaserMode laserMode = LaserMode.Normal;

    [Header("Bounces")]
    [Tooltip("Delay before each bounced segment fires, in seconds per world unit of the preceding segment.")]
    [SerializeField, Min(0f)] private float bounceDelayPerUnit = 0.01f;
    [SerializeField, Min(0f)] private float bounceSurfaceOffset = 0.01f;
    [Tooltip("Random angle variation (in degrees) applied to each bounce reflection.")]
    [SerializeField, Min(0f)] private float bounceAngleRandomRange = 0f;
    [Tooltip("Fraction of laser width removed per bounce (0.05 = 5% thinner each bounce).")]
    [SerializeField, Range(0f, 0.95f)] private float bounceWidthFalloffPerBounce = 0.05f;

    [Header("Wall Hit VFX")]
    [SerializeField, Min(1)] private int wallHitMinParticles = 2;
    [SerializeField, Min(1)] private int wallHitMaxParticles = 5;

    [Header("Wall Exit")]
    [Tooltip("Step size used to move a laser start point out of walls before raycasts begin.")]
    [SerializeField, Min(0.001f)] private float initialWallExitStep = 0.05f;
    [Tooltip("Maximum distance a shot may be advanced to escape wall overlap at spawn.")]
    [SerializeField, Min(0f)] private float maxInitialWallExitDistance = 2f;
    [Tooltip("Small padding added once outside the wall to avoid immediate re-contact.")]
    [SerializeField, Min(0f)] private float initialWallExitPadding = 0.01f;

    [Header("Segment Cap")]
    [Tooltip("Maximum travel distance per segment when no wall is hit. Acts as a safety cap.")]
    [SerializeField, Min(1f)] private float segmentMaxDistance = 50f;

    [Header("Body Rotation")]
    [SerializeField, Min(0f)] private float bodyRotationLerpSpeed = 30f;
    [SerializeField, Min(0f)] private float idleBodySweepAngle = 12f;
    [SerializeField, Min(0f)] private float idleBodySweepSpeed = 2f;

    private float _desiredBodyAngle;
    private bool _hasDesiredBodyAngle;
    private float _idleBaseBodyAngle;
    private bool _idleBaseBodyAngleInitialized;

    // Track last state so we only rebuild the filter when needed.
    // private bool _lastCanShootThroughWalls;

    public override void Awake()
    {
        base.Awake();
    }

    public override void Start()
    {
        base.Start();

        // _lastCanShootThroughWalls = canShootThroughWalls;
    }

    // Multi-hit base-damage bonus is handled generically in Tower.GetDamageModified via CustomDamageData.numHit.

    public override string GetUpgradeDescription(UpgradeData.UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }

    public override void ActivateUpgrade(UpgradeData.UID uid)
    {
        if (uid == UpgradeData.UID.LaserBonusBounce)
        {
            AddBaseBounceCount(1);
        }
        base.ActivateUpgrade(uid);
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

    public LaserMode CurrentLaserMode => laserMode;

    public override void Attack()
    {
        base.Attack();

        if (laserMode == LaserMode.Cardinal)
        {
            AttackCardinal();
            return;
        }

        if (bulletSpawnTransform == null || rangeManager == null || targettedEnemy == null) return;

        Vector2 origin = bulletSpawnTransform.position;

        float width = LaserObjectPool.instance.laserWidth;
        Vector3 targetPosition = Vector3.zero;
        if (rangeManager.GetTargettingMode() == RangeManager.TargettingMode.Manual)
        {
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
        Vector3 toTarget = targetPosition - bulletSpawnTransform.position;
        toTarget.z = 0f;
        if (toTarget.sqrMagnitude < 0.000001f) return;
        Vector2 dir = ((Vector2)toTarget).normalized;

        if (rotateBodyWhenAttacking && bulletSpawnTransform != null)
        {
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
           SetDesiredBodyAngle(angleDeg);
        }

        float consumedDistance;
        origin = MoveOriginOutsideWalls(origin, dir, out consumedDistance);

        if (LaserHelper.instance != null)
        {
            LaserHelper.instance.LaserAttackHelper(
                this, (Vector3)origin, (Vector3)dir, width, GetColor(),
                applyEffects: true,
                segmentMaxDistance: segmentMaxDistance,
                bounceSurfaceOffset: bounceSurfaceOffset,
                bounceAngleRandomRange: bounceAngleRandomRange,
                bounceWidthFalloffPerBounce: bounceWidthFalloffPerBounce,
                bounceDelayPerUnit: bounceDelayPerUnit,
                wallHitMinParticles: wallHitMinParticles,
                wallHitMaxParticles: wallHitMaxParticles);
        }
        // if (id == Tower.ID.Sun)
        // {
        //     rangeManager.RotateManualTarget(10);
        // }
    }

    private void AttackCardinal()
    {
        if (bulletSpawnTransform == null || LaserHelper.instance == null) return;

        float width = LaserObjectPool.instance != null ? LaserObjectPool.instance.laserWidth : 0.5f;
        Vector2 origin = bulletSpawnTransform.position;
        Vector2[] directions =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 dir = directions[i];
            float consumedDistance;
            Vector2 attackOrigin = MoveOriginOutsideWalls(origin, dir, out consumedDistance);

            if (rotateBodyWhenAttacking && bulletSpawnTransform != null && i == 0)
            {
                float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                SetDesiredBodyAngle(angleDeg);
            }

            LaserHelper.instance.LaserAttackHelper(
                this, (Vector3)attackOrigin, (Vector3)dir, width, GetColor(),
                applyEffects: true,
                segmentMaxDistance: segmentMaxDistance,
                bounceSurfaceOffset: bounceSurfaceOffset,
                bounceAngleRandomRange: bounceAngleRandomRange,
                bounceWidthFalloffPerBounce: bounceWidthFalloffPerBounce,
                bounceDelayPerUnit: bounceDelayPerUnit,
                wallHitMinParticles: wallHitMinParticles,
                wallHitMaxParticles: wallHitMaxParticles);
        }
    }

    private Vector2 MoveOriginOutsideWalls(Vector2 origin, Vector2 direction, out float consumedDistance)
    {
        consumedDistance = 0f;

        int wallMask = LayerMaskManager.instance != null ? LayerMaskManager.instance.wallLayerMask : 0;
        if (wallMask == 0)
        {
            return origin;
        }

        float step = Mathf.Max(0.001f, initialWallExitStep);
        float maxExitDistance = Mathf.Max(0f, maxInitialWallExitDistance);
        Vector2 dir = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
        Vector2 current = origin;

        while (consumedDistance < maxExitDistance && Physics2D.OverlapPoint(current, wallMask) != null)
        {
            current += dir * step;
            consumedDistance += step;
        }

        if (consumedDistance > 0f)
        {
            float padding = Mathf.Min(initialWallExitPadding, maxExitDistance - consumedDistance);
            if (padding > 0f)
            {
                current += dir * padding;
                consumedDistance += padding;
            }
        }

        return current;
    }
}
