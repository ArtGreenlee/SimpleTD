using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;

public class TowerInteractable : Interactable
{
    [Header("References")]
    [SerializeField] private RangeManager rangeManager;
    [SerializeField] private Transform chargeMeterTransform;
    [SerializeField] private Transform buffMeterTransform;
    private Tower tower;
    public bool pickupable = true;

    /// <summary>
    /// Static toggle to enable/disable coloring tower outline sprites by rarity.
    /// </summary>
    public static bool useRarityOutlineColoring = true;

    public Tower GetTower() => tower;
    public Transform ChargeMeterTransform => chargeMeterTransform;
    public Transform BuffMeterTransform => buffMeterTransform;
    public GameObject rotateIndicator;
    public GameObject unseenIndicator;
    /// <summary>
    /// Maps a Tower.Rarity to its corresponding ColorManager ColorType.
    /// </summary>
    public static CM.ColorType GetRarityColorType(Tower.Rarity rarity)
    {
        return rarity switch
        {
            Tower.Rarity.Legendary => CM.ColorType.RarityTier3,
            Tower.Rarity.Rare      => CM.ColorType.RarityTier2,
            _                      => CM.ColorType.RarityTier1,
        };
    }

    /// <summary>
    /// Applies rarity color to all outline sprites if useRarityOutlineColoring is enabled.
    /// </summary>
    private void ApplyRarityOutlineColor()
    {
        if (!useRarityOutlineColoring || tower == null || CM.i == null) return;

        Tower.Rarity rarity = TowerManager.GetTowerRarity(tower.id);
        CM.ColorType rarityColor = GetRarityColorType(rarity);
        Color rarityColorValue = CM.i.ColorTypeToColor(rarityColor);

        foreach (var outline in outlines)
        {
            if (outline == null) continue;

            var c = outline.color;
            c.r = rarityColorValue.r;
            c.g = rarityColorValue.g;
            c.b = rarityColorValue.b;
            outline.color = c;
        }
    }

    public string GetDescription()
    {
        if (tower == null) return string.Empty;
        return TowerDescriptionUtility.GetTowerDescription(tower);
    }

    public override string GetCursorToolTipText()
    {
        if (tower == null || CM.i == null) return GetName();

        Tower.Rarity rarity = TowerManager.GetTowerRarity(tower.id);
        CM.ColorType rarityColor = GetRarityColorType(rarity);
        string rarityLabel = rarity switch
        {
            Tower.Rarity.Legendary => "Legendary",
            Tower.Rarity.Rare      => "Rare",
            _                      => "Common",
        };
        return CM.i.RTC(rarityColor, rarityLabel);
    }

public static class TowerDescriptionUtility
{
    public static string GetTowerDescription(Tower tower)
    {
        if (tower == null) return string.Empty;

        string description = tower.GetDescription();
        if (string.IsNullOrEmpty(description))
        {
            description = GetBaseDescription(tower);
        }

        var effects = tower.GetComponents<Effect>();
        for (int i = 0; i < effects.Length; i++)
        {
            string effectDescription = GetEffectDescription(effects[i]);
            if (string.IsNullOrEmpty(effectDescription)) continue;

            if (!string.IsNullOrEmpty(description)) description += ", ";
            description += effectDescription;
        }

        return description;
    }

    public static List<string> GetTowerTooltipDescriptions(Tower tower)
    {
        var descriptions = new List<string>();
        var seen = new HashSet<string>();

        if (tower == null) return descriptions;

        AddMechanicTooltips(tower, descriptions, seen);
        AddUpgradeTooltips(tower, descriptions, seen);
        AddRelicTooltips(tower, descriptions, seen);
        AddTagTooltips(tower, descriptions, seen);

        return descriptions;
    }

