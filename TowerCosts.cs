using System.Collections.Generic;
using UnityEngine;

public class TowerCosts : MonoBehaviour
{
    public static readonly Dictionary<Tower.ID, int> towerCosts = new Dictionary<Tower.ID, int>
    {
        // Tier 1
        { Tower.ID.GreenTower,                   1 },
        { Tower.ID.RedTower,                     1 },
        { Tower.ID.BlueTower,                    1 },

        // Tier 2
        { Tower.ID.BuffTower,        3 },
        { Tower.ID.PurpleLaser,             3 },
        { Tower.ID.OrangeFlamethrower,      3 },
        { Tower.ID.A,3 },
        { Tower.ID.BombTower,     3 },
        { Tower.ID.IceTower,             3 },
        { Tower.ID.Shotgun,            3 },
        { Tower.ID.Sniper,                  3 },
        { Tower.ID.Gatling,                 3 },
    };

    public static int GetCost(Tower.ID id, int fallbackCost = 1)
    {
        if (towerCosts.TryGetValue(id, out int cost))
        {
            return Mathf.Max(1, cost);
        }

        return Mathf.Max(1, fallbackCost);
    }
}
