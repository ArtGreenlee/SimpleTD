using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Grid Cell Tracking")]
    [SerializeField, Min(0.01f)] private float updateGridCellEverySeconds = 0.2f;

    private GridManager _grid;
    private bool _hasTrackedGridCell;
    private float _nextGridCellUpdateTime;
    private Vector2Int gridCell;
    public void UpdateGridCell()
    {
        if (_grid == null) _grid = GridManager.instance;
        if (_grid == null) _grid = FindFirstObjectByType<GridManager>();

        var manager = EnemyManager.instance;
        if (_grid == null || manager == null)
        {
            if (_hasTrackedGridCell && manager != null)
            {
                manager.RemoveEnemyFromGrid(this, hasKnownCell: true, knownCell: gridCell);
            }

            _hasTrackedGridCell = false;
            return;
        }

        if (_grid.TryWorldToCell(transform.position, out var currentCell))
        {
            manager.UpdateEnemyGridCell(this, _hasTrackedGridCell, gridCell, currentCell);
            gridCell = currentCell;
            _hasTrackedGridCell = true;
            return;
        }

        if (_hasTrackedGridCell)
        {
            manager.RemoveEnemyFromGrid(this, hasKnownCell: true, knownCell: gridCell);
            _hasTrackedGridCell = false;
        }
    }
    private readonly Dictionary<EnemyEffect.TriggerCondition, List<EnemyEffect>> enemyEffects = new Dictionary<EnemyEffect.TriggerCondition, List<EnemyEffect>>();
    public int value = 1;
    public int lifeCost = 1;
    [SerializeField] private int xpValue = 1;
    [Header("Agent Combat")]
    [SerializeField] private float damageToAgent = 1f;
    [SerializeField] private float damageToAgentCooldown = 1f;
    [SerializeField] private bool damageAllEngagedAgents = false;
    [SerializeField] private bool preventAgentEngagement = false;
    private int _runtimeAgentEngagementBlockCount;
    private float nextDamageToEngagedAgentTime;
    private readonly Dictionary<Tower, float> _damageByTower = new Dictionary<Tower, float>();
    private readonly HashSet<Agent> _engagingAgents = new HashSet<Agent>();
    public enum ID
    {
        Grunt,
        Tank,
        Cluster,
        Ghost,
        Stun,
        Heal,
        EliteGrunt,
        Teleport,
        Speck,
        Scythe
    }

    [Header("Classification")]
    public ID id = ID.Grunt;

    [HideInInspector] public Rigidbody2D rb;
    [SerializeField] private CircleCollider2D damageCollider;
    private HealthBar healthBar;

    private Movement _movement;

    private float _lastSentSlow = -1f;
    private readonly List<RangeManager> _targetingRanges = new List<RangeManager>(8);
    [HideInInspector] public Health health;

    private bool _reachedGoal;
    private bool _despawning;
    private SwellAnimation _swell;
    [Header("Visual")]
    public SpriteRenderer enemyBodySR;
    public SpriteRenderer stunnedVisualizer;
    public float stunCooldown;
    private float stunCooldownTimer;
    public Transform slowVisualizer;
    private SRC slowSRC;
    private Vector3 slowVisualizerStartingScale;
    private SRC src;
    public enum StatusEffect
    {
        Slowed,
        Exposed,
        Burning,
        Poisoned,
        Stunned,
        Shocked
    }

    private HashSet<StatusEffect> _currentStatusEffects = new HashSet<StatusEffect>();
    public GameObject exposedIndicator;

    public SpriteRenderer markVisualizer;
    private CM.ColorType mark;

	private float _exposedUntil = -1f;
	private float _stunnedUntil = -1f;

	[System.Flags]
	public enum ImmunityFlags
	{
		None        = 0,
		Burning     = 1 << 0,
		Vulnerable  = 1 << 1,
		Marks       = 1 << 2,
		Slow        = 1 << 3,
		CriticalHit = 1 << 4,
		Stunned     = 1 << 5,
		Shocked     = 1 << 6,
		Poisoned    = 1 << 7,
	}

	[Header("Immunity")]
	[SerializeField] private bool immuneToAll = false;
	[Tooltip("Ignored when Immune To All is enabled.")]
	[SerializeField] private ImmunityFlags immunities = ImmunityFlags.None;
    bool killed = false;

    
    public bool HasStatusEffect(StatusEffect effect)
    {
        if (effect == StatusEffect.Exposed
            && health != null
            && health.IsBurning
            && TagManager.instance != null
            && TagManager.instance.OrangeTagBurningEnemiesAreAlwaysExposed())
        {
            return true;
        }

        return _currentStatusEffects.Contains(effect);
    }

    public int GetStatusEffectCount()
    {
        return _currentStatusEffects.Count;
    }

    public bool IsImmuneTo(ImmunityFlags flag)
    {
        return immuneToAll || (immunities & flag) != 0;
    }

    public virtual void OnHealthChange()
    {
        if (health == null) return;
        TriggerEnemyEffects(EnemyEffect.TriggerCondition.HealthBelowThreshold, health.GetHealthPercentage());
    }

    public float GetDamageToAgent()
    {
        return Mathf.Max(0f, damageToAgent);
    }

    public float GetDamageToAgentCooldown()
    {
        return Mathf.Max(0.01f, damageToAgentCooldown);
    }

    private bool IsAgentEngagementPrevented()
    {
        return preventAgentEngagement || _runtimeAgentEngagementBlockCount > 0;
    }

    public void SetRuntimePreventAgentEngagement(bool prevented)
    {
        if (prevented)
        {
            _runtimeAgentEngagementBlockCount++;
        }
        else
        {
            _runtimeAgentEngagementBlockCount = Mathf.Max(0, _runtimeAgentEngagementBlockCount - 1);
        }

        if (IsAgentEngagementPrevented() && _engagingAgents.Count > 0)
        {
            ForceDisengageAllAgents();
        }
    }

    public bool CanBeEngagedByAgents()
    {
        return !IsAgentEngagementPrevented();
    }

    public void SetAgentEngagement(Agent agent, bool engaged)
    {
        if (agent == null) return;

        if (engaged)
        {
            if (IsAgentEngagementPrevented())
            {
                _engagingAgents.Remove(agent);
                agent.EngageEnemy(null);
                return;
            }

            _engagingAgents.Add(agent);
        }
        else
        {
            _engagingAgents.Remove(agent);
        }
    }

    public bool IsEngagedByAgents()
    {
        _engagingAgents.RemoveWhere(a => a == null || !a.isActiveAndEnabled);

        if (IsAgentEngagementPrevented() && _engagingAgents.Count > 0)
        {
            ForceDisengageAllAgents();
        }

        return _engagingAgents.Count > 0;
    }

    public bool TryDamageAgents()
    {
        if (IsAgentEngagementPrevented()) return false;

        _engagingAgents.RemoveWhere(a => a == null || !a.isActiveAndEnabled);
        if (_engagingAgents.Count == 0) return false;

        if (Time.time < nextDamageToEngagedAgentTime) return false;

        float damage = GetDamageToAgent();
        if (damage <= 0f) return false;

        bool applied = false;

        if (damageAllEngagedAgents)
        {
            var agentsSnapshot = new List<Agent>(_engagingAgents);
            for (int i = 0; i < agentsSnapshot.Count; i++)
            {
                var agent = agentsSnapshot[i];
                if (agent == null || !agent.isActiveAndEnabled) continue;
                FaceAgent(agent);
                applied |= agent.ReceiveEnemyDamage(this, damage);
            }
        }
        else
        {
            Agent target = SelectEngagedAgentForDamage();
            if (target != null)
            {
                FaceAgent(target);
                applied = target.ReceiveEnemyDamage(this, damage);
            }
        }

        nextDamageToEngagedAgentTime = Time.time + GetDamageToAgentCooldown();
        return applied;
    }

    private void ForceDisengageAllAgents()
    {
        if (_engagingAgents.Count == 0) return;

        var snapshot = new List<Agent>(_engagingAgents);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var agent = snapshot[i];
            if (agent == null) continue;
            agent.EngageEnemy(null);
        }

        _engagingAgents.Clear();
    }

    private void FaceAgent(Agent agent)
    {
        if (agent == null) return;

        Vector3 toAgent = agent.transform.position - transform.position;
        toAgent.z = 0f;
        if (toAgent.sqrMagnitude <= 0.000001f) return;

        float targetAngle = Mathf.Atan2(toAgent.y, toAgent.x) * Mathf.Rad2Deg - 90f;

        if (_movement == null) _movement = GetComponent<Movement>();
        Transform rotateT = (_movement != null && _movement.rotationTransform != null)
            ? _movement.rotationTransform
            : transform;

        rotateT.rotation = Quaternion.Euler(0f, 0f, targetAngle);
    }

    private Agent SelectEngagedAgentForDamage()
    {
        Agent best = null;
        float bestDist = float.PositiveInfinity;

        Vector3 myPos = transform.position;
        foreach (var agent in _engagingAgents)
        {
            if (agent == null || !agent.isActiveAndEnabled) continue;

            float d = (agent.transform.position - myPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = agent;
            }
        }

        return best;
    }

    public void SetRuntimeValue(int runtimeValue)
    {
        value = Mathf.Max(0, runtimeValue);
    }

    public virtual void OnDamageTaken(float damageAmount)
    {
        if (damageAmount <= 0f) return;
        TriggerEnemyEffects(EnemyEffect.TriggerCondition.DamageAboveThreshold, damageAmount);
    }

    public void RecordDamageSource(Tower source, float amount)
    {
        if (source == null || amount <= 0f) return;
        _damageByTower.TryGetValue(source, out float existing);
        _damageByTower[source] = existing + amount;
    }

    public void Expose(float duration)
    {
        if (IsImmuneTo(ImmunityFlags.Vulnerable)) return;
        _currentStatusEffects.Add(StatusEffect.Exposed);
        _exposedUntil = Mathf.Max(_exposedUntil, Time.fixedTime + duration);
    }

    public void SetMark(CM.ColorType markColor)
    {
        if (IsImmuneTo(ImmunityFlags.Marks)) return;
        mark = markColor;
        SetMarkVisualizer();
    }

    public CM.ColorType GetMark() 
    {
        return mark;
    }

    private void SetMarkVisualizer()
    {
        markVisualizer.color = CM.i.ColorTypeToColor(mark);
    }

    public Movement GetMovement()
    {
        return _movement;
    }

    public void ConsumeMark()
    {
        if (mark == CM.ColorType.None)
        {
            Debug.LogError("Consuming None Mark");
        }
        EnemyManager.instance.PlayMarkVfx(transform.position, mark);
        mark = CM.ColorType.None;
        SetMarkVisualizer();
    }

    public CircleCollider2D GetDamageCollider()
    {
        return damageCollider;
    }

    public Enemy GetNearestEnemy(float maxDistance)
    {
        if (QuadTree2D.instance == null)
            return null;

        Vector2 myPosition = rb != null ? rb.position : (Vector2)transform.position;
        List<Enemy> nearby = new List<Enemy>();
        float searchRadius = 10f; // Adjust as needed

        QuadTree2D.instance.QueryCircle(myPosition, searchRadius, nearby, this);

        Enemy nearest = null;
        float minDistSqr = maxDistance * maxDistance;
        foreach (var e in nearby)
        {
            if (e == null || e == this) continue;
            if (e.health == null) continue;
            if (e.health.GetCurrentHealth() <= 0f) continue;
            Vector2 ePos = e.rb != null ? e.rb.position : (Vector2)e.transform.position;
            float distSqr = (myPosition - ePos).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = e;
            }
        }
        return nearest;
    }

    public void OnClicked()
    {
        //health.ApplyShock(1, null);
    }

    public ParticleSystem onBurnVfx;

    private void Awake()
    {
        src = GetComponent<SRC>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        _grid = GridManager.instance;
        if (slowVisualizer != null)
        {
            slowSRC = slowVisualizer.GetComponent<SRC>();
        }

        _movement = GetComponent<Movement>();

        // Don't rely on Health.Awake ordering; bind later in Start/OnEnable.
        healthBar = null;

        RefreshEnemyEffects();

        _swell = GetComponent<SwellAnimation>();
        if (_swell == null) _swell = gameObject.AddComponent<SwellAnimation>();
    }

    private void Start()
    {
        BindHealthBar();
        PushSlowToHealthBar(force: true);
        slowVisualizerStartingScale = slowVisualizer.transform.localScale;

           // Apply Blue tag max slow bonus
           if (TagManager.instance != null && _movement != null)
           {
               float maxSlowBonus = TagManager.instance.GetBlueTagMaxSlowBonus();
               if (maxSlowBonus > 0f)
               {
                   _movement.SetMaxSlow(_movement.GetMaxSlow() + maxSlowBonus);
               }
           }
    }

    public float GetSlowPercentage()
    {
        if (_movement == null) _movement = GetComponent<Movement>();
        return _movement != null ? _movement.Slow01 : 0f;
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Goal"))
        {
            _reachedGoal = true;

            if (WaveManager.instance != null
                && WaveManager.instance.CurrencySpawnOnKill
                && CurrencyManager.instance != null)
            {
                int reward = Mathf.Max(0, value);
                if (reward > 0)
                {
                    CurrencyManager.instance.SpawnCurrencyForAmount(reward, transform.position);
                }
            }

            int cost = Mathf.Max(0, lifeCost);
            if (cost > 0)
            {
                if (GameController.instance != null) GameController.instance.LoseLifes(cost);
                else
                {
                    GameController.instance.LoseLifes(cost);
                }
            }
            ScreenShake.Instance.Play();
            TextObjectPool.instance.PlayFloatingText(transform.position, "-" + lifeCost.ToString(), Color.red, 1, 1);

            if (RM.i != null && RM.i.Active(RM.ID.vengeance))
            {
                var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
                for (int i = 0; i < enemies.Length; i++)
                {
                    Enemy target = enemies[i];
                    if (target == null || target == this) continue;
                    if (target.health == null) continue;
                    float damage = target.health.GetMaxHealth() * RM.vengeancePercentMaxHealthDamage;
                    target.health.TakeDamage(damage, null);
                }
            }

            DespawnWithSwell();
        }
    }

    public void OnKill(Tower source = null)
    {
        if (killed)
        {
            return;
        }
        killed = true;

        if (RM.i != null && RM.i.Active(RM.ID.zeal) && source != null)
        {
            var movement = GetMovement();
            Transform goal = movement != null ? movement.Goal : null;
            if (goal != null)
            {
                float distanceToGoal = Vector2.Distance(transform.position, goal.position);
                if (distanceToGoal <= RM.zealGoalDistanceThreshold)
                {
                    source.AddZealStack(1);
                    RM.i.IndicateRelic(RM.ID.zeal);

                    if (LaserObjectPool.instance != null)
                    {
                        Color c = CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.White) : Color.white;
                        LaserObjectPool.instance.ShowLaser(transform.position, source.transform.position, c, .05f, .12f, .25f);
                    }
                }
            }
        }

        if (RM.i != null && RM.i.Active(RM.ID.fireExplosion) && health != null && health.IsBurning)
        {
            float burnDamage = health.BurnStacks * Health.fireTickDamageGlobal;
            float radius = 1f;
            if (TagManager.instance != null && TagManager.instance.GetTagCount(Tower.Tag.Red) >= 6)
            {
                radius += 1.5f;
            }

            if (AOEObjectPool.instance != null)
            {
                AOEObjectPool.instance.PlayPulse(transform.position, radius * 2f, CM.i.ColorTypeToColor(CM.ColorType.Orange));
            }

            if (AOEHelper.instance != null)
            {
                AOEHelper.instance.DamageEnemiesInRadiusNoSource(
                    transform.position,
                    radius,
                    burnDamage,
                    ignoredEnemy: this,
                    damageTypeOverride: CM.ColorType.Orange);
            }
        }

        if (RM.i != null && RM.i.Active(RM.ID.inspirationRelic) && TowerManager.instance != null)
        {
            float inspirationRadius = Mathf.Max(0f, RM.inspirationRelicRadius);
            bool chargedAnyTower = false;

            foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
            {
                if (tower == null) continue;
                if (Vector2.Distance(transform.position, tower.transform.position) > inspirationRadius) continue;

                tower.Charge(RM.inspirationRelicChargeAmount);
                chargedAnyTower = true;

                if (LaserObjectPool.instance != null)
                {
                    Color c = CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.White) : Color.white;
                    LaserObjectPool.instance.ShowLaser(transform.position, tower.transform.position, c, .05f, .15f, 1f);
                }

            }

            if (chargedAnyTower)
            {
                RM.i.IndicateRelic(RM.ID.inspirationRelic);
            }
        }

        NotifyPlacedTowersEnemyKilledInRange();

        // Award currency only when dying (i.e., being destroyed for reasons other than goal).
        int reward = Mathf.Max(0, value);
        if (reward > 0 && CurrencyManager.instance != null)
        {
            CurrencyManager.instance.SpawnCurrencyForAmount(reward, transform.position);
        }

        DistributeXP();
    }

    private void NotifyPlacedTowersEnemyKilledInRange()
    {
        if (TowerManager.instance == null) return;

        foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower == null) continue;

            RangeManager rm = tower.GetRangeManager();
            if (rm == null) continue;
            if (!rm.PointInsideRange(transform.position)) continue;

            tower.OnEnemyKilledInRange(this);
        }
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
            if (!controller.HasEnemyInRange(this)) continue;

            result.Add(lensTower);
        }

        return result;
    }

    private void DistributeXP()
    {
        if (xpValue <= 0 || _damageByTower.Count == 0) return;

        // Prune destroyed, non-placed, or max-level towers.
        var keys = new List<Tower>(_damageByTower.Keys);
        for (int i = keys.Count - 1; i >= 0; i--)
        {
            Tower t = keys[i];
            if (t == null || t.CurrentState != Tower.State.Placed || t.IsMaxLevel())
            {
                _damageByTower.Remove(t);
            }
        }

        if (_damageByTower.Count == 0) return;

        float totalDamage = 0f;
        foreach (var kvp in _damageByTower) totalDamage += kvp.Value;
        if (totalDamage <= 0f) return;

        // Largest-remainder method so allocated XP sums exactly to xpValue.
        var entries = new List<KeyValuePair<Tower, float>>(_damageByTower);
        int[] allocations = new int[entries.Count];
        float[] remainders = new float[entries.Count];
        int allocated = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            float exact = (entries[i].Value / totalDamage) * xpValue;
            allocations[i] = Mathf.FloorToInt(exact);
            remainders[i] = exact - allocations[i];
            allocated += allocations[i];
        }

        int remaining = xpValue - allocated;
        for (int r = 0; r < remaining; r++)
        {
            int bestIdx = -1;
            float bestRemainder = -1f;  
            for (int i = 0; i < remainders.Length; i++)
            {
                if (remainders[i] > bestRemainder)
                {
                    bestRemainder = remainders[i];
                    bestIdx = i;
                }
            }
            if (bestIdx >= 0)
            {
                allocations[bestIdx]++;
                remainders[bestIdx] = -1f;
            }
        }

        var pool = XPObjectPool.instance;
        for (int i = 0; i < entries.Count; i++)
        {
            if (allocations[i] <= 0) continue;
            Tower tower = entries[i].Key;
            if (pool != null)
            {
                pool.SpawnXP(transform.position, tower, allocations[i]);
            }
            else
            {
                tower.AddXP(allocations[i]);
            }
        }
    }

    public void DespawnWithSwell()
    {
        if (_despawning) return;
        _despawning = true;

        if (damageCollider != null) damageCollider.enabled = false;
        if (_movement != null) _movement.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (_swell != null)
            _swell.SwellOut(destroy: true);
        else
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        // In case pooled/re-enabled.
        BindHealthBar();
        PushSlowToHealthBar(force: true);
        RefreshEnemyEffects();

        _nextGridCellUpdateTime = 0f;
        UpdateGridCell();
    }

    private void OnDisable()
    {
        if (EnemyManager.instance != null)
        {
            EnemyManager.instance.RemoveEnemyFromGrid(this, hasKnownCell: _hasTrackedGridCell, knownCell: gridCell);
        }

        _hasTrackedGridCell = false;
    }

    public void RefreshEnemyEffects()
    {
        enemyEffects.Clear();

        var attachedEffects = GetComponentsInChildren<EnemyEffect>(includeInactive: true);
        for (int i = 0; i < attachedEffects.Length; i++)
        {
            var effect = attachedEffects[i];
            if (effect == null) continue;

            effect.BindEnemy();

            if (!enemyEffects.TryGetValue(effect.Condition, out var effectsForCondition))
            {
                effectsForCondition = new List<EnemyEffect>();
                enemyEffects.Add(effect.Condition, effectsForCondition);
            }

            effectsForCondition.Add(effect);
        }
    }

    private void TriggerEnemyEffects(EnemyEffect.TriggerCondition condition, float eventValue)
    {
        if (!enemyEffects.TryGetValue(condition, out var effectsForCondition))
        {
            return;
        }

        for (int i = 0; i < effectsForCondition.Count; i++)
        {
            var effect = effectsForCondition[i];
            if (effect == null) continue;
            effect.TryTrigger(eventValue);
        }
    }

    public void StunEnemy(float duration)
    {
        if (IsImmuneTo(ImmunityFlags.Stunned)) return;
        if (duration <= 0f) return;
        if (stunCooldownTimer > 0f) return;

        _currentStatusEffects.Add(StatusEffect.Stunned);
        _stunnedUntil = Mathf.Max(_stunnedUntil, Time.fixedTime + duration);
    }

    public bool IsStunned()
    {
        return HasStatusEffect(StatusEffect.Stunned);
    }

    private void SyncBurningFromHealth()
    {
        bool burning = health != null && health.IsBurning;
        if (burning) _currentStatusEffects.Add(StatusEffect.Burning);
        else _currentStatusEffects.Remove(StatusEffect.Burning);
    }

    public float GetBurnPercentage()
    {
        return health.BurnStacks / Health.maxBurnStacksGlobal;
    }

    private void SyncShockedFromHealth()
    {
        bool shocked = health != null && health.IsShocked;
        if (shocked) _currentStatusEffects.Add(StatusEffect.Shocked);
        else _currentStatusEffects.Remove(StatusEffect.Shocked);
    }

    private void SyncPoisonedFromHealth()
    {
        bool poisoned = health != null && health.IsPoisoned;
        if (poisoned) _currentStatusEffects.Add(StatusEffect.Poisoned);
        else _currentStatusEffects.Remove(StatusEffect.Poisoned);
    }

    public float GetShockPercentage()
    {
        return health.ShockStacks / Health.maxShockStacksGlobal;
    }

    private void BindHealthBar()
    {
        if (healthBar != null) return;
        if (health != null && health.healthBar != null)
        {
            healthBar = health.healthBar;
            return;
        }

        // Fallback: find on children if not wired.
        healthBar = GetComponentInChildren<HealthBar>();
    }

    private void PushSlowToHealthBar(bool force)
    {
        if (healthBar == null) return;
        float slow01 = 0f;
        if (_movement != null)
        {
            // Blue bar should reflect absolute slow amount (0..1), not "percent of max slow".
            slow01 = Mathf.Clamp01(_movement.Slow01 * Mathf.Clamp01(_movement.GetMaxSlow()));
        }
        if (!force && Mathf.Approximately(slow01, _lastSentSlow)) return;
        _lastSentSlow = slow01;
        healthBar.RegisterSlowChange(slow01);
    }

    public void RegisterTargetingRangeManager(RangeManager rm)
    {
        if (rm == null) return;
        if (_targetingRanges.Contains(rm)) return;
        _targetingRanges.Add(rm);
    }

    public void UnregisterTargetingRangeManager(RangeManager rm)
    {
        if (rm == null) return;
        _targetingRanges.Remove(rm);
    }

    private void NotifyTargeters()
    {
        for (int i = _targetingRanges.Count - 1; i >= 0; i--)
        {
            var rm = _targetingRanges[i];
            if (rm == null)
            {
                _targetingRanges.RemoveAt(i);
                continue;
            }

            rm.ForceRetarget();
        }

        _targetingRanges.Clear();
    }

    private void OnDestroy()
    {

        NotifyTargeters();

        if (EnemyManager.instance != null)
        {
            EnemyManager.instance.RemoveEnemyFromGrid(this, hasKnownCell: _hasTrackedGridCell, knownCell: gridCell);
        }

        _hasTrackedGridCell = false;

        // If we were destroyed because we reached the goal, don't award currency.
        if (_reachedGoal) return;


    }

    public void RemoveSlow()
    {
        if (_movement == null) _movement = GetComponent<Movement>();
        if (_movement == null) return;
        _movement.RemoveSlow();
        SyncSlowedFromMovement();
        PushSlowToHealthBar(force: false);
    }

    public void ReduceSlow(float slowAmount)
    {
        if (_movement == null) _movement = GetComponent<Movement>();
        if (_movement == null) return;
        _movement.ReduceSlow(slowAmount);
        SyncSlowedFromMovement();
        PushSlowToHealthBar(force: false);
    }

    public void ApplySlow(float slowAmount)
    {
        if (IsImmuneTo(ImmunityFlags.Slow)) return;
        if (_movement == null) _movement = GetComponent<Movement>();
        if (_movement == null) return;

        float maxSlow = Mathf.Clamp01(_movement.GetMaxSlow());
        float beforeSlow = Mathf.Clamp01(_movement.Slow01) * maxSlow;
        float incomingSlow = Mathf.Max(0f, slowAmount);

        _movement.ApplySlow(slowAmount);

        float afterSlow = Mathf.Clamp01(_movement.Slow01) * maxSlow;
        float acceptedSlow = Mathf.Max(0f, afterSlow - beforeSlow);
        float overflowSlow = Mathf.Max(0f, incomingSlow - acceptedSlow);

        if (overflowSlow > 0f && RM.i != null && RM.i.Active(RM.ID.slowOverload) && health != null)
        {
            RM.i.IndicateRelic(RM.ID.slowOverload);
            health.TakeDamage(overflowSlow * 10f, null, CM.ColorType.Blue, null);
        }

        SyncSlowedFromMovement();
        PushSlowToHealthBar(force: false);
    }

    private void SyncSlowedFromMovement()
    {
        if (_movement == null) _movement = GetComponent<Movement>();
        bool slowed = _movement != null && _movement.IsSlowed;
        if (slowed) _currentStatusEffects.Add(StatusEffect.Slowed);
        else _currentStatusEffects.Remove(StatusEffect.Slowed);
    }

    private void FixedUpdate()
    {
        SyncSlowedFromMovement();
        SyncBurningFromHealth();
        SyncShockedFromHealth();
        SyncPoisonedFromHealth();
        slowVisualizer.transform.localScale = Vector3.Lerp(Vector3.zero, slowVisualizerStartingScale, GetSlowPercentage());
        if (slowSRC != null)
        {
            slowSRC.SetSpriteRendererAlpha(Mathf.Lerp(.3f, .6f, GetSlowPercentage()));
            if (_movement.AtMaxSlow())
            {
                slowSRC.ApplyColorToAll(CM.ColorType.Cyan);
            }
            else
            {
                slowSRC.ApplyColorToAll(CM.ColorType.Blue);
            }
        }

        // Tick stun cooldown (post-stun immunity).
        if (stunCooldownTimer > 0f)
        {
            stunCooldownTimer = Mathf.Max(0f, stunCooldownTimer - Time.fixedDeltaTime);
        }

        // Handle Stunned expiration
        if (_currentStatusEffects.Contains(StatusEffect.Stunned) && Time.fixedTime >= _stunnedUntil)
        {
            _currentStatusEffects.Remove(StatusEffect.Stunned);
            _stunnedUntil = -1f;
            stunCooldownTimer = Mathf.Max(0f, stunCooldown);
        }

        if (stunnedVisualizer != null)
        {
            stunnedVisualizer.enabled = IsStunned();
        }

        // Handle Exposed expiration
        if (_currentStatusEffects.Contains(StatusEffect.Exposed) && Time.fixedTime >= _exposedUntil)
        {
            _currentStatusEffects.Remove(StatusEffect.Exposed);
            _exposedUntil = -1f;
        }

        if (exposedIndicator != null)
        {
            if (HasStatusEffect(StatusEffect.Exposed))
            {
                exposedIndicator.transform.Rotate(Vector3.forward, 180f * Time.fixedDeltaTime);
                exposedIndicator.SetActive(true);
            }
            else
            {
                exposedIndicator.SetActive(false);
            }
        }

        if (src != null)
        {
            src.UpdateHitEffect();
        }
    }

    public void Flash()
    {
        if (src != null)
        {
            src.TriggerHitEffect();
        }
    }

    public virtual void Update()
    {
        if (IsAgentEngagementPrevented() && _engagingAgents.Count > 0)
        {
            ForceDisengageAllAgents();
        }

        // Keep UI in sync with decay. (Only sends when value changes.)
        SyncSlowedFromMovement();
        SyncBurningFromHealth();
        SyncShockedFromHealth();
        SyncPoisonedFromHealth();
        PushSlowToHealthBar(force: false);

        if (Time.time >= _nextGridCellUpdateTime)
        {
            UpdateGridCell();
            _nextGridCellUpdateTime = Time.time + Mathf.Max(0.01f, updateGridCellEverySeconds);
        }
    }
}