    public static string GetEffectDescription(Effect effect)
    {
        if (effect == null) return string.Empty;
        if (!effect.active) return string.Empty;

        string chance = effect.effectProbability < 1f ? (effect.effectProbability * 100f).ToString("0") + "% chance to " : string.Empty;

        if (effect is BurnOnHitEffect burn)
        {
            return chance + burn.burnStacks + " " + CM.i.RTC(CM.ColorType.Orange, "Burning");
        }

        if (effect is SlowEffect slow)
        {
            return chance + CM.i.RTC(CM.ColorType.Blue, "Slow") + " enemy by " + CM.i.RTC(CM.ColorType.Green, (slow.GetSlowAmount() * 100f).ToString("0") + "%");
        }

        if (effect is ShockEffect shock)
        {
            return chance + shock.shockStacks + " " + CM.i.RTC(CM.ColorType.Cyan, "Shock");
        }

        if (effect is ExposeOnHitEffect expose)
        {
            return chance + CM.i.RTC(CM.GetExposeColor(), "Exposed") + " for " + expose.time.ToString("0.##") + "s";
        }

        if (effect is AddMarkOnHitEffect mark)
        {
            return chance + mark.GetMarkDescriptionText() + " Mark";
        }

        if (effect is StunEffect stun)
        {
            return chance + CM.i.RTC(CM.ColorType.Purple, "Stun") + " for " + stun.duration.ToString("0.##") + "s";
        }

        if (effect is PercentHealthOnHitEffect percentHealth)
        {
            string healthType = percentHealth.mode == PercentHealthOnHitEffect.Mode.Max ? "maximum" : "current";
            return chance + (percentHealth.percentHealthDamage * 100f).ToString("0.##") + "% of enemy " + healthType + " health as damage";
        }

        if (effect is AOEEffect)
        {
            return chance + CM.i.RTC(CM.ColorType.Red, "Explode") + " nearby enemies";
        }

        return string.Empty;
    }

    public static string GetBaseDescription(Tower tower)
    {
        if (tower == null) return string.Empty;

        switch (tower.id)
        {
            case Tower.ID.GreenTower:
                return "Shoots a projectile";
            case Tower.ID.RedTower:
                return CM.i.RTC(CM.ColorType.Red, "Explodes") + " in an area";
            case Tower.ID.BlueTower:
                return "Shoots a fast projectile";
            case Tower.ID.BuffTower:
                return "Buffs nearby towers in straight lines";
            case Tower.ID.PurpleLaser:
                return "Fires a laser through enemies";
            case Tower.ID.OrangeFlamethrower:
                return "Sprays flames in front of it";
            case Tower.ID.A:
                return "Shoots a projectile that bursts in an area";
            case Tower.ID.BombTower:
                return "Shoots a slow explosive projectile  ";
            case Tower.ID.IceTower:
                return "Damages all enemies in an area";
            case Tower.ID.Shotgun:
                return "Fires a spread of projectiles";
            case Tower.ID.Sniper:
                return "Fires a powerful long-range shot";
            case Tower.ID.Gatling:
                return "Rapidly fires projectiles";
            case Tower.ID.Missile:
                return "Fires multiple homing projectiles";
            case Tower.ID.Mine:
                return "Deploys damaging fields";
            case Tower.ID.FireField:
                return "Projects a field that adds fire damage to all projectiles that pass through it";
            case Tower.ID.BlackHole:
                return "Creates a singularity that damages enemies";
            case Tower.ID.Mortar:
                return "Launches explosive shots";
            case Tower.ID.Lightning:
                return "Chains lightning through nearby enemies";
            case Tower.ID.GoldProjectile:
                return "High damage projectiles that costs " + CM.i.RTC(CM.ColorType.Gold, "$" + ProjectileTower.goldTowerProjectileCost.ToString()) + " per shot";
            case Tower.ID.Agent:
                return "Spawns " + tower.GetComponent<AgentTower>()?.GetMaxNumAgents().ToString() + " agents that attack enemies";
            case Tower.ID.Bank:
            {
                BankTower bankTower = tower.GetComponent<BankTower>();
                string bonusDropped = bankTower != null ? bankTower.BonusCurrencyGenerated().ToString() : "0";
                if (CM.i != null)
                {
                    bonusDropped = CM.i.RTC(CM.ColorType.Gold, bonusDropped);
                }

                return "Enemies that die within range drop " + CM.i.RTC(CM.ColorType.Green, "+" + bankTower?.enemyValueIncrease.ToString()) + CM.i.RTC(CM.ColorType.Gold, " Gold") + " for the player\n"
                    + " bonus " + CM.i.RTC(CM.ColorType.Gold, "Gold") + " dropped: " + bonusDropped;
            }
            case Tower.ID.Sun:
            {
                ProjectileExplosionOnHitEffect projectileExplosion = tower.GetComponent<ProjectileExplosionOnHitEffect>();
                LaserExplosionOnHitEffect laserExplosion = tower.GetComponent<LaserExplosionOnHitEffect>();

                bool laserMode = laserExplosion != null && laserExplosion.active;
                if (laserMode)
                {
                    int radialLaserCount = laserExplosion.GetCurrentRadialLaserCount();
                    return "Explodes on hit into " + radialLaserCount + " radial lasers";
                }

                int radialProjectileCount = projectileExplosion != null ? Mathf.Max(1, projectileExplosion.radialProjectileCount) : 0;
                return "Explodes on hit into " + radialProjectileCount + " radial projectiles";
            }
            case Tower.ID.Lens:
                return tower.GetComponent<LensTower>()?.GetLensDescription() ?? string.Empty;
            default:
                return string.Empty;
        }
    }

