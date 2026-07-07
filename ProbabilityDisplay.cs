using System.Collections.Generic;
using UnityEngine;

public class ProbabilityDisplay : MonoBehaviour
{
    public enum HideMode
    {
        None,
        SetActive,
        SlideAnimation,
    }

    public static ProbabilityDisplay instance { get; private set; }

    [SerializeField] private Transform panelTransform;
    [SerializeField] private HideMode hideMode = HideMode.SlideAnimation;
    [SerializeField, Min(0f)] private float hideDelaySeconds = 0.08f;
    public Transform slotParent;
    [SerializeField] private TowerManager towerManager;
    private readonly List<ProbabilityDisplaySlot> slots = new List<ProbabilityDisplaySlot>();
    private readonly List<GameObject> spawnedDisplayTowers = new List<GameObject>();
    private readonly Dictionary<ProbabilityDisplaySlot, Tower.ID> slotTowerIds = new Dictionary<ProbabilityDisplaySlot, Tower.ID>();
    private readonly Dictionary<Tower.ID, ProbabilityDisplaySlot> towerIdToSlot = new Dictionary<Tower.ID, ProbabilityDisplaySlot>();
    private readonly Dictionary<Tower.ID, GameObject> uniquePrefabByTowerId = new Dictionary<Tower.ID, GameObject>();

    private float _hideDelayTimer;

    // Hover preview state
    private Tower.ID _hoveredTowerId = default;
    private bool _isHovering = false;
    private Dictionary<Tower.ID, float> _hoveredProbabilityPreview;

    private void Awake()
    {
        instance = this;
        UpdatePanelVisibility();
    }

    private void OnEnable()
    {
        EnsureTowerManagerReference();
        UpdatePanelVisibility();

        if (towerManager != null)
            towerManager.TowerPurchasedFromShop += HandleTowerPurchasedFromShop;
    }

    private void OnDisable()
    {
        if (towerManager != null)
            towerManager.TowerPurchasedFromShop -= HandleTowerPurchasedFromShop;
    }

    private void Update()
    {
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (hideMode == HideMode.None)
        {
            if (panelTransform != null && !panelTransform.gameObject.activeSelf)
                panelTransform.gameObject.SetActive(true);

            for (int i = 0; i < spawnedDisplayTowers.Count; i++)
            {
                GameObject towerObject = spawnedDisplayTowers[i];
                if (towerObject != null && !towerObject.activeSelf)
                    towerObject.SetActive(true);
            }
            return;
        }

        if (hideMode == HideMode.SetActive)
        {
            bool show = ShouldShowPanelImmediate();
            if (panelTransform != null && panelTransform.gameObject.activeSelf != show)
                panelTransform.gameObject.SetActive(show);

            for (int i = 0; i < spawnedDisplayTowers.Count; i++)
            {
                GameObject towerObject = spawnedDisplayTowers[i];
                if (towerObject == null) continue;
                if (towerObject.activeSelf != show)
                    towerObject.SetActive(show);
            }
            return;
        }

        // SlideAnimation mode: same as SetActive but with a hide delay
        bool showDelayed = ShouldShowPanelWithDelay();
        if (panelTransform != null && panelTransform.gameObject.activeSelf != showDelayed)
            panelTransform.gameObject.SetActive(showDelayed);

        for (int i = 0; i < spawnedDisplayTowers.Count; i++)
        {
            GameObject towerObject = spawnedDisplayTowers[i];
            if (towerObject == null) continue;
            if (towerObject.activeSelf != showDelayed)
                towerObject.SetActive(showDelayed);
        }
    }

    private bool ShouldShowPanelWithDelay()
    {
        if (ShouldShowPanelImmediate())
        {
            _hideDelayTimer = 0f;
            return true;
        }

        if (hideDelaySeconds <= 0f)
        {
            return false;
        }

        if (_hideDelayTimer <= 0f)
        {
            _hideDelayTimer = hideDelaySeconds;
        }

        _hideDelayTimer -= Time.unscaledDeltaTime;
        return _hideDelayTimer > 0f;
    }

    private bool ShouldShowPanelImmediate()
    {
        if (TowerShopManager.instance != null && TowerShopManager.instance.isMouseInsideTowerShop)
            return true;

        if (PIC.instance == null)
            return false;

        // Always show while the player is actively holding a tower.
        if (PIC.instance.isHoldingTower())
            return true;

        if (IsShopOrInventoryTower(PIC.instance.hoveredInteractable))
            return true;

        if (IsShopOrInventoryTower(Interactable.GetClickedInteractable()))
            return true;

        return false;
    }

