using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System;
public class RecipeManager : MonoBehaviour
{
    public static RecipeManager instance;
    public GameObject recipeTutorialObject;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Initialize the UI immediately on scene load.
        OnTowersChange();
    }

    [Serializable]
    public struct Recipe
    {
        public List<Tower.ID> requiredTowers;
        public Tower.ID resultTower;
    }

    //public List<Recipe> recipes;
    public List<RecipeDisplay> recipeDisplays;

    private Dictionary<Tower.ID, List<Tower.ID>> recipeDictionary = new Dictionary<Tower.ID, List<Tower.ID>>
    {
        // --- Tier 2: two base towers ---
        { Tower.ID.BombTower,      new List<Tower.ID> { Tower.ID.RedTower,       Tower.ID.OrangeFlamethrower } },
        { Tower.ID.IceTower,       new List<Tower.ID> { Tower.ID.BlueTower,      Tower.ID.BuffTower   } },
        { Tower.ID.Shotgun,        new List<Tower.ID> { Tower.ID.RedTower,       Tower.ID.GreenTower         } },
        { Tower.ID.Sniper,         new List<Tower.ID> { Tower.ID.GreenTower,     Tower.ID.BuffTower   } },
        { Tower.ID.Gatling,        new List<Tower.ID> { Tower.ID.RedTower,       Tower.ID.BlueTower          } },
        { Tower.ID.Missile,          new List<Tower.ID> { Tower.ID.PurpleLaser,    Tower.ID.BlueTower          } },
        { Tower.ID.GoldProjectile, new List<Tower.ID> { Tower.ID.GreenTower,     Tower.ID.OrangeFlamethrower } },

        // --- Tier 3: tier 2 + base tower ---
        { Tower.ID.Mine,        new List<Tower.ID> { Tower.ID.IceTower,  Tower.ID.GreenTower         } },
        { Tower.ID.FireField,   new List<Tower.ID> { Tower.ID.BombTower, Tower.ID.OrangeFlamethrower } },
        { Tower.ID.Mortar,      new List<Tower.ID> { Tower.ID.BombTower, Tower.ID.RedTower            } },
        { Tower.ID.Lightning,   new List<Tower.ID> { Tower.ID.Gatling,   Tower.ID.BuffTower   } },
        { Tower.ID.BlackHole,   new List<Tower.ID> { Tower.ID.Missile,     Tower.ID.PurpleLaser        } },
        { Tower.ID.Lens,   new List<Tower.ID> { Tower.ID.Sniper,    Tower.ID.GreenTower         } },
        { Tower.ID.Bank,        new List<Tower.ID> { Tower.ID.GoldProjectile, Tower.ID.GreenTower    } },

        // --- Tier 4: two tier 3 or tier 3 + base ---
        { Tower.ID.LightningRod, new List<Tower.ID> { Tower.ID.Lightning,  Tower.ID.BuffTower } },
        { Tower.ID.Necromancer,  new List<Tower.ID> { Tower.ID.BlackHole,  Tower.ID.Mine             } },
        { Tower.ID.Agent,        new List<Tower.ID> { Tower.ID.Sniper,     Tower.ID.GreenTower, Tower.ID.BuffTower } },
        { Tower.ID.Rainbow,      new List<Tower.ID> { Tower.ID.Missile,      Tower.ID.GoldProjectile   } },

        // --- Tier 5: endgame ---
        { Tower.ID.LaserAgent, new List<Tower.ID> { Tower.ID.Agent,    Tower.ID.PurpleLaser } },
        { Tower.ID.Sun,        new List<Tower.ID> { Tower.ID.Rainbow,  Tower.ID.FireField   } },
    };

    public IReadOnlyDictionary<Tower.ID, List<Tower.ID>> RecipeDictionary => recipeDictionary;

    public void ShowRecipeTutorial()
    {
        recipeTutorialObject.SetActive(true);
    }

    public void HideRecipeTutorial()
    {
        recipeTutorialObject.SetActive(false);
    }
    public void OnTowersChange()
    {
        if (TowerManager.instance == null) return;
        if (recipeDisplays == null || recipeDisplays.Count == 0) return;
        
        List<Tower> allTowers = TowerManager.instance.GetAllPurchasedTowers();
        List<Recipe> allPossibleRecipes = GetAllPossibleRecipes();
        if (allPossibleRecipes == null) return;
        
        for (int i = 0; i < Mathf.Min(recipeDisplays.Count, allPossibleRecipes.Count); i++)
        {
            if (recipeDisplays[i] == null) continue;
            recipeDisplays[i].gameObject.SetActive(true);
            recipeDisplays[i].DisplayRecipe(allPossibleRecipes[i]);
        }
        for (int i = allPossibleRecipes.Count; i < recipeDisplays.Count; i++)
        {
            if (recipeDisplays[i] == null) continue;
            recipeDisplays[i].gameObject.SetActive(false); 
        }
        UIAnimation.instance.IndicateCrafting();
    }

    public List<Recipe> GetAllPossibleRecipes()
    {
        if (TowerManager.instance == null) return new List<Recipe>();

        List<Tower> allTowers = TowerManager.instance.GetAllPurchasedTowers();
        HashSet<Tower.ID> towerIDs = new HashSet<Tower.ID>();
        foreach (Tower tower in allTowers)
        {
            towerIDs.Add(tower.id);
        }
        List<Recipe> possibleRecipes = new List<Recipe>();
        if (recipeDictionary == null || recipeDictionary.Count == 0)
        {
            return possibleRecipes;
        }

        foreach (var kvp in recipeDictionary)
        {
            List<Tower.ID> requiredTowers = kvp.Value;
            if (requiredTowers == null || requiredTowers.Count == 0)
                continue;

            bool hasAnyIngredient = false;
            foreach (Tower.ID requiredID in requiredTowers)
            {
                if (towerIDs.Contains(requiredID))
                {
                    hasAnyIngredient = true;
                    break;
                }
            }

            if (hasAnyIngredient)
            {
                possibleRecipes.Add(new Recipe
                {
                    resultTower = kvp.Key,
                    requiredTowers = new List<Tower.ID>(requiredTowers)
                });
            }
        }
        return possibleRecipes;
    }

    public void CombineTowers(List<Tower> towersToCombine, Tower resultTower) 
    {
        // destroy the towers that were combined
        foreach (Tower tower in towersToCombine)
        {
            Destroy(tower.gameObject);
            // TowerManager.instance.SellTower(tower);
        }    
    }
}
