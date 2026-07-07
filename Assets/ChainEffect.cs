using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ChainEffect is a type of effect that can trigger other effects.
/// Only non-chain effects can be triggered by chain effects.
/// 
/// This system works dynamically with runtime effect additions/removals:
/// - When non-chain effects are added, ChainEffects automatically include them
/// - When effects are disabled (active = false), they won't be triggered
/// - When effects are removed, they're automatically excluded from the chain
/// </summary>
public abstract class ChainEffect : Effect
{
    /// <summary>
    /// Gets the list of currently active non-chain effects from the tower.
    /// This query is performed each time to ensure the list reflects runtime changes.
    /// </summary>
    protected List<Effect> GetNonChainEffectsFromTower()
    {
        if (tower == null)
        {
            return new List<Effect>();
        }

        return tower.GetNonChainEffects();
    }
}
