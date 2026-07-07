using UnityEngine;
using System.Collections.Generic;

public class Health : MonoBehaviour
{
    public static float fireTickDamageGlobal = 1;
    public static float fireTickCooldownGlobal = .25f;
    public static int maxBurnStacksGlobal = 10;
    public ParticleSystem burningVFX;

    public static int shockLightningChainMinTargets = 1;
    public static float shockTickDamage = 1;
    public static float shockLightningChainDamage = 1;
    public static float shockLightningChainChance = .25f;
    public static int shockLightningChainMaxTargets = 3;
    public static float shockTickCooldownGlobal = .5f;
    public static int maxShockStacksGlobal = 10;
    public ParticleSystem shockedVFX;

    public static float poisonTickDamageGlobal = 1;
    public static float poisonTickCooldownGlobal = 1;
    public static int maxPoisonStacksGlobal = 10;
    public ParticleSystem poisonedVFX;

    public static float overkillRelicSearchRadius = 10f;

    [HideInInspector] public Enemy enemy;
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private bool destroyOnDeath = true;

    [Header("UI")]
    [Tooltip("Optional health bar to notify when health changes.")]
    [HideInInspector] public HealthBar healthBar;

    [Header("Juice")]
    [Tooltip("Optional. If present, will apply spring-based position/scale juice on damage.")]
    [SerializeField] private bool enableJuiceOnDamage = true;
    [Min(0f)]
    [SerializeField] private float juiceForceBase = 1f;
    [Min(0f)]
    [SerializeField] private float juiceBounceBase = 1f;
    [Tooltip("Clamp for damage/avg scaling used by Juice impulses.")]
    [Min(0.01f)]
    [SerializeField] private float juiceAvgScaleClampMax = 4f;

    [Header("Damage Text")]
    [Tooltip("If false, this Health instance will not spawn floating damage/heal text.")]
    [SerializeField] private bool showFloatingDamageText = true;
    [Tooltip("Rolling window size used to compute average recent damage.")]
    [Min(1)]
    [SerializeField] private int damageTextScalingWindowCount = 20;
    public float currentHealth { get; private set; }

    private Juice _juice;
    private CRTJuice _crtJuice;

    // Rolling window for "average damage lately" on this Health instance.
    private readonly Queue<float> _damageWindow = new Queue<float>(64);
    private float _damageSum;

    private readonly Queue<Tower> _burnSources = new Queue<Tower>(32);
    private float _burnTickTimer;

    public int BurnStacks => _burnSources.Count;
    public bool IsBurning => _burnSources.Count > 0;

    public float GetBurnTickTimeRemaining()
    {
        if (_burnSources.Count <= 0) return -1f;
        return Mathf.Max(0f, _burnTickTimer);
    }

    private readonly Queue<Tower> _shockSources = new Queue<Tower>(32);
    private float _shockTickTimer;

    public int ShockStacks => _shockSources.Count;
    public bool IsShocked => _shockSources.Count > 0;

    public float GetShockTickTimeRemaining()
    {
        if (_shockSources.Count <= 0) return -1f;
        return Mathf.Max(0f, _shockTickTimer);
    }

    private readonly Queue<Tower> _poisonSources = new Queue<Tower>(32);
    private float _poisonTickTimer;

    [Header("Plague Relic")]
    private Color plagueLaserColor => CM.i.ColorTypeToColor(CM.ColorType.Green);

    public int PoisonStacks => _poisonSources.Count;
    public bool IsPoisoned => _poisonSources.Count > 0;

    public static float GetEffectiveBurnTickDamage()
    {
        float damage = Mathf.Max(0f, fireTickDamageGlobal);
        if (TagManager.instance != null)
        {
            damage *= Mathf.Max(0f, TagManager.instance.GetOrangeTagBurnDamageMultiplier());
        }

        return damage;
    }

    public float GetPoisonTickTimeRemaining()
    {
        if (_poisonSources.Count <= 0) return -1f;
        return Mathf.Max(0f, _poisonTickTimer);
    }

