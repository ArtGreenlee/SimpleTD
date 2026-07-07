using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Random = UnityEngine.Random;
using ParticleSystem = UnityEngine.ParticleSystem;
using UID = UpgradeData.UID;

[RequireComponent(typeof(Juice), typeof(TowerInteractable))]
public class Tower : MonoBehaviour
{
    private TowerVfxManager vfx;
    protected enum CardinalRotation
    {
        North,
        East,
        South,
        West,
    }

    public static float exposeMultiplerGlobal = .5f;
    public static float markGlobalDamageMultiplier = 4;
    public static float chargingCooldownMultiplierGlobal = 0.5f;
    public static float maxChargeAmount = 5;
    public static float maxBuffAmount = 5f;
    public static float doubleAttackDelay = .01f;
    public bool doubleAttackOverride = false;
    public bool rotateable;
    public bool rotateBodyWhenAttacking = true;

    public bool RollDoubleAttack()
    {
        if (doubleAttackOverride)
        {
            return true;
        }
        return false;
    }
    public enum ID
    {
        GreenTower,
        RedTower,
        BlueTower,
        BuffTower,
        PurpleLaser,
        OrangeFlamethrower,
        A,
        BombTower,
        IceTower,
        Shotgun,
        Sniper,
        Gatling,
        Missile,
        Mine,
        FireField,
        BlackHole,
        Mortar,
        Lightning,
        Rainbow,
        GoldProjectile,
        Agent,
        Necromancer,
        Bank,
        LaserAgent,
        Sun,
        Lens,
        LightningRod,
        QuadLaser,
        V,
        W,
        X,
        Y,
        Z,
    }

    public virtual void OnEnemyKilledInRange(Enemy enemy)
    {

    }

    public enum Tag
    {
        Red,
        Blue,
        Green,
        Orange,
        Purple,
        White,
        Black,
        Yellow,
        Cyan

    }

    public enum State
    {
        Shop,
        Placed,
        Inventory,
    }

    public enum StatusEffect
    {
        Stunned
    }

    public enum Rarity
    {
        Common,
        Rare,
        Legendary
    }

    [Flags]
    public enum DamageTypeFlags
    {
        None = 0,
        Red = 1 << 0,
        Blue = 1 << 1,
        Green = 1 << 2,
        Yellow = 1 << 3,
        Purple = 1 << 4,
        Orange = 1 << 5,
        White = 1 << 6,
        Black = 1 << 7,
        Cyan = 1 << 8,
        Gold = 1 << 9,
        Moss = 1 << 10,
    }

    private Rarity rarity;

    public Rarity GetRarity()
    {
        return rarity;
    }

    public void SetRarity(Rarity r)
    {
        rarity = r;
    }

    public ID id;

    [Header("Shop")]
    [SerializeField] private int cost;

    [Header("Combat")]
    [Tooltip("If true, Attack() can be called even when no enemy is currently targeted.")]
    public bool attackWithoutTarget = false;
    [Header("Info Display")]
    [Tooltip("If false, hide this tower's damage text in TowerInformationDisplay.")]
    public bool showDamageInfo = true;
    [Tooltip("Optional prefix override for damage text in TowerInformationDisplay. Leave empty to use the default prefix.")]
    public string damageInfoPrefixOverride = string.Empty;
    [Tooltip("If false, hide this tower's cooldown text in TowerInformationDisplay.")]
    public bool showCooldownInfo = true;
    [Tooltip("Optional prefix override for cooldown text in TowerInformationDisplay. Leave empty to use the default prefix.")]
    public string cooldownInfoPrefixOverride = string.Empty;
    [Tooltip("If false, hide this tower's range text in TowerInformationDisplay.")]
    public bool showRangeInfo = true;
    [Tooltip("Optional prefix override for range text in TowerInformationDisplay. Leave empty to use the default prefix.")]
    public string rangeInfoPrefixOverride = string.Empty;
    [Tooltip("If true, this tower's range is only shown while it is being held by the tower placement tool.")]
    [SerializeField] private bool onlyShowRangeWhenHoldingTowerTool = false;
    [Tooltip("If true, this tower will only attack when a wave is active in WaveManager. Only used when attackWithoutTarget is true.")]
    [SerializeField] private bool onlyAttackWhenWaveIsActive = false;
    public float onDamageJuiceModifier = 1f;
    [SerializeField, Min(1)] private int lightningChainCount = 5;

    [Header("Laser")]
    [SerializeField, Min(0)] private int baseBounceCount = 1;
    [SerializeField, Min(0)] private int basePierceCount = 1;

    [Header("Transforms")]
    public Transform bulletSpawnTransform;

    [Header("Color")]
    [SerializeField] private DamageTypeFlags damageTypeFlags = DamageTypeFlags.Red;
    public List<Tag> tags = new List<Tag> { };
    [SerializeField] private List<CM.ColorType> toolTipTags;

    [Header("Crafting")]
    [SerializeField, HideInInspector] private List<ID> craftingRequiredTowers = new List<ID>();

    [Header("Charge")]
    public bool canBeCharged = true;
    [Tooltip("Accumulated cooldown reduction budget consumed on future attacks.")]
    [SerializeField, Min(0f)] protected float chargeAmount = 0f;
    private const float minCooldownRatio = 0.2f;
    public static float chargingCooldownMultiplier = 0.5f;

    [Header("Buff")]
    public bool canBeBuff = true;
    [Tooltip("Accumulated damage multiplier bonus consumed on the next attack (does not apply to DOT effects).")]
    [SerializeField, Min(0f)] protected float buffAmount = 0f;
    private const float minBuffMultiplierBonus = 0f;
    private const float maxBuffMultiplierBonus = 2f;

    [Header("Upgrades")]
    private List<UID> activeUpgrades = new List<UID>();

    [Header("XP")]
    private XPBar xpBar;
    private int xpCount = 0;

    private State state = State.Shop;
    private float cooldownTimer;
    private float totalDamageDealt;
    private int enemiesKilled;
    private readonly HashSet<StatusEffect> currentStatusEffects = new HashSet<StatusEffect>();
    private readonly HashSet<Enemy> maxHealthPercentDamageAppliedOnEnterRange = new HashSet<Enemy>();
    private float stunnedUntil = -1f;
    private bool usedChargeForLastAttack;
    private bool usedBuffForLastAttack;
    private float _pendingBuffMultiplier;
    private int zealStacks;
    private int sellCostBonusFromKills;

    protected Enemy targettedEnemy;
    protected RangeManager rangeManager;
    private GridManager _grid;
    private Juice juice;
    private Image chargeMeterImage;
    private Image buffMeterImage;
    private List<SpriteRenderer> upgradeVisualizers;
    private List<Effect> effects;
    private static FieldInfo s_heldTowerField;

    protected TowerTool towerTool;
    protected CardinalRotation towerRotation = CardinalRotation.North;
    private bool _wasManualMode;

    public bool HasCraftingRequirements => craftingRequiredTowers != null && craftingRequiredTowers.Count > 0;

    public IReadOnlyList<ID> CraftingRequiredTowers
    {
        get
        {
            if (craftingRequiredTowers == null) craftingRequiredTowers = new List<ID>();
            return craftingRequiredTowers;
        }
    }

    public Vector3 GetTargetPosition()
    {
        if (towerTool != null && towerTool.gameObject.activeInHierarchy)
        {
            return towerTool.transform.position;
        }
        return transform.position;
    }

    public bool IsIncludedInDamageTypes(CM.ColorType type)
    {
        DamageTypeFlags flag = ColorTypeToDamageTypeFlag(type);

        if (flag == DamageTypeFlags.None) return false;
        return (damageTypeFlags & flag) != 0;
    }

    public static DamageTypeFlags ColorTypeToDamageTypeFlag(CM.ColorType type)
    {
        return type switch
        {
            CM.ColorType.Red => DamageTypeFlags.Red,
            CM.ColorType.Blue => DamageTypeFlags.Blue,
            CM.ColorType.Green => DamageTypeFlags.Green,
            CM.ColorType.Yellow => DamageTypeFlags.Yellow,
            CM.ColorType.Purple => DamageTypeFlags.Purple,
            CM.ColorType.Orange => DamageTypeFlags.Orange,
            CM.ColorType.White => DamageTypeFlags.White,
            CM.ColorType.Black => DamageTypeFlags.Black,
            CM.ColorType.Cyan => DamageTypeFlags.Cyan,
            CM.ColorType.Gold => DamageTypeFlags.Gold,
            CM.ColorType.Moss => DamageTypeFlags.Moss,
            _ => DamageTypeFlags.None,
        };
    }

