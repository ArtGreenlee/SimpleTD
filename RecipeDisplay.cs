using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RecipeDisplay : MonoBehaviour
{
    
    public TextMeshProUGUI t1;
    public TextMeshProUGUI t2;
    public TextMeshProUGUI t3;
    public List<Transform> slots;

    public List<GameObject> displayedTowers = new List<GameObject>();
    public List<UnityEngine.UI.RawImage> slotImages;

    private Coroutine _positionRoutine;
    private bool _pendingPositionAfterEnable;
    private RecipeManager.Recipe _currentRecipe;
    private Tower.ID? _highlightedTowerId;
    private readonly List<GameObject> _spawnedIndicators = new List<GameObject>();

    [Header("Recipe Indicators")]
    [SerializeField, Min(0.01f)] private float indicatorTimeoutSeconds = 6f;
    [SerializeField, Min(0.05f)] private float indicatorRadius = 0.3f;

    private void OnEnable()
    {
        if (_pendingPositionAfterEnable)
        {
            _pendingPositionAfterEnable = false;
            StartPositionRoutine();
        }
    }

    private void OnDisable()
    {
        if (_positionRoutine != null)
        {
            StopCoroutine(_positionRoutine);
            _positionRoutine = null;
        }

        _pendingPositionAfterEnable = false;
        DestroySpawnedIndicators();
        DestroyDisplayedTowers();
    }

    /// <summary>
    /// Remove a tower from any active recipe display tracking so it won't be destroyed when displays are hidden.
    /// Intended to be called when a player picks up a recipe result.
    /// </summary>
    public static void UntrackPickedUpTower(GameObject pickedUpTower)
    {
        if (pickedUpTower == null) return;

        // Recipe results are picked up while their display is active, so searching active displays is sufficient.
        var displays = FindObjectsByType<RecipeDisplay>(FindObjectsSortMode.None);
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] == null) continue;
            displays[i].RemoveDisplayedTower(pickedUpTower);
        }
    }

    public void RemoveDisplayedTower(GameObject towerObject)
    {
        if (towerObject == null || displayedTowers == null) return;
        displayedTowers.Remove(towerObject);
    }

    private void DestroyDisplayedTowers()
    {
        if (displayedTowers == null) return;

        for (int i = displayedTowers.Count - 1; i >= 0; i--)
        {
            var go = displayedTowers[i];
            displayedTowers.RemoveAt(i);
            if (go != null) Destroy(go);
        }
    }

    private void DestroySpawnedIndicators()
    {
        if (_spawnedIndicators == null) return;

        for (int i = _spawnedIndicators.Count - 1; i >= 0; i--)
        {
            var go = _spawnedIndicators[i];
            _spawnedIndicators.RemoveAt(i);
            if (go != null) Destroy(go);
        }
    }

    public void DisplayRecipe(RecipeManager.Recipe recipe, bool allowInteraction = true, Tower.ID? highlightedTowerId = null)
    {
        if (_positionRoutine != null)
        {
            StopCoroutine(_positionRoutine);
            _positionRoutine = null;
        }

        _currentRecipe = recipe;
        _highlightedTowerId = highlightedTowerId;

        DestroySpawnedIndicators();
        DestroyDisplayedTowers();

        if (TowerManager.instance == null || TowerManager.instance.towerPrefabDictionary == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!TowerManager.instance.towerPrefabDictionary.TryGetValue(recipe.resultTower, out var resultPrefab) || resultPrefab == null)
        {
            gameObject.SetActive(false);
            return;
        }

        List<GameObject> ingredientPrefabs = new List<GameObject>();
        if (recipe.requiredTowers != null)
        {
            for (int i = 0; i < recipe.requiredTowers.Count; i++)
            {
                if (!TowerManager.instance.towerPrefabDictionary.TryGetValue(recipe.requiredTowers[i], out var ingredientPrefab) || ingredientPrefab == null)
                {
                    gameObject.SetActive(false);
                    return;
                }

                ingredientPrefabs.Add(ingredientPrefab);
            }
        }

        bool canMakeTower = true;
        Dictionary<Tower.ID, int> remaining = new Dictionary<Tower.ID, int>();
        foreach (var t in TowerManager.instance.GetAllPurchasedTowers())
        {
            if (t == null) continue;
            if (!remaining.ContainsKey(t.id)) remaining[t.id] = 0;
            remaining[t.id]++;
        }

        for (int i = 0; i < ingredientPrefabs.Count; i++)
        {
            GameObject ingredient = Instantiate(ingredientPrefabs[i]);
            ingredient.SetActive(false);
            ConfigureRecipeDisplayTowerVisuals(ingredient);

            var ingredientSwell = ingredient.GetComponent<SwellAnimation>();
            if (ingredientSwell != null) ingredientSwell.CancelAndReset();
            AdjustSortingOrder(ingredient, 10);
            Tower tower = ingredient.GetComponent<Tower>();
            TowerInteractable towerInteractable = ingredient.GetComponent<TowerInteractable>();
            if (towerInteractable != null) towerInteractable.pickupable = false;
            SetTowerInteractionEnabled(ingredient, false);
            displayedTowers.Add(ingredient);

            var requiredId = recipe.requiredTowers[i];
            bool hasIngredient = remaining.TryGetValue(requiredId, out int count) && count > 0;
            if (hasIngredient)
            {
                remaining[requiredId] = count - 1;
            }

            if (!hasIngredient)
            {
                if (tower != null) tower.EnableGhostMode();
                canMakeTower = false;
            }
            else
            {
                if (tower != null) tower.DisableGhostMode();
            }
        }

        GameObject resultObject = Instantiate(resultPrefab);
        resultObject.SetActive(false);
        ConfigureRecipeDisplayTowerVisuals(resultObject);

        var resultSwell = resultObject.GetComponent<SwellAnimation>();
        if (resultSwell != null) resultSwell.CancelAndReset();
        AdjustSortingOrder(resultObject, 10);
        displayedTowers.Add(resultObject);

        var resultTower = resultObject.GetComponent<Tower>();
        if (resultTower != null) resultTower.SetCraftingRequiredTowers(recipe.requiredTowers);

        var resultInteractable = resultObject.GetComponent<TowerInteractable>();
        if (!canMakeTower)
        {
            if (resultInteractable != null) resultInteractable.pickupable = false;
            if (resultTower != null) resultTower.EnableGhostMode();
        }
        else
        {
            if (resultInteractable != null) resultInteractable.pickupable = allowInteraction;
            if (resultTower != null) resultTower.DisableGhostMode();
        }

        SetTowerInteractionEnabled(resultObject, allowInteraction);

        int occupiedSlots = ingredientPrefabs.Count + 1;
        for (int i = 0; i < slotImages.Count; i++)
        {
            if (slotImages[i] != null)
                slotImages[i].enabled = i < occupiedSlots;
        }

        if (ingredientPrefabs.Count >= 3)
        {
            t1.text = "+";
            t2.text = "+";
            t3.text = "=";
        }
        else if (ingredientPrefabs.Count == 2)
        {
            t1.text = "+";
            t2.text = "=";
            t3.text = "";
        }
        else if (ingredientPrefabs.Count == 1)
        {
            t1.text = "=";
            t2.text = "";
            t3.text = "";
        }
        else
        {
            t1.text = "";
            t2.text = "";
            t3.text = "";
        }

        if (isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            StartPositionRoutine();
        }
        else
        {
            _pendingPositionAfterEnable = true;
        }
    }

    private void StartPositionRoutine()
    {
        if (_positionRoutine != null)
        {
            StopCoroutine(_positionRoutine);
        }

        _positionRoutine = StartCoroutine(PositionTowersAfterLayout());
    }

    private IEnumerator PositionTowersAfterLayout()
    {
        yield return new WaitForEndOfFrame();

        // displayedTowers: [ingredient0, ingredient1, ..., result]
        for (int i = 0; i < displayedTowers.Count && i < slots.Count; i++)
        {
            var tower = displayedTowers[i];
            if (tower == null) continue;

            tower.transform.position = slots[i].transform.position;
            tower.SetActive(true);
        }

        SpawnRecipeIndicators();

        _positionRoutine = null;
    }

    private void SpawnRecipeIndicators()
    {
        DestroySpawnedIndicators();

        if (AOEObjectPool.instance == null) return;
        if (displayedTowers == null || displayedTowers.Count == 0) return;

        var placedCounts = BuildPlacedTowerCounts();

        for (int i = 0; i < displayedTowers.Count; i++)
        {
            var towerObject = displayedTowers[i];
            if (towerObject == null) continue;

            var tower = towerObject.GetComponent<Tower>();
            if (tower == null) continue;

            Color color = Color.red;
            bool isCurrentTower = _highlightedTowerId.HasValue && tower.id == _highlightedTowerId.Value;
            if (isCurrentTower)
            {
                color = Color.white;
            }
            else if (placedCounts.TryGetValue(tower.id, out int count) && count > 0)
            {
                color = Color.green;
                placedCounts[tower.id] = count - 1;
            }

            color.a = 0.2f;

            int sortingOrder = GetHighestSortingOrder(towerObject) + 1;
            float diameter = GetIndicatorDiameter(towerObject);
            GameObject indicator = AOEObjectPool.instance.SpawnUnmanagedIndicator(
                towerObject.transform.position,
                diameter,
                color,
                sortingOrder,
                indicatorTimeoutSeconds);

            if (indicator != null)
            {
                _spawnedIndicators.Add(indicator);
            }
        }
    }

    private static Dictionary<Tower.ID, int> BuildPlacedTowerCounts()
    {
        var result = new Dictionary<Tower.ID, int>();
        if (TowerManager.instance == null) return result;

        foreach (Tower placedTower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (placedTower == null) continue;
            if (!result.ContainsKey(placedTower.id)) result[placedTower.id] = 0;
            result[placedTower.id]++;
        }

        return result;
    }

    private int GetHighestSortingOrder(GameObject towerObject)
    {
        int maxOrder = int.MinValue;
        var renderers = towerObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (sr == null) continue;
            if (sr.sortingOrder > maxOrder) maxOrder = sr.sortingOrder;
        }

        return maxOrder == int.MinValue ? 0 : maxOrder;
    }

    private float GetIndicatorDiameter(GameObject towerObject)
    {
        return Mathf.Max(0.1f, indicatorRadius * 2f);
    }

    private static void SetTowerInteractionEnabled(GameObject towerObject, bool enabled)
    {
        if (towerObject == null) return;

        var interactable = towerObject.GetComponent<TowerInteractable>();
        if (interactable != null)
        {
            // interactable.enabled = enabled;
            if (!enabled) interactable.pickupable = false;
        }
    }

    private static void AdjustSortingOrder(GameObject towerObject, int delta)
    {
        if (towerObject == null || delta == 0) return;

        var renderers = towerObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sortingOrder += delta;
        }
    }

    private static void ConfigureRecipeDisplayTowerVisuals(GameObject towerObject)
    {
        if (towerObject == null) return;

        var lensTower = towerObject.GetComponent<LensTower>();
        if (lensTower == null) return;

        if (lensTower.lensTransform == null) return;

        var lensSpriteRenderers = lensTower.lensTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < lensSpriteRenderers.Length; i++)
        {
            if (lensSpriteRenderers[i] == null) continue;
            lensSpriteRenderers[i].enabled = false;
        }

        var lensLineRenderers = lensTower.lensTransform.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lensLineRenderers.Length; i++)
        {
            if (lensLineRenderers[i] == null) continue;
            lensLineRenderers[i].enabled = false;
        }
    }
}