    private float GetPoisonTickCooldown()
    {
        float baseCooldown = Mathf.Max(0f, poisonTickCooldownGlobal);
        if (baseCooldown <= 0f) return 0f;
        if (enemy == null) return baseCooldown;

        List<LensTower> lenses = enemy.GetLens();
        if (lenses == null || lenses.Count == 0) return baseCooldown;

        for (int i = 0; i < lenses.Count; i++)
        {
            LensTower lens = lenses[i];
            if (lens == null) continue;
            if (!lens.UpgradeActive(UpgradeData.UID.PoisonFasterTickRateInLens)) continue;
            return baseCooldown / 5f;
        }

        return baseCooldown;
    }

    public void ClearBurning()
    {
        _burnSources.Clear();
        _burnTickTimer = 0f;

        if (burningVFX != null)
        {
            burningVFX.Stop();
        }

        if (healthBar != null)
        {
            healthBar.RegisterBurnChange(0f);
        }
    }

    public void ReduceBurning(int stackAmount)
    {
        if (stackAmount <= 0) return;
        if (_burnSources.Count == 0) return;

        int toRemove = Mathf.Min(stackAmount, _burnSources.Count);
        for (int i = 0; i < toRemove; i++)
        {
            _burnSources.Dequeue();
        }

        if (_burnSources.Count == 0)
        {
            _burnTickTimer = 0f;
            if (burningVFX != null) burningVFX.Stop();
        }

        if (healthBar != null)
        {
            healthBar.RegisterBurnChange((float)_burnSources.Count / maxBurnStacksGlobal);
        }
    }

    public void ApplyBurn(int stacks, Tower source)
    {
        if (stacks <= 0) return;
        if (fireTickCooldownGlobal <= 0f || fireTickDamageGlobal <= 0f) return;
        if (enemy != null && enemy.IsImmuneTo(Enemy.ImmunityFlags.Burning)) return;

        bool wasBurning = _burnSources.Count > 0;

        int available = Mathf.Max(0, maxBurnStacksGlobal - _burnSources.Count);
        int toAdd = Mathf.Min(stacks, available);
        for (int i = 0; i < toAdd; i++)
        {
            _burnSources.Enqueue(source);
        }

        // Start ticking when burn is first applied.
        if (!wasBurning && _burnSources.Count > 0)
        {
            _burnTickTimer = fireTickCooldownGlobal;
        }
    }

    public void ClearShocked()
    {
        _shockSources.Clear();
        _shockTickTimer = 0f;

        if (shockedVFX != null)
        {
            shockedVFX.Stop();
        }
    }

    public void ReduceShocked(int stackAmount)
    {
        if (stackAmount <= 0) return;
        if (_shockSources.Count == 0) return;

        int toRemove = Mathf.Min(stackAmount, _shockSources.Count);
        for (int i = 0; i < toRemove; i++)
        {
            _shockSources.Dequeue();
        }

        if (_shockSources.Count == 0)
        {
            _shockTickTimer = 0f;
            if (shockedVFX != null) shockedVFX.Stop();
        }
    }

    public void ApplyShock(int stacks, Tower source)
    {
        if (stacks <= 0) return;
        if (shockTickCooldownGlobal <= 0f) return;
        if (enemy != null && enemy.IsImmuneTo(Enemy.ImmunityFlags.Shocked)) return;

        bool wasShocked = _shockSources.Count > 0;

        int available = Mathf.Max(0, maxShockStacksGlobal - _shockSources.Count);
        int toAdd = Mathf.Min(stacks, available);
        for (int i = 0; i < toAdd; i++)
        {
            _shockSources.Enqueue(source);
        }

        // Start ticking when shock is first applied.
        if (!wasShocked && _shockSources.Count > 0)
        {
            _shockTickTimer = shockTickCooldownGlobal;
        }
    }

    public void ClearPoisoned()
    {
        _poisonSources.Clear();
        _poisonTickTimer = 0f;

        if (poisonedVFX != null)
        {
            poisonedVFX.Stop();
        }
    }

    public void ReducePoisoned(int stackAmount)
    {
        if (stackAmount <= 0) return;
        if (_poisonSources.Count == 0) return;

        int toRemove = Mathf.Min(stackAmount, _poisonSources.Count);
        for (int i = 0; i < toRemove; i++)
        {
            _poisonSources.Dequeue();
        }

        if (_poisonSources.Count == 0)
        {
            _poisonTickTimer = 0f;
            if (poisonedVFX != null) poisonedVFX.Stop();
        }
    }