    public void SetTargetIndicatorPosition(Vector3 newPosition)
    {
        if (towerTool != null)
        {
            towerTool.transform.position = newPosition;
        }
    }

    public virtual int GetBaseBounceCount()
    {
        return Mathf.Max(0, baseBounceCount);
    }

    protected void AddBaseBounceCount(int amount)
    {
        baseBounceCount = Mathf.Max(0, baseBounceCount + amount);
    }

    public virtual int GetPierceCount()
    {
        return Mathf.Max(0, basePierceCount);
    }

    protected void AddBasePierceCount(int amount)
    {
        basePierceCount = Mathf.Max(0, basePierceCount + amount);
    }

    public State CurrentState
    {
        get => state;
        set => state = value;
    }

    public virtual void OnPickedUp() { }

    private void OnValidate()
    {
        if (damageTypeFlags == DamageTypeFlags.None)
        {
            SetDamageTypeFlags(DamageTypeFlags.Red);
        }

        AutoSetupTowerReferences();
    }

    protected void SetDamageTypeFlags(DamageTypeFlags flags)
    {
        damageTypeFlags = flags == DamageTypeFlags.None ? DamageTypeFlags.Red : flags;
        RefreshRangeVisualizationColor();
    }

    private void RefreshRangeVisualizationColor()
    {
        if (rangeManager == null) rangeManager = GetComponent<RangeManager>();
        if (rangeManager == null) rangeManager = GetComponentInChildren<RangeManager>();
        if (rangeManager == null) return;

        rangeManager.SetRangeVisualizerColor(GetColor());
    }

    public CM.ColorType GetConfiguredDamageType()
    {
        var configured = GetConfiguredDamageTypes();
        return configured.Count > 0 ? configured[0] : CM.ColorType.None;
    }

    public bool CanDealDamageType(CM.ColorType type)
    {
        var configured = GetConfiguredDamageTypes();
        return configured.Contains(type);
    }

    public List<CM.ColorType> GetConfiguredDamageTypes()
    {
        var configured = new List<CM.ColorType>(8);
        AddIfSelected(configured, DamageTypeFlags.Red, CM.ColorType.Red);
        AddIfSelected(configured, DamageTypeFlags.Blue, CM.ColorType.Blue);
        AddIfSelected(configured, DamageTypeFlags.Green, CM.ColorType.Green);
        AddIfSelected(configured, DamageTypeFlags.Yellow, CM.ColorType.Yellow);
        AddIfSelected(configured, DamageTypeFlags.Purple, CM.ColorType.Purple);
        AddIfSelected(configured, DamageTypeFlags.Orange, CM.ColorType.Orange);
        AddIfSelected(configured, DamageTypeFlags.White, CM.ColorType.White);
        AddIfSelected(configured, DamageTypeFlags.Black, CM.ColorType.Black);
        AddIfSelected(configured, DamageTypeFlags.Cyan, CM.ColorType.Cyan);
        AddIfSelected(configured, DamageTypeFlags.Gold, CM.ColorType.Gold);
        AddIfSelected(configured, DamageTypeFlags.Moss, CM.ColorType.Moss);
        return configured;
    }

    private void AddIfSelected(List<CM.ColorType> configured, DamageTypeFlags flag, CM.ColorType type)
    {
        if ((damageTypeFlags & flag) != 0)
        {
            configured.Add(type);
        }
    }

    public CM.ColorType GetDamageType(CustomDamageData data = null, bool rollAndStore = false)
    {
        if (!rollAndStore && data != null && data.damageType != CM.ColorType.None)
        {
            return data.damageType;
        }

        CM.ColorType resolved = RollRandomDamageTypeFromFlags();
        if (data != null)
        {
            data.damageType = resolved;
        }
        return resolved;
    }

    private CM.ColorType RollRandomDamageTypeFromFlags()
    {
        var configured = GetConfiguredDamageTypes();
        if (configured.Count == 0) return CM.ColorType.Red;
        return configured[Random.Range(0, configured.Count)];
    }

    public int GetCost()
    {
        int c = TowerCosts.GetCost(id, cost);
        foreach (var uid in activeUpgrades)
        {
            c += UpgradeCosts.GetCost(uid);
        }
        return c;
    } 

    public int GetSellCost()
    {
        int baseSellCost = GetCost() / 2;
        if (UpgradeActive(UID.IncreaseSellCostOnKill))
        {
            baseSellCost += Mathf.Max(0, sellCostBonusFromKills);
        }

        return Mathf.Max(0, baseSellCost);
    }
    public float GetTotalDamageDealt() => totalDamageDealt;
    public int GetXPCount() => xpCount;

    public void SetXPCount(int value)
    {
        xpCount = Mathf.Max(0, value);
        RefreshXPBar();
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;
        if (GetUpgradeLevel() >= GetNumberOfLevels()) return;
        xpCount += amount;
        RefreshXPBar();
    }

    public int GetUpgradeLevel() => activeUpgrades != null ? activeUpgrades.Count : 0;
    public bool IsCharged() => chargeAmount > baseCooldown;
    public int GetNumberOfLevels() => UpgradeData.GetUpgradesForTower(id).Count;
    public bool IsMaxLevel() => GetUpgradeLevel() >= GetNumberOfLevels();

    public RangeManager GetRangeManager()
    {
        return rangeManager;
    }

    public virtual string GetUpgradeDescription(UID uid)
    {
        string effectDescription = GetEffectUpgradeDescription(uid);
        if (!string.IsNullOrEmpty(effectDescription))
        {
            return effectDescription;
        }

        string description = UpgradeData.GetUpgradeDescription(uid);
        return !string.IsNullOrEmpty(description) ? description : "Unknown";
    }

    private string GetEffectUpgradeDescription(UID uid)
    {
        string ColorWord(CM.ColorType type, string text)
        {
            if (CM.i == null) return text;
            return CM.i.RTC(type, text);
        }

        switch (uid)
        {
            case UID.AddBurnOnHitEffect:
            {
                BurnOnHitEffect burnEffect = GetComponent<BurnOnHitEffect>();
                int burnStacks = burnEffect != null ? Mathf.Max(1, burnEffect.burnStacks) : 1;
                return "Applies " + ColorWord(CM.ColorType.Orange, "Burn") + " on hit (" + ColorWord(CM.ColorType.Green, "+" + burnStacks) + " stacks)";
            }
            case UID.AddPoisonOnHitEffect:
            {
                PoisonOnHitEffect poisonEffect = GetComponent<PoisonOnHitEffect>();
                int poisonStacks = poisonEffect != null ? Mathf.Max(1, poisonEffect.poisonStacks) : 1;
                return "Applies " + ColorWord(CM.ColorType.Green, "Poison") + " on hit (" + ColorWord(CM.ColorType.Green, "+" + poisonStacks) + " stacks)";
            }
            case UID.AddShockOnHitEffect:
            {
                ShockOnHitEffect shockEffect = GetComponent<ShockOnHitEffect>();
                int shockStacks = shockEffect != null ? Mathf.Max(1, shockEffect.shockStacks) : 1;
                return "Applies " + ColorWord(CM.ColorType.Cyan, "Shock") + " on hit (" + ColorWord(CM.ColorType.Green, "+" + shockStacks) + " stacks)";
            }
            case UID.ExposeOnHit:
            {
                ExposeOnHitEffect exposeEffect = GetComponent<ExposeOnHitEffect>();
                float exposeDuration = exposeEffect != null ? Mathf.Max(0f, exposeEffect.time) : 5f;
                return "Applies " + ColorWord(CM.GetExposeColor(), "Exposed") + " on hit for " + ColorWord(CM.ColorType.Green, exposeDuration.ToString("0.##") + "s");
            }
            case UID.AddStunOnHitEffect:
            {
                StunEffect stunEffect = GetComponent<StunEffect>();
                float stunChancePercent = stunEffect != null
                    ? Mathf.Clamp01(stunEffect.effectProbability) * 100f
                    : UpgradeData.AddStunOnHitChancePercent;
                float stunDuration = stunEffect != null
                    ? Mathf.Max(0f, stunEffect.duration)
                    : UpgradeData.AddStunOnHitDurationSeconds;
                return stunChancePercent.ToString("0.#") + "% chance to " + ColorWord(CM.ColorType.Purple, "Stun") + " for " + ColorWord(CM.ColorType.Green, stunDuration.ToString("0.#") + "s") + " on hit";
            }
            case UID.IncreaseSlow:
            {
                SlowEffect slowEffect = GetComponent<SlowEffect>();
                float predictedSlow = slowEffect != null
                    ? slowEffect.GetSlowAmount() + (UpgradeActive(UID.IncreaseSlow) ? 0f : (UpgradeData.IncreaseSlowAmount / 100f))
                    : (UpgradeData.IncreaseSlowAmount / 100f);
                return ColorWord(CM.ColorType.Green, "+" + UpgradeData.IncreaseSlowAmount.ToString("0.#") + "%")
                    + " slow (total " + ColorWord(CM.ColorType.Blue, (Mathf.Max(0f, predictedSlow) * 100f).ToString("0.#") + "%") + ")";
            }
            default:
                return null;
        }
    }

