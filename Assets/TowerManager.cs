using NUnit.Framework;
using UnityEngine;
using System;
using System.Collections.Generic;
public class TowerManager : MonoBehaviour
{
    public static TowerManager instance;
    public event Action<Tower> TowerPurchasedFromShop;
    [SerializeField] private int maximumPlacedTowers = 5;
    [Header("Relic Visualization")]
    [SerializeField] private Color wolfPackPlacementLaserColor = Color.red;
    [SerializeField, Min(0.001f)] private float wolfPackPlacementLaserWidth = 0.08f;
    [SerializeField, Min(0.01f)] private float wolfPackPlacementLaserDuration = 0.35f;
    [SerializeField, UnityEngine.Range(0f, 1f)] private float wolfPackPlacementLaserAlpha = 0.9f;
    [SerializeField] private Color wolfPackFloatingTextColor = Color.red;
    [SerializeField, Min(0.05f)] private float wolfPackFloatingTextLifetime = 0.7f;
    [SerializeField] private Vector3 wolfPackFloatingTextOffset = new Vector3(0f, 0.7f, 0f);
    private const float WolfPackFloatingTextSize = 0.85f;
    private const float WolfPackInitialTextSizeMultiplier = 0.5f;
    private int currentPlacedTowers = 0;
    public List<GameObject> towerPrefabs;
    public Dictionary<Tower.ID, GameObject> towerPrefabDictionary;
    private Dictionary<Tower.State, HashSet<Tower>> stateToTowers = new Dictionary<Tower.State, HashSet<Tower>>();

    private static readonly Dictionary<Tower.ID, Tower.Rarity> towerRarityDictionary = new Dictionary<Tower.ID, Tower.Rarity>
    {
        // --- Base towers (Tier 1) → Common ---
        { Tower.ID.GreenTower,       Tower.Rarity.Common },
        { Tower.ID.RedTower,         Tower.Rarity.Common },
        { Tower.ID.BlueTower,        Tower.Rarity.Common },
        { Tower.ID.BuffTower,        Tower.Rarity.Common },
        { Tower.ID.PurpleLaser,      Tower.Rarity.Common },
        { Tower.ID.OrangeFlamethrower, Tower.Rarity.Common },
        { Tower.ID.A,                Tower.Rarity.Common },
        { Tower.ID.QuadLaser,        Tower.Rarity.Common },
        { Tower.ID.V,                Tower.Rarity.Common },
        { Tower.ID.W,                Tower.Rarity.Common },
        { Tower.ID.X,                Tower.Rarity.Common },
        { Tower.ID.Y,                Tower.Rarity.Common },
        { Tower.ID.Z,                Tower.Rarity.Common },

        // --- Tier 2 (two base towers) → Common ---
        { Tower.ID.BombTower,        Tower.Rarity.Common },
        { Tower.ID.IceTower,         Tower.Rarity.Common },
        { Tower.ID.Shotgun,          Tower.Rarity.Common },
        { Tower.ID.Sniper,           Tower.Rarity.Common },
        { Tower.ID.Gatling,          Tower.Rarity.Common },
        { Tower.ID.Missile,          Tower.Rarity.Common },
        { Tower.ID.GoldProjectile,   Tower.Rarity.Common },

        // --- Tier 3 (tier 2 + base) → Rare ---
        { Tower.ID.Mine,             Tower.Rarity.Rare },
        { Tower.ID.FireField,        Tower.Rarity.Rare },
        { Tower.ID.Mortar,           Tower.Rarity.Rare },
        { Tower.ID.Lightning,        Tower.Rarity.Rare },
        { Tower.ID.BlackHole,        Tower.Rarity.Rare },
        { Tower.ID.Lens,             Tower.Rarity.Rare },
        { Tower.ID.Bank,             Tower.Rarity.Rare },

        // --- Tier 4 (two tier 3s) → Rare ---
        { Tower.ID.LightningRod,     Tower.Rarity.Rare },
        { Tower.ID.Necromancer,      Tower.Rarity.Rare },
        { Tower.ID.Agent,            Tower.Rarity.Rare },
        { Tower.ID.Rainbow,          Tower.Rarity.Rare },

        // --- Tier 5 (endgame) → Legendary ---
        { Tower.ID.LaserAgent,       Tower.Rarity.Legendary },
        { Tower.ID.Sun,              Tower.Rarity.Legendary },
    };