    public void ApplyPoison(int stacks, Tower source)
    {
        if (stacks <= 0) return;
        if (poisonTickCooldownGlobal <= 0f || poisonTickDamageGlobal <= 0f) return;
        if (enemy != null && enemy.IsImmuneTo(Enemy.ImmunityFlags.Poisoned)) return;

        bool wasPoisoned = _poisonSources.Count > 0;

        int available = Mathf.Max(0, maxPoisonStacksGlobal - _poisonSources.Count);
        int toAdd = Mathf.Min(stacks, available);
        for (int i = 0; i < toAdd; i++)
        {
            _poisonSources.Enqueue(source);
        }

        // Start ticking when poison is first applied.
        if (!wasPoisoned && _poisonSources.Count > 0)
        {
            _poisonTickTimer = GetPoisonTickCooldown();
        }
    }

    public float GetTotalPendingDamage()
    {
        float burnPending = Mathf.Max(0, BurnStacks) * GetEffectiveBurnTickDamage();
        float poisonPending = Mathf.Max(0, PoisonStacks) * Mathf.Max(0f, poisonTickDamageGlobal);
        float shockPending = Mathf.Max(0, ShockStacks) * Mathf.Max(0f, shockTickDamage);
        return burnPending + poisonPending + shockPending;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        currentHealth = Mathf.Max(0.0001f, maxHealth);
        healthBar = GetComponentInChildren<HealthBar>();    
        _juice = GetComponent<Juice>();
        _crtJuice = GetComponent<CRTJuice>();

        // Auto-bind health bar if not set.
        if (healthBar == null) healthBar = GetComponentInChildren<HealthBar>();
        if (healthBar != null) healthBar.RegisterChange(GetHealthPercentage());
        if (healthBar != null) healthBar.RegisterSlowChange(0);
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public void SetMaxHealth(float newMax)
    {
        newMax = Mathf.Max(0.0001f, newMax);

        // Avoid resetting health bar buffer timing when max health is unchanged.
        // AgentTower refreshes bonuses frequently; this preserves white-bar catch-up animation.
        if (Mathf.Approximately(maxHealth, newMax))
        {
            if (currentHealth > newMax)
            {
                currentHealth = newMax;
                if (healthBar != null) healthBar.RegisterChange(GetHealthPercentage());
            }
            return;
        }

        float ratio = maxHealth > 0f ? currentHealth / maxHealth : 1f;
        maxHealth = newMax;
        currentHealth = maxHealth * ratio;
        if (healthBar != null) healthBar.RegisterChange(GetHealthPercentage());
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }



    public void ChangeHealth(float amount, Tower source, CM.ColorType damageTypeOverride = CM.ColorType.None, Tower.CustomDamageData customDamageData = null)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        if (amount < 0f)
        {
            CM.ColorType healType = damageTypeOverride != CM.ColorType.None ? damageTypeOverride : CM.ColorType.Green;
            Heal(-amount, source, healType, customDamageData);
            return;
        }

        TakeDamage(amount, source, damageTypeOverride, customDamageData);
    }

    public void SetShowFloatingDamageText(bool show)
    {
        showFloatingDamageText = show;
    }

