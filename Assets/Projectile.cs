using UnityEngine;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    public enum ProjectileType
    {
        Constant,
        Homing,
    }
    [HideInInspector] public Rigidbody2D rb;
    private CircleCollider2D col;

    [Tooltip("Tower that fired this projectile.")]
    public Tower sourceTower;

    [Tooltip("Pool that owns this projectile (optional). If set, projectile will be returned instead of destroyed.")]
    public ProjectileObjectPool pool;

    // Runtime config
    private ProjectileType _type = ProjectileType.Constant;
    private Enemy _target;
    private float _speed;
    private float _projectileAcceleration;
    private float _baseXScale;
    private float _baseZScale;
    private float _sizeMultiplier = 1f;
    private bool _spawnScaleLerpActive;
    private Vector3 _spawnScaleTarget;
    [SerializeField, Min(0f)] private float spawnScaleLerpSpeed = 14f;

    [Header("Piercing")]
    [Tooltip("How many enemies this projectile can damage before despawning.1 = default (no pierce).")]
    [Min(1)]
    [SerializeField] private int pierceCount = 1;

    private int _remainingPierces;
    private bool _spent;
    private bool _pendingDespawnAfterHit;
    private readonly HashSet<Health> _hitHealth = new HashSet<Health>();
    public Tower.CustomDamageData data = new Tower.CustomDamageData();
    public List<Effect> effectOverrideList;

    private float _lifetimeTimer = -1f;
    private float lifetime = -1f;
    private RaycastHit2D[] raycastResults = new RaycastHit2D[64];
    private ContactFilter2D wallCastContactFilter;

    [Header("Homing")]
    [Tooltip("How fast a homing projectile can turn towards its target (degrees per second).")]
    [SerializeField] private float steeringSpeed = 360f;
    private const float HomingRetargetRadius = 3f;
    private bool _homingRetargettingEnabled;
    private readonly List<Enemy> _homingRetargetCandidates = new List<Enemy>(16);

    [Header("Lifetime")]
    [Tooltip("If true, projectile will self-destruct after traveling its range.")]
    [SerializeField] private bool useLifetime = true;

    private List<Tower> fieldTowers = new List<Tower>();
    public SpriteRenderer chargeEffectVisualizer;
    public SpriteRenderer damageBuffEffectVisualizer;

    [Tooltip("If false, this projectile will not trigger OnHit particle VFX on hit.")]
    public bool triggerOnHitVfx = true;

    [Header("Hit Indicator")]
    [Range(0f, 1f)]
    [SerializeField] private float hitIndicatorTowardsEnemyLerp = 0.5f;

    private SpriteRenderer sr;
    public ParticleSystem ps;
    private bool _isSoftReleasingForParticles;

    [Header("Bounce")]
    [Tooltip("Search radius used by Projectile Bounce upgrade to find another enemy target.")]
    [Min(0f)]
    [SerializeField] private float bounceSearchRadius = 10f;

    private readonly List<Enemy> _bounceCandidates = new List<Enemy>(16);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
        _baseXScale = transform.localScale.x;
        _baseZScale = transform.localScale.z;
        StopParticleVfx();
    }

    public void SetOutlineColor(CM.ColorType color)
    {
        if (chargeEffectVisualizer == null || CM.i == null) return;
        Color c = CM.i.ColorTypeToColor(color);
        if (color != CM.ColorType.None && sr != null) c.a = sr.color.a;
        chargeEffectVisualizer.color = c;
    }

    public void SetChargeEffectActive(bool active)
    {
        if (chargeEffectVisualizer == null) return;
        chargeEffectVisualizer.enabled = active;
    }

    public void SetDamageBuffEffectActive(bool active)
    {
        if (damageBuffEffectVisualizer == null) return;
        damageBuffEffectVisualizer.enabled = active;
    }

    private void Start()
    {
        wallCastContactFilter = new ContactFilter2D();
        wallCastContactFilter.layerMask = LayerMaskManager.instance.wallLayerMask;
    }

    public void AddFieldEffect(Tower tower)
    {
        if (fieldTowers.Contains(tower))
        {
            return;
        }
        fieldTowers.Add(tower);
    }

    public List<Tower> GetFieldEffects()
    {
        return fieldTowers;
    }

    private void OnEnable()
    {
        _isSoftReleasingForParticles = false;
        _spawnScaleLerpActive = false;
        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;
        SetOutlineColor(CM.ColorType.None);
        SetChargeEffectActive(false);
        SetDamageBuffEffectActive(false);
        StopParticleVfx();
        fieldTowers.Clear();
        _remainingPierces = Mathf.Max(1, pierceCount);
        _spent = false;
        _pendingDespawnAfterHit = false;
        _hitHealth.Clear();
        _lifetimeTimer = float.MaxValue;
        triggerOnHitVfx = true;
    }

    public void OnSpawnedFromPool()
    {
        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;
        StopParticleVfx();
    }

    public void OnReturnedToPool()
    {
        StopParticleVfx();
    }



    private void OnDisable()
    {
        _spawnScaleLerpActive = false;
        SetChargeEffectActive(false);
        SetDamageBuffEffectActive(false);
        StopParticleVfx();
        if (sourceTower != null)
        {
            sourceTower.OnProjectileReturned(this);
        }
        if (rb != null) rb.linearVelocity = Vector2.zero;
        _target = null;
        _speed = 0f;
        _projectileAcceleration = 0f;
        _type = ProjectileType.Constant;
        _lifetimeTimer = float.MaxValue;
        _sizeMultiplier = 1f;
        _pendingDespawnAfterHit = false;
        effectOverrideList = null;
        _homingRetargettingEnabled = false;
        _homingRetargetCandidates.Clear();
        OnReturnedToPool();
    }

    private void Update()
    {
        if (useLifetime && Time.fixedTime > _lifetimeTimer)
        {
            //AOEObjectPool.instance.PlayTimerIndicator(transform.position, 1, Color.white, 1);
            DestroySelf();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (_spawnScaleLerpActive)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, spawnScaleLerpSpeed) * Time.fixedDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, _spawnScaleTarget, t);
            if ((transform.localScale - _spawnScaleTarget).sqrMagnitude <= 0.000001f)
            {
                transform.localScale = _spawnScaleTarget;
                _spawnScaleLerpActive = false;
            }
        }

        _speed = Mathf.Max(0f, _speed + (_projectileAcceleration * Time.fixedDeltaTime));

        if (_spawnScaleLerpActive)
        {
            return;
        }

        ApplyScaleFromSpeed();

        if (_type == ProjectileType.Constant)
        {
            if (_speed <= 0f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 dir = rb.linearVelocity.sqrMagnitude > 0.000001f
                ? rb.linearVelocity.normalized
                : (Vector2)transform.up;
            rb.linearVelocity = dir * _speed;
            return;
        }

        if (_type != ProjectileType.Homing) return;

        if (!IsValidHomingTarget(_target))
        {
            TryRetargetHomingTarget();
        }

        if (!IsValidHomingTarget(_target))
        {
            if (_speed <= 0f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            if (rb.linearVelocity.sqrMagnitude > 0.000001f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * _speed;
            }
            return;
        }

        Vector2 toTarget = (_target.rb != null ? _target.rb.position : (Vector2)_target.transform.position) - rb.position;
        if (toTarget.sqrMagnitude < 0.000001f) return;

        // Face the target with steering
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, steeringSpeed * Time.fixedDeltaTime);

        // Use the steered direction so the projectile curves into the target
        // instead of snapping velocity directly at it (which causes oscillation
        // and missed collisions at close range).
        rb.linearVelocity = _speed > 0f ? (Vector2)transform.up * _speed : Vector2.zero;
    }

    private bool IsValidHomingTarget(Enemy enemy)
    {
        if (enemy == null) return false;
        if (!enemy.isActiveAndEnabled) return false;
        if (!enemy.gameObject.activeInHierarchy) return false;
        if (enemy.health != null && enemy.health.GetCurrentHealth() <= 0f) return false;
        return true;
    }

    private bool TryRetargetHomingTarget()
    {
        if (!_homingRetargettingEnabled) return false;
        if (QuadTree2D.instance == null || rb == null) return false;

        _homingRetargetCandidates.Clear();
        QuadTree2D.instance.QueryCircle(rb.position, HomingRetargetRadius, _homingRetargetCandidates);

        Enemy nearest = null;
        float nearestDistSqr = float.MaxValue;

        for (int i = 0; i < _homingRetargetCandidates.Count; i++)
        {
            Enemy candidate = _homingRetargetCandidates[i];
            if (!IsValidHomingTarget(candidate)) continue;

            Vector2 candidatePos = candidate.rb != null ? candidate.rb.position : (Vector2)candidate.transform.position;
            float distSqr = (candidatePos - rb.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = candidate;
            }
        }

        if (nearest == null) return false;
        _target = nearest;
        return true;
    }

    private void ApplyScaleFromSpeed()
    {
        float xScale = _baseXScale * _sizeMultiplier;
        float zScale = _baseZScale * _sizeMultiplier;
        var poolRef = pool != null ? pool : ProjectileObjectPool.instance;
        if (poolRef == null || poolRef.projectileYScaleSpeedReference <= 0f)
        {
            transform.localScale = new Vector3(xScale, xScale, zScale);
            return;
        }

        float i = Mathf.Clamp01(_speed / poolRef.projectileYScaleSpeedReference);
        float yScale = Mathf.Lerp(xScale, poolRef.projectileYScaleMax * _sizeMultiplier, i);
        transform.localScale = new Vector3(xScale, yScale, zScale);
    }

    /// <summary>
    /// Arms a self-destruct timer based on <paramref name="distance"/> and <paramref name="speed"/>.
    /// This avoids doing distance checks every frame.
    /// </summary>
    public void ArmLifetime(float distance, float speed)
    {
        if (!useLifetime) return;
        float maxDistance = Mathf.Max(0f, distance);
        float s = Mathf.Max(0.0001f, speed);
        lifetime = maxDistance / s;
        _lifetimeTimer = Time.fixedTime + lifetime;
    }

    /// <summary>
    /// Arms a self-destruct timer using a direct duration in seconds.
    /// </summary>
    public void ArmLifetimeDirect(float seconds)
    {
        if (!useLifetime) return;
        lifetime = Mathf.Max(0f, seconds);
        _lifetimeTimer = Time.fixedTime + lifetime;
    }

    public void ApplyColor(Color c)
    {
        if (sr != null) sr.color = c;

    }

    public void ConfigureParticleVfx(bool enabled, Color color)
    {
        if (ps == null) return;
        _isSoftReleasingForParticles = false;

        if (!enabled)
        {
            ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        var shape = ps.shape;
        shape.radius = transform.localScale.x;

        var main = ps.main;
        Color startColor = color;
        startColor.a = 1f;
        main.startColor = startColor;
        ps.Play(withChildren: true);
    }

    public bool BeginSoftReleaseForParticleVfx()
    {
        if (ps == null) return false;

        bool isLive = ps.IsAlive(withChildren: true) || ps.particleCount > 0;
        if (!isLive) return false;

        _isSoftReleasingForParticles = true;
        ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;

        SetChargeEffectActive(false);
        SetDamageBuffEffectActive(false);

        return true;
    }

    public bool HasLiveParticleVfx()
    {
        if (ps == null) return false;
        return ps.IsAlive(withChildren: true) || ps.particleCount > 0;
    }

    public void RestoreAfterSoftRelease()
    {
        _isSoftReleasingForParticles = false;
        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;
    }

    private void StopParticleVfx()
    {
        if (_isSoftReleasingForParticles) return;
        if (ps == null) return;
        ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void IndicateHit(CM.ColorType color, Vector3 enemyPosition)
    {
        if (HitIndicatorObjectPool.instance == null) return;

        float t = Mathf.Clamp01(hitIndicatorTowardsEnemyLerp);
        Vector3 indicatorPosition = Vector3.Lerp(transform.position, enemyPosition, t);
        HitIndicatorObjectPool.instance.IndicateProjectileHit(indicatorPosition, color);
    }

    private void DestroySelf()
    {
        _spent = true;
        ReturnToPoolOrDestroy();
    }

    private void ReturnToPoolOrDestroy()
    {
        if (pool != null)
        {
            pool.Release(gameObject);
        }
        else
        {
            Debug.LogError("No pooL");
            Destroy(gameObject);
        }
    }

    public void Deactivate()
    {
        if (_spent) return;
        _spent = true;
        ReturnToPoolOrDestroy();
    }

    public bool TryRegisterHit(Health h)
    {
        if (h == null) return false;
        if (_spent) return false;

        // Only consume pierce once per unique target. This prevents inconsistent counts caused by
        // multi-collider enemies or multiple trigger callbacks.
        if (_hitHealth.Contains(h)) return false;
        _hitHealth.Add(h);

        _remainingPierces--;
        if (_remainingPierces <= 0)
        {
            _pendingDespawnAfterHit = true;
        }

        return true;
    }

    public bool WillDespawnAfterCurrentHit()
    {
        return _pendingDespawnAfterHit && _remainingPierces <= 0;
    }

    public void AddPierce(int amount)
    {
        if (amount <= 0) return;
        if (_spent) return;

        _remainingPierces += amount;
        if (_remainingPierces > 0)
        {
            _pendingDespawnAfterHit = false;
        }
    }

    public bool TryBounceToNearbyEnemy(Enemy enemyJustHit)
    {
        if (_spent) return false;
        if (_type != ProjectileType.Constant) return false;
        if (_remainingPierces <= 0) return false;
        if (sourceTower == null || !sourceTower.UpgradeActive(UpgradeData.UID.ProjectileBounce)) return false;
        if (QuadTree2D.instance == null || rb == null) return false;

        float radius = Mathf.Max(0f, bounceSearchRadius);
        if (radius <= 0.0001f) return false;

        Vector2 origin = rb.position;
        _bounceCandidates.Clear();
        QuadTree2D.instance.QueryCircle(origin, radius, _bounceCandidates, enemyJustHit);

        Enemy nearest = null;
        float nearestDistSqr = float.MaxValue;

        for (int i = 0; i < _bounceCandidates.Count; i++)
        {
            Enemy candidate = _bounceCandidates[i];
            if (candidate == null || candidate == enemyJustHit) continue;

            Vector2 candidatePos = candidate.rb != null ? candidate.rb.position : (Vector2)candidate.transform.position;
            float distSqr = (candidatePos - origin).sqrMagnitude;
            if (distSqr <= 0.000001f) continue;

            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = candidate;
            }
        }

        if (nearest == null) return false;

        Vector2 nearestPos = nearest.rb != null ? nearest.rb.position : (Vector2)nearest.transform.position;
        Vector2 direction = (nearestPos - origin).normalized;
        if (direction.sqrMagnitude <= 0.000001f) return false;

        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        rb.linearVelocity = direction * _speed;
        return true;
    }

    public void FinalizeHit()
    {
        if (_spent) return;
        if (!_pendingDespawnAfterHit) return;
        if (_remainingPierces > 0)
        {
            _pendingDespawnAfterHit = false;
            return;
        }

        _pendingDespawnAfterHit = false;
        _spent = true;
        ReturnToPoolOrDestroy();
    }

    public void SetSizeMultiplier(float multiplier, bool lerpToTargetOnSpawn = false)
    {
        _sizeMultiplier = Mathf.Max(0f, multiplier);

        _spawnScaleTarget = new Vector3(
            _baseXScale * _sizeMultiplier,
            _baseXScale * _sizeMultiplier,
            _baseZScale * _sizeMultiplier);

        if (lerpToTargetOnSpawn)
        {
            transform.localScale = _spawnScaleTarget * 0.2f;
            _spawnScaleLerpActive = true;
        }
        else
        {
            transform.localScale = _spawnScaleTarget;
            _spawnScaleLerpActive = false;
        }
    }

    // Optional external configuration from towers.
    public void SetSteeringSpeed(float speed) => steeringSpeed = Mathf.Max(0f, speed);

    public void SetPierceCount(int count)
    {
        // Homing projectiles should never pierce.
        if (_type == ProjectileType.Homing)
        {
            pierceCount = 1;
            _remainingPierces = 1;
            return;
        }

        pierceCount = Mathf.Max(1, count);
        _remainingPierces = pierceCount;
    }

    /// <summary>
    /// Configure projectile movement behavior.
    /// </summary>
    public void Configure(ProjectileType type, Enemy target, float speed, Tower t, float projectileAcceleration = 0f, bool homingRetargettingEnabled = false)
    {
        sourceTower = t;
        _type = type;
        _target = target;
        _speed = Mathf.Max(0f, speed);
        _projectileAcceleration = projectileAcceleration;
        _homingRetargettingEnabled = homingRetargettingEnabled;

        ApplyScaleFromSpeed();

        // Homing projectiles should never pierce.
        if (_type == ProjectileType.Homing)
        {
            pierceCount = 1;
            _remainingPierces = 1;
        }
        //else if (_type == ProjectileType.Constant)
        //{
        //    PreComputeWallCollisions(target.transform.position - transform.position);
        //}
    }

    //public void PreComputeWallCollisions(Vector2 direction)
    //{
    //    wallsPierced = 0;
    //    int wallCnt = Physics2D.CircleCast(transform.position, col.radius, direction, contactFilter: wallCastContactFilter, raycastResults, sourceTower.GetRange());
    //    wallPierceCount = 2;
    //    if (wallCnt == 0)
    //    {
    //        return;
    //    }
    //    if ()
    //    for (int i = 0; i < wallCnt; i++)
    //    {
    //        Wall wall = raycastResults[i].collider.gameObject.GetComponent<Wall>();
    //    }

    //}

    //public void OnTriggerEnter2D(Collider2D collision)
    //{
    //    if (collision.CompareTag("Wall"))
    //    {
    //        wallsPierced++;
    //        if (wallsPierced >= wallPierceCount)
    //        {
    //            ReturnToPoolOrDestroy();
    //        }

    //    }
    //}
}