    private static bool IsShopOrInventoryTower(Interactable interactable)
    {
        TowerInteractable towerInteractable = interactable as TowerInteractable;
        if (towerInteractable == null)
            return false;

        Tower tower = towerInteractable.GetTower();
        if (tower == null)
            return false;

        return tower.CurrentState == Tower.State.Shop || tower.CurrentState == Tower.State.Inventory;
    }

    private void PopulateSlots()
    {
        if (slotParent == null) return;

        EnsureTowerManagerReference();

        if (towerManager == null || towerManager.towerPrefabs == null)
            return;

        ClearDisplayTowers();
        slots.Clear();
        slotTowerIds.Clear();
        towerIdToSlot.Clear();
        uniquePrefabByTowerId.Clear();
        slotParent.GetComponentsInChildren(true, slots);
        if (slots.Count == 0) return;

        List<GameObject> uniqueTowerPrefabs = GetUniqueTowerPrefabs(towerManager.towerPrefabs);
        int count = Mathf.Min(slots.Count, uniqueTowerPrefabs.Count);

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = uniqueTowerPrefabs[i];
            if (prefab == null) continue;

            Tower tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;

            uniquePrefabByTowerId[tower.id] = prefab;

            ProbabilityDisplaySlot mappedSlot = slots[i];
            if (mappedSlot == null) continue;

            slotTowerIds[mappedSlot] = tower.id;
            if (!towerIdToSlot.ContainsKey(tower.id))
                towerIdToSlot.Add(tower.id, mappedSlot);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ProbabilityDisplaySlot slot = slots[i];
            if (slot == null) continue;

            bool hasTower = i < count;
            Tower.ID towerId = default;
            bool hasMappedTower = hasTower && slotTowerIds.TryGetValue(slot, out towerId);

            if (slot.text != null)
            {
                slot.text.gameObject.SetActive(hasTower);
                if (hasTower)
                    slot.text.text = hasMappedTower ? GetProbabilityText(towerId) : "0%";
            }

            if (!hasTower)
            {
                if (slot.hiddenText != null)
                    slot.hiddenText.gameObject.SetActive(false);
                continue;
            }

            // Check if tower has been purchased
            Tower towerComponent = hasMappedTower
                ? null
                : uniqueTowerPrefabs[i].GetComponent<Tower>();

            if (hasMappedTower && uniquePrefabByTowerId.TryGetValue(towerId, out var mappedPrefab) && mappedPrefab != null)
                towerComponent = mappedPrefab.GetComponent<Tower>();

            bool hasPurchased = SaveDataManager.instance != null &&
                                towerComponent != null &&
                                SaveDataManager.instance.HasPurchasedTowerFromShop(towerComponent.id);

            // Always show probability text for mapped towers.
            if (slot.hiddenText != null)
                slot.hiddenText.gameObject.SetActive(false);

            if (!hasPurchased)
                continue;

            GameObject displayPrefab = hasMappedTower && uniquePrefabByTowerId.TryGetValue(towerId, out var p)
                ? p
                : uniqueTowerPrefabs[i];

            GameObject towerObject = SpawnTowerVisual(slot, displayPrefab);
            if (towerObject == null)
                continue;

            spawnedDisplayTowers.Add(towerObject);

            Tower tower = towerObject.GetComponent<Tower>();
            if (tower != null)
            {
                // Keep behavior consistent with display-only shop visuals.
                tower.CurrentState = Tower.State.Shop;
            }

            TowerInteractable interactable = towerObject.GetComponent<TowerInteractable>();
            if (interactable != null)
                interactable.pickupable = false;
        }
    }

    void Start()
    {
        StartCoroutine(InitializeAfterLayout());
    }

    private System.Collections.IEnumerator InitializeAfterLayout()
    {
        // Match shop setup timing so UI children and singleton managers are ready.
        yield return null;
        PopulateSlots();
        RefreshDisplayedProbabilities();
        RefreshTagMatchingVisuals();
    }

    private GameObject SpawnTowerVisual(ProbabilityDisplaySlot slot, GameObject towerPrefab)
    {
        if (slot == null || towerPrefab == null)
            return null;

        Transform targetTransform = slot.slotImageTransform;
        GameObject towerObject = Instantiate(towerPrefab, targetTransform.position, towerPrefab.transform.rotation);
        TryIndicateSpawnedTowerDamageType(towerObject);
        return towerObject;
    }

    private static void TryIndicateSpawnedTowerDamageType(GameObject towerObject)
    {
        if (towerObject == null) return;
        if (AOEObjectPool.instance == null) return;

        Tower tower = towerObject.GetComponent<Tower>();
        if (tower == null) return;

        CM.ColorType damageType = tower.GetDamageType();
        Color color = (CM.i != null && damageType != CM.ColorType.None)
            ? CM.i.ColorTypeToColor(damageType)
            : Color.white;

        AOEObjectPool.instance.Indicate(towerObject.transform.position, 0.5f, color);
    }

    private void ClearDisplayTowers()
    {
        for (int i = 0; i < spawnedDisplayTowers.Count; i++)
        {
            if (spawnedDisplayTowers[i] != null)
                Destroy(spawnedDisplayTowers[i]);
        }

        spawnedDisplayTowers.Clear();
    }

    public void RefreshPurchasedTowerState(Tower.ID towerId)
    {
        if (!towerIdToSlot.TryGetValue(towerId, out var slot) || slot == null)
            return;

        if (slot.text != null)
        {
            slot.text.gameObject.SetActive(true);
            slot.text.text = GetProbabilityText(towerId);
        }

        if (slot.hiddenText != null)
            slot.hiddenText.gameObject.SetActive(false);

        bool alreadySpawned = false;
        for (int j = 0; j < spawnedDisplayTowers.Count; j++)
        {
            if (spawnedDisplayTowers[j] == null) continue;

            Tower spawnedTower = spawnedDisplayTowers[j].GetComponent<Tower>();
            if (spawnedTower == null || spawnedTower.id != towerId) continue;

            alreadySpawned = true;
            break;
        }

        if (alreadySpawned) return;
        if (!uniquePrefabByTowerId.TryGetValue(towerId, out var prefab) || prefab == null) return;

        GameObject towerObject = SpawnTowerVisual(slot, prefab);
        if (towerObject == null) return;

        spawnedDisplayTowers.Add(towerObject);

        Tower tower = towerObject.GetComponent<Tower>();
        if (tower != null)
            tower.CurrentState = Tower.State.Shop;

        TowerInteractable interactable = towerObject.GetComponent<TowerInteractable>();
        if (interactable != null)
            interactable.pickupable = false;
    }

    public void RefreshDisplayedProbabilities()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            ProbabilityDisplaySlot slot = slots[i];
            if (slot == null || slot.text == null) continue;
            if (!slotTowerIds.TryGetValue(slot, out var towerId)) continue;

            if (!slot.text.gameObject.activeSelf)
                slot.text.gameObject.SetActive(true);

            if (slot.hiddenText != null && slot.hiddenText.gameObject.activeSelf)
                slot.hiddenText.gameObject.SetActive(false);

            if (_isHovering && _hoveredProbabilityPreview != null)
            {
                slot.text.text = GetProbabilityTextWithHover(towerId);
            }
            else
            {
                slot.text.text = GetProbabilityText(towerId);
            }
        }
    }

    /// <summary>
    /// Refreshes visual feedback for towers that have tag matches.
    /// Updates slot appearance to indicate which towers have active tag bonuses.
    /// </summary>
    public void RefreshTagMatchingVisuals()
    {
        if (TowerShopManager.instance == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            ProbabilityDisplaySlot slot = slots[i];
            if (slot == null) continue;
            if (!slotTowerIds.TryGetValue(slot, out var towerId)) continue;

            // Check if this tower has tag matches
            bool hasTagMatch = TowerShopManager.instance.HasTowerTagMatches(towerId);
            int tagMatchCount = TowerShopManager.instance.GetTowerTagMatchCount(towerId);

            // Update visual feedback based on tag matching
            UpdateSlotTagMatchVisuals(slot, hasTagMatch, tagMatchCount);
        }
    }

    /// <summary>
    /// Updates the visual appearance of a slot based on tag matching.
    /// Can be overridden to provide custom visual feedback.
    /// </summary>
    private void UpdateSlotTagMatchVisuals(ProbabilityDisplaySlot slot, bool hasTagMatch, int tagMatchCount)
    {
        if (slot == null || slot.text == null)
            return;

        // Update text color based on tag match intensity
        if (hasTagMatch)
        {
            // Apply color based on number of matches
            Color textColor = tagMatchCount switch
            {
                1 => new Color(1.0f, 1.0f, 0.5f),  // Light yellow for 1 match
                2 => new Color(1.0f, 0.8f, 0.0f),  // Orange for 2 matches
                3 => new Color(1.0f, 0.5f, 0.0f),  // Dark orange for 3 matches
                _ => new Color(1.0f, 0.2f, 0.2f)   // Red for 4+ matches
            };
            slot.text.color = textColor;
        }
        else
        {
            // No tag match - normal color
            slot.text.color = Color.white;
        }
    }

    /// <summary>
    /// Called when the player hovers over a tower in the tower shop.
    /// Calculates and displays the probability preview if that tower were purchased.
    /// </summary>
    public void OnTowerShopSlotHovered(Tower.ID towerId)
    {
        if (TowerShopManager.instance == null)
            return;

        _hoveredTowerId = towerId;
        _isHovering = true;
        _hoveredProbabilityPreview = TowerShopManager.instance.CalculateProbabilitiesIfTowerPurchased(towerId);
        RefreshDisplayedProbabilities();
    }

    /// <summary>
    /// Called when the player stops hovering over a tower in the tower shop.
    /// Returns the display to showing current probabilities.
    /// </summary>
    public void OnTowerShopSlotHoverEnded()
    {
        _isHovering = false;
        _hoveredTowerId = default;
        _hoveredProbabilityPreview = null;
        RefreshDisplayedProbabilities();
    }

    private static string GetProbabilityText(Tower.ID towerId)
    {
        if (TowerShopManager.instance == null)
            return "0%";

        if (!TowerShopManager.instance.TryGetTowerProbability(towerId, out float probability))
            return "0%";

        int percent = Mathf.RoundToInt(Mathf.Clamp01(probability) * 100f);
        return percent.ToString() + "%";
    }

    /// <summary>
    /// Gets the probability text formatted with hover preview.
    /// Format: [currentPercentage] [+/-] [changePercentage] (Green for +, Red for -)
    /// </summary>
    private string GetProbabilityTextWithHover(Tower.ID towerId)
    {
        if (TowerShopManager.instance == null || _hoveredProbabilityPreview == null)
            return GetProbabilityText(towerId);

        // Get current probability
        if (!TowerShopManager.instance.TryGetTowerProbability(towerId, out float currentProbability))
            currentProbability = 0f;

        // Get hovered probability
        if (!_hoveredProbabilityPreview.TryGetValue(towerId, out float hoveredProbability))
            hoveredProbability = currentProbability;

        int currentPercent = Mathf.RoundToInt(Mathf.Clamp01(currentProbability) * 100f);
        int hoveredPercent = Mathf.RoundToInt(Mathf.Clamp01(hoveredProbability) * 100f);
        int changePercent = hoveredPercent - currentPercent;

        // Format: [currentPercent] [+/-][changePercent]
        // Only color the change part, keep current percent at normal size
        if (changePercent > 0)
        {
            string sign = "+";
            string changeText = $"{sign}{changePercent}%";
            if (CM.i != null)
                changeText = CM.i.RTC(CM.ColorType.Green, changeText);
            return $"{currentPercent}{changeText}";
        }
        else if (changePercent < 0)
        {
            string sign = "";
            string changeText = $"{sign}{changePercent}%";
            if (CM.i != null)
                changeText = CM.i.RTC(CM.ColorType.Red, changeText);
            return $"{currentPercent}{changeText}";
        }
        else
        {
            return $"{currentPercent}%";
        }
    }

    private void EnsureTowerManagerReference()
    {
        if (towerManager == null)
            towerManager = TowerManager.instance != null ? TowerManager.instance : FindFirstObjectByType<TowerManager>();
    }

    private void HandleTowerPurchasedFromShop(Tower tower)
    {
        if (tower == null)
            return;

        RefreshPurchasedTowerState(tower.id);
        
        // Update probabilities and tag visuals since shop changed
        RefreshDisplayedProbabilities();
        RefreshTagMatchingVisuals();
    }



    private static List<GameObject> GetUniqueTowerPrefabs(List<GameObject> prefabs)
    {
        List<GameObject> uniquePrefabs = new List<GameObject>();
        HashSet<Tower.ID> seenIds = new HashSet<Tower.ID>();

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null) continue;

            Tower tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;

            if (seenIds.Add(tower.id))
            {
                uniquePrefabs.Add(prefab);
            }
        }

        return uniquePrefabs;

    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        ClearDisplayTowers();
    }

}