    public void Heal(float healAmount, Tower source = null, CM.ColorType healTypeOverride = CM.ColorType.Green, Tower.CustomDamageData customDamageData = null)
    {
        if (healAmount <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

        // No effective heal.
        if (currentHealth <= before) return;

        if (enemy != null)
        {
            enemy.OnHealthChange();
        }

        if (healthBar != null)
        {
            healthBar.RegisterChange(GetHealthPercentage());
        }

        if (showFloatingDamageText && TextObjectPool.instance != null)
        {
            CM.ColorType colorType = healTypeOverride != CM.ColorType.None ? healTypeOverride : CM.ColorType.Green;
            TextObjectPool.instance.PlayDamageText(transform, transform.position, -healAmount, CM.i.ColorTypeToColor(colorType), source, customDamageData, directional: false);
        }
    }

	public void TakeDamage(float amount, Tower source, CM.ColorType damageTypeOverride = CM.ColorType.None, Tower.CustomDamageData customDamageData = null)
	{
		if (amount <= 0f) return;
        if (currentHealth <= 0f) return;

        Agent targetAgent = null;
        if (enemy == null)
        {
            targetAgent = GetComponent<Agent>();
            if (targetAgent == null)
            {
                targetAgent = GetComponentInParent<Agent>();
            }
        }

        float sourceBaseAmount = amount;

		// Update rolling average once per damage event.
		float dmg = Mathf.Max(0.0001f, amount);
		float avg = PushDamageAndGetAverage(dmg);

		// Track actual damage dealt (don't count overkill).
        float applied = Mathf.Min(amount, currentHealth);
        if (applied > 0f && source != null)
		{
            float sourceBaseApplied = Mathf.Min(Mathf.Max(0f, sourceBaseAmount), applied);
            if (sourceBaseApplied > 0f)
            {
                source.RecordDamageDealt(sourceBaseApplied);
                if (enemy != null) enemy.RecordDamageSource(source, sourceBaseApplied);
            }
		}

		if (applied > 0f && enemy != null && SaveDataManager.instance != null)
		{
			SaveDataManager.instance.AddDamageDealtToEnemies(applied);
			SaveDataManager.instance.NotifySingleHitDamage(amount);

			if (damageTypeOverride == CM.ColorType.Orange)
			{
				SaveDataManager.instance.AddFireDamageDealt(applied);
			}

			if (damageTypeOverride == CM.ColorType.Cyan)
			{
				SaveDataManager.instance.AddLightningDamageDealt(applied);
			}

            if (damageTypeOverride == CM.ColorType.Green && IsPoisoned)
            {
                SaveDataManager.instance.AddPoisonDamageDealt(applied);
            }

			if (enemy.HasStatusEffect(Enemy.StatusEffect.Exposed))
			{
				SaveDataManager.instance.AddExposedDamageDealt(applied);
			}
		}
        if (targetAgent != null)
        {
            List<LensTower> lenses = targetAgent.GetLens();
            if (lenses != null && lenses.Count > 0)
            {
                for (int i = 0; i < lenses.Count; i++)
                {
                    LensTower lens = lenses[i];
                    if (lens == null) continue;

                    float multiplier = lens.GetAgentDamageMultiplier(this, customDamageData);
                    amount = Mathf.Max(0f, amount * multiplier);
                    if (customDamageData != null)
                    {
                        customDamageData.ApplyFinalDamageMultiplier(multiplier);
                    }
                    if (multiplier != 1) 
                    {
                        lens.OnAgentDamagedInLens(targetAgent);
                    }
                    if (amount <= 0f) return;

                    
                }
            }
        }

		currentHealth -= amount;

		if (enemy != null)
		{
			enemy.OnDamageTaken(amount);
			enemy.OnHealthChange();

            if (applied > 0f && RenderFeatureEffects.instance != null)
            {
                RenderFeatureEffects.instance.OnEnemyDamagedChromaticImpulse();
            }
		}

		// Notify UI.
		if (healthBar != null)
		{
			healthBar.RegisterChange(GetHealthPercentage());
		}

		// Floating damage text
		if (showFloatingDamageText && TextObjectPool.instance != null)
		{
			Color textColor;
			if (damageTypeOverride != CM.ColorType.None && CM.i != null)
			{
				textColor = CM.i.ColorTypeToColor(damageTypeOverride);
			}
			else
			{
				textColor = source != null ? source.GetColor() : Color.white;
			}

            TextObjectPool.instance.PlayDamageText(transform, transform.position, amount, textColor, source, customDamageData, directional: true);
		}

		TryApplyJuice(dmg, avg, source);

        if (_crtJuice != null && enemy != null)
        {
            CM.ColorType crtDamageType = damageTypeOverride;
            if (crtDamageType == CM.ColorType.None && customDamageData != null)
            {
                crtDamageType = customDamageData.damageType;
            }
            if (crtDamageType == CM.ColorType.None && source != null)
            {
                crtDamageType = source.GetDamageType(customDamageData);
            }

            if (crtDamageType != CM.ColorType.None)
            {
                if (source != null)
                {
                    _crtJuice.OnDamage(amount, crtDamageType, source.transform.position);
                }
                else
                {
                    _crtJuice.OnDamage(amount, crtDamageType);
                }
            }
        }

		if (enemy != null) enemy.Flash();

		if (currentHealth <= 0f)
		{
			float overkillAmount = -currentHealth;
			currentHealth = 0f;

			if (destroyOnDeath)
			{
				// Attribute the kill to the source tower, if any.
				if (source != null)
				{
					source.RecordKill();
				}

				if (enemy != null)
				{
					if (RM.i != null && RM.i.Active(RM.ID.overkill) && overkillAmount > 0f)
					{
						Enemy nearest = enemy.GetNearestEnemy(3f);
						if (nearest != null && nearest.health != null)
						{
                            if (LaserObjectPool.instance != null)
                            {
                                Color overkillColor = Color.white;
                                if (damageTypeOverride != CM.ColorType.None && CM.i != null)
                                {
                                    overkillColor = CM.i.ColorTypeToColor(damageTypeOverride);
                                }
                                else if (customDamageData != null && customDamageData.damageType != CM.ColorType.None && CM.i != null)
                                {
                                    overkillColor = CM.i.ColorTypeToColor(customDamageData.damageType);
                                }
                                else if (source != null)
                                {
                                    overkillColor = source.GetColor(customDamageData);
                                }

                                LaserObjectPool.instance.ShowLaser(enemy.transform.position, nearest.transform.position, overkillColor, 0.05f, 0.15f, 0.9f);
                            }

							nearest.health.TakeDamage(overkillAmount, source, damageTypeOverride, customDamageData);
						}
					}

					enemy.OnKill(source);
					enemy.DespawnWithSwell();
				}
				else
				{
                    Agent agent = GetComponent<Agent>();
                    if (agent == null) agent = GetComponentInParent<Agent>();

                    if (agent != null)
                    {
                        agent.DespawnWithSwell();
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
				}
			}
		}

	}

    private float GetCurrentDamageAverage()
    {
        return _damageWindow.Count > 0 ? (_damageSum / _damageWindow.Count) : 0f;
    }

    public void FixedUpdate()
    {
        var movement = enemy != null ? enemy.GetMovement() : null;
        if (movement != null && movement.SlowDecayEnabled())
        {
            healthBar.blueBox.color = CM.i.ColorTypeToColor(CM.ColorType.Blue);
        }   
        else
        {
            healthBar.blueBox.color = CM.i.ColorTypeToColor(CM.ColorType.Cyan);
        }
    }

    private void Update()
    {
        if (_burnSources.Count > 0 && burningVFX != null && !burningVFX.isPlaying)
        {
            burningVFX.Play();
        }
        else if (_burnSources.Count == 0 && burningVFX != null && burningVFX.isPlaying)
        {
            burningVFX.Stop();
        }

        if (healthBar != null)
        {
            float maxHp = Mathf.Max(0.0001f, maxHealth);
            healthBar.RegisterPoisonChange((PoisonStacks * Mathf.Max(0f, poisonTickDamageGlobal)) / maxHp);
            healthBar.RegisterBurnChange((BurnStacks * GetEffectiveBurnTickDamage()) / maxHp);
            healthBar.RegisterShockChange((ShockStacks * Mathf.Max(0f, shockTickDamage)) / maxHp);
        }

        float burnTickDamage = GetEffectiveBurnTickDamage();
        if (_burnSources.Count > 0 && fireTickCooldownGlobal > 0f && burnTickDamage > 0f && currentHealth > 0f)
        {
            _burnTickTimer -= Time.deltaTime;
            while (_burnSources.Count > 0 && _burnTickTimer <= 0f)
            {
                Tower source = _burnSources.Dequeue();

                // Each burn tick is real base damage attributed to the tower that applied that stack.
                // Burning damage always uses the Orange damage type.
                TakeDamage(burnTickDamage, source, CM.ColorType.Orange, source != null ? source.towerDamageData : null);
                TryPlayStatusDamageHitVfx(burnTickDamage, CM.ColorType.Orange);
                if (enemy != null && enemy.onBurnVfx != null)
                {
                    enemy.onBurnVfx.Play();
                }

                if (currentHealth <= 0f) return;

                _burnTickTimer += fireTickCooldownGlobal;
            }
        }

        // --- Shock tick ---
        if (_shockSources.Count > 0 && shockedVFX != null && !shockedVFX.isPlaying)
        {
            shockedVFX.Play();
        }
        else if (_shockSources.Count == 0 && shockedVFX != null && shockedVFX.isPlaying)
        {
            shockedVFX.Stop();
        }
        if (_shockSources.Count > 0 && shockTickCooldownGlobal > 0f && currentHealth > 0f)
        {
            _shockTickTimer -= Time.deltaTime;
            while (_shockSources.Count > 0 && _shockTickTimer <= 0f)
            {
                Tower source = _shockSources.Dequeue();

                float currentShockTickDamage = Mathf.Max(0f, shockTickDamage);
                if (currentShockTickDamage > 0f)
                {
                    TakeDamage(currentShockTickDamage, source, CM.ColorType.Cyan, source != null ? source.towerDamageData : null);
                    TryPlayStatusDamageHitVfx(currentShockTickDamage, CM.ColorType.Cyan);

                    if (currentHealth <= 0f) return;
                }

                if (source != null && enemy != null && LightningHelper.instance != null)
                {
                    int chainTargets = Mathf.Max(1, shockLightningChainMinTargets);
                    float chainDamage = Mathf.Max(0f, shockLightningChainDamage);
                    float chainChance = Mathf.Clamp01(shockLightningChainChance);

                    if (TagManager.instance != null)
                    {
                        chainChance = Mathf.Clamp01(TagManager.instance.GetCyanTagShockLightningChance());
                        chainDamage += Mathf.Max(0f, TagManager.instance.GetCyanTagShockLightningChainDamageBonus());
                        chainTargets += Mathf.Max(0, TagManager.instance.GetCyanTagShockLightningChainCountBonus());
                    }

                    if (chainDamage > 0f && Random.value <= chainChance)
                    {
                        LightningHelper.instance.Lightning(
                            source,
                            enemy,
                            chainTargets,
                            applyEffects: false,
                            visualize: true,
                            includeTowerInVisualization: false,
                            data: null,
                            fixedDamageOverride: chainDamage);
                        TryPlayStatusDamageHitVfx(chainDamage, CM.ColorType.Cyan);
                    }
                }

                if (currentHealth <= 0f) return;

                _shockTickTimer += shockTickCooldownGlobal;
            }
        }

        // --- Poison tick ---
        if (_poisonSources.Count > 0 && poisonedVFX != null && !poisonedVFX.isPlaying)
        {
            poisonedVFX.Play();
        }
        else if (_poisonSources.Count == 0 && poisonedVFX != null && poisonedVFX.isPlaying)
        {
            poisonedVFX.Stop();
        }
        float poisonTickCooldown = GetPoisonTickCooldown();
        if (_poisonSources.Count > 0 && poisonTickCooldown > 0f && poisonTickDamageGlobal > 0f && currentHealth > 0f)
        {
            _poisonTickTimer -= Time.deltaTime;
            while (_poisonSources.Count > 0 && _poisonTickTimer <= 0f)
            {
                Tower source = _poisonSources.Dequeue();

                // Poison tick uses green damage type.
                TakeDamage(poisonTickDamageGlobal, source, CM.ColorType.Green, source != null ? source.towerDamageData : null);
                TryPlayStatusDamageHitVfx(poisonTickDamageGlobal, CM.ColorType.Green);

                if (SaveDataManager.instance != null)
                {
                    SaveDataManager.instance.AddPoisonDamageDealt(Mathf.Max(0f, poisonTickDamageGlobal));
                }

                TryTriggerPlagueSpread(source);

                if (currentHealth <= 0f) return;

                _poisonTickTimer += poisonTickCooldown;
            }
        }
    }

    private void TryPlayStatusDamageHitVfx(float damage, CM.ColorType colorType)
    {
        if (damage <= 0f) return;

        var hitEffect = OnHitParticleEffect.instance;
        if (hitEffect == null) return;

        Color hitColor = CM.i != null ? CM.i.ColorTypeToColor(colorType) : Color.white;
        float damageScale = TextObjectPool.instance != null
            ? TextObjectPool.instance.GetDamageTextScalePreview(transform, Mathf.Max(0.0001f, damage))
            : 1f;
        int particleCount = hitEffect.GetParticleCountFromDamageScale(damageScale);
        hitEffect.OnHitVfx(transform.position, particleCount, hitColor, false, 0f);
    }

    private void TryTriggerPlagueSpread(Tower source)
    {
        if (enemy == null || source == null) return;
        if (RM.i == null || !RM.i.Active(RM.ID.plague)) return;
        if (Random.value > Mathf.Clamp01(RM.plagueSpreadChance)) return;
        if (QuadTree2D.instance == null) return;

        float range = Mathf.Max(0f, RM.plagueSpreadRange);
        if (range <= 0f) return;

        int stacks = Mathf.Max(1, RM.plagueSpreadStacks);
        Vector2 center = enemy.rb != null ? enemy.rb.position : (Vector2)enemy.transform.position;

        List<Enemy> nearby = new List<Enemy>(16);
        QuadTree2D.instance.QueryCircle(center, range, nearby, enemy);

        Enemy best = null;
        float bestD2 = float.MaxValue;
        for (int i = 0; i < nearby.Count; i++)
        {
            Enemy candidate = nearby[i];
            if (candidate == null || candidate == enemy) continue;
            if (candidate.health == null) continue;
            if (candidate.health.GetCurrentHealth() <= 0f) continue;
            if (candidate.IsImmuneTo(Enemy.ImmunityFlags.Poisoned)) continue;

            Vector2 candidatePos = candidate.rb != null ? candidate.rb.position : (Vector2)candidate.transform.position;
            float d2 = (candidatePos - center).sqrMagnitude;
            if (d2 > range * range) continue;
            if (best == null || d2 < bestD2)
            {
                best = candidate;
                bestD2 = d2;
            }
        }

        if (best == null || best.health == null) return;

        best.health.ApplyPoison(stacks, source);

        if (LaserObjectPool.instance != null)
        {
            LaserObjectPool.instance.ShowLaser(
                enemy.transform.position,
                best.transform.position,
                plagueLaserColor,
                laserWidthOverride: null,
                fadeDurationOverride: 0.2f,
                alphaOverride: 0.9f);
        }

        RM.i.IndicateRelic(RM.ID.plague);
    }

    private void TryApplyJuice(float dmg, float avg, Tower source)
    {
        if (!enableJuiceOnDamage) return;
        if (_juice == null) return;

        float ratio = dmg / Mathf.Max(0.0001f, avg);
        float scaled = Mathf.Clamp(ratio, 0f, Mathf.Max(0.01f, juiceAvgScaleClampMax));

        Vector2 dir;
        if (source != null)
        {
            Vector3 away3 = (transform.position - source.transform.position);
            away3.z = 0f;
            dir = away3.sqrMagnitude > 0.0001f ? new Vector2(away3.x, away3.y).normalized : Vector2.up;
        }
        else
        {
            dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
        }

        float forceMag = Mathf.Max(0f, juiceForceBase) * scaled;
        float bounceMag = Mathf.Max(0f, juiceBounceBase) * scaled;
        if (source != null)
        {
            forceMag *= source.onDamageJuiceModifier;
            bounceMag *= source.onDamageJuiceModifier;
        }
        if (forceMag > 0f) _juice.AddForce(dir, forceMag);
        if (bounceMag != 0f) _juice.AddBounce(bounceMag);
    }

    private float PushDamageAndGetAverage(float dmg)
    {
        int window = Mathf.Max(1, damageTextScalingWindowCount);

        _damageWindow.Enqueue(dmg);
        _damageSum += dmg;

        while (_damageWindow.Count > window)
        {
            _damageSum -= _damageWindow.Dequeue();
        }

        return _damageWindow.Count > 0 ? (_damageSum / _damageWindow.Count) : dmg;
    }


    //public void OnParticleCollision(GameObject other)
    //{
    //    Tower source = ParticleSystemManager.instance.registeredParticleSystems[other];
    //    float damage = 0f;
    //    if (source != null)
    //    {
    //        damage = source.GetDamage();
    //    }

    //    if (damage > 0f)
    //    {
    //        TakeDamage(damage, source, CM.ColorType.None, source != null ? source.towerDamageData : null);

    //        // Apply effects after damage is dealt, with projectile context.
    //        var enemy = GetComponent<Enemy>();
    //        if (enemy != null && source != null)
    //        {
    //            source.ApplyEffects(enemy);
    //        }
    //    }
    //}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var projectile = collision.GetComponent<Projectile>();
        if (projectile == null) return;

        // Inform projectile so it can consume pierce and despawn when needed.
        if (!projectile.TryRegisterHit(this)) return;

        // Capture impact direction before any on-hit behavior can alter velocity/despawn state.
        Vector2 impactDir = Vector2.zero;
        if (projectile.rb != null && projectile.rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            impactDir = projectile.rb.linearVelocity.normalized;
        }
        else
        {
            Vector2 fallbackDir = projectile.transform.up;
            if (fallbackDir.sqrMagnitude > 0.001f)
            {
                impactDir = fallbackDir.normalized;
            }
        }
        bool hasImpactDir = impactDir.sqrMagnitude > 0.001f;
        float impactAngle = hasImpactDir ? Mathf.Atan2(impactDir.y, impactDir.x) * Mathf.Rad2Deg : 0f;
        bool nonPiercingHit = projectile.WillDespawnAfterCurrentHit();

        float damage =0f;
        Tower source = projectile.sourceTower;
        CM.ColorType damageType = CM.ColorType.None;
        if (source != null)
        {
            projectile.data.enemyHit = enemy;
            damage = source.GetDamage(projectile.data, rollCrit: false);
            damageType = source.GetDamageType(projectile.data);
        }

        foreach (Tower tower in projectile.GetFieldEffects())
        {
            tower.ApplyEffects(enemy, null);
        }

        if (damage >0f)
        {
            TakeDamage(damage, source, damageType, projectile.data);
            projectile.IndicateHit(damageType, transform.position);

            if (enemy != null && EnemyManager.instance != null)
            {
                EnemyManager.instance.OnProjectileHitEnemyCell(enemy, damageType);
            }

            if (enemy != null && source != null)
            {
                if (projectile.effectOverrideList != null)
                {
                    for (int i = 0; i < projectile.effectOverrideList.Count; i++)
                    {
                        Effect effect = projectile.effectOverrideList[i];
                        if (effect == null) continue;
                        effect.ApplyEffect(enemy, projectile);
                    }
                }
                else
                {
                    source.ApplyEffects(enemy, projectile);
                }
            }

            if (projectile.data != null && projectile.data.crit && RM.i != null && RM.i.Active(RM.ID.criticalPierce))
            {
                projectile.AddPierce(1);
                RM.i.IndicateRelic(RM.ID.criticalPierce);
            }

            if (enemy != null)
            {
                projectile.TryBounceToNearbyEnemy(enemy);
            }
        }
        var hitEffect = OnHitParticleEffect.instance;
        if (projectile.triggerOnHitVfx && hitEffect != null && damage > 0f)
        {
            Color hitColor = Color.white;
            if (damageType != CM.ColorType.None && CM.i != null)
            {
                hitColor = CM.i.ColorTypeToColor(damageType);
            }

            bool hasDir;
            float angle;
            if (nonPiercingHit)
            {
                hasDir = hasImpactDir;
                angle = impactAngle;
            }
            else
            {
                Vector2 vel = projectile.rb != null ? projectile.rb.linearVelocity : Vector2.zero;
                hasDir = vel.sqrMagnitude > 0.001f;
                angle = hasDir ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg : 0f;
            }

            float damageScale = TextObjectPool.instance != null
                ? TextObjectPool.instance.GetDamageTextScalePreview(transform, Mathf.Max(0.0001f, damage))
                : 1f;
            int particleCount = hitEffect.GetParticleCountFromDamageScale(damageScale);
            hitEffect.OnHitVfx(transform.position, particleCount, hitColor, hasDir, angle);
        }

        projectile.FinalizeHit();
    }
}
