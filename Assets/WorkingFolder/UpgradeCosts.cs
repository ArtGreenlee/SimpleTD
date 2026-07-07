using System.Collections.Generic;
using UnityEngine;

public class UpgradeCosts : MonoBehaviour
{
    public static float upgradeCostGlobalMultiplier = 2;
    private static readonly Dictionary<UpgradeData.UID, int> upgradeCosts = new Dictionary<UpgradeData.UID, int>
    {
        // Green Projectile
        { UpgradeData.UID.IncreaseCritChance,       1 },
        { UpgradeData.UID.GoldOnConsumeMark,    2 },
        { UpgradeData.UID.IncreasePierce,          3 },

        // Blue Projectile
        { UpgradeData.UID.IncreaseSlow,        1 }, 
        { UpgradeData.UID.ExposeOnHit,           2 },
        { UpgradeData.UID.ExposeOnConsumeMark,   2 },
        { UpgradeData.UID.ProjectileBounce,               2 },
        { UpgradeData.UID.ManualTargetMover,             2 },

        // Red AOE
        { UpgradeData.UID.AOESizeIncrease,                1 },
        { UpgradeData.UID.SingleTargetAOEDamageIncrease,       2 },
        { UpgradeData.UID.AOEExposeHighestHealth,         3 },
        { UpgradeData.UID.RedTagDamageIncrease,           3 },

        // Blue AOE Slow
        { UpgradeData.UID.AOESingleTargetSlowBoost,       1 },
        { UpgradeData.UID.EnemiesInRangeSlowDecayDisable, 2 },
        { UpgradeData.UID.MaxSlowIncreaseOnHit,           3 },

        // Yellow AOE Projectile
        { UpgradeData.UID.ApplyBlueMarkOnCrit,            1 },
        { UpgradeData.UID.SetProjectileHoming,                         3 },

        // Sniper
        { UpgradeData.UID.LongRangeDamageBonus,          1 },
        { UpgradeData.UID.ShortRangeDamageBonus,         1 },

        // Buff / Charge Tower
        { UpgradeData.UID.HomingOnCharge,                     2 },

        // Laser
        { UpgradeData.UID.MultiHitDamageIncrease,         1 },
        { UpgradeData.UID.LaserBurn,                      2 },
        { UpgradeData.UID.AddStunOnHitEffect,             3 },
        { UpgradeData.UID.LaserBonusBounce,               2 },
        { UpgradeData.UID.LightningLaserConversion,       2 },

        // Flamethrower
        { UpgradeData.UID.FlameThrowerMaxBurnDamage,      2 },
        { UpgradeData.UID.FlameThrowerExtraBurn,          3 },

        // Shotgun (Particle)
        { UpgradeData.UID.ShotgunNarrowSpread,            1 },
        { UpgradeData.UID.ShotgunKillDamage,              2 },
        { UpgradeData.UID.ShotgunChargedDoubleEmit,       3 },
        { UpgradeData.UID.ShotgunDoubleShots,             2 },

        // Shotgun (Projectile variant)
        { UpgradeData.UID.ReduceArc,               1 },

        // Magic
        { UpgradeData.UID.MagicExposedExtraShot,          1 },

        // Agent
        { UpgradeData.UID.AgentExplodeOnDeath,            2 },
        { UpgradeData.UID.SunLaserConversion,             2 },

        // Generic
        { UpgradeData.UID.CritDamageBoost,                2 },
        { UpgradeData.UID.IncreaseBaseDamage,             2 },
        { UpgradeData.UID.BlueTagDamageIncrease,          2 },
        { UpgradeData.UID.GreenTagDamageIncrease,         2 },
        { UpgradeData.UID.YellowTagDamageIncrease,        2 },
        { UpgradeData.UID.PurpleTagDamageIncrease,        2 },
        { UpgradeData.UID.OrangeTagDamageIncrease,        2 },
        { UpgradeData.UID.WhiteTagDamageIncrease,         2 },
        { UpgradeData.UID.BlackTagDamageIncrease,         2 },
        { UpgradeData.UID.ConsumeMarkAlwaysCrit,          2 },
        { UpgradeData.UID.ApplyRedMarkOnHit,              2 },
        { UpgradeData.UID.ApplyBlueMarkOnHit,             2 },
        { UpgradeData.UID.ApplyGreenMarkOnHit,            2 },
        { UpgradeData.UID.ApplyYellowMarkOnHit,           2 },
        { UpgradeData.UID.ApplyPurpleMarkOnHit,           2 },
        { UpgradeData.UID.ApplyOrangeMarkOnHit,           2 },
        { UpgradeData.UID.ApplyWhiteMarkOnHit,            2 },
        { UpgradeData.UID.AddBurnOnHitEffect,             2 },
        { UpgradeData.UID.AddPoisonOnHitEffect,           2 },
        { UpgradeData.UID.AddShockOnHitEffect,            2 },
        { UpgradeData.UID.ConvertBurningToBaseDamageOnCrit, 3 },
        { UpgradeData.UID.ConvertShockedToBaseDamageOnCrit, 3 },
        { UpgradeData.UID.ConvertPoisonedToBaseDamageOnCrit, 3 },
        { UpgradeData.UID.IncreaseSellCostOnKill,         2 },
    };

    public static int GetCost(UpgradeData.UID uid)
    {
        return Mathf.RoundToInt((upgradeCosts.TryGetValue(uid, out int cost) ? Mathf.Max(1, cost) : 1) * upgradeCostGlobalMultiplier);
    }
}