    public virtual string GetDescription()
    {
        return string.Empty;
    }

    public IReadOnlyList<UID> GetActiveUpgrades() => activeUpgrades;

    public bool HasEnemiesInRange()
    {
        return rangeManager != null && rangeManager.enemiesInRange.Count > 0;
    }

    [Header("Stats")]
    // THESE VALUES SHOULD NEVER BE CHANGED
    [SerializeField] private float baseCooldown;
    [SerializeField] private float baseDamage;
    [SerializeField] private float baseRange;
    [SerializeField] private float baseCritChance;
    [SerializeField] private float baseAOESize;
    private float baseCritModifier = 2;

    public virtual void OnRotate(int direction)
    {
        if (!rotateable) return;
        if (direction != -1 && direction != 1)
        {
            Debug.LogError("OnRotate got a bad direction");
            return;
        }

        if (direction > 0)
        {
            towerRotation = (CardinalRotation)(((int)towerRotation + 1) % 4);
        }
        else
        {
            towerRotation = (CardinalRotation)(((int)towerRotation + 3) % 4);
        }
    }

    public class CustomDamageData
    {
        public int numHit = 0;
        public Enemy enemyHit;
        public Collider2D[] hitColliders;
        public bool crit = false;
        public int critCount = 0;
        public bool isAOE = false;
        public CM.ColorType damageType = CM.ColorType.None;
        public Vector3? hitDirection = null;
        public float baseDamageRatio = 1;
        public float finalBaseDamage = 0f;
        public float finalMultiplier = 1f;
        public bool hasFinalDamageBreakdown = false;

        public void SetFinalDamageBreakdown(float baseDamage, float multiplier)
        {
            finalBaseDamage = Mathf.Max(0f, baseDamage);
            finalMultiplier = Mathf.Max(0f, multiplier);
            hasFinalDamageBreakdown = true;
        }

        public void ApplyFinalDamageMultiplier(float multiplier)
        {
            if (!hasFinalDamageBreakdown) return;
            finalMultiplier *= Mathf.Max(0f, multiplier);
        }

        public float GetFinalDamageAmount()
        {
            return finalBaseDamage * finalMultiplier;
        }

        //add as necessary for custom damage calculation
    }

    [Serializable]
    public class UpgradeLevel
    {
        public List<UID> upgrades = new List<UID>();
        public List<int> costOverrides = new List<int>();

        public int GetCost(int upgradeIndex)
        {
            if (upgrades == null || upgradeIndex < 0 || upgradeIndex >= upgrades.Count)
            {
                return 1;
            }

            if (costOverrides != null && upgradeIndex < costOverrides.Count && costOverrides[upgradeIndex] > 0)
            {
                return costOverrides[upgradeIndex];
            }

            return UpgradeCosts.GetCost(upgrades[upgradeIndex]);
        }
    }

    public CustomDamageData towerDamageData = new CustomDamageData();

    protected virtual float GetCooldownModified(float cooldown, CustomDamageData data = null)
    {
        return cooldown;
    }

    protected virtual float GetCooldownMultiplier(float multiplier, CustomDamageData data=null)
    {
        if (RM.i != null && RM.i.Active(RM.ID.loneWolfArtifact)
            && TowerManager.instance != null
                && TowerManager.instance.GetCurrentPlacedTowers() == 1
            && CurrentState == State.Placed)
        {
            multiplier *= 0.5f;
        }

        if (UpgradeActive(UID.DecreaseCooldown))
        {
            multiplier *= 1f - UpgradeData.DecreaseCooldownPercent / 100f;
        }

        return multiplier;
    }

    public float GetCooldownPreview(CustomDamageData data = null)
    {
        float cooldown = GetCooldownMultiplier(1, data) * GetCooldownModified(baseCooldown, data);
        float minAllowedCooldown = Mathf.Max(0f, baseCooldown * minCooldownRatio);
        return Mathf.Max(minAllowedCooldown, cooldown);
    }

    public virtual float GetCooldown(CustomDamageData data = null)
    {
        float cooldown = GetCooldownPreview(data);
        float minAllowedCooldown = Mathf.Max(0f, baseCooldown * minCooldownRatio);

        // Spend as much charge as possible to reduce this cooldown, while preserving leftover charge.
        float reducibleAmount = Mathf.Max(0f, cooldown - minAllowedCooldown);
        float chargeToConsume = Mathf.Min(Mathf.Max(0f, chargeAmount), reducibleAmount);
        usedChargeForLastAttack = chargeToConsume > 0f;
        cooldown -= chargeToConsume;
        chargeAmount -= chargeToConsume;

        // Snapshot and consume accumulated buff multiplier for the upcoming attack.
        _pendingBuffMultiplier = maxBuffAmount > 0f
            ? Mathf.Lerp(minBuffMultiplierBonus, maxBuffMultiplierBonus, Mathf.Clamp01(buffAmount / maxBuffAmount))
            : 0f;
        usedBuffForLastAttack = buffAmount > 0f;
        buffAmount = 0f;

        return Mathf.Max(minAllowedCooldown, cooldown);
    }

    public void SetBaseCooldown(float c)
    {
        Debug.Log("halfing tower base cooldown");
        baseCooldown = c;
    }
    
    public float GetBaseCooldown()
    {
        return baseCooldown;
    }

    public float GetChargeAmount()
    {
        return chargeAmount;
    }

    public bool IsAtMaxCharge()
    {
        return chargeAmount >= maxChargeAmount - 0.0001f;
    }

    public float GetMinCooldown()
    {
        return Mathf.Max(0f, baseCooldown * minCooldownRatio);
    }

    public bool UsedChargeForLastAttack()
    {
        return usedChargeForLastAttack;
    }

    public float GetCriticalChance()
    {
        float c = baseCritChance;
        if (UpgradeActive(UID.IncreaseCritChance))
        {
            c += .1f;
        }
        if (IsCharged() && RM.i.Active(RM.ID.criticalCharge))
        {
            c += .25f;
        }
        if (IsAtMaxCharge() && RM.i != null && RM.i.Active(RM.ID.alwaysCritOnFullyCharge))
        {
            c += 1f;
        }

        // GreenTag effects: Get bonus based on global green tower count
        if (TagManager.instance != null)
        {
            int greenTagLevel = TagManager.instance.GetTagLevel(Tag.Green);
            
            // Level 3: +20% crit chance for all towers
            if (greenTagLevel == 3)
            {
                c += TagManager.greenTagLevelThreeGlobalCritChanceIncrease;
            }
            // Level 2: +20% crit chance for towers with Green tag only
            else if (greenTagLevel == 2 && tags.Contains(Tag.Green))
            {
                c += TagManager.greenTagLevelTwoCritChanceIncrease;
            }
            // Level 1: +10% crit chance for towers with Green tag only
            else if (greenTagLevel == 1 && tags.Contains(Tag.Green))
            {
                c += TagManager.greenTagLevelOneCritChanceIncrease;
            }
        }

        return c;
    }

    public float GetCritModifier()
    {
        float critModifier = baseCritModifier;
        if (RM.i != null && RM.i.Active(RM.ID.criticalDamageBuff))
        {
            critModifier += 0.5f;
        }
        if (UpgradeActive(UID.CritDamageBoost))
        {
            critModifier += 0.2f;
        }
        return critModifier;
    }

    private static int GetEffectiveCritCount(CustomDamageData data)
    {
        if (data == null || !data.crit) return 0;
        return Mathf.Max(1, data.critCount);
    }

    protected virtual float GetCriticalDamageBonusMultiplier(CustomDamageData data)
    {
        int critCount = GetEffectiveCritCount(data);
        if (critCount <= 0) return 0f;

        if (data.enemyHit != null && data.enemyHit.IsImmuneTo(Enemy.ImmunityFlags.CriticalHit))
        {
            return 0f;
        }

        return (GetCritModifier() - 1f) * critCount;
    }

