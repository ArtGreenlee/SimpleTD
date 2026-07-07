using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public enum CombatMode
    {
        Default,
        Laser,
    }

    public static int activeAgentCount = 0;

    public float moveSpeed = 5f;
    public Transform agentBody;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float engageDistance = 1f;
    [SerializeField] private float disengageDistanceMultiplier = 1.25f;
    [SerializeField] private float damageEnemyCooldown = 1f;
    [SerializeField] private CombatMode combatMode = CombatMode.Default;
    [SerializeField] private float laserWidth = 0.08f;
    [SerializeField] private bool laserAppliesEffects = true;
    [Header("Necromancer")]
    [Tooltip("If > 0 and this agent is owned by a Necromancer tower, health decays by this amount per second while a wave is active.")]
    [SerializeField, Min(0f)] private float necromancerHealthDecayRate = 0f;
    [Header("Idle Behavior")]
    [SerializeField] private bool enableIdleBehavior = true;
    [SerializeField] private float idleEnterDistance = 0.75f;
    [SerializeField] private float idleWanderRadius = 1.25f;
    [SerializeField] private float idleWanderMinDistance = 0.35f;
    [SerializeField] private float idleMoveArriveDistance = 0.12f;
    [SerializeField] private Vector2 idleWaitSecondsRange = new Vector2(0.5f, 1.1f);
    [SerializeField] private Vector2 idleLookChangeSecondsRange = new Vector2(0.25f, 0.65f);
    [SerializeField] private int idleWanderCandidateAttempts = 10;
    private Enemy targettedEnemy;
    private Tower ownerTower;
    private Rigidbody2D rb;
    private Health health;
    private SwellAnimation swellAnimation;
    private float nextDamageTime;
    private readonly HashSet<Enemy> nearbyEnemies = new HashSet<Enemy>();
    private Enemy claimedEnemy;
    private Enemy movementLockedEnemy;
    private bool isEngagingTarget;
    private bool deathExplosionTriggered;
    private bool isDespawning;
    private bool isNecromancerAgent;
    private float baseMaxHealth = -1f;
    private float lastAppliedMaxHealth = -1f;
    private float damageMultiplier = 1f;
    private readonly HashSet<Enemy> _stolenGoldFrom = new HashSet<Enemy>();

    private enum IdleState
    {
        None,
        Moving,
        Waiting,
    }

    private IdleState idleState = IdleState.None;
    private Vector3 idleAnchorPosition;
    private Vector3 idleWanderDestination;
    private Vector3 idleLookDirection;
    private float idleWaitUntilTime;
    private float idleNextLookChangeTime;

    private void Awake()
    {
        if (agentBody == null)
        {
            agentBody = transform;
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody2D>();
        }

        if (rb == null)
        {
            Debug.LogWarning("Agent requires a Rigidbody2D to move.", this);
        }

        health = GetComponent<Health>();
        if (health == null)
        {
            health = GetComponentInChildren<Health>();
        }

        swellAnimation = GetComponent<SwellAnimation>();

        if (health != null)
        {
            health.SetShowFloatingDamageText(false);
            baseMaxHealth = health.GetMaxHealth();
            lastAppliedMaxHealth = baseMaxHealth;
        }
    }

    private void OnEnable()
    {
        activeAgentCount++;
        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyAgentsOnField(activeAgentCount);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Existing enemy engagement logic
        var enemy = GetEnemyFromCollider(collision);
        if (enemy != null)
        {
            nearbyEnemies.Add(enemy);
            TryAcquireTargetPreferUnengaged();
            return;
        }

        // Agent-to-agent engagement logic
        var otherAgent = collision.GetComponent<Agent>();
        if (otherAgent != null && otherAgent != this)
        {
            // If one agent is engaged and the other is not, engage the same enemy
            if (this.targettedEnemy == null && otherAgent.targettedEnemy != null)
            {
                EngageEnemy(otherAgent.targettedEnemy);
            }
            else if (this.targettedEnemy != null && otherAgent.targettedEnemy == null)
            {
                otherAgent.EngageEnemy(this.targettedEnemy);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        var enemy = GetEnemyFromCollider(collision);
        if (enemy == null) return;

        nearbyEnemies.Add(enemy);
        TryAcquireTargetPreferUnengaged();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        var enemy = GetEnemyFromCollider(collision);
        if (enemy == null) return;

        nearbyEnemies.Remove(enemy);
        if (targettedEnemy == enemy)
        {
            isEngagingTarget = false;
            EngageEnemy(null);
            TryAcquireTargetPreferUnengaged();
        }
    }

    public void EngageEnemy(Enemy enemy)
    {
        if (targettedEnemy == enemy) return;

        ResetIdleBehavior();
        UpdateMovementLock(null, engaged: false);
        isEngagingTarget = false;

        ReleaseClaimedEnemy();

        targettedEnemy = enemy;
        claimedEnemy = null;

        ClaimEnemyIfPossible(targettedEnemy);
    }

    private void OnDisable()
    {
        activeAgentCount = Mathf.Max(0, activeAgentCount - 1);

        OnHitParticleEffect.instance.OnHitVfx(transform.position, CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.White) : Color.white);
        TryExplodeOnDeath();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        ResetIdleBehavior();
        UpdateMovementLock(null, engaged: false);
        nearbyEnemies.Clear();
        _stolenGoldFrom.Clear();

        ReleaseClaimedEnemy();

        targettedEnemy = null;
        isEngagingTarget = false;
    }

    public void SetOwnerTower(Tower tower)
    {
        ownerTower = tower;
        RefreshYellowTagHealthBonus();
    }

    public void SetAgentTower(AgentTower tower)
    {
        SetOwnerTower(tower);
        isNecromancerAgent = tower != null && tower.id == Tower.ID.Necromancer;
    }

    public List<LensTower> GetLens()
    {
        List<LensTower> result = new List<LensTower>();
        if (TowerManager.instance == null) return result;

        foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            LensTower lensTower = tower as LensTower;
            if (lensTower == null) continue;

            Lens controller = lensTower.lensController;
            if (controller == null)
            {
                controller = lensTower.GetComponentInChildren<Lens>();
                lensTower.lensController = controller;
            }

            if (controller == null) continue;
            if (!controller.HasAgentInRange(this)) continue;

            result.Add(lensTower);
        }

        return result;
    }

    public void SetCombatMode(CombatMode mode)
    {
        combatMode = mode;
    }

    public void SetColor(CM.ColorType colorType)
    {
        SRC src = GetComponentInChildren<SRC>();
        if (src != null)
        {
            src.ApplyColorToAll(colorType);
        }
    }

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(0f, multiplier);
    }

    public float GetDamageMultiplier()
    {
        return damageMultiplier;
    }

    public void RefreshYellowTagHealthBonus()
    {
        if (health == null) return;

        if (baseMaxHealth <= 0f)
        {
            baseMaxHealth = health.GetMaxHealth();
        }

        float multiplier = 1f;
        if (TagManager.instance != null)
        {
            multiplier = TagManager.instance.GetYellowTagAgentMaxHealthMultiplier();
        }

        float desiredMaxHealth = baseMaxHealth * Mathf.Max(0.0001f, multiplier);
        if (Mathf.Abs(desiredMaxHealth - lastAppliedMaxHealth) <= 0.001f)
        {
            return;
        }

        lastAppliedMaxHealth = desiredMaxHealth;
        health.SetMaxHealth(desiredMaxHealth);
    }

    private void FixedUpdate()
    {
        if (rb == null || isDespawning) return;

        TickNecromancerDecay(Time.fixedDeltaTime);

        if (targettedEnemy != null && !targettedEnemy.gameObject.activeInHierarchy)
        {
            EngageEnemy(null);
        }

        if (targettedEnemy != null && !targettedEnemy.CanBeEngagedByAgents())
        {
            EngageEnemy(null);
        }

        if (targettedEnemy == null)
        {
            TryAcquireTargetPreferUnengaged();
        }

        Vector3 destination = Vector3.zero;
        bool hasDestination = false;

        if (targettedEnemy != null)
        {
            destination = targettedEnemy.transform.position;
            hasDestination = true;
        }
        else if (ownerTower != null)
        {
            destination = GetDesiredPosition();
            hasDestination = true;
        }

        Vector2 velocity = Vector2.zero;
        if (hasDestination)
        {
            Vector3 moveDir = Vector3.zero;

            bool handledByIdle = targettedEnemy == null && TryHandleIdleBehavior(destination, out moveDir);
            if (!handledByIdle)
            {
                ResetIdleBehavior();
            }

            if (!handledByIdle)
            {
                float distSqr = (destination - transform.position).sqrMagnitude;
                float engageDist = Mathf.Max(0.01f, engageDistance);
                float disengageDist = engageDist * Mathf.Max(1f, disengageDistanceMultiplier);

                bool targetStillNearby = targettedEnemy != null && nearbyEnemies.Contains(targettedEnemy);
                if (isEngagingTarget)
                {
                    if (targettedEnemy == null || !targetStillNearby || distSqr > disengageDist * disengageDist)
                    {
                        isEngagingTarget = false;
                        UpdateMovementLock(null, engaged: false);
                    }
                }
                else if (targettedEnemy != null && targetStillNearby && distSqr <= engageDist * engageDist)
                {
                    isEngagingTarget = true;
                }

                if (!isEngagingTarget && distSqr > engageDist * engageDist)
                {
                    moveDir = GetMoveDirectionTowards(destination);
                }
                else if (isEngagingTarget && targettedEnemy != null && ownerTower != null)
                {
                    RotateTowards(targettedEnemy.transform.position - transform.position);
                    UpdateMovementLock(targettedEnemy, engaged: true);
                    targettedEnemy.TryDamageAgents();

                    if (Time.time >= nextDamageTime)
                    {
                        Vector3 enemyPos = targettedEnemy != null ? targettedEnemy.transform.position : transform.position;
                        Color towerColor = ownerTower != null ? ownerTower.GetColor() : Color.white;

                        bool dealtDamage = TryDealEngageDamage(enemyPos);
                        if (dealtDamage)
                        {
                            ShowCombatLaser(transform.position, enemyPos, towerColor);
                        }

                        nextDamageTime = Time.time + Mathf.Max(0.01f, damageEnemyCooldown);
                    }
                }
                else
                {
                    UpdateMovementLock(null, engaged: false);
                    isEngagingTarget = false;
                }
            }

            if (moveDir.sqrMagnitude > 0.000001f)
            {
                RotateTowards(moveDir);
                float speed = moveSpeed * (RM.i != null && RM.i.Active(RM.ID.speedyAgent) ? 2f : 1f);
                velocity = (Vector2)moveDir * speed;
            }
        }

        rb.linearVelocity = velocity;

        // Keep transform in XY gameplay plane.
        var p = transform.position;
        if (Mathf.Abs(p.z) > 0.0001f)
        {
            p.z = 0f;
            transform.position = p;
        }
    }

    private void TickNecromancerDecay(float dt)
    {
        if (!isNecromancerAgent) return;
        if (necromancerHealthDecayRate <= 0f) return;
        if (health == null) return;
        if (WaveManager.instance == null || !WaveManager.instance.IsWaveActive()) return;

        health.TakeDamage(necromancerHealthDecayRate * dt, ownerTower as Tower);
    }

    private Enemy GetEnemyFromCollider(Collider2D collision)
    {
        if (collision == null) return null;
        var enemy = collision.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = collision.GetComponentInParent<Enemy>();
        }
        return enemy;
    }

    private void TryAcquireTargetPreferUnengaged()
    {
        if (ownerTower == null || AgentHelper.instance == null) return;

        Enemy bestUnengaged = null;
        float bestUnengagedDist = float.PositiveInfinity;

        Enemy bestAny = null;
        float bestAnyDist = float.PositiveInfinity;

        foreach (var enemy in nearbyEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            if (!enemy.CanBeEngagedByAgents()) continue;

            float dist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (dist < bestAnyDist)
            {
                bestAnyDist = dist;
                bestAny = enemy;
            }

            bool engagedByOtherAgent = enemy != targettedEnemy && AgentHelper.instance.IsEnemyEngagedByAgent(ownerTower, enemy);
            if (!engagedByOtherAgent && dist < bestUnengagedDist)
            {
                bestUnengagedDist = dist;
                bestUnengaged = enemy;
            }
        }

        Enemy preferred = bestUnengaged != null ? bestUnengaged : bestAny;
        if (preferred != null && preferred != targettedEnemy)
        {
            EngageEnemy(preferred);
        }
    }

    public bool ReceiveEnemyDamage(Enemy enemy, float damage)
    {
        if (enemy == null || health == null) return false;
        if (damage <= 0f) return false;

        Vector3 enemyPos = enemy.transform.position;
        Color damageColor = CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.Red) : Color.red;
        health.TakeDamage(damage, source: null, damageTypeOverride: CM.ColorType.Red, customDamageData: null);
        ShowCombatLaser(enemyPos, transform.position, damageColor);
        return true;
    }

    private void ShowCombatLaser(Vector3 start, Vector3 end, Color color)
    {
        if (LaserObjectPool.instance == null) return;
        LaserObjectPool.instance.ShowLaser(start, end, color, 0.04f, 0.08f, 0.9f);
    }

    private void UpdateMovementLock(Enemy enemy, bool engaged)
    {
        // Release old lock if target changed or if disengaging.
        if (movementLockedEnemy != null && (movementLockedEnemy != enemy || !engaged))
        {
            movementLockedEnemy.SetAgentEngagement(this, engaged: false);
            movementLockedEnemy = null;
        }

        if (!engaged || enemy == null) return;

        enemy.SetAgentEngagement(this, engaged: true);
        movementLockedEnemy = enemy;
    }

    private void RotateTowards(Vector3 direction)
    {
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.000001f) return;

        Transform rotateTransform = agentBody != null ? agentBody : transform;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = rotateTransform.eulerAngles.z;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(0f, rotationSpeed) * Time.fixedDeltaTime);
        rotateTransform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    private void ResetIdleBehavior()
    {
        idleState = IdleState.None;
        idleWaitUntilTime = 0f;
        idleNextLookChangeTime = 0f;
        idleLookDirection = Vector3.zero;
    }

    private bool TryHandleIdleBehavior(Vector3 anchorDestination, out Vector3 moveDir)
    {
        moveDir = Vector3.zero;

        if (!enableIdleBehavior || ownerTower == null || targettedEnemy != null)
        {
            return false;
        }

        anchorDestination.z = 0f;

        float enterDistance = Mathf.Max(0.01f, idleEnterDistance);
        if (idleState == IdleState.None)
        {
            if ((anchorDestination - transform.position).sqrMagnitude > enterDistance * enterDistance)
            {
                return false;
            }

            idleAnchorPosition = anchorDestination;
            if (!TryBeginIdleMove())
            {
                BeginIdleWait();
            }
        }

        // Keep the idle area centered around the latest desired resting position.
        idleAnchorPosition = anchorDestination;

        if (idleState == IdleState.Moving)
        {
            float arriveDistance = Mathf.Max(0.01f, idleMoveArriveDistance);
            if ((idleWanderDestination - transform.position).sqrMagnitude <= arriveDistance * arriveDistance)
            {
                BeginIdleWait();
                return true;
            }

            moveDir = GetMoveDirectionTowards(idleWanderDestination);
            if (moveDir.sqrMagnitude <= 0.000001f)
            {
                BeginIdleWait();
            }

            return true;
        }

        if (idleState == IdleState.Waiting)
        {
            if (Time.time >= idleWaitUntilTime)
            {
                if (!TryBeginIdleMove())
                {
                    BeginIdleWait();
                }
                return true;
            }

            if (Time.time >= idleNextLookChangeTime)
            {
                idleLookDirection = GetRandomDirection2D();
                idleNextLookChangeTime = Time.time + RandomRangeSafe(idleLookChangeSecondsRange, 0.2f);
            }

            RotateTowards(idleLookDirection);
            return true;
        }

        return true;
    }

    private bool TryBeginIdleMove()
    {
        if (!TryPickIdleWanderDestination(out idleWanderDestination))
        {
            return false;
        }

        idleState = IdleState.Moving;
        return true;
    }

    private void BeginIdleWait()
    {
        idleState = IdleState.Waiting;
        idleLookDirection = GetRandomDirection2D();
        float lookDelay = RandomRangeSafe(idleLookChangeSecondsRange, 0.25f);
        idleNextLookChangeTime = Time.time + lookDelay;
        idleWaitUntilTime = Time.time + RandomRangeSafe(idleWaitSecondsRange, 0.5f);
    }

    private bool TryPickIdleWanderDestination(out Vector3 destination)
    {
        destination = idleAnchorPosition;

        float radius = Mathf.Max(0.05f, idleWanderRadius);
        float minDistance = Mathf.Clamp(idleWanderMinDistance, 0.01f, radius);
        int attempts = Mathf.Max(1, idleWanderCandidateAttempts);

        for (int i = 0; i < attempts; i++)
        {
            float dist = Random.Range(minDistance, radius);
            Vector3 candidate = idleAnchorPosition + GetRandomDirection2D() * dist;
            candidate.z = 0f;

            // Keep idle wandering local: reject points blocked by walls so agents
            // do not pathfind around long corridors to "nearby" points.
            if (!CanDirectlyReachIdleCandidate(candidate))
            {
                continue;
            }

            Vector3 dir = GetMoveDirectionTowards(candidate);
            if (dir.sqrMagnitude > 0.000001f)
            {
                destination = candidate;
                return true;
            }
        }

        return false;
    }

    private bool CanDirectlyReachIdleCandidate(Vector3 candidate)
    {
        int wallMask = LayerMaskManager.instance != null ? LayerMaskManager.instance.wallLayerMask : 0;
        if (wallMask == 0)
        {
            return true;
        }

        Vector2 start = transform.position;
        Vector2 end = candidate;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        Vector2 direction = delta / distance;
        const float startPadding = 0.03f;
        const float endPadding = 0.03f;

        float castDistance = Mathf.Max(0f, distance - startPadding - endPadding);
        if (castDistance <= 0.0001f)
        {
            return true;
        }

        Vector2 castStart = start + direction * startPadding;
        RaycastHit2D hit = Physics2D.Raycast(castStart, direction, castDistance, wallMask);
        return hit.collider == null;
    }

    private static Vector3 GetRandomDirection2D()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
    }

    private static float RandomRangeSafe(Vector2 range, float fallback)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        if (max <= 0f)
        {
            return Mathf.Max(0.01f, fallback);
        }

        min = Mathf.Max(0f, min);
        if (max <= min)
        {
            return min;
        }

        return Random.Range(min, max);
    }

    private Vector3 GetMoveDirectionTowards(Vector3 destination)
    {
        Vector3 direct = destination - transform.position;
        direct.z = 0f;

        if (Pathfinding.instance != null)
        {
            Vector3 pathDir = Pathfinding.instance.FindDirection(transform.position, destination);
            if (pathDir.sqrMagnitude > 0.000001f)
            {
                return pathDir;
            }
        }

        return direct.sqrMagnitude > 0.000001f ? direct.normalized : Vector3.zero;
    }

    private void TryExplodeOnDeath()
    {
        if (deathExplosionTriggered) return;
        if (health == null || health.GetCurrentHealth() > 0f) return;
        if (ownerTower == null || AgentHelper.instance == null || !AgentHelper.instance.ExplodeOnDeathActive(ownerTower)) return;
        if (ProjectileHelper.instance == null) return;

        deathExplosionTriggered = true;

        int projectileCount = UpgradeData.AgentExplodeProjectileCount;
        float projectileSpeed = UpgradeData.AgentExplodeProjectileSpeed;
        float projectileDistance = UpgradeData.AgentExplodeProjectileDistance;
        float lifetime = projectileDistance / projectileSpeed;

        Color color = ownerTower.GetColor();
        Vector3 origin = transform.position;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = i * (360f / projectileCount);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

            ProjectileHelper.instance.FireProjectile(
                start: origin,
                direction: dir,
                mode: Projectile.ProjectileType.Constant,
                speed: projectileSpeed,
                source: ownerTower,
                target: null,
                size: 1f,
                color: color,
                projectileAcceleration: 0f,
                lifetime: lifetime,
                pierceCount: 10,
                baseDamageRatio: ownerTower.towerDamageData.baseDamageRatio,
                triggerOnHitVfx: true);
        }
    }

    private bool TryDealEngageDamage(Vector3 enemyPos)
    {
        if (ownerTower == null || targettedEnemy == null) return false;

        if (combatMode == CombatMode.Laser && LaserHelper.instance != null)
        {
            Vector3 laserDirection = enemyPos - transform.position;
            laserDirection.z = 0f;
            if (laserDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            LaserHelper.instance.LaserAttackHelper(
                tower: ownerTower,
                start: transform.position,
                direction: laserDirection,
                width: .01f,
                color: ownerTower.GetColor(),
                applyEffects: laserAppliesEffects);
            return true;
        }

        if (AgentHelper.instance == null) return false;
        bool didDamage = AgentHelper.instance.DealAgentEngageDamage(ownerTower, targettedEnemy, damageMultiplier);
        if (didDamage)
        {
            TryStealGold(targettedEnemy);
        }
        return didDamage;
    }

    private void TryStealGold(Enemy enemy)
    {
        if (enemy == null || ownerTower == null) return;
        if (!ownerTower.UpgradeActive(UpgradeData.UID.AgentStealGold)) return;
        if (_stolenGoldFrom.Contains(enemy)) return;
        if (Random.value > UpgradeData.AgentStealGoldChancePercent / 100f) return;

        _stolenGoldFrom.Add(enemy);
        int goldAmount = UpgradeData.AgentStealGoldAmount;
        if (CurrencyManager.instance != null)
        {
            CurrencyManager.instance.AddCurrency(goldAmount, transform.position);
        }
    }

    private Vector3 GetDesiredPosition()
    {
        if (ownerTower == null || AgentHelper.instance == null)
        {
            Vector3 fallback = transform.position;
            fallback.z = 0f;
            return fallback;
        }

        return AgentHelper.instance.GetDesiredAgentPosition(ownerTower, this);
    }

    private void ClaimEnemyIfPossible(Enemy enemy)
    {
        claimedEnemy = null;
        if (enemy == null || ownerTower == null || AgentHelper.instance == null) return;

        AgentHelper.instance.ClaimEnemy(ownerTower, enemy);
        claimedEnemy = enemy;
    }

    private void ReleaseClaimedEnemy()
    {
        if (claimedEnemy == null || ownerTower == null || AgentHelper.instance == null)
        {
            claimedEnemy = null;
            return;
        }

        AgentHelper.instance.ReleaseEnemy(ownerTower, claimedEnemy);
        claimedEnemy = null;
    }

    public void DespawnWithSwell()
    {
        if (isDespawning) return;
        isDespawning = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        ResetIdleBehavior();
        UpdateMovementLock(null, engaged: false);
        ReleaseClaimedEnemy();
        targettedEnemy = null;
        isEngagingTarget = false;

        if (swellAnimation != null)
        {
            swellAnimation.SwellOut(destroy: true);
            return;
        }

        Destroy(gameObject);
    }
}