    private static void AddMechanicTooltips(Tower tower, List<string> descriptions, HashSet<string> seen)
    {
        var toolTipTags = tower.GetToolTipTags();
        for (int i = 0; i < toolTipTags.Count; i++)
        {
            AddUnique(descriptions, seen, CM.i != null ? CM.i.ColorToTooltip(toolTipTags[i]) : toolTipTags[i].ToString());
        }
    }

    private static void AddUpgradeTooltips(Tower tower, List<string> descriptions, HashSet<string> seen)
    {
        var activeUpgrades = tower.GetActiveUpgrades();
        for (int i = 0; i < activeUpgrades.Count; i++)
        {
            string description = tower.GetUpgradeDescription(activeUpgrades[i]);
            if (string.IsNullOrEmpty(description)) continue;
            AddUnique(descriptions, seen, "Upgrade " + (i + 1) + ": " + description);
        }
    }

    private static void AddRelicTooltips(Tower tower, List<string> descriptions, HashSet<string> seen)
    {
        if (RM.i == null) return;

        var relicIds = (RM.ID[])Enum.GetValues(typeof(RM.ID));
        for (int i = 0; i < relicIds.Length; i++)
        {
            RM.ID relicId = relicIds[i];
            if (!RM.i.Active(relicId)) continue;

            string relicDescription = GetRelevantRelicDescription(tower, relicId);
            if (string.IsNullOrEmpty(relicDescription)) continue;

            AddUnique(descriptions, seen, RM.i.GetName(relicId) + ": " + relicDescription);
        }
    }

    private static void AddTagTooltips(Tower tower, List<string> descriptions, HashSet<string> seen)
    {
        if (TagManager.instance == null) return;

        AddUnique(descriptions, seen, GetTagTooltip(Tower.Tag.Red, GetRedTagDescription(tower)));
        AddUnique(descriptions, seen, GetTagTooltip(Tower.Tag.Green, GetGreenTagDescription(tower)));
        AddUnique(descriptions, seen, GetTagTooltip(Tower.Tag.Purple, GetPurpleTagDescription(tower)));
        AddUnique(descriptions, seen, GetTagTooltip(Tower.Tag.Yellow, GetYellowTagDescription(tower)));
    }

    private static string GetTagTooltip(Tower.Tag tag, string description)
    {
        return string.IsNullOrEmpty(description) ? null : tag + " Tag: " + description;
    }