    private void TriggerCriticalHitEffects(CustomDamageData data)
    {
        int critCount = GetEffectiveCritCount(data);
        if (critCount <= 0) return;

        data.crit = true;
        data.critCount = critCount;
        OnCriticalHit(data);
    }

    protected virtual float GetDamageModified(float damage, CustomDamageData data=null)
    {
        if (data != null && data.enemyHit != null && data.enemyHit.health != null && data.crit)
        {
            Health enemyHealth = data.enemyHit.health;

            if (UpgradeActive(UID.ConvertBurningToBaseDamageOnCrit) && enemyHealth.BurnStacks > 0)
            {
                damage += Mathf.Max(0, enemyHealth.BurnStacks) * Mathf.Max(0f, Health.fireTickDamageGlobal);
                enemyHealth.ClearBurning();
            }

            if (UpgradeActive(UID.ConvertShockedToBaseDamageOnCrit) && enemyHealth.ShockStacks > 0)
            {
                damage += Mathf.Max(0, enemyHealth.ShockStacks) * Mathf.Max(0f, Health.shockLightningChainDamage);
                enemyHealth.ClearShocked();
            }

            if (UpgradeActive(UID.ConvertPoisonedToBaseDamageOnCrit) && enemyHealth.PoisonStacks > 0)
            {
                damage += Mathf.Max(0, enemyHealth.PoisonStacks) * Mathf.Max(0f, Health.poisonTickDamageGlobal);
                enemyHealth.ClearPoisoned();
            }
        }

        if (UpgradeActive(UID.MultiHitDamageIncrease) && data != null)
        {
            damage += Mathf.Max(0, data.numHit);
        }

        if (TagManager.instance != null)
        {
            if (UpgradeActive(UID.RedTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Red);
            }
            if (UpgradeActive(UID.BlueTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Blue);
            }
            if (UpgradeActive(UID.GreenTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Green);
            }
            if (UpgradeActive(UID.YellowTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Yellow);
            }
            if (UpgradeActive(UID.PurpleTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Purple);
            }
            if (UpgradeActive(UID.OrangeTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Orange);
            }
            if (UpgradeActive(UID.WhiteTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.White);
            }
            if (UpgradeActive(UID.BlackTagDamageIncrease))
            {
                damage += TagManager.instance.GetTagCount(Tag.Black);
            }
        }

        if (RM.i != null)
        {
            CM.ColorType resolvedType = GetDamageType(data);
            damage += RM.i.GetDamageTypeBaseDamageBonus(resolvedType);
        }

        if (UpgradeActive(UID.MassiveIncreaseBaseDamage))
        {
            damage += UpgradeData.MassiveIncreaseBaseDamageAmount;
        }

        if (UpgradeActive(UID.GatlingConditionExtraProjectile)
            && TowerManager.instance != null
            && TowerManager.instance.GetCurrentPlacedTowers() == 1
            && CurrentState == State.Placed)
        {
            damage += UpgradeData.OnlyPlacedTowerDamageIncreaseAmount;
        }

        return damage;
    }
    protected virtual float GetDamageMultiplier(float multiplier, CustomDamageData data=null)
    {
        if (data != null)
        {
            if (data.enemyHit != null && data.enemyHit.GetMark() == GetDamageType(data))
            {
                OnConsumeMark(data.enemyHit);
                if (!UpgradeActive(UID.DoesNotClearMark))
                {
                    data.enemyHit.ConsumeMark();
                }
                multiplier += markGlobalDamageMultiplier;

                if (UpgradeActive(UID.ConsumeMarkAlwaysCrit) && !data.crit)
                {
                    data.crit = true;
                    data.critCount = Mathf.Max(1, data.critCount);
                    TriggerCriticalHitEffects(data);
                }
            }
        }
        if (data != null)
        {
            multiplier += GetCriticalDamageBonusMultiplier(data);
        }
        
        // Add lens multiplier bonuses (additive, not multiplicative)
        if (data != null && data.enemyHit != null)
        {
            List<LensTower> lenses = data.enemyHit.GetLens();
            if (lenses != null && lenses.Count > 0)
            {
                for (int i = 0; i < lenses.Count; i++)
                {
                    LensTower lens = lenses[i];
                    if (lens == null) continue;

                    float lensMultiplier = lens.GetDamageMultiplier(data.enemyHit.health, data);
                    if (lensMultiplier != 1) lens.OnEnemyDamagedByLens(data.enemyHit);
                    multiplier += (lensMultiplier - 1f);
                }
            }
        }
        
        if (data != null && data.enemyHit != null && data.enemyHit.HasStatusEffect(Enemy.StatusEffect.Exposed))
        {
            multiplier += exposeMultiplerGlobal;
        }
        if (data != null && data.enemyHit != null && data.enemyHit.health != null && data.enemyHit.health.IsBurning && TagManager.instance != null)
        {
            multiplier += TagManager.instance.GetOrangeTagBurningEnemyDamageMultiplierBonus();
        }
        if (data != null && RM.i.Active(RM.ID.slowDamageBuff) && data.enemyHit != null)
        {
            multiplier += data.enemyHit.GetSlowPercentage() / 2;
        }
        if (data != null && RM.i.Active(RM.ID.entropyArtifact) && data.enemyHit != null)
        {
            multiplier += data.enemyHit.GetStatusEffectCount() * 0.1f;
        }
        if (RM.i != null)
        {
            CM.ColorType resolvedType = data != null ? GetDamageType(data) : GetConfiguredDamageType();
            multiplier += RM.i.GetDamageTypeDamageMultiplierBonus(resolvedType);
        }
        if (RM.i != null && RM.i.Active(RM.ID.zeal))
        {
            multiplier += GetZealStacks() * RM.zealDamagePerStack;
        }
        if (RM.i != null && RM.i.Active(RM.ID.loneWolfArtifact)
            && TowerManager.instance != null
                && TowerManager.instance.GetCurrentPlacedTowers() == 1
            && CurrentState == State.Placed)
        {
            multiplier += 1f;
        }
        if (RM.i != null && RM.i.Active(RM.ID.InventoryBuff)
            && TowerManager.instance != null
            && TowerManager.instance.GetInventoryTowerCount() == 0)
        {
            multiplier += 0.1f;
        }
        if (RM.i != null && RM.i.Active(RM.ID.currencyDamage)
            && CurrencyManager.instance != null
            && CurrencyManager.instance.GetCurrency() > 100)
        {
            multiplier += 0.2f;
        }
        if (RM.i != null && RM.i.Active(RM.ID.wolfPack) && rangeManager != null)
        {
            int towersInRange = rangeManager.GetAllActiveTowersInRange().Count;
            multiplier += towersInRange * RM.wolfPackDamagePerTower;
        }
        if (RM.i != null && RM.i.Active(RM.ID.explosionTradeoff) && data != null && data.isAOE)
        {
            multiplier += 1f;
        }

        if (UpgradeActive(UID.IncreaseDamageToStunOrSlowedTarget) && data != null && data.enemyHit != null)
        {
            Movement movement = data.enemyHit.GetMovement();
            bool atMaxSlow = movement != null && movement.AtMaxSlow();
            bool isStunned = data.enemyHit.IsStunned();
            if (atMaxSlow || isStunned)
            {
                multiplier += UpgradeData.IncreaseDamageToStunOrSlowedTargetPercent / 100f;
            }
        }

        // Apply the buff multiplier snapshot from AddBuff (does not affect DOT ticks).
        multiplier += _pendingBuffMultiplier;

        return multiplier;
    }

    public void SetBaseDamage(float d)
    {
        baseDamage = d;
    }

    public void AddBaseDamage(float amount)
    {
        baseDamage += amount;
    }

    public int RollCritCount()
    {
        float critChance = Mathf.Max(0f, GetCriticalChance());
        int critCount = 0;

        while (critChance >= 1f)
        {
            critCount++;
            critChance -= 1f;
        }

        if (Random.value < critChance)
        {
            critCount++;
        }

        return critCount;
    }

    public virtual void OnHitEnemy(Enemy enemy)
    {

    }

    public virtual void OnConsumeMark(Enemy enemy)
    {
        if (enemy == null) return;

        if (HitIndicatorObjectPool.instance != null)
            HitIndicatorObjectPool.instance.IndicateProjectileHit(enemy.transform.position, enemy.GetMark());

        if (UpgradeActive(UID.ExposeOnConsumeMark))
        {
            enemy.Expose(UpgradeData.ExposeOnConsumeMarkDurationSeconds);
        }
    }

