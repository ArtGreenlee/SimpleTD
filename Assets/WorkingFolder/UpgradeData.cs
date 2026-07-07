using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UpgradeData : MonoBehaviour
{
    public static UpgradeData instance;

    public const float FlameThrowerSlowReductionPercent = 10f;
    public const float DealMaxHealthPercentDamageOnEnterRangePercent = 10f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Description constants for upgrade tooltips and UI.
    public const float CritChanceBoostPercent = 10f;
    public const float CritDamageBoostPercent = 20f;
    public const int PierceIncrease = 1;
    public const float IncreaseLensSizePercent = 25f;
    public const int AddAOEIncreaseCostProjectileCostIncrease = 1;
    public const int BonusProjectileCount = 1;
    public const float BonusProjectileArcDegrees = 10f;
    public const float IncreaseSlowAmount = 10f;
    public const int IncreaseBaseDamageAmount = 1;
    public const float ExposeOnConsumeMarkDurationSeconds = 10f;
    public const float AoeRangeIncrease = 1f;
    public const float AoeSingleTargetDamageMultiplier = 2f;
    public const float AoeSingleTargetDamagePercentMore = 100f;
    public const float AOESingleTargetSlowBoostAmount = 10f;
    public const float AoeMaxSlowBoostPercent = 1f;
    public const int MultiHitDamageIncreasePerEnemy = 1;
    public const int LaserBurnStacksIncrease = 1;
    public const int LaserBounceIncrease = 1;
    public const float AddStunOnHitChancePercent = 10f;
    public const float AddStunOnHitDurationSeconds = 1f;
    public const int ProjectileMarkCurrencyGain = 1;
    public const int AgentExplodeProjectileCount = 4;
    public const float AgentExplodeProjectileSpeed = 8f;
    public const float AgentExplodeProjectileDistance = 1f;
    public const float IncreaseDamageToStunOrSlowedTargetPercent = 25f;
    public const float AgentStealGoldChancePercent = 20f;
    public const int AgentStealGoldAmount = 2;
    public const float IncreaseLaserDamagePerBouncePercent = 15f;
    public const float IncreaseLaserDamagePerBounceCap = 60f;
    public const float BankDiscountPercent = 50f;
    public const float ProjectileSpeedToRangeSpeedReductionPercent = 50f;
    public const float ProjectileSpeedToRangeRangeIncreasePercent = 50f;
    public const float DecreaseCooldownPercent = 25f;
    public const int MassiveIncreaseBaseDamageAmount = 5;
    public const int AOEAddProjectilesCount = 6;
    public const float AOEAddProjectilesSpeed = 5;
    public const int IncreaseSellCostOnKillAmount = 1;
    public const float SpawnCurrencyOnChargeChancePercent = 25f;
    public const int IncreaseLightningChainAmount = 1;
    public const int IncreaseLightningTargetsAmount = 1;
    public const int MegaShockOnFullyChargedLightningStacks = 3;
    public const int BaseDamageLightningChainTradeoffBaseDamageAmount = 2;
    public const int BaseDamageLightningChainTradeoffLightningChainDecrease = 1;
    public const float IncreaseChargeAmountMultiplier = 1.5f;
    public const float ChargeOnKillPercent = 25f;
    public const float AgentChargeBuff_Bonus = 0.5f; // 50% bigger, more health, more damage
    public const int OnlyPlacedTowerDamageIncreaseAmount = 3;
    public const int ConditionalBonusProjectileAdditionalShots = 1;
    public const Tower.ID ConditionalBonusProjectileRequiredTowerId = Tower.ID.Gatling;
    public const int ConditionalBonusProjectileRequiredPlacedCount = 2;

    public enum UID
    {
        IncreaseCritChance,
        GoldOnConsumeMark,
        IncreasePierce,
        IncreaseLensSize,
        ChargeTowersInLens,
        PoisonFasterTickRateInLens,
        AddAOEIncreaseCost,
        BonusProjectile,
        IncreaseSlow,
        ExposeOnHit,
        ExposeOnConsumeMark,
        ProjectileBounce,
        AOESizeIncrease,
        SingleTargetAOEDamageIncrease,
        AOEExposeHighestHealth,
        RedTagDamageIncrease,
        AOESingleTargetSlowBoost,
        EnemiesInRangeSlowDecayDisable,
        MaxSlowIncreaseOnHit,
        ApplyBlueMarkOnCrit,
        SetProjectileHoming,
        LongRangeDamageBonus,
        ShortRangeDamageBonus,
        BuffChargedTowers,
        BuffCooldownReduction,
        BuffAOEExplosion,
        HomingOnCharge,
        MultiHitDamageIncrease,
        LaserBurn,
        AddStunOnHitEffect,
        LaserBonusBounce,
        FlameThrowerMaxBurnDamage,
        FlameThrowerExtraBurn,
        FlameThrowerDoubleBaseDamageSlowTradeoff,
        ShotgunNarrowSpread,
        ShotgunKillDamage,
        ShotgunChargedDoubleEmit,
        ShotgunDoubleShots,
        ReduceArc,
        MagicExposedExtraShot,
        AgentExplodeOnDeath,
        AgentLaserConversion,
        SunLaserConversion,
        ShockIfAllExposed,
        CritDamageBoost,
        IncreaseBaseDamage,
        BlueTagDamageIncrease,
        GreenTagDamageIncrease,
        YellowTagDamageIncrease,
        PurpleTagDamageIncrease,
        OrangeTagDamageIncrease,
        WhiteTagDamageIncrease,
        BlackTagDamageIncrease,
        AddRedTag,
        AddBlueTag,
        AddGreenTag,
        AddOrangeTag,
        AddPurpleTag,
        AddWhiteTag,
        AddBlackTag,
        AddYellowTag,
        AddCyanTag,
        ConsumeMarkAlwaysCrit,
        ApplyRedMarkOnHit,
        ApplyBlueMarkOnHit,
        ApplyGreenMarkOnHit,
        ApplyYellowMarkOnHit,
        ApplyPurpleMarkOnHit,
        ApplyOrangeMarkOnHit,
        ApplyWhiteMarkOnHit,
        ApplyCyanMarkOnHit,
        AddBurnOnHitEffect,
        AddPoisonOnHitEffect,
        AddShockOnHitEffect,
        ConvertBurningToBaseDamageOnCrit,
        ConvertShockedToBaseDamageOnCrit,
        ConvertPoisonedToBaseDamageOnCrit,
        IncreaseDamageToStunOrSlowedTarget,
        AgentStealGold,
        IncreaseLaserDamagePerBounce,
        LightningLaserConversion,
        BankDiscount,
        DoesNotClearMark,
        ProjectileSpeedToRange,
        ManualTargetMover,
        SmartManualTargetting,
        DecreaseCooldown,
        MassiveIncreaseBaseDamage,
        AOEAddProjectiles,
        IncreaseSellCostOnKill,
        SpawnCurrencyOnCharge,
        IncreaseChargeAmount,
        ChargeOnKill,
        IncreaseLightningChain,
        IncreaseLightningTargets,
        MegaShockOnFullyChargedLightning,
        BaseDamageLightningChainTradeoff,
        DealMaxHealthPercentDamageOnEnterRange,
        ChargeCanCrit,
        AgentChargeBuff,
        GatlingConditionExtraProjectile,
    }

    public class UpgradeLevel
    {
        public List<UID> upgrades = new List<UID>();
        public List<int> costOverrides = new List<int>();
    }

    private static Tower.UpgradeLevel UL(params UID[] upgrades)
    {
        return new Tower.UpgradeLevel { upgrades = new List<UID>(upgrades) };
    }

    private static Tower.UpgradeLevel UL((UID uid, int? costOverride)[] upgrades)
    {
        var level = new Tower.UpgradeLevel();
        if (upgrades == null || upgrades.Length == 0) return level;

        for (int i = 0; i < upgrades.Length; i++)
        {
            level.upgrades.Add(upgrades[i].uid);
            level.costOverrides.Add(upgrades[i].costOverride.GetValueOrDefault());
        }

        return level;
    }

    private static readonly Dictionary<Tower.ID, List<Tower.UpgradeLevel>> towerUpgrades = new Dictionary<Tower.ID, List<Tower.UpgradeLevel>>
    {
        { Tower.ID.GreenTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.AddPoisonOnHitEffect, UID.IncreaseCritChance),
                UL(UID.ApplyRedMarkOnHit, UID.IncreasePierce),
                UL(UID.GoldOnConsumeMark, UID.CritDamageBoost),
            }
        },
        { Tower.ID.BlueTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.ProjectileSpeedToRange, UID.IncreaseSlow),
                UL(UID.ApplyGreenMarkOnHit, UID.ConsumeMarkAlwaysCrit),
                UL(UID.ConsumeMarkAlwaysCrit, UID.MaxSlowIncreaseOnHit),
            }
        },
        { Tower.ID.RedTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.DecreaseCooldown, UID.IncreaseBaseDamage),
                UL(UID.ApplyBlueMarkOnHit, UID.RedTagDamageIncrease),
                UL(UID.ExposeOnConsumeMark, UID.SingleTargetAOEDamageIncrease),
            }
        },
        { Tower.ID.BuffTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.AgentChargeBuff, UID.DecreaseCooldown),
                UL(UID.IncreaseChargeAmount),
                UL(UID.ChargeCanCrit),
            }
        },
        { Tower.ID.Agent, new List<Tower.UpgradeLevel>
            {
                UL(UID.AgentChargeBuff, UID.IncreaseDamageToStunOrSlowedTarget),
                UL(UID.AgentStealGold, UID.AgentLaserConversion),
            }
        },
        { Tower.ID.PurpleLaser, new List<Tower.UpgradeLevel>
            {
                UL(UID.DecreaseCooldown, UID.LaserBonusBounce),
                UL(UID.LaserBurn, UID.LightningLaserConversion),
                UL(UID.ApplyRedMarkOnHit, UID.IncreaseLaserDamagePerBounce),
            }
        },
        { Tower.ID.Lightning, new List<Tower.UpgradeLevel>
            {
                UL(UID.BaseDamageLightningChainTradeoff, UID.AddStunOnHitEffect),
                UL(UID.MegaShockOnFullyChargedLightning, UID.IncreaseLightningTargets),
            }
        },
        { Tower.ID.OrangeFlamethrower, new List<Tower.UpgradeLevel>
            {
                UL(UID.FlameThrowerExtraBurn, UID.IncreaseBaseDamage),
                UL(UID.DecreaseCooldown, UID.FlameThrowerDoubleBaseDamageSlowTradeoff),
                UL(UID.OrangeTagDamageIncrease),
            }
        },
        { Tower.ID.GoldProjectile, new List<Tower.UpgradeLevel>
            {
                UL(UID.MassiveIncreaseBaseDamage, UID.IncreaseSellCostOnKill),
                UL(UID.AddAOEIncreaseCost, UID.BankDiscount),
            }
        },
        { Tower.ID.BombTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.ApplyPurpleMarkOnHit, UID.SetProjectileHoming),
            }
        },
        { Tower.ID.Lens, new List<Tower.UpgradeLevel>
            {
                UL(UID.DoesNotClearMark, UID.IncreaseLensSize),
                UL(UID.PoisonFasterTickRateInLens),
            }
        },
        { Tower.ID.Necromancer, new List<Tower.UpgradeLevel>
            {
                UL(UID.AgentExplodeOnDeath, UID.AddPoisonOnHitEffect),
            }
        },
        { Tower.ID.Sniper, new List<Tower.UpgradeLevel>
            {
                UL(UID.AddPoisonOnHitEffect, UID.IncreasePierce),
                UL(UID.ApplyCyanMarkOnHit, UID.LongRangeDamageBonus),
                UL(UID.ConvertShockedToBaseDamageOnCrit, UID.ConvertPoisonedToBaseDamageOnCrit),
            }
        },
        { Tower.ID.Gatling, new List<Tower.UpgradeLevel>
            {
                UL(UID.IncreaseBaseDamage, UID.GreenTagDamageIncrease),
                UL(UID.IncreaseCritChance, UID.DoesNotClearMark),
                UL(UID.GatlingConditionExtraProjectile, UID.MassiveIncreaseBaseDamage),
            }
        },
        { Tower.ID.Shotgun, new List<Tower.UpgradeLevel>
            {
                UL(UID.ShotgunDoubleShots, UID.ReduceArc),
                UL(UID.ShotgunKillDamage, UID.IncreaseBaseDamage),
                UL(UID.ShotgunChargedDoubleEmit),
            }
        },
        { Tower.ID.Missile, new List<Tower.UpgradeLevel>
            {
                UL(UID.ApplyCyanMarkOnHit, UID.MagicExposedExtraShot),
                UL(UID.ExposeOnHit, UID.BonusProjectile),
            }
        },
        { Tower.ID.Bank, new List<Tower.UpgradeLevel>
            {
                UL(UID.SpawnCurrencyOnCharge),
            }
        },
        { Tower.ID.Sun, new List<Tower.UpgradeLevel>
            {
                UL(UID.ApplyPurpleMarkOnHit),
                UL(UID.SunLaserConversion),
            }
        },
        { Tower.ID.Rainbow, new List<Tower.UpgradeLevel>
            {
                UL(UID.DecreaseCooldown, UID.IncreaseBaseDamage),
                UL(UID.GatlingConditionExtraProjectile, UID.BonusProjectile),
                UL(UID.MassiveIncreaseBaseDamage),
            }
        },
        { Tower.ID.IceTower, new List<Tower.UpgradeLevel>
            {
                UL(UID.IncreaseBaseDamage, UID.IncreaseSlow),
                UL(UID.MaxSlowIncreaseOnHit),
                UL(UID.EnemiesInRangeSlowDecayDisable),
            }
        },
        { Tower.ID.Mortar, new List<Tower.UpgradeLevel>
            {
                UL(UID.AOESizeIncrease, UID.DecreaseCooldown),
                UL(UID.ChargeOnKill, UID.AOEAddProjectiles),
                UL(UID.SmartManualTargetting),
            }
        },
    };

    private static readonly Dictionary<UID, int> hardcodedUpgradeCosts = new Dictionary<UID, int>
    {
        { UID.IncreaseCritChance, 1 },
        { UID.GoldOnConsumeMark, 2 },
        { UID.IncreasePierce, 3 },
        { UID.IncreaseLensSize, 2 },
        { UID.ChargeTowersInLens, 2 },
        { UID.PoisonFasterTickRateInLens, 2 },
        { UID.AddAOEIncreaseCost, 2 },
        { UID.IncreaseSlow, 1 },
        { UID.ExposeOnHit, 2 },
        { UID.ExposeOnConsumeMark, 2 },
        { UID.ProjectileBounce, 2 },
        { UID.ManualTargetMover, 2 },
        { UID.AOESizeIncrease, 1 },
        { UID.SingleTargetAOEDamageIncrease, 2 },
        { UID.AOEExposeHighestHealth, 3 },
        { UID.RedTagDamageIncrease, 3 },
        { UID.AOESingleTargetSlowBoost, 1 },
        { UID.EnemiesInRangeSlowDecayDisable, 2 },
        { UID.MaxSlowIncreaseOnHit, 3 },
        { UID.ApplyBlueMarkOnCrit, 1 },
        { UID.SetProjectileHoming, 3 },
        { UID.LongRangeDamageBonus, 1 },
        { UID.ShortRangeDamageBonus, 1 },
        { UID.HomingOnCharge, 2 },
        { UID.MultiHitDamageIncrease, 1 },
        { UID.LaserBurn, 2 },
        { UID.AddStunOnHitEffect, 3 },
        { UID.LaserBonusBounce, 2 },
        { UID.FlameThrowerMaxBurnDamage, 2 },
        { UID.FlameThrowerExtraBurn, 3 },
        { UID.FlameThrowerDoubleBaseDamageSlowTradeoff, 3 },
        { UID.ShotgunNarrowSpread, 1 },
        { UID.ShotgunKillDamage, 2 },
        { UID.ShotgunChargedDoubleEmit, 3 },
        { UID.ShotgunDoubleShots, 2 },
        { UID.ReduceArc, 1 },
        { UID.MagicExposedExtraShot, 1 },
        { UID.AgentExplodeOnDeath, 2 },
        { UID.AgentLaserConversion, 3 },
        { UID.CritDamageBoost, 2 },
        { UID.IncreaseBaseDamage, 2 },
        { UID.BlueTagDamageIncrease, 2 },
        { UID.GreenTagDamageIncrease, 2 },
        { UID.YellowTagDamageIncrease, 2 },
        { UID.PurpleTagDamageIncrease, 2 },
        { UID.OrangeTagDamageIncrease, 2 },
        { UID.WhiteTagDamageIncrease, 2 },
        { UID.BlackTagDamageIncrease, 2 },
        { UID.AddRedTag, 2 },
        { UID.AddBlueTag, 2 },
        { UID.AddGreenTag, 2 },
        { UID.AddOrangeTag, 2 },
        { UID.AddPurpleTag, 2 },
        { UID.AddWhiteTag, 2 },
        { UID.AddBlackTag, 2 },
        { UID.AddYellowTag, 2 },
        { UID.AddCyanTag, 2 },
        { UID.ConsumeMarkAlwaysCrit, 2 },
        { UID.ApplyRedMarkOnHit, 2 },
        { UID.ApplyBlueMarkOnHit, 2 },
        { UID.ApplyGreenMarkOnHit, 2 },
        { UID.ApplyYellowMarkOnHit, 2 },
        { UID.ApplyPurpleMarkOnHit, 2 },
        { UID.ApplyOrangeMarkOnHit, 2 },
        { UID.ApplyWhiteMarkOnHit, 2 },
        { UID.ApplyCyanMarkOnHit, 2 },
        { UID.AddBurnOnHitEffect, 2 },
        { UID.AddPoisonOnHitEffect, 2 },
        { UID.AddShockOnHitEffect, 2 },
        { UID.ConvertBurningToBaseDamageOnCrit, 3 },
        { UID.ConvertShockedToBaseDamageOnCrit, 3 },
        { UID.ConvertPoisonedToBaseDamageOnCrit, 3 },
        { UID.IncreaseSellCostOnKill, 2 },
        { UID.BankDiscount, 2 },
        { UID.DoesNotClearMark, 2 },
        { UID.ProjectileSpeedToRange, 2 },
        { UID.DecreaseCooldown, 2 },
        { UID.MassiveIncreaseBaseDamage, 2 },
        { UID.AOEAddProjectiles, 2 },
        { UID.SpawnCurrencyOnCharge, 2 },
        { UID.IncreaseChargeAmount, 2 },
        { UID.ChargeOnKill, 2 },
        { UID.IncreaseLightningChain, 2 },
        { UID.IncreaseLightningTargets, 2 },
        { UID.MegaShockOnFullyChargedLightning, 2 },
        { UID.BaseDamageLightningChainTradeoff, 2 },
        { UID.DealMaxHealthPercentDamageOnEnterRange, 3 },
        { UID.ChargeCanCrit, 2 },
        { UID.AgentChargeBuff, 3 },
        { UID.GatlingConditionExtraProjectile, 2 },
        { UID.SunLaserConversion, 2 },
    };

    public static List<Tower.UpgradeLevel> GetUpgradesForTower(Tower.ID id)
    {
        if (!towerUpgrades.TryGetValue(id, out var list) || list == null)
            return new List<Tower.UpgradeLevel>();

        var copy = new List<Tower.UpgradeLevel>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            var src = list[i];
            copy.Add(new Tower.UpgradeLevel
            {
              upgrades = src != null && src.upgrades != null ? new List<UID>(src.upgrades) : new List<UID>(),
                costOverrides = src != null && src.costOverrides != null ? new List<int>(src.costOverrides) : new List<int>()
            });
        }

        return copy;
    }

    public static int GetCost(UID uid)
    {
        int cost = hardcodedUpgradeCosts.TryGetValue(uid, out int c) ? Mathf.Max(1, c) : 1;
        return Mathf.RoundToInt(cost * 2f);
    }

    private static string Green(string text)
    {
        return CM.i != null ? CM.i.RTC(CM.ColorType.Green, text) : text;
    }

    private static string BaseDamage(string text)
    {
        return CM.i != null ? CM.i.RTC(CM.ColorType.Blue, text) : text;
    }

    private static string DamageMultiplier(float multiplier)
    {
        string text = "+" + multiplier.ToString("0.##") + "x";
        return CM.i != null ? CM.i.RTC(CM.ColorType.Red, text) : text;
    }

    private static string Color(CM.ColorType colorType, string text = null)
    {
        if (CM.i == null)
        {
            return text ?? colorType.ToString();
        }

        return text == null ? CM.i.RTC(colorType) : CM.i.RTC(colorType, text);
    }

    public static string GetUpgradeDescription(UID uid)
    {
        switch (uid)
        {
            case UID.AOESizeIncrease:
                return Green("+" + AoeRangeIncrease.ToString("0.#")) + " Range";
            case UID.SingleTargetAOEDamageIncrease:
                return DamageMultiplier(1f + (AoeSingleTargetDamagePercentMore / 100f)) + " damage when only one enemy is hit";
            case UID.AOEExposeHighestHealth:
                return "Applies " + Color(CM.GetExposeColor(), "Exposed") + " to highest health enemy hit";
            case UID.RedTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Red, "Red") + " tag";
            case UID.BlueTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Blue, "Blue") + " tag";
            case UID.GreenTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Green, "Green") + " tag";
            case UID.YellowTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Yellow, "Yellow") + " tag";
            case UID.PurpleTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Purple, "Purple") + " tag";
            case UID.OrangeTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Orange, "Orange") + " tag";
            case UID.WhiteTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.White, "White") + " tag";
            case UID.BlackTagDamageIncrease:
                return BaseDamage("+1") + " base damage per active " + Color(CM.ColorType.Black, "Black") + " tag";
            case UID.AddRedTag:
                return "Adds " + Color(CM.ColorType.Red, "Red") + " tag to this tower";
            case UID.AddBlueTag:
                return "Adds " + Color(CM.ColorType.Blue, "Blue") + " tag to this tower";
            case UID.AddGreenTag:
                return "Adds " + Color(CM.ColorType.Green, "Green") + " tag to this tower";
            case UID.AddOrangeTag:
                return "Adds " + Color(CM.ColorType.Orange, "Orange") + " tag to this tower";
            case UID.AddPurpleTag:
                return "Adds " + Color(CM.ColorType.Purple, "Purple") + " tag to this tower";
            case UID.AddWhiteTag:
                return "Adds " + Color(CM.ColorType.White, "White") + " tag to this tower";
            case UID.AddBlackTag:
                return "Adds " + Color(CM.ColorType.Black, "Black") + " tag to this tower";
            case UID.AddYellowTag:
                return "Adds " + Color(CM.ColorType.Yellow, "Yellow") + " tag to this tower";
            case UID.AddCyanTag:
                return "Adds " + Color(CM.ColorType.Cyan, "Cyan") + " tag to this tower";
            case UID.AOESingleTargetSlowBoost:
                return Green("+" + AOESingleTargetSlowBoostAmount.ToString("0.#") + "%") + " " + Color(CM.ColorType.Blue, "slow") + " on a single target";
            case UID.EnemiesInRangeSlowDecayDisable:
                return Color(CM.ColorType.Blue, "Slow") + " amount does not decay for enemies in range";
            case UID.MaxSlowIncreaseOnHit:
                return Green("+" + AoeMaxSlowBoostPercent.ToString("0.#") + "%") + " max " + Color(CM.ColorType.Blue, "slow") + " on hit";
            case UID.ShockIfAllExposed:
                return "If all enemies hit are " + Color(CM.GetExposeColor(), "Exposed") + " does something... TODO";
            case UID.MultiHitDamageIncrease:
                return "Deals " + BaseDamage("+" + MultiHitDamageIncreasePerEnemy) + " base damage per enemy hit";
            case UID.LaserBurn:
                return "Converts this laser to " + Color(CM.ColorType.Orange, "Orange") + ": applies " + Color(CM.ColorType.Orange, "Burn") + ", adds " + Color(CM.ColorType.Orange, "Orange") + " tag, and deals only " + Color(CM.ColorType.Orange, "Orange") + " damage";
            case UID.AddStunOnHitEffect:
                return AddStunOnHitChancePercent.ToString("0.#") + "% chance to " + Color(CM.ColorType.Purple, "Stun") + " for " + AddStunOnHitDurationSeconds.ToString("0.#") + "s on hit";
            case UID.LaserBonusBounce:
                return Green("+" + LaserBounceIncrease) + " laser bounce";
            case UID.SetProjectileHoming:
                return "Projectiles are " + Color(CM.ColorType.Purple, "Homing");
            case UID.IncreaseCritChance:
                return Green("+" + CritChanceBoostPercent.ToString("0.#") + "%") + " critical hit chance";
            case UID.GoldOnConsumeMark:
                return "Grants " + ProjectileMarkCurrencyGain + " currency on consuming a " + Color(CM.ColorType.Green) + " Mark";
            case UID.IncreasePierce:
                return Green("+" + PierceIncrease) + " pierce";
            case UID.IncreaseLensSize:
                return Green("+" + IncreaseLensSizePercent.ToString("0.#") + "%") + " lens size";
            case UID.ChargeTowersInLens:
                return "When the " + Color(CM.ColorType.White, "Lens") + " affects damage, " + Color(CM.ColorType.Purple, "Charge") + " all towers inside it";
            case UID.PoisonFasterTickRateInLens:
                return Color(CM.ColorType.Green, "Poison") + " ticks " + Green("5x faster") + " inside this lens";
            case UID.AddAOEIncreaseCost:
                return "Enables " + Color(CM.ColorType.Red, "AOE") + " on hit and increases projectile cost by " + Green("+" + AddAOEIncreaseCostProjectileCostIncrease);
            case UID.BonusProjectile:
                return Green("+" + BonusProjectileCount) + " projectile per attack and " + Green("+" + BonusProjectileArcDegrees.ToString("0.#") + "°") + " arc";
            case UID.IncreaseSlow:
                return Green("+" + IncreaseSlowAmount.ToString("0.#") + "%") + " slow";
            case UID.ProjectileBounce:
                return "Projectiles bounce to a nearby enemy if they pierce the target";
            case UID.LongRangeDamageBonus:
                return DamageMultiplier(2f) + " damage for enemies more than half range";
            case UID.ShortRangeDamageBonus:
                return DamageMultiplier(2f) + " damage for enemies less than half range";
            case UID.MagicExposedExtraShot:
                return "Hitting an " + Color(CM.GetExposeColor(), "Exposed") + " enemy fires a projectile in a random direction";
            case UID.ExposeOnHit:
                return "Applies " + Color(CM.GetExposeColor(), "Exposed") + " on hit";
            case UID.ExposeOnConsumeMark:
                return "Consuming a Mark applies " + Color(CM.GetExposeColor(), "Exposed") + " for " + Green(ExposeOnConsumeMarkDurationSeconds.ToString("0.#") + "s");
            case UID.BuffChargedTowers:
                return "Increases the number of towers that can be " + Color(CM.ColorType.Purple, "charged") + " simultaneously";
            case UID.BuffCooldownReduction:
                return "Reduces " + Color(CM.ColorType.White, "cooldown") + " of " + Color(CM.ColorType.Purple, "charged") + " towers";
            case UID.BuffAOEExplosion:
                return "Charged towers create an " + Color(CM.ColorType.Red, "AOE explosion") + " on their next attack";
            case UID.HomingOnCharge:
                return "Charging a projectile tower makes its next attack " + Color(CM.ColorType.Purple, "Homing");
            case UID.FlameThrowerMaxBurnDamage:
                return "Increases max " + Color(CM.ColorType.Orange, "Burn") + " base damage dealt per stack";
            case UID.FlameThrowerExtraBurn:
                return Green("+" + LaserBurnStacksIncrease) + " " + Color(CM.ColorType.Orange, "Burn") + " stacks applied";
            case UID.FlameThrowerDoubleBaseDamageSlowTradeoff:
                return BaseDamage("x2") + " base damage, but hitting a slowed enemy reduces its " + Color(CM.ColorType.Blue, "slow") + " by " + Green(FlameThrowerSlowReductionPercent.ToString("0.#") + "%");
            case UID.ShotgunNarrowSpread:
                return "Reduces projectile spread for " + Green("tighter") + " grouping";
            case UID.ShotgunKillDamage:
                return BaseDamage("Bonus base damage") + " for each enemy killed this wave";
            case UID.ShotgunChargedDoubleEmit:
                return "When " + Color(CM.ColorType.Purple, "charged") + ", fires " + Green("twice");
            case UID.ShotgunDoubleShots:
                return Green("Doubles") + " the number of projectiles fired";
            case UID.ReduceArc:
                return "Reduces projectile arc for";
            case UID.AgentExplodeOnDeath:
                return "Agents explode on death, firing " + AgentExplodeProjectileCount + " projectiles in a circle.";
            case UID.AgentLaserConversion:
                return "Converts this agent tower to " + Color(CM.ColorType.Purple, "Purple") + ": adds " + Color(CM.ColorType.Purple, "Purple") + " tag, spawned agents become " + Color(CM.ColorType.Purple, "Purple") + " and fire lasers, and this tower deals only " + Color(CM.ColorType.Purple, "Purple") + " damage";
            case UID.SunLaserConversion:
                return "Converts this sun tower to " + Color(CM.ColorType.Purple, "Purple") + ": switches explosion on hit from projectiles to radial lasers and adds " + Color(CM.ColorType.Purple, "Purple") + " tag";
            case UID.CritDamageBoost:
                return DamageMultiplier(1f + (CritDamageBoostPercent / 100f)) + " critical hit damage";
            case UID.IncreaseBaseDamage:
                return BaseDamage("+" + IncreaseBaseDamageAmount) + " base damage";
            case UID.ConsumeMarkAlwaysCrit:
                return "Consuming a Mark always " + Green("crits");
            case UID.ApplyRedMarkOnHit:
                return "Applies " + Color(CM.ColorType.Red) + " Mark on hit";
            case UID.ApplyBlueMarkOnHit:
                return "Applies " + Color(CM.ColorType.Blue) + " Mark on hit";
            case UID.ApplyGreenMarkOnHit:
                return "Applies " + Color(CM.ColorType.Green) + " Mark on hit";
            case UID.ApplyYellowMarkOnHit:
                return "Applies " + Color(CM.ColorType.Yellow) + " Mark on hit";
            case UID.ApplyPurpleMarkOnHit:
                return "Applies " + Color(CM.ColorType.Purple) + " Mark on hit";
            case UID.ApplyOrangeMarkOnHit:
                return "Applies " + Color(CM.ColorType.Orange) + " Mark on hit";
            case UID.ApplyWhiteMarkOnHit:
                return "Applies " + Color(CM.ColorType.White) + " Mark on hit";
            case UID.ApplyCyanMarkOnHit:
                return "Applies " + Color(CM.ColorType.Cyan) + " Mark on hit";
            case UID.AddBurnOnHitEffect:
                return "Applies " + Color(CM.ColorType.Orange, "Burn") + " on hit";
            case UID.AddPoisonOnHitEffect:
                return "Applies " + Color(CM.ColorType.Green, "Poison") + " on hit";
            case UID.AddShockOnHitEffect:
                return "Applies " + Color(CM.ColorType.Cyan, "Shock") + " on hit";
            case UID.ConvertBurningToBaseDamageOnCrit:
                return "On critical hit against a " + Color(CM.ColorType.Orange, "Burning") + " enemy, converts remaining burn damage to " + Color(CM.ColorType.Blue, "base damage") + " and clears " + Color(CM.ColorType.Orange, "Burning");
            case UID.ConvertShockedToBaseDamageOnCrit:
                return "On critical hit against a " + Color(CM.ColorType.Cyan, "Shocked") + " enemy, converts remaining shock damage to " + Color(CM.ColorType.Blue, "base damage") + " and clears " + Color(CM.ColorType.Cyan, "Shocked");
            case UID.ConvertPoisonedToBaseDamageOnCrit:
                return "On critical hit against a " + Color(CM.ColorType.Green, "Poisoned") + " enemy, converts remaining poison damage to " + Color(CM.ColorType.Blue, "base damage") + " and clears " + Color(CM.ColorType.Green, "Poisoned");
            case UID.IncreaseDamageToStunOrSlowedTarget:
                return DamageMultiplier(1f + (IncreaseDamageToStunOrSlowedTargetPercent / 100f)) + " damage to enemies at max " + Color(CM.ColorType.Blue, "Slow") + " or " + Color(CM.ColorType.Purple, "Stunned");
            case UID.AgentStealGold:
                return Green(AgentStealGoldChancePercent.ToString("0.#") + "%") + " chance for agents to steal " + Green(AgentStealGoldAmount.ToString()) + " gold from an enemy (once per enemy)";
            case UID.IncreaseLaserDamagePerBounce:
                return "Laser damage increases by " + DamageMultiplier(1f + (IncreaseLaserDamagePerBouncePercent / 100f)) + " per bounce, up to " + DamageMultiplier(1f + (IncreaseLaserDamagePerBounceCap / 100f));
            case UID.LightningLaserConversion:
                return "Converts this laser to " + Color(CM.ColorType.Cyan, "Cyan") + ": applies " + Color(CM.ColorType.Cyan, "Shock") + ", adds " + Color(CM.ColorType.Cyan, "Cyan") + " tag, and deals only " + Color(CM.ColorType.Cyan, "Cyan") + " damage";
            case UID.BankDiscount:
                return "While in range of a " + Color(CM.ColorType.Gold, "Bank") + ", projectile cost is reduced by " + Green(BankDiscountPercent.ToString("0.#") + "%");
            case UID.DoesNotClearMark:
                return "Consuming a Mark does " + Green("not") + " clear it from the enemy";
            case UID.ProjectileSpeedToRange:
                return Green("-" + ProjectileSpeedToRangeSpeedReductionPercent.ToString("0.#") + "%") + " projectile speed, " + Green("+" + ProjectileSpeedToRangeRangeIncreasePercent.ToString("0.#") + "%") + " range";
            case UID.ManualTargetMover:
                return "While in " + Color(CM.ColorType.Purple, "Manual") + " target mode, moves the target indicator to a random enemy in range before each shot";
            case UID.SmartManualTargetting:
                return "While in " + Color(CM.ColorType.Purple, "Manual") + " target mode, moves the target indicator to the position where its AOE would hit the most enemies";
            case UID.DecreaseCooldown:
                return Green("-" + DecreaseCooldownPercent.ToString("0.#") + "%") + " " + Color(CM.ColorType.White, "cooldown");
            case UID.MassiveIncreaseBaseDamage:
                return BaseDamage("+" + MassiveIncreaseBaseDamageAmount) + " base damage";
            case UID.AOEAddProjectiles:
                return "Fires " + Green(AOEAddProjectilesCount.ToString()) + " projectiles outward in a ring when the AOE triggers";
            case UID.IncreaseSellCostOnKill:
                return Green("+" + IncreaseSellCostOnKillAmount.ToString()) + " sell value per enemy kill";
            case UID.SpawnCurrencyOnCharge:
                return Green(SpawnCurrencyOnChargeChancePercent.ToString("0.#") + "%") + " chance to spawn a Tier 1 " + Color(CM.ColorType.Gold, "currency") + " when this tower becomes " + Color(CM.ColorType.Purple, "Charged");
            case UID.IncreaseChargeAmount:
                return Green("+" + IncreaseChargeAmountMultiplier.ToString("0.#") + "x") + " charge amount";
            case UID.ChargeOnKill:
                return "On kill, gain " + Green(ChargeOnKillPercent.ToString("0.#") + "%") + " charge";
            case UID.IncreaseLightningChain:
                return Green("+" + IncreaseLightningChainAmount) + " lightning chain";
            case UID.IncreaseLightningTargets:
                return Green("+" + IncreaseLightningTargetsAmount) + " lightning target";
            case UID.MegaShockOnFullyChargedLightning:
                return "If fully " + Color(CM.ColorType.Purple, "Charged") + ", lightning applies " + Green("+" + MegaShockOnFullyChargedLightningStacks) + " " + Color(CM.ColorType.Cyan, "Shock") + " stacks to each target hit";
            case UID.BaseDamageLightningChainTradeoff:
                return BaseDamage("+" + BaseDamageLightningChainTradeoffBaseDamageAmount) + " base damage and " + Green("-" + BaseDamageLightningChainTradeoffLightningChainDecrease) + " lightning chain";
            case UID.DealMaxHealthPercentDamageOnEnterRange:
                return "When an enemy enters this tower's range, deal " + BaseDamage(DealMaxHealthPercentDamageOnEnterRangePercent.ToString("0.#") + "% max health") + " damage once per enemy";
            case UID.ChargeCanCrit:
                return "This tower's " + Color(CM.ColorType.White, "charge amount") + " can be modified by critical hit logic at runtime";
            case UID.AgentChargeBuff:
                float bonusPct = AgentChargeBuff_Bonus * 100f;
                return "When spawned at max " + Color(CM.ColorType.White, "Charge") + ", agents are " + Green("+" + bonusPct.ToString("0.#") + "%") + " bigger and have " + Green("+" + bonusPct.ToString("0.#") + "%") + " health and " + DamageMultiplier(1f + (bonusPct / 100f)) + " damage";
            case UID.GatlingConditionExtraProjectile:
                return "Fires " + Green("+" + ConditionalBonusProjectileAdditionalShots) + " projectile if at least " + Green(ConditionalBonusProjectileRequiredPlacedCount.ToString()) + " " + ConditionalBonusProjectileRequiredTowerId + " towers are placed";

            default:
                return null;
        }
    }
}