    private static string GetRelevantRelicDescription(Tower tower, RM.ID relicId)
    {
        if (RM.i != null && RM.i.TryGetDamageTypeRelicColor(relicId, out CM.ColorType damageType))
        {
            return tower.CanDealDamageType(damageType) ? RM.i.GetDescription(relicId) : null;
        }

        switch (relicId)
        {
            case RM.ID.fireTickRate:
                return TowerAppliesBurn(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.slowDamageBuff:
                return TowerDealsDamage(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.slowTickRate:
                return TowerAppliesSlow(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.criticalSlow:
                return TowerCanCrit(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.criticalCharge:
                return tower.canBeCharged ? RM.i.GetDescription(relicId) : null;
            case RM.ID.criticalDamageBuff:
                return TowerCanCrit(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.slowOverload:
                return TowerAppliesSlow(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.fireExplosion:
                return TowerAppliesBurn(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.entropyArtifact:
                return TowerDealsDamage(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.loneWolfArtifact:
                return IsLoneWolfActiveForTower(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.InventoryBuff:
                return TowerManager.instance != null && TowerManager.instance.GetInventoryTowerCount() == 0 ? RM.i.GetDescription(relicId) : null;
            case RM.ID.LaserBounce:
                return tower is LaserTower ? RM.i.GetDescription(relicId) : null;
            case RM.ID.currencyDamage:
                return CurrencyManager.instance != null && CurrencyManager.instance.GetCurrency() > 100 ? RM.i.GetDescription(relicId) : null;
            case RM.ID.inspirationRelic:
                return tower.canBeCharged ? RM.i.GetDescription(relicId) : null;
            case RM.ID.agentMaxIncrease:
                return tower is AgentTower ? RM.i.GetDescription(relicId) : null;
            case RM.ID.criticalPierce:
                return tower is ProjectileTower ? RM.i.GetDescription(relicId) : null;
            case RM.ID.overkill:
                return TowerDealsDamage(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.laserStun:
                return tower is LaserTower ? RM.i.GetDescription(relicId) : null;
            case RM.ID.wolfPack:
                return tower != null && tower.GetRangeManager() != null && tower.GetRangeManager().GetAllActiveTowersInRange().Count > 0 ? RM.i.GetDescription(relicId) : null;
            case RM.ID.explosionTradeoff:
                return TowerUsesAoe(tower) ? RM.i.GetDescription(relicId) : null;
            case RM.ID.speedyAgent:
                return tower is AgentTower ? RM.i.GetDescription(relicId) : null;
            case RM.ID.lightningChain:
                return tower.id == Tower.ID.Lightning ? RM.i.GetDescription(relicId) : null;
            case RM.ID.zeal:
                return tower != null && tower.GetZealStacks() > 0 ? RM.i.GetDescription(relicId) + " Current bonus: " + CM.i.RTC(CM.ColorType.Green, "+" + (tower.GetZealStacks() * RM.zealDamagePerStack * 100f).ToString("0.#") + "%") : null;
            case RM.ID.alwaysCritOnFullyCharge:
                return tower.canBeCharged ? RM.i.GetDescription(relicId) : null;
            default:
                return null;
        }
    }

    private static string GetRedTagDescription(Tower tower)
    {
        if (TagManager.instance == null || !TowerUsesAoe(tower)) return null;

        int level = TagManager.instance.GetTagLevel(Tower.Tag.Red);
        if (level <= 0) return null;
        if (level < 3 && (tower.tags == null || !tower.tags.Contains(Tower.Tag.Red))) return null;

        return TagManager.instance.GetRedTagLevelDescription(level);
    }

    private static string GetGreenTagDescription(Tower tower)
    {
        if (TagManager.instance == null) return null;

        int level = TagManager.instance.GetTagLevel(Tower.Tag.Green);
        if (level <= 0) return null;
        if (level < 3 && (tower.tags == null || !tower.tags.Contains(Tower.Tag.Green))) return null;

        return TagManager.instance.GetGreenTagLevelDescriptions(level);
    }

    private static string GetPurpleTagDescription(Tower tower)
    {
        int level = TagManager.instance.GetTagLevel(Tower.Tag.Purple);
        if (level <= 0) return null;

        bool hasPurpleTag = tower.tags != null && tower.tags.Contains(Tower.Tag.Purple);
        if (level < 3)
        {
            return hasPurpleTag ? TagManager.instance.GetPurpleTagLevelDescription(level) : null;
        }

        return hasPurpleTag ? TagManager.instance.GetPurpleTagLevelDescription(level) : null;
    }

    private static string GetYellowTagDescription(Tower tower)
    {
        if (TagManager.instance == null || !(tower is AgentTower)) return null;

        int level = TagManager.instance.GetTagLevel(Tower.Tag.Yellow);
        if (level <= 0) return null;

        bool hasAnyRuntimeBonus = TagManager.instance.GetYellowTagAgentBaseDamageBonus() > 0f
            || TagManager.instance.GetYellowTagAgentMaxHealthMultiplier() > 1f
            || TagManager.instance.GetYellowTagMaxAgentsPerTowerBonus() > 0;

        if (!hasAnyRuntimeBonus) return null;

        return TagManager.instance.GetYellowTagLevelDescription(level);
    }

    private static bool TowerCanCrit(Tower tower)
    {
        return tower != null && tower.GetCriticalChance() > 0f;
    }

    private static bool TowerDealsDamage(Tower tower)
    {
        return tower != null && tower.GetDamage(rollCrit: false) > 0f;
    }

    private static bool TowerUsesAoe(Tower tower)
    {
        if (tower == null) return false;
        if (tower is AOETower) return true;
        if (tower.GetBaseAOESize() > 0f) return true;

        AOEEffect aoeEffect = tower.GetComponent<AOEEffect>();
        return aoeEffect != null && aoeEffect.active;
    }

    private static bool TowerAppliesBurn(Tower tower)
    {
        return HasActiveEffect<BurnOnHitEffect>(tower);
    }

    private static bool TowerAppliesSlow(Tower tower)
    {
        return HasActiveEffect<SlowEffect>(tower);
    }

    private static bool HasActiveEffect<T>(Tower tower) where T : Effect
    {
        if (tower == null) return false;

        T effect = tower.GetComponent<T>();
        return effect != null && effect.active;
    }

    private static bool IsLoneWolfActiveForTower(Tower tower)
    {
        return tower != null
            && TowerManager.instance != null
            && TowerManager.instance.GetCurrentPlacedTowers() == 1
            && tower.CurrentState == Tower.State.Placed;
    }

    private static void AddUnique(List<string> descriptions, HashSet<string> seen, string description)
    {
        if (string.IsNullOrEmpty(description)) return;
        if (!seen.Add(description)) return;
        descriptions.Add(description);
    }
}

    public void Update()
    {
        if (tower != null && PIC.instance.IsHoldingTower(tower) && tower.rotateable)
        {
            rotateIndicator.SetActive(true);
        }
        else
        {
            rotateIndicator.SetActive(false);
        }
    }

    public string GetName()
    {
        if (tower == null) return string.Empty;

        switch (tower.id)
        {
            case Tower.ID.GreenTower: return "Green";
            case Tower.ID.RedTower: return "Red";
            case Tower.ID.BlueTower: return "Blue";
            case Tower.ID.BuffTower: return "Charge";
            case Tower.ID.PurpleLaser: return "Laser";
            case Tower.ID.OrangeFlamethrower: return "Flamethrower";
            case Tower.ID.A: return "Bomb";
            case Tower.ID.BombTower: return "Bomb";
            case Tower.ID.IceTower: return "Ice";
            case Tower.ID.Shotgun: return "Shotgun";
            case Tower.ID.Sniper: return "Sniper";
            case Tower.ID.Gatling: return "Gatling";
            case Tower.ID.Missile: return "Missile";
            case Tower.ID.Mine: return "Mine";
            case Tower.ID.FireField: return "Fire Field";
            case Tower.ID.BlackHole: return "Black Hole";
            case Tower.ID.Mortar: return "Mortar";
            case Tower.ID.Lightning: return "Lightning";
            case Tower.ID.Agent: return "Agent";
            case Tower.ID.Bank: return "Bank";
            case Tower.ID.GoldProjectile: return "Gold";
            default: return HumanizeId(tower.id);
        }
    }

    public override void Awake()
    {
        base.Awake();

        if (rangeManager == null) rangeManager = GetComponent<RangeManager>();
        if (rangeManager == null) rangeManager = GetComponentInChildren<RangeManager>();
        if (tower == null) tower = GetComponent<Tower>();
        if (chargeMeterTransform == null) chargeMeterTransform = transform.Find("ChargeMeter");
        if (buffMeterTransform == null) buffMeterTransform = transform.Find("BuffMeter");

        ApplyRarityOutlineColor();
    }

    public override void InteractableOnMouseEnter()
    {
        bool shouldShowRange = true;
        if (tower != null && tower.ShouldOnlyShowRangeWhenHoldingTowerTool())
        {
           shouldShowRange = tower.IsManualTargetIndicatorBeingMoved();
        }

        if (rangeManager != null && shouldShowRange && !TowerInformationDisplay.instance.PlayerMouseHovering()) rangeManager.VisualizeRange();

        // Recipe display UX: hovering the recipe result should ping the ingredient towers
        // that will be consumed to craft it.
        IndicateCraftingIngredientTowers();

        base.InteractableOnMouseEnter();
    }

    public override void InteractableOnMouseExit()
    {
        if (rangeManager != null) rangeManager.HideRangeVisualization();
        base.InteractableOnMouseExit();
    }

    public void OnMouseEnter()
    {
        
        //Debug.Log("Mouse entered tower interactable");    
    }

    private static string HumanizeId(Tower.ID id)
    {
        string raw = id.ToString();
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length + 8);
        sb.Append(raw[0]);
        for (int i = 1; i < raw.Length; i++)
        {
            char current = raw[i];
            char previous = raw[i - 1];
            if (char.IsUpper(current) && !char.IsUpper(previous))
            {
                sb.Append(' ');
            }
            sb.Append(current);
        }
        return sb.ToString();
    }

    private void IndicateCraftingIngredientTowers()
    {
        if (tower == null) return;
        if (!tower.HasCraftingRequirements) return;

        var required = tower.CraftingRequiredTowers;
        if (required == null || required.Count == 0) return;
        if (TowerManager.instance == null) return;

        // Match pickup-combine behavior: prefer inventory towers, then placed towers,
        // and do not reuse the same tower instance for duplicate requirements.
        var inventory = new List<Tower>(TowerManager.instance.EnumerateTowersInState(Tower.State.Inventory));
        var placed = new List<Tower>(TowerManager.instance.EnumeratePlacedTowers());

        Color c = Color.white;
        if (CM.i != null)
        {
            c = CM.i.ColorTypeToColor(CM.ColorType.Yellow);
        }

        for (int i = 0; i < required.Count; i++)
        {
            var id = required[i];
            var ingredient = FindAndRemoveFirstById(inventory, id) ?? FindAndRemoveFirstById(placed, id);
            if (ingredient != null) ingredient.Indicate(c);
        }
    }

    private static Tower FindAndRemoveFirstById(List<Tower> towers, Tower.ID id)
    {
        if (towers == null) return null;
        for (int i = 0; i < towers.Count; i++)
        {
            var t = towers[i];
            if (t == null) continue;
            if (t.id != id) continue;
            towers.RemoveAt(i);
            return t;
        }
        return null;
    }
}