    protected virtual void OnCriticalHit(CustomDamageData data)
    {
        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.AddCritLanded(GetDamageType(data));
        }

        if (RM.i.Active(RM.ID.criticalSlow) && data != null && data.enemyHit != null && data.enemyHit.GetMovement().GetMaxSlow() - data.enemyHit.GetSlowPercentage() < .05f) 
        {
            data.enemyHit.RemoveSlow();
            RM.i.IndicateRelic(RM.ID.criticalSlow);
            AOEHelper.instance.AOEAttackHelper(this, data.enemyHit.transform.position, GetAOESize(1), false, data);
        }
    }

    public float GetDamage(CustomDamageData data = null, bool rollCrit=true)
    {
        if (data != null && rollCrit)
        {
            data.critCount = RollCritCount();
            data.crit = data.critCount > 0;
            TriggerCriticalHitEffects(data);
        }
        else if (data != null && !rollCrit && data.crit)
        {
            TriggerCriticalHitEffects(data);
        }

        float startingDamage = baseDamage;
        if (UpgradeActive(UID.FlameThrowerDoubleBaseDamageSlowTradeoff))
        {
            startingDamage *= 2f;
        }
        if (data != null)
        {
            startingDamage *= data.baseDamageRatio;
        }

        float finalBaseDamage = GetDamageModified(startingDamage, data);
        float finalMultiplier = GetDamageMultiplier(1, data);

        if (data != null)
        {
            data.SetFinalDamageBreakdown(finalBaseDamage, finalMultiplier);
        }

        float damage = finalMultiplier * finalBaseDamage;

        return damage;
        //return GetDamageMultiplier(GetDamageModified(baseDamage, data), data);
    }
    protected virtual float GetRangeModified(float range, CustomDamageData data=null)
    {
        return range;
    }

    protected virtual float GetRangeMultiplier(float multiplier, CustomDamageData data = null)
    {
        return multiplier;
    }

    public float GetBaseAOESize() {
        return baseAOESize;
    }
    
    public float GetAOESize(float baseSize)
    {
        // Apply RedTag AOE bonus
        if (TagManager.instance != null)
        {
            int redTagLevel = TagManager.instance.GetTagLevel(Tag.Red);
            
            // Level 3 (5+ red towers): +AOE size for ALL towers
            if (redTagLevel == 3)
            {
                baseSize += TagManager.instance.GetRedTagAOESizeBonus();
            }
            // Level 2 (3+ red towers): +AOE size for red towers only
            else if (redTagLevel == 2 && tags.Contains(Tag.Red))
            {
                baseSize += TagManager.instance.GetRedTagAOESizeBonus();
            }
            // Level 1 (1+ red towers): +AOE size for red towers only
            else if (redTagLevel == 1 && tags.Contains(Tag.Red))
            {
                baseSize += TagManager.instance.GetRedTagAOESizeBonus();
            }
        }

        if (RM.i != null && RM.i.Active(RM.ID.explosionTradeoff))
        {
            baseSize *= 0.5f;
        }

        return baseSize;
    }

    public void SetBaseAOESize(float size)
    {
        baseAOESize = size;
    }

    public float GetRange(CustomDamageData data=null)
    {
        return GetRangeMultiplier(1, data) * GetRangeModified(baseRange, data); 
    }

    public float GetBaseRange()
    {
        return baseRange;
    }

    public void SetBaseRange(float r)
    {
        baseRange = r;
        if (rangeManager != null) rangeManager.SetRange(GetRange());
    }

    public void AddToolTipTag(CM.ColorType colorType)
    {
        if (!toolTipTags.Contains(colorType)) toolTipTags.Add(colorType);
    }

    public IReadOnlyList<CM.ColorType> GetToolTipTags()
    {
        return toolTipTags.AsReadOnly();
    }
    public void SetState(State newState)
    {
        state = newState;
    }

    public void SetCraftingRequiredTowers(List<ID> required)
    {
        if (craftingRequiredTowers == null) craftingRequiredTowers = new List<ID>();

        craftingRequiredTowers.Clear();
        if (required == null || required.Count == 0) return;

        craftingRequiredTowers.AddRange(required);
    }

    public void ClearCraftingRequiredTowers()
    {
        if (craftingRequiredTowers == null) return;
        craftingRequiredTowers.Clear();
    }

    public void ResetTotalDamageDealt()
    {
        totalDamageDealt = 0f;
    }

    public void RecordDamageDealt(float amount)
    {
        if (amount <= 0f) return;
        totalDamageDealt += amount;
    }

    public int GetEnemiesKilled()
    {
        return enemiesKilled;
    }

    public void AddZealStack(int amount = 1)
    {
        if (amount <= 0) return;
        zealStacks = Mathf.Max(0, zealStacks + amount);
    }

    public int GetZealStacks()
    {
        return Mathf.Max(0, zealStacks);
    }

    public void RecordKill()
    {
        enemiesKilled++;

        if (RM.i != null && RM.i.Active(RM.ID.ChargeOnCrit))
        {
            Charge(RM.chargeOnCritKillChargeAmount);
        }

        if (UpgradeActive(UID.ChargeOnKill))
        {
            Charge(maxChargeAmount * (UpgradeData.ChargeOnKillPercent / 100f));
        }

        if (UpgradeActive(UID.IncreaseSellCostOnKill))
        {
            sellCostBonusFromKills += Mathf.Max(0, UpgradeData.IncreaseSellCostOnKillAmount);
        }
    }

    public Color GetColor(CustomDamageData data = null)
    {
        return CM.i.colorDictionary[GetDamageType(data)];
    }

    public bool HasStatusEffect(StatusEffect effect)
    {
        return currentStatusEffects.Contains(effect);
    }

    public bool IsStunned()
    {
        return HasStatusEffect(StatusEffect.Stunned);
    }

    public void EnableGhostMode()
    {
        SetGhostAlpha(0.5f);
    }

    public void DisableGhostMode()
    {
        SetGhostAlpha(1f);
    }
    
    public void Charge(float chargeAmountToAdd = -1f)
    {
        if (!canBeCharged) return;
        if (AOEObjectPool.instance != null) AOEObjectPool.instance.PlayPulse(transform.position, .3f, GetColor());

        float previousCharge = chargeAmount;
        bool wasCharged = IsCharged();
        float requestedCharge = chargeAmountToAdd >= 0f ? chargeAmountToAdd : baseCooldown * chargingCooldownMultiplierGlobal;
        float chargeMultiplier = UpgradeActive(UID.IncreaseChargeAmount) ? UpgradeData.IncreaseChargeAmountMultiplier : 1f;
        float addedCharge = Mathf.Max(0f, requestedCharge * chargeMultiplier);
        chargeAmount = Mathf.Min(chargeAmount + addedCharge, maxChargeAmount);

        // Apply newly added charge to the current in-progress cooldown immediately.
        float appliedCharge = Mathf.Max(0f, chargeAmount - previousCharge);
        if (id != ID.Lens && appliedCharge > 0f && cooldownTimer > 0f)
        {
            float minAllowedCooldown = GetMinCooldown();
            float reducibleNow = Mathf.Max(0f, cooldownTimer - minAllowedCooldown);
            float immediateReduction = Mathf.Min(appliedCharge, reducibleNow);
            if (immediateReduction > 0f)
            {
                cooldownTimer = Mathf.Max(minAllowedCooldown, cooldownTimer - immediateReduction);
                chargeAmount -= immediateReduction;
            }
        }

        bool isChargedNow = IsCharged();

        OnChargeApplied(previousCharge, chargeAmount, wasCharged, isChargedNow);
    }

    protected virtual void OnChargeApplied(float previousCharge, float currentCharge, bool wasCharged, bool isChargedNow)
    {
    }

    public void AddBuff(float buffAmountToAdd = 1f)
    {
        if (!canBeBuff) return;
        if (AOEObjectPool.instance != null) AOEObjectPool.instance.PlayPulse(transform.position, .3f, GetColor());

        float previousBuff = buffAmount;
        float addedBuff = Mathf.Max(0f, buffAmountToAdd);
        buffAmount = Mathf.Min(buffAmount + addedBuff, maxBuffAmount);

        OnBuffApplied(previousBuff, buffAmount);
    }

    protected virtual void OnBuffApplied(float previousBuff, float currentBuff)
    {
    }

    public bool IsAtMaxBuff() => buffAmount >= maxBuffAmount - 0.0001f;
    public bool IsBuffed() => buffAmount > 0f;
    public float GetBuffAmount() => buffAmount;
    public bool UsedBuffForLastAttack() => usedBuffForLastAttack;

    public virtual void Awake()
    {
        AutoSetupTowerReferences();

        vfx = GetComponent<TowerVfxManager>();
        juice = GetComponent<Juice>();
        rangeManager = GetComponent<RangeManager>();
        _grid = FindFirstObjectByType<GridManager>();
        towerTool = GetComponentInChildren<TowerTool>(true);
        if (towerTool == null)
        {
            Debug.LogError("Tower missing target indicator");
        }
        
        effects = GetComponents<Effect>().ToList();
        if (xpBar == null) xpBar = GetComponentInChildren<XPBar>(includeInactive: true);
    }

    private void AutoSetupTowerReferences()
    {
        Transform body = bulletSpawnTransform != null ? bulletSpawnTransform : transform.Find("TowerBody");
        if (body == null) return;

        bulletSpawnTransform = body;

        SpriteRenderer towerBodySprite = body.GetComponent<SpriteRenderer>();

        Juice localJuice = GetComponent<Juice>();
        if (localJuice != null)
        {
            localJuice.juiceBody = body;
        }

        TowerInteractable interactable = GetComponent<TowerInteractable>();
        if (interactable != null)
        {
            if (interactable.outlines == null)
            {
                interactable.outlines = new List<SpriteRenderer>(1);
            }

            interactable.outlines.Clear();

            Transform outlineTransform = body.Find("Outline");
            if (outlineTransform != null)
            {
                SpriteRenderer outlineSprite = outlineTransform.GetComponent<SpriteRenderer>();
                if (outlineSprite != null)
                {
                    if (towerBodySprite != null)
                    {
                        outlineSprite.sprite = towerBodySprite.sprite;
                    }

                    interactable.outlines.Add(outlineSprite);
                }
            }
        }
    }

    public virtual void Start()
    {
        // Always start with no stored charge or buff regardless of serialized/runtime carryover.
        chargeAmount = 0f;
        buffAmount = 0f;

        rangeManager = GetComponentInChildren<RangeManager>();
        rangeManager.SetRange(GetRange());

        var towerInteractable = GetComponent<TowerInteractable>();
        Transform chargeMeterTransform = towerInteractable != null ? towerInteractable.ChargeMeterTransform : null;
        if (chargeMeterTransform != null)
        {
            chargeMeterImage = chargeMeterTransform.GetComponent<Image>();
        }
        Transform buffMeterTransform = towerInteractable != null ? towerInteractable.BuffMeterTransform : null;
        if (buffMeterTransform != null)
        {
            buffMeterImage = buffMeterTransform.GetComponent<Image>();
        }

        if (upgradeVisualizers == null) upgradeVisualizers = new List<SpriteRenderer>(4);
        upgradeVisualizers.Clear();

        Transform uvRoot = transform.Find("UpgradeVisualizers");
        if (uvRoot != null)
        {
            var srs = uvRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (srs != null && srs.Length > 0)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i] == null) continue;
                    if (srs[i].transform == uvRoot) continue;
                    upgradeVisualizers.Add(srs[i]);
                }
            }
        }

        if (upgradeVisualizers != null)
        {
            for (int i = 0; i < upgradeVisualizers.Count; i++)
            {
                if (upgradeVisualizers[i] != null) upgradeVisualizers[i].enabled = false;
            }
        }

        if (bulletSpawnTransform != null)
        {
            SetUpgradeLevel(GetUpgradeLevel());
        }

        RefreshXPBar();

        SRC src = GetComponent<SRC>();
        if (src != null)
        {
            src.Indicate();
        }
    }

    public void EnsureTargetIndicatorInValidPosition()
    {
        if (towerTool == null) return;
        // No longer constrain manual target indicator to be within range - allow placement anywhere
    }

    public void OnManualTargetModeSelected()
    {
        if (towerTool == null) return;
        var gm = GridManager.instance;

        // Allow placing the manual targeting tool anywhere (no position constraints)
        Vector3 chosen = towerTool.transform.position;

        // If we have a range manager and valid collider, try placing within range
        if (gm != null && rangeManager != null && rangeManager._collider != null)
        {
            Vector3 randomPoint = gm.GetRandomPointInsideMazeWithinRadius(
                rangeManager.transform.position, rangeManager._collider.radius);
            chosen = randomPoint;
        }

        towerTool.transform.position = chosen;
        _wasManualMode = true;
    }

    public virtual void OnTargetIndicatorDropped()
    {
    }

    public virtual void OnProjectileReturned(Projectile projectile)
    {
    }

    public virtual void Update() {
        if (chargeMeterImage != null)
        {   
            if (CM.i != null)
            {
                chargeMeterImage.color = CM.i.ColorTypeToColor(CM.ColorType.White);
            }
            float fillAmount = Mathf.Clamp01(GetChargeAmount() / maxChargeAmount);
            chargeMeterImage.fillAmount = fillAmount;
        }
        if (buffMeterImage != null)
        {
            if (CM.i != null)
            {
                buffMeterImage.color = CM.i.ColorTypeToColor(CM.ColorType.Red);
            }
            float fillAmount = Mathf.Clamp01(GetBuffAmount() / maxBuffAmount);
            buffMeterImage.fillAmount = fillAmount;
        }
    }

    private void FixedUpdate()
    {
        UpdateStatusEffects();
        SyncStunVfx();
        bool isManualMode = GetRangeManager().GetTargettingMode() == RangeManager.TargettingMode.Manual &&
            state == State.Placed && towerTool != null;
        if (isManualMode)
        {
            EnsureTargetIndicatorInValidPosition();
            towerTool.gameObject.SetActive(true);
            if (!_wasManualMode)
            {
                OnManualTargetModeSelected();
            }
        }
        else
        {
            EnsureTargetIndicatorInValidPosition();
            towerTool.gameObject.SetActive(false);
            _wasManualMode = false;
        }

        if (!IsAttachedToValidCell() || IsBeingMovedByPlayer()) return;

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
            return;
        }

        if (rangeManager == null)
        {
            Debug.LogWarning("Tower has no RangeManager reference; cannot acquire targets.");
            return;
        }

        if (targettedEnemy == null || !rangeManager.IsEnemyValidTarget(targettedEnemy))
        {
            targettedEnemy = rangeManager.GetTargettedEnemy();
        }
        if (targettedEnemy == null && !attackWithoutTarget) return;

        // If onlyAttackWhenWaveIsActive is enabled and no enemy is targeted, check if wave is active
        if (targettedEnemy == null && onlyAttackWhenWaveIsActive && WaveManager.instance != null)
        {
            if (!WaveManager.instance.IsWaveActive()) return;
        }

        float cooldown = GetCooldown();
        Attack();
        if (RollDoubleAttack())
        {
            Invoke(nameof(Attack), Mathf.Min(cooldown / 2, doubleAttackDelay));
        }

        cooldownTimer = cooldown;

        if (!attackWithoutTarget && juice != null && targettedEnemy != null)
        {
            juice.AddBounce(10);
            Vector3 away = transform.position - targettedEnemy.transform.position;
            away.z = 0f;
            juice.AddForce(away, 1f);
        }
    }

    private void OnDestroy()
    {
        // Skip teardown callbacks when the owning scene is unloading.
        if (!gameObject.scene.isLoaded) return;

        if (state != State.Shop)
        {
            if (TowerManager.instance != null) TowerManager.instance.OnTowerDestroyed(this);
            if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
        }
    }

    public virtual void Attack() { }

    public void ApplyEffects(Enemy enemy, Projectile projectile = null, HashSet<Effect> appliedEffects = null)
    {
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (appliedEffects != null && appliedEffects.Contains(effect)) continue;
                effect.ApplyEffect(enemy, projectile);
            }
        }

        OnHitEnemy(enemy);
    }

    /// <summary>
    /// Returns a list of currently active non-chain effects on this tower.
    /// ChainEffects use this to trigger secondary effects.
    /// 
    /// This method queries the current effects list dynamically, so it reflects:
    /// - Effects added at runtime via RegisterEffectIfMissing()
    /// - Effects disabled via active = false (they're still in the list but won't apply)
    /// - Effects destroyed/removed (null checks prevent errors)
    /// </summary>
    public List<Effect> GetNonChainEffects()
    {
        if (effects == null)
        {
            return new List<Effect>();
        }

        return effects.Where(effect => effect != null && !(effect is ChainEffect)).ToList();
    }

    public virtual void SetTargettedEnemy()
    {
        rangeManager.ForceRetarget();
        targettedEnemy = rangeManager != null ? rangeManager.GetTargettedEnemy() : null;
    }

    public void SetUpgradeLevel(int level)
    {
        int currentLevel = GetUpgradeLevel();

        if (level < currentLevel)
        {
            Debug.LogWarning($"Cannot downgrade tower from upgrade level {currentLevel} to {level}");
            return;
        }

        for (int i = currentLevel + 1; i <= level; i++)
        {
            if (i == 0)
            {
                continue;
            }
            int vizIndex = i - 1;
            if (upgradeVisualizers != null && vizIndex >= 0 && vizIndex < upgradeVisualizers.Count && upgradeVisualizers[vizIndex] != null)
            {
                upgradeVisualizers[vizIndex].enabled = true;
            }

            OnUpgrade(i);
        }

        RefreshXPBar();
    }

    public void ApplyUpgrade(UID uid)
    {
        int currentLevel = GetUpgradeLevel();
        if (currentLevel >= GetNumberOfLevels()) return;

        int newLevel = currentLevel + 1;
        int vizIndex = newLevel - 1;
        if (upgradeVisualizers != null && vizIndex >= 0 && vizIndex < upgradeVisualizers.Count && upgradeVisualizers[vizIndex] != null)
        {
            upgradeVisualizers[vizIndex].enabled = true;
        }

        ActivateUpgrade(uid);
        RefreshXPBar();
    }

    public bool UpgradeActive(UID uid)
    {
        return activeUpgrades.Contains(uid);
    }

    private void RegisterEffectIfMissing(Effect effect)
    {
        if (effect == null) return;

        if (effects == null)
        {
            effects = new List<Effect>();
        }

        if (!effects.Contains(effect))
        {
            effects.Add(effect);
        }
    }

    private T EnsureActiveEffect<T>() where T : Effect
    {
        T effect = GetComponent<T>();
        if (effect == null)
        {
            effect = gameObject.AddComponent<T>();
        }

        effect.active = true;
        RegisterEffectIfMissing(effect);
        return effect;
    }

    public virtual void ActivateUpgrade(UID uid)
    {
        if (activeUpgrades.Contains(uid)) return;
        activeUpgrades.Add(uid);

        if (uid == UID.AddAOEIncreaseCost)
        {
            EnsureActiveEffect<AOEEffect>();
        }

        if (uid == UID.IncreaseLensSize)
        {
            if (this is LensTower lensTower)
            {
                lensTower.IncreaseLensSizePercent(UpgradeData.IncreaseLensSizePercent);
            }
        }

        if (uid == UID.AddRedTag)
        {
            if (!tags.Contains(Tag.Red))
            {
                tags.Add(Tag.Red);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddBlueTag)
        {
            if (!tags.Contains(Tag.Blue))
            {
                tags.Add(Tag.Blue);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddGreenTag)
        {
            if (!tags.Contains(Tag.Green))
            {
                tags.Add(Tag.Green);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddOrangeTag)
        {
            if (!tags.Contains(Tag.Orange))
            {
                tags.Add(Tag.Orange);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddPurpleTag)
        {
            if (!tags.Contains(Tag.Purple))
            {
                tags.Add(Tag.Purple);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddWhiteTag)
        {
            if (!tags.Contains(Tag.White))
            {
                tags.Add(Tag.White);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddBlackTag)
        {
            if (!tags.Contains(Tag.Black))
            {
                tags.Add(Tag.Black);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddYellowTag)
        {
            if (!tags.Contains(Tag.Yellow))
            {
                tags.Add(Tag.Yellow);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddCyanTag)
        {
            if (!tags.Contains(Tag.Cyan))
            {
                tags.Add(Tag.Cyan);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.IncreaseBaseDamage)
        {
            AddBaseDamage(UpgradeData.IncreaseBaseDamageAmount);
        }

        if (uid == UID.BaseDamageLightningChainTradeoff)
        {
            AddBaseDamage(UpgradeData.BaseDamageLightningChainTradeoffBaseDamageAmount);
            AddLightningChainCount(-UpgradeData.BaseDamageLightningChainTradeoffLightningChainDecrease);
        }
 
        // Handle generic mark-on-hit upgrades
        if (uid == UID.ApplyRedMarkOnHit || uid == UID.ApplyBlueMarkOnHit || 
            uid == UID.ApplyGreenMarkOnHit || uid == UID.ApplyYellowMarkOnHit || 
            uid == UID.ApplyPurpleMarkOnHit || uid == UID.ApplyOrangeMarkOnHit || 
            uid == UID.ApplyWhiteMarkOnHit || uid == UID.ApplyCyanMarkOnHit)
        {
            AddMarkOnHitEffect markEffect = GetComponent<AddMarkOnHitEffect>();
            if (markEffect == null)
            {
                markEffect = gameObject.AddComponent<AddMarkOnHitEffect>();
            }
            markEffect.active = true;
            RegisterEffectIfMissing(markEffect);
        }

        if (uid == UID.ExposeOnHit)
        {
            ExposeOnHitEffect exposeEffect = GetComponent<ExposeOnHitEffect>();
            if (exposeEffect == null)
            {
                exposeEffect = gameObject.AddComponent<ExposeOnHitEffect>();
            }
            exposeEffect.active = true;
            RegisterEffectIfMissing(exposeEffect);
        }

        if (uid == UID.AddBurnOnHitEffect)
        {
            BurnOnHitEffect burnEffect = EnsureActiveEffect<BurnOnHitEffect>();
            burnEffect.burnStacks = Mathf.Max(1, burnEffect.burnStacks);
        }

        if (uid == UID.LaserBurn)
        {
            BurnOnHitEffect burnEffect = EnsureActiveEffect<BurnOnHitEffect>();
            burnEffect.burnStacks = Mathf.Max(1, burnEffect.burnStacks);

            SetDamageTypeFlags(DamageTypeFlags.Orange);

            if (!tags.Contains(Tag.Orange))
            {
                tags.Add(Tag.Orange);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }

            var src = GetComponent<SRC>();
            if (src != null)
            {
                src.ApplyColorToAll(CM.ColorType.Orange);
                src.Indicate();
            }
        }

        if (uid == UID.AddPoisonOnHitEffect)
        {
            PoisonOnHitEffect poisonEffect = EnsureActiveEffect<PoisonOnHitEffect>();
            poisonEffect.poisonStacks = Mathf.Max(1, poisonEffect.poisonStacks);
        }

        if (uid == UID.AddShockOnHitEffect)
        {
            ShockOnHitEffect shockEffect = EnsureActiveEffect<ShockOnHitEffect>();
            shockEffect.shockStacks = Mathf.Max(1, shockEffect.shockStacks);
        }

        if (uid == UID.LightningLaserConversion)
        {
            ShockOnHitEffect shockEffect = EnsureActiveEffect<ShockOnHitEffect>();
            shockEffect.shockStacks = Mathf.Max(1, shockEffect.shockStacks);

            SetDamageTypeFlags(DamageTypeFlags.Cyan);

            if (!tags.Contains(Tag.Cyan))
            {
                tags.Add(Tag.Cyan);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }

            var src = GetComponent<SRC>();
            if (src != null)
            {
                src.ApplyColorToAll(CM.ColorType.Cyan);
                src.Indicate();
            }
        }

        if (uid == UID.SunLaserConversion)
        {
            ProjectileExplosionOnHitEffect projectileExplosion = GetComponent<ProjectileExplosionOnHitEffect>();
            if (projectileExplosion != null)
            {
                projectileExplosion.active = false;
            }

            LaserExplosionOnHitEffect laserExplosion = GetComponent<LaserExplosionOnHitEffect>();
            if (laserExplosion == null)
            {
                laserExplosion = gameObject.AddComponent<LaserExplosionOnHitEffect>();
            }
            laserExplosion.active = true;
            RegisterEffectIfMissing(laserExplosion);

            if (!tags.Contains(Tag.Purple))
            {
                tags.Add(Tag.Purple);
                if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
            }
        }

        if (uid == UID.AddStunOnHitEffect)
        {
            StunEffect stunEffect = EnsureActiveEffect<StunEffect>();
            stunEffect.effectProbability = Mathf.Clamp01(UpgradeData.AddStunOnHitChancePercent / 100f);
            stunEffect.duration = Mathf.Max(stunEffect.duration, UpgradeData.AddStunOnHitDurationSeconds);
        }

        if (OnHitParticleEffect.instance != null)
        {
            OnHitParticleEffect.instance.OnHitVfx(transform.position, GetColor());
        }
    }

    private int GetCumulativeUpgradeCostBelowLevel(int level)
    {
        var possibleUpgrades = UpgradeData.GetUpgradesForTower(id);
        if (possibleUpgrades == null) return 0;
        int max = Mathf.Clamp(level, 0, possibleUpgrades.Count);
        int total = 0;
        for (int i = 0; i < max; i++)
        {
            var ul = possibleUpgrades[i];
            if (ul == null || ul.upgrades == null || ul.upgrades.Count == 0)
            {
                total += 1;
                continue;
            }
            total += ul.GetCost(0);
        }
        return total;
    }

    public int GetNextUpgradeCost()
    {
        int currentLevel = GetUpgradeLevel();
        var possibleUpgrades = UpgradeData.GetUpgradesForTower(id);
        if (possibleUpgrades == null || currentLevel < 0 || currentLevel >= possibleUpgrades.Count)
            return 1;
        var ul = possibleUpgrades[currentLevel];
        if (ul == null || ul.upgrades == null || ul.upgrades.Count == 0)
            return 1;
        return ul.GetCost(0);
    }

    private void TryApplyXpUpgrades()
    {
        while (GetUpgradeLevel() < GetNumberOfLevels())
        {
            int currentLevel = GetUpgradeLevel();
            int nextCost = GetNextUpgradeCost();
            if (nextCost == int.MaxValue) break;

            int spentBelowLevel = GetCumulativeUpgradeCostBelowLevel(currentLevel);
            int availableTowardsNext = xpCount - spentBelowLevel;

            if (availableTowardsNext < nextCost) break;
            SetUpgradeLevel(currentLevel + 1);
        }
    }

    private void RefreshXPBar()
    {
        if (xpBar == null) return;

        int currentLevel = GetUpgradeLevel();
        if (currentLevel >= GetNumberOfLevels())
        {
            xpBar.SetPercentageImmediate(1f);
            return;
        }

        int spentBelowLevel = GetCumulativeUpgradeCostBelowLevel(currentLevel);
        int nextCost = GetNextUpgradeCost();
        if (nextCost <= 0 || nextCost == int.MaxValue)
        {
            xpBar.SetPercentageImmediate(0f);
            return;
        }

        float progress = (xpCount - spentBelowLevel) / (float)nextCost;
        xpBar.SetPercentage(Mathf.Clamp01(progress));
    }

    protected virtual void OnUpgrade(int level)
    {
        var possibleUpgrades = UpgradeData.GetUpgradesForTower(id);
        int levelIndex = level - 1;
        if (possibleUpgrades != null && levelIndex >= 0 && levelIndex < possibleUpgrades.Count)
        {
            var upgradesAtLevel = possibleUpgrades[levelIndex];
            if (upgradesAtLevel != null && upgradesAtLevel.upgrades != null && upgradesAtLevel.upgrades.Count > 0)
            {
                ActivateUpgrade(upgradesAtLevel.upgrades[0]);
            }
        }
    }

    public float GetRemainingCooldown()
    {
        return Mathf.Max(0f, cooldownTimer);
    }

    public bool IsOnlyAttackWhenWaveActiveEnabled()
    {
        return onlyAttackWhenWaveIsActive;
    }

    public bool ShouldOnlyShowRangeWhenHoldingTowerTool()
    {
        return onlyShowRangeWhenHoldingTowerTool;
    }

    public bool IsManualTargetIndicatorBeingMoved()
    {
        return towerTool != null && towerTool.IsClicked();
    }

    public void SetOnlyAttackWhenWaveActive(bool enabled)
    {
        onlyAttackWhenWaveIsActive = enabled;   
    }

    public void FlatReduceCooldown(float amount)
    {
        cooldownTimer = Mathf.Max(0, cooldownTimer - amount);
    }

    public void Stun(float duration) {
        if (duration <= 0f) return;

        FlatIncreaseCooldown(duration);
        currentStatusEffects.Add(StatusEffect.Stunned);
        stunnedUntil = Mathf.Max(stunnedUntil, Time.fixedTime + duration);
    }

    public void FlatIncreaseCooldown(float amount) {
        cooldownTimer = cooldownTimer + amount;
    }

    private void UpdateStatusEffects()
    {
        if (currentStatusEffects.Contains(StatusEffect.Stunned) && Time.fixedTime >= stunnedUntil)
        {
            currentStatusEffects.Remove(StatusEffect.Stunned);
            stunnedUntil = -1f;
        }
    }

    private void SyncStunVfx()
    {
        if (vfx == null) vfx = GetComponent<TowerVfxManager>();

        if (IsStunned())
        {
            vfx.stunVisualizer.transform.localScale = Vector3.Lerp(Vector3.one * .7f, vfx.stunVisualizer.transform.localScale, 10 * Time.deltaTime);
            // vfx.stunVisualizer.SetActive(true);
            vfx.stunVisualizer.transform.rotation = Quaternion.Euler(0, 0, vfx.stunVisualizer.transform.eulerAngles.z + 100 * Time.deltaTime);
        }
        else {
            vfx.stunVisualizer.transform.localScale = Vector3.Lerp(vfx.stunVisualizer.transform.localScale, Vector3.zero, 10 * Time.deltaTime);
            // vfx.stunVisualizer.SetActive(false);
        }
    }

    public virtual void OnClick() 
    {
        // Stun(10);
        // Debug.Log("tower clicked");
        //AddXP(1);
        GetComponent<SRC>().Indicate();
    }

    public void Indicate(Color color)
    {
        var pool = AOEObjectPool.instance;
        if (pool == null) return;

        const float diameter = 0.5f;
        pool.Indicate(transform.position, diameter, color);
    }

    public bool IsAttachedToValidCell()
    {
        if (_grid == null) _grid = FindFirstObjectByType<GridManager>();
        if (_grid == null) return false;
        if (!_grid.TryWorldToCell(transform.position, out var idx)) return false;
        return _grid.TryGetTowerAtCell(idx, out var t) && t == this;
    }

    protected List<Enemy> GetEnemiesInRangeSnapshot()
    {
        return rangeManager.GetAllEnemiesInRange();
    }

    public virtual void OnEnterRange(Enemy enemy)
    {
        OnEnemyEnterRange(enemy);
    }

    public virtual void OnEnemyEnterRange(Enemy enemy)
    {
        if (enemy == null || enemy.health == null) return;
        if (!UpgradeActive(UID.DealMaxHealthPercentDamageOnEnterRange)) return;
        if (!maxHealthPercentDamageAppliedOnEnterRange.Add(enemy)) return;

        float damage = enemy.health.GetMaxHealth() * (UpgradeData.DealMaxHealthPercentDamageOnEnterRangePercent / 100f);
        if (damage <= 0f) return;

        var damageData = new CustomDamageData
        {
            enemyHit = enemy,
            damageType = GetDamageType()
        };
        damageData.SetFinalDamageBreakdown(damage, 1f);

        enemy.health.TakeDamage(damage, this, damageData.damageType, damageData);
    }
    
    public virtual void OnEnemyExitRange(Enemy enemy)
    {
        
    }

    private void SetGhostAlpha(float alpha)
    {
        var src = GetComponent<SRC>();
        if (src != null)
        {
            src.SetSpriteRendererAlpha(alpha);
            return;
        }

        // Fallback if no SRC component
        alpha = Mathf.Clamp01(alpha);
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var spriteRenderer in renderers)
        {
            if (spriteRenderer == null) continue;
            var c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }
    }
    private bool IsBeingMovedByPlayer()
    {
        var pic = PIC.instance;
        if (pic == null) return false;
        if (pic.currentState != PIC.PICState.PlacingTower) return false;

        s_heldTowerField ??= typeof(PIC).GetField("_heldTower", BindingFlags.NonPublic | BindingFlags.Instance);
        if (s_heldTowerField == null) return false;

        var held = s_heldTowerField.GetValue(pic) as TowerInteractable;
        return held != null && held.GetTower() == this;
    }

    public int GetLightningChainCount()
    {
        int baseCount = Mathf.Max(1, lightningChainCount);
        if (UpgradeActive(UID.IncreaseLightningChain))
        {
            baseCount += UpgradeData.IncreaseLightningChainAmount;
        }
        if (RM.i != null && RM.i.Active(RM.ID.lightningChain))
        {
            baseCount += 1;
        }

        return baseCount;
    }

    protected void AddLightningChainCount(int amount)
    {
        lightningChainCount = Mathf.Max(1, lightningChainCount + amount);
    }
}