    public static Tower.Rarity GetTowerRarity(Tower.ID id)
    {
        if (towerRarityDictionary.TryGetValue(id, out Tower.Rarity rarity))
            return rarity;
        return Tower.Rarity.Common;
    }

    public int GetMaximumPlacedTowers()
    {
        return Mathf.Max(0, maximumPlacedTowers);
    }

    public void SetMaximumPlacedTowers(int value)
    {
        maximumPlacedTowers = Mathf.Max(0, value);
        RefreshPlacedTowersDisplay();
    }

    public int GetCurrentPlacedTowers()
    {
        return Mathf.Max(0, currentPlacedTowers);
    }

    public int GetInventoryTowerCount()
    {
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Inventory, out var inv) && inv != null)
        {
            return Mathf.Max(0, inv.Count);
        }

        return 0;
    }

    private void RefreshPlacedTowersDisplay()
    {
        if (GameInfoDisplay.instance != null) GameInfoDisplay.instance.RefreshPlacedTowersText();
    }

    public IEnumerable<Tower> EnumerateTowersInState(Tower.State state)
    {
        if (stateToTowers == null) yield break;
        if (!stateToTowers.TryGetValue(state, out var set) || set == null) yield break;

        foreach (var tower in set)
        {
            if (tower == null) continue;
            yield return tower;
        }
    }

    public IEnumerable<Tower> EnumeratePlacedTowers()
    {
        return EnumerateTowersInState(Tower.State.Placed);
    }

    public int GetPlacedTowerCountById(Tower.ID id)
    {
        if (stateToTowers == null) return 0;
        if (!stateToTowers.TryGetValue(Tower.State.Placed, out var placed) || placed == null) return 0;

        int count = 0;
        foreach (var tower in placed)
        {
            if (tower == null) continue;
            if (tower.id != id) continue;
            count++;
        }

        return count;
    }

    private void Awake()
    {
        instance = this;

        // Initialize state sets.
        stateToTowers = new Dictionary<Tower.State, HashSet<Tower>>
        {
            { Tower.State.Shop, new HashSet<Tower>() },
            { Tower.State.Placed, new HashSet<Tower>() },
            { Tower.State.Inventory, new HashSet<Tower>() },
        };

        towerPrefabDictionary = new Dictionary<Tower.ID, GameObject>();
        foreach (GameObject towerPrefab in towerPrefabs)
        {
            Tower towerComponent = towerPrefab.GetComponent<Tower>();
            if (towerComponent != null)
            {
                towerPrefabDictionary[towerComponent.id] = towerPrefab;
            }
            else
            {
                Debug.LogError("Tower prefab " + towerPrefab.name + " does not have a Tower component.");
            }
        }
    }

    public List<Tower> GetNNearestPlacedTowers(int n, Vector3 referencePoint)
    {
        List<Tower> placedTowers = new List<Tower>();
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Placed, out var placed) && placed != null)
        {
            foreach (var tower in placed)
            {
                if (tower != null)
                {
                    placedTowers.Add(tower);
                }
            }
        }
        placedTowers.Sort((a, b) =>
            Vector3.Distance(a.transform.position, referencePoint)
                .CompareTo(Vector3.Distance(b.transform.position, referencePoint)));
        if (n < placedTowers.Count)
            placedTowers.RemoveRange(n, placedTowers.Count - n);
        return placedTowers;
    }

    private void RemoveFromAllStates(Tower tower)
    {
        if (tower == null) return;
        foreach (var kvp in stateToTowers)
        {
            kvp.Value.Remove(tower);
        }
    }

    private void AddToState(Tower tower, Tower.State state)
    {
        if (tower == null) return;
        if (!stateToTowers.TryGetValue(state, out var set) || set == null)
        {
            set = new HashSet<Tower>();
            stateToTowers[state] = set;
        }
        set.Add(tower);
    }

    private void RecomputePlacedCount()
    {
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Placed, out var placed) && placed != null)
        {
            currentPlacedTowers = placed.Count;
        }
        else
        {
            currentPlacedTowers = 0;
        }

        RefreshPlacedTowersDisplay();
    }

    public bool PlayerHasTower(Tower.ID id)
    {
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Inventory, out var inv) && inv != null)
        {
            foreach (Tower tower in inv)
            {
                if (tower.id == id) return true;
            }
        }
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Placed, out var placed) && placed != null)
        {
            foreach (Tower tower in placed)
            {
                if (tower.id == id) return true;
            }
        }
        return false;
    }

    private bool HasInventorySpace()
    {
        if (PIC.instance == null) return false;
        if (PIC.instance.towerInventorySlots == null || PIC.instance.towerInventorySlots.Count == 0) return false;

        int invCount = 0;
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Inventory, out var inv) && inv != null)
        {
            invCount = inv.Count;
        }

        return invCount < PIC.instance.towerInventorySlots.Count;
    }

    private bool HasPlacementCapacity()
    {
        int max = GetMaximumPlacedTowers();
        RecomputePlacedCount();
        return currentPlacedTowers < max;
    }

    public List<Tower> GetAllPurchasedTowers()
    {
        List<Tower> purchased = new List<Tower>();
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Inventory, out var inv) && inv != null)
        {
            purchased.AddRange(inv);
        }
        if (stateToTowers != null && stateToTowers.TryGetValue(Tower.State.Placed, out var placed) && placed != null)
        {
            purchased.AddRange(placed);
        }
        return purchased;
    }

    public void OnTowerPlaced(Tower tower)
    {
        if (tower == null) return;

        RemoveFromAllStates(tower);
        AddToState(tower, Tower.State.Placed);
        tower.CurrentState = Tower.State.Placed;

        if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();

        RecomputePlacedCount();

        if (SaveDataManager.instance != null) SaveDataManager.instance.UpdateMaxTagTowersPlaced();
        if (SaveDataManager.instance != null) SaveDataManager.instance.NotifyTowersPlaced(GetCurrentPlacedTowers());
        if (SaveDataManager.instance != null) SaveDataManager.instance.TryUnlockExplosionTradeoffByProgress();

        VisualizeWolfPackPlacementLinks(tower);

        if (RecipeManager.instance != null) RecipeManager.instance.OnTowersChange();
    }

    private void VisualizeWolfPackPlacementLinks(Tower placedTower)
    {
        if (placedTower == null) return;
        if (RM.i == null || !RM.i.Active(RM.ID.wolfPack)) return;
        if (LaserObjectPool.instance == null) return;

        bool drewAnyLink = false;
        Dictionary<Tower, int> bonusStacksByTower = new Dictionary<Tower, int>();
        foreach (Tower otherTower in EnumeratePlacedTowers())
        {
            if (otherTower == null || otherTower == placedTower) continue;

            RangeManager placedRange = placedTower.GetRangeManager();
            RangeManager otherRange = otherTower.GetRangeManager();

            bool otherInPlacedRange = IsTowerInRangeList(placedRange, otherTower);
            bool placedInOtherRange = IsTowerInRangeList(otherRange, placedTower);
            if (!otherInPlacedRange && !placedInOtherRange) continue;

            LaserObjectPool.instance.ShowLaser(
                placedTower.transform.position,
                otherTower.transform.position,
                wolfPackPlacementLaserColor,
                wolfPackPlacementLaserWidth,
                wolfPackPlacementLaserDuration,
                wolfPackPlacementLaserAlpha);

            if (otherInPlacedRange)
            {
                AddWolfPackBonusStacks(bonusStacksByTower, placedTower, 1);
            }

            if (placedInOtherRange)
            {
                AddWolfPackBonusStacks(bonusStacksByTower, otherTower, 1);
            }

            drewAnyLink = true;
        }

        if (drewAnyLink)
        {
            ShowWolfPackFloatingBonusText(bonusStacksByTower);
            RM.i.IndicateRelic(RM.ID.wolfPack);
        }
    }

    public void VisualizeWolfPackForAllPlacedTowersOnRelicPurchase()
    {
        if (RM.i == null || !RM.i.Active(RM.ID.wolfPack)) return;

        List<Tower> placedTowers = new List<Tower>();
        foreach (Tower tower in EnumeratePlacedTowers())
        {
            if (tower != null)
            {
                placedTowers.Add(tower);
            }
        }

        if (placedTowers.Count < 2) return;

        bool drewAnyLink = false;
        Dictionary<Tower, int> bonusStacksByTower = new Dictionary<Tower, int>();
        for (int i = 0; i < placedTowers.Count; i++)
        {
            Tower a = placedTowers[i];
            if (a == null) continue;

            for (int j = i + 1; j < placedTowers.Count; j++)
            {
                Tower b = placedTowers[j];
                if (b == null) continue;

                RangeManager aRange = a.GetRangeManager();
                RangeManager bRange = b.GetRangeManager();

                bool bInARange = IsTowerInRangeList(aRange, b);
                bool aInBRange = IsTowerInRangeList(bRange, a);
                if (!bInARange && !aInBRange) continue;

                if (LaserObjectPool.instance != null)
                {
                    LaserObjectPool.instance.ShowLaser(
                        a.transform.position,
                        b.transform.position,
                        wolfPackPlacementLaserColor,
                        wolfPackPlacementLaserWidth,
                        wolfPackPlacementLaserDuration,
                        wolfPackPlacementLaserAlpha);
                }

                if (bInARange)
                {
                    AddWolfPackBonusStacks(bonusStacksByTower, a, 1);
                }

                if (aInBRange)
                {
                    AddWolfPackBonusStacks(bonusStacksByTower, b, 1);
                }

                drewAnyLink = true;
            }
        }

        if (!drewAnyLink) return;

        ShowWolfPackFloatingBonusText(bonusStacksByTower, WolfPackInitialTextSizeMultiplier);
        RM.i.IndicateRelic(RM.ID.wolfPack);
    }

    public Dictionary<Tower, int> BuildWolfPackBonusStacksForPlacedTowers()
    {
        Dictionary<Tower, int> bonusStacksByTower = new Dictionary<Tower, int>();
        List<Tower> placedTowers = new List<Tower>();
        foreach (Tower tower in EnumeratePlacedTowers())
        {
            if (tower != null)
            {
                placedTowers.Add(tower);
            }
        }

        if (placedTowers.Count < 2) return bonusStacksByTower;

        for (int i = 0; i < placedTowers.Count; i++)
        {
            Tower a = placedTowers[i];
            if (a == null) continue;

            for (int j = i + 1; j < placedTowers.Count; j++)
            {
                Tower b = placedTowers[j];
                if (b == null) continue;

                RangeManager aRange = a.GetRangeManager();
                RangeManager bRange = b.GetRangeManager();

                bool bInARange = IsTowerInRangeList(aRange, b);
                bool aInBRange = IsTowerInRangeList(bRange, a);
                if (!bInARange && !aInBRange) continue;

                if (bInARange)
                {
                    AddWolfPackBonusStacks(bonusStacksByTower, a, 1);
                }

                if (aInBRange)
                {
                    AddWolfPackBonusStacks(bonusStacksByTower, b, 1);
                }
            }
        }

        return bonusStacksByTower;
    }

    public void ShowWolfPackHoverBonusText(float lifetime)
    {
        Dictionary<Tower, int> bonusStacksByTower = BuildWolfPackBonusStacksForPlacedTowers();
        if (bonusStacksByTower.Count == 0) return;

        ShowWolfPackFloatingBonusText(bonusStacksByTower, 1f, lifetime);
    }

    private static void AddWolfPackBonusStacks(Dictionary<Tower, int> bonusStacksByTower, Tower tower, int stacks)
    {
        if (bonusStacksByTower == null || tower == null || stacks <= 0) return;

        if (bonusStacksByTower.TryGetValue(tower, out int existing))
        {
            bonusStacksByTower[tower] = existing + stacks;
        }
        else
        {
            bonusStacksByTower[tower] = stacks;
        }
    }

    private void ShowWolfPackFloatingBonusText(Dictionary<Tower, int> bonusStacksByTower, float sizeMultiplier = 1f, float? lifetimeOverride = null)
    {
        if (bonusStacksByTower == null || bonusStacksByTower.Count == 0) return;
        if (TextObjectPool.instance == null) return;

        float resolvedSizeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
        float resolvedLifetime = Mathf.Max(0.05f, lifetimeOverride ?? wolfPackFloatingTextLifetime);

        foreach (var kvp in bonusStacksByTower)
        {
            Tower tower = kvp.Key;
            int stacks = kvp.Value;
            if (tower == null || stacks <= 0) continue;

            float percent = Mathf.Max(0f, stacks * RM.wolfPackDamagePerTower * 100f);
            string text = "+" + percent.ToString("0.#") + "%";
            Vector3 textPosition = tower.transform.position + wolfPackFloatingTextOffset;
            TextObjectPool.instance.PlayFloatingText(textPosition, text, wolfPackFloatingTextColor, WolfPackFloatingTextSize * resolvedSizeMultiplier, resolvedLifetime);
        }
    }

    private static bool IsTowerInRangeList(RangeManager rangeManager, Tower tower)
    {
        if (rangeManager == null || tower == null) return false;

        List<Tower> towersInRange = rangeManager.GetAllActiveTowersInRange();
        for (int i = 0; i < towersInRange.Count; i++)
        {
            if (towersInRange[i] == tower)
            {
                return true;
            }
        }

        return false;
    }

    public void OnTowerSpawnedInShop(Tower tower)
    {
        if (tower == null) return;

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyTowerShownInShop(tower.id);
        }

        RemoveFromAllStates(tower);
        AddToState(tower, Tower.State.Shop);
        tower.CurrentState = Tower.State.Shop;

        SRC src = tower.GetComponent<SRC>();
        if (src != null)
        {
            src.Indicate();
        }
    }

    public void OnTowerPurchasedFromShop(Tower tower)
    {
        if (tower == null) return;

        // After purchase the tower should live in inventory until placed.
        RemoveFromAllStates(tower);
        AddToState(tower, Tower.State.Inventory);
        tower.CurrentState = Tower.State.Inventory;

        // Do not register tags here; TagManager counts only placed towers.

        TowerPurchasedFromShop?.Invoke(tower);

        if (RecipeManager.instance != null) RecipeManager.instance.OnTowersChange();
    }

    public void OnTowerMovedToInventory(Tower tower)
    {
        if (tower == null) return;

        RemoveFromAllStates(tower);
        AddToState(tower, Tower.State.Inventory);
        tower.CurrentState = Tower.State.Inventory;

        if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();

        RecomputePlacedCount();

        if (RecipeManager.instance != null) RecipeManager.instance.OnTowersChange();
    }

    public bool TowerCanBePurchasedFromShop()
    {
        // Can buy if there's a way to accommodate it: either a placement slot is available,
        // or inventory has free space.
        return HasPlacementCapacity() || HasInventorySpace();
    }

    public bool TowerCanBePlaced()
    {
        return HasPlacementCapacity();
    }

    public bool TowerCanBePickedUpFromInventory()
    {
        // "Pick up" here means taking an inventory tower into placement mode.
        // We allow it if the player can ultimately place it, OR there is still room
        // in inventory (so cancel/return doesn't overflow inventory).
        return HasPlacementCapacity() || HasInventorySpace();
    }

    public void OnTowerDestroyed(Tower tower)
    {
        if (tower == null) return;

        RemoveFromAllStates(tower);

        if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();

        RecomputePlacedCount();

        if (RecipeManager.instance != null) RecipeManager.instance.OnTowersChange();
    }
}
