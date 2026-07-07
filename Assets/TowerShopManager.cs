using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.ObjectModel;

public class TowerShopManager : MonoBehaviour
{
    public static TowerShopManager instance;
    [Header("Mouse State")]
    [Tooltip("True while the pointer is inside the tower shop UI area.")]
    public bool isMouseInsideTowerShop;

    [Header("Shop Slots")]
    [Tooltip("TowerSlot components for each shop slot. Each TowerSlot's transform is used as the spawn point.")]
    public List<TowerSlot> shopSlots;

    private static readonly int[] UnlockCosts = { 10, 100, 200, 300, 500 };

    [Header("Source")]
    [Tooltip("If null, will use TowerManager.instance.")]
    [SerializeField] private TowerManager towerManager;

    [Header("Shop Filter")]
    // Removed allowedTowerIds and debugAllowedTowerIds public lists

    [Header("Probability-Based Selection")]
    [Tooltip("When enabled, tower selection uses weighted probabilities from ProbabilityDisplay percentages instead of uniform random.")]
    public bool useProbabilities = false;

    [Header("Debug")]
    [Tooltip("If true, uses debugAllowedTowerIds instead of allowedTowerIds to populate shop slots.")]
    public bool debug = false;
    // debugAllowedTowerIds is now private and set at runtime
    private List<Tower.ID> debugAllowedTowerIds = new List<Tower.ID>();
    [Tooltip("When debug is enabled, these tower IDs are forced into shop slots first (in order), then remaining slots are random.")]
    [SerializeField] private List<Tower.ID> priorityTowerIds = new List<Tower.ID>();

    private readonly List<GameObject> _shopTowerPrefabs = new List<GameObject>(16);
    private readonly Dictionary<Tower.ID, float> _towerProbabilityById = new Dictionary<Tower.ID, float>(64);
    private ReadOnlyDictionary<Tower.ID, float> _readOnlyTowerProbabilityById;
    
    // Track tag state to detect when active tags change
    private readonly Dictionary<Tower.Tag, int> _lastTagCounts = new Dictionary<Tower.Tag, int>();
    private bool _tagStateInitialized = false;

    public KeyCode refreshTowerKeyCode;
    [SerializeField] private Button refreshShopButton;
    public TextMeshProUGUI refreshShopCostText;
    public int refreshShopCost = 2;
    public int refreshShopCostIncrement = 2;
    [Header("Wave Refresh")]
    [SerializeField] private bool freeRefreshAfterEachWave = false;

    private bool _wasWaveActive;
    private bool _hasFreeRefreshAvailable;

    private static readonly HashSet<Tower.ID> PlaceholderIds = BuildPlaceholderIds();

    private static HashSet<Tower.ID> BuildPlaceholderIds()
    {
        var placeholders = new HashSet<Tower.ID>();
        foreach (Tower.ID id in System.Enum.GetValues(typeof(Tower.ID)))
        {
            string enumName = id.ToString();
            if (!string.IsNullOrEmpty(enumName) && enumName.Length == 1)
            {
                placeholders.Add(id);
            }
        }

        return placeholders;
    }

    private void Awake()
    {
        instance = this;
        _readOnlyTowerProbabilityById = new ReadOnlyDictionary<Tower.ID, float>(_towerProbabilityById);
        // Populate debugAllowedTowerIds with all non-placeholder tower IDs
        debugAllowedTowerIds.Clear();
        foreach (Tower.ID id in System.Enum.GetValues(typeof(Tower.ID)))
        {
            if (!PlaceholderIds.Contains(id))
                debugAllowedTowerIds.Add(id);
        }
    }

    public IReadOnlyDictionary<Tower.ID, float> TowerProbabilityById => _readOnlyTowerProbabilityById;

    public bool TryGetTowerProbability(Tower.ID towerId, out float probability)
    {
        return _towerProbabilityById.TryGetValue(towerId, out probability);
    }

    /// <summary>
    /// Gets the number of tags on a tower that match currently active tags.
    /// Used by ProbabilityDisplay to show which towers have tag bonuses.
    /// </summary>
    public int GetTowerTagMatchCount(Tower.ID towerId)
    {
        if (TagManager.instance == null)
            return 0;

        // Find the tower prefab to access its tags
        Tower towerComponent = null;
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null || tower.id != towerId) continue;

            towerComponent = tower;
            break;
        }

        if (towerComponent == null || towerComponent.tags == null || towerComponent.tags.Count == 0)
            return 0;

        // Count matching tags
        int tagMatches = 0;
        for (int i = 0; i < towerComponent.tags.Count; i++)
        {
            Tower.Tag tag = towerComponent.tags[i];
            if (TagManager.instance.GetTagCount(tag) > 0)
            {
                tagMatches++;
            }
        }

        return tagMatches;
    }

    /// <summary>
    /// Checks if a tower has any tag matches with currently active tags.
    /// </summary>
    public bool HasTowerTagMatches(Tower.ID towerId)
    {
        return GetTowerTagMatchCount(towerId) > 0;
    }
    private void Start()
    {
        _wasWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
        
        // Subscribe to TagManager to rebuild probabilities when towers change
        if (TagManager.instance != null)
        {
            // We'll manually call RebuildTowerProbabilityTable when needed
        }
        
        StartCoroutine(InitializeShopAfterLayout());
    }

    private void OnEnable()
    {
        // Initialize tag tracking when this component is enabled
        _tagStateInitialized = false;
    }

    private void OnDisable()
    {
        isMouseInsideTowerShop = false;
    }

    private System.Collections.IEnumerator InitializeShopAfterLayout()
    {
        yield return null;
        PopulateShopSlots();
        BuildShop();
        UpdateShopTextsAndInteractables();
        UpdateRefreshText();
    }

    private void Update()
    {
        UpdateWaveRefreshState();

        if (Input.GetKeyDown(refreshTowerKeyCode))
        {
            OnRefreshTowerButtonPressed();
        }
        
        // Detect when tag state changes and rebuild probabilities accordingly
        if (CheckIfTagStateChanged())
        {
            RebuildTowerProbabilityTable();
        }
		
		UpdateShopTextsAndInteractables();
    }

    /// <summary>
    /// Checks if any tag counts have changed since the last check.
    /// Returns true if tags changed, false otherwise.
    /// Also initializes the tag snapshot on first call.
    /// </summary>
    private bool CheckIfTagStateChanged()
    {
        if (TagManager.instance == null)
            return false;

        // Initialize tag snapshot on first call
        if (!_tagStateInitialized)
        {
            InitializeTagSnapshot();
            return true; // Consider initialization as a state change to rebuild initially
        }

        // Check if any tag count has changed
        var allTags = (Tower.Tag[])System.Enum.GetValues(typeof(Tower.Tag));
        for (int i = 0; i < allTags.Length; i++)
        {
            Tower.Tag tag = allTags[i];
            int currentCount = TagManager.instance.GetTagCount(tag);
            
            if (!_lastTagCounts.TryGetValue(tag, out int previousCount) || previousCount != currentCount)
            {
                // Tag count changed, update snapshot and return true
                _lastTagCounts[tag] = currentCount;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Initializes the tag count snapshot for change detection.
    /// </summary>
    private void InitializeTagSnapshot()
    {
        _lastTagCounts.Clear();

        if (TagManager.instance == null)
        {
            _tagStateInitialized = true;
            return;
        }

        var allTags = (Tower.Tag[])System.Enum.GetValues(typeof(Tower.Tag));
        for (int i = 0; i < allTags.Length; i++)
        {
            Tower.Tag tag = allTags[i];
            _lastTagCounts[tag] = TagManager.instance.GetTagCount(tag);
        }

        _tagStateInitialized = true;
    }

    // EventTrigger hook: call on Pointer Enter.
    public void MouseEnter()
    {
        isMouseInsideTowerShop = true;
    }

    // EventTrigger hook: call on Pointer Exit.
    public void MouseExit()
    {
        isMouseInsideTowerShop = false;
    }

    private void UpdateWaveRefreshState()
    {
        bool isWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
        if (_wasWaveActive && !isWaveActive && freeRefreshAfterEachWave)
        {
            _hasFreeRefreshAvailable = true;
            UpdateRefreshText();
        }

        _wasWaveActive = isWaveActive;
    }

    public void UpdateRefreshText() 
    {
        string costText;
        if (IsRefreshFree())
        {
            costText = CM.i != null ? CM.i.RTC(CM.ColorType.Gold, "free") : "free";
        }
        else
        {
            string paidCost = "$" + GetEffectiveRefreshCost().ToString();
            costText = CM.i != null ? CM.i.RTC(CM.ColorType.Gold, paidCost) : paidCost;
        }

        refreshShopCostText.text = "Refresh " + costText + " (" + refreshTowerKeyCode.ToString() + ")";
        UpdateRefreshButtonInteractable();
    }

    private bool IsRefreshFree()
    {
        return freeRefreshAfterEachWave && _hasFreeRefreshAvailable;
    }

    private int GetEffectiveRefreshCost()
    {
        if (RM.i != null && RM.i.Active(RM.ID.rerollDiscount))
            return Mathf.Max(1, Mathf.RoundToInt(refreshShopCost * 0.9f));
        return refreshShopCost;
    }

    public void OnRefreshTowerButtonPressed()
    {
        if (IsRefreshFree())
        {
            _hasFreeRefreshAvailable = false;
            _wasWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
            PopulateShopSlots();
            BuildShop();
            UpdateRefreshText();
            return;
        }

        int cost = GetEffectiveRefreshCost();
        if (CurrencyManager.instance != null && CurrencyManager.instance.GetCurrency() >= cost)
        {
            RefreshShop();
            refreshShopCost += refreshShopCostIncrement;
            if (SaveDataManager.instance != null) SaveDataManager.instance.NotifyReroll();
            UpdateRefreshText();
        }
        else
        {
            Debug.Log("TODO INDICATE INSUFFICIENT FUNDS");
        }
    }


    public void OnTowerPurchased(Tower tower)
    {
        if (shopSlots == null || tower == null) return;
        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null && shopSlots[i].GetTower() == tower)
            {
                shopSlots[i].ReleaseOwnership();
                break;
            }
        }
    }

    private int GetTotalShopSlotCount()
    {
        return shopSlots != null ? shopSlots.Count : 0;
    }

    private bool IsShopSlotEnabled(int slotIndex)
    {
        if (slotIndex < 0) return false;
        if (slotIndex >= GetTotalShopSlotCount()) return false;

        var slot = shopSlots[slotIndex];
        if (slot == null) return false;
        if (!slot.gameObject.activeInHierarchy) return false;

        return !slot.locked;
    }

	private int GetEnabledShopSlotCount()
	{
        int totalSlots = GetTotalShopSlotCount();
        int enabledSlots = 0;
        for (int i = 0; i < totalSlots; i++)
        {
            if (IsShopSlotEnabled(i)) enabledSlots++;
        }

		return enabledSlots;
	}

	private void UpdateShopSlotVisuals()
	{
		int totalSlots = GetTotalShopSlotCount();
		AssignUnlockCosts();
		for (int i = 0; i < totalSlots; i++)
		{
			if (shopSlots[i] == null) continue;
			shopSlots[i].SetSlotEnabled(shopSlots[i].gameObject.activeInHierarchy);
		}
	}

	private void AssignUnlockCosts()
	{
		int lockedIndex = 0;
		int totalSlots = GetTotalShopSlotCount();
		for (int i = 0; i < totalSlots; i++)
		{
			var slot = shopSlots[i];
			if (slot == null || !slot.locked) continue;
			slot.unlockCost = lockedIndex < UnlockCosts.Length ? UnlockCosts[lockedIndex] : UnlockCosts[UnlockCosts.Length - 1];
			lockedIndex++;
		}
	}

	private void UpdateShopTextsAndInteractables()
	{
		int totalSlots = GetTotalShopSlotCount();
		for (int i = 0; i < totalSlots; i++)
		{
			if (shopSlots[i] != null)
				shopSlots[i].RefreshVisuals();
		}

        UpdateRefreshButtonInteractable();
    }

    private void UpdateRefreshButtonInteractable()
    {
        if (refreshShopButton == null) return;

        if (IsRefreshFree())
        {
            refreshShopButton.interactable = true;
            return;
        }

        int currency = CurrencyManager.instance != null ? CurrencyManager.instance.GetCurrency() : 0;
        refreshShopButton.interactable = currency >= GetEffectiveRefreshCost();
    }

    private List<Tower.ID> GetAllowedShopTowerIds()
    {
        var allIds = System.Enum.GetValues(typeof(Tower.ID));
        var allowed = new List<Tower.ID>();
        var recipeDict = RecipeManager.instance != null ? RecipeManager.instance.RecipeDictionary : null;
        var placed = new HashSet<Tower.ID>();
        if (TowerManager.instance != null)
        {
            foreach (var t in TowerManager.instance.EnumeratePlacedTowers())
            {
                if (t != null) placed.Add(t.id);
            }
        }

        foreach (Tower.ID id in allIds)
        {
            if (PlaceholderIds.Contains(id)) continue;
            if (recipeDict != null && recipeDict.ContainsKey(id))
            {
                // Only allow if all ingredients are placed
                var ingredients = recipeDict[id];
                bool allPlaced = true;
                foreach (var ing in ingredients)
                {
                    if (!placed.Contains(ing)) { allPlaced = false; break; }
                }
                if (!allPlaced) continue;
            }
            allowed.Add(id);
        }
        return allowed;
    }

    public void PopulateShopSlots()
    {
        if (towerManager == null) towerManager = TowerManager.instance != null ? TowerManager.instance : FindFirstObjectByType<TowerManager>();

        _shopTowerPrefabs.Clear();
        _towerProbabilityById.Clear();
        if (towerManager == null) return;

        HashSet<Tower.ID> allowed = null;
        if (debug)
        {
            allowed = new HashSet<Tower.ID>(debugAllowedTowerIds);
            if (priorityTowerIds != null)
            {
                for (int i = 0; i < priorityTowerIds.Count; i++)
                {
                    allowed.Add(priorityTowerIds[i]);
                }
            }
        }
        else
        {
            // Not in debug, use dynamic allowed list
            allowed = new HashSet<Tower.ID>(GetAllowedShopTowerIds());
        }

        if (towerManager.towerPrefabs == null) return;
        UIAnimation.instance.IndicateShop();
        for (int i = 0; i < towerManager.towerPrefabs.Count; i++)
        {
            var prefab = towerManager.towerPrefabs[i];
            if (prefab == null) continue;

            var t = prefab.GetComponent<Tower>();
            if (t == null) continue;
            if (allowed != null && !allowed.Contains(t.id)) continue;

            _shopTowerPrefabs.Add(prefab);
        }

        RebuildTowerProbabilityTable();
    }

    /// <summary>
    /// Destroys existing shop towers and spawns new ones at the configured positions.
    /// </summary>
    public void RefreshShop()
    {
        if (IsRefreshFree())
        {
            _hasFreeRefreshAvailable = false;
            _wasWaveActive = WaveManager.instance != null && WaveManager.instance.IsWaveActive();
            PopulateShopSlots();
            BuildShop();
            UpdateRefreshText();
            return;
        }

        if (CurrencyManager.instance.GetCurrency() < GetEffectiveRefreshCost())
        {
            return;
        }
        CurrencyManager.instance.RemoveCurrency(GetEffectiveRefreshCost());
        PopulateShopSlots();
        BuildShop();
    }

    private GameObject GetTowerShopPrefab()
    {
        if (_shopTowerPrefabs.Count == 0) return null;

        if (useProbabilities)
        {
            return GetTowerShopPrefabByProbability();
        }

        return _shopTowerPrefabs[Random.Range(0, _shopTowerPrefabs.Count)];
    }

    private GameObject GetTowerShopPrefabByProbability()
    {
        if (_shopTowerPrefabs.Count == 0) return null;

        if (_towerProbabilityById.Count == 0)
        {
            RebuildTowerProbabilityTable();
        }

        float totalWeight = 0f;
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;
            if (!_towerProbabilityById.TryGetValue(tower.id, out float probability)) continue;

            totalWeight += probability;
        }

        if (totalWeight <= 0f) return _shopTowerPrefabs[0];

        float roll = Random.value * totalWeight;
        float accumulated = 0f;

        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;
            if (!_towerProbabilityById.TryGetValue(tower.id, out float probability)) continue;

            accumulated += probability;
            if (roll <= accumulated)
            {
                return prefab;
            }
        }

        return _shopTowerPrefabs[Mathf.Max(0, _shopTowerPrefabs.Count - 1)];
    }

    /// <summary>
    /// Rebuilds the tower probability table based on tag matches and rarity.
    /// - Towers matching active tags have higher probability
    /// - Legendary towers get highest base weight, Rare middle, Common lowest
    /// - Probabilities are normalized to sum to 1.0
    /// </summary>
    private void RebuildTowerProbabilityTable()
    {
        _towerProbabilityById.Clear();

        if (_shopTowerPrefabs.Count == 0) return;

        // Build list of unique tower IDs and calculate their weights
        var uniqueTowerIds = new List<Tower.ID>(_shopTowerPrefabs.Count);
        var towerWeights = new Dictionary<Tower.ID, float>();
        var seenIds = new HashSet<Tower.ID>();

        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;
            if (!seenIds.Add(tower.id)) continue;

            uniqueTowerIds.Add(tower.id);
            float weight = CalculateTowerProbabilityWeight(tower.id);
            towerWeights[tower.id] = weight;
        }

        if (uniqueTowerIds.Count == 0) return;

        // Normalize weights to probabilities (sum = 1.0)
        float totalWeight = 0f;
        foreach (var weight in towerWeights.Values)
        {
            totalWeight += weight;
        }

        if (totalWeight <= 0f) totalWeight = 1f; // Fallback to equal probability if all weights are 0

        float assignedTotal = 0f;
        for (int i = 0; i < uniqueTowerIds.Count; i++)
        {
            Tower.ID id = uniqueTowerIds[i];
            float normalizedProbability = towerWeights[id] / totalWeight;
            
            // For the last tower, use remaining probability to ensure sum = 1.0
            if (i == uniqueTowerIds.Count - 1)
            {
                normalizedProbability = Mathf.Max(0f, 1f - assignedTotal);
            }
            
            _towerProbabilityById[id] = normalizedProbability;
            assignedTotal += normalizedProbability;
        }

        // Notify ProbabilityDisplay of probability changes and tag matching
        if (ProbabilityDisplay.instance != null)
        {
            ProbabilityDisplay.instance.RefreshDisplayedProbabilities();
            ProbabilityDisplay.instance.RefreshTagMatchingVisuals();
        }
    }

    /// <summary>
    /// Calculates the probability weight for a tower based on:
    /// 1. Tag matching: towers with tags matching currently active tags get bonus
    /// 2. Rarity: Legendary > Rare > Common
    /// 
    /// Base weights by rarity:
    /// - Legendary: 3.0
    /// - Rare: 2.0
    /// - Common: 1.0
    /// 
    /// Tag match bonus: +0.5 for each tag match (multiplicative)
    /// </summary>
    private float CalculateTowerProbabilityWeight(Tower.ID towerId)
    {
        // Get base weight from rarity
        Tower.Rarity rarity = TowerManager.GetTowerRarity(towerId);
        float baseWeight = rarity switch
        {
            Tower.Rarity.Legendary => 3.0f,
            Tower.Rarity.Rare => 2.0f,
            _ => 1.0f  // Common
        };

        // Get tag match bonus
        float tagMatchMultiplier = CalculateTagMatchMultiplier(towerId);

        return baseWeight * tagMatchMultiplier;
    }

    /// <summary>
    /// Calculates a multiplier based on how many of the tower's tags match active tags.
    /// 
    /// Each matching tag adds +0.25 to the multiplier (min 1.0).
    /// A tower with all 4 tags matching would have multiplier of 2.0 (1.0 + 4 * 0.25)
    /// </summary>
    private float CalculateTagMatchMultiplier(Tower.ID towerId)
    {
        if (TagManager.instance == null)
            return 1.0f;

        // Get the tower prefab to access its tags
        Tower towerComponent = null;
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null || tower.id != towerId) continue;

            towerComponent = tower;
            break;
        }

        if (towerComponent == null || towerComponent.tags == null || towerComponent.tags.Count == 0)
            return 1.0f;

        // Count how many of the tower's tags are currently active
        int tagMatches = 0;
        for (int i = 0; i < towerComponent.tags.Count; i++)
        {
            Tower.Tag towerTag = towerComponent.tags[i];
            int activeTagCount = TagManager.instance.GetTagCount(towerTag);
            
            if (activeTagCount > 0)
            {
                tagMatches++;
            }
        }

        // Calculate multiplier: each match adds +0.25
        float multiplier = 1.0f + (tagMatches * 0.25f);
        return multiplier;
    }

    /// <summary>
    /// Calculates what the tower probabilities would be if a specific tower is purchased.
    /// This simulates the effect of adding the purchased tower's tags to the active tag pool.
    /// Used by ProbabilityDisplay to show hover previews.
    /// </summary>
    public Dictionary<Tower.ID, float> CalculateProbabilitiesIfTowerPurchased(Tower.ID purchasedTowerId)
    {
        var result = new Dictionary<Tower.ID, float>();

        if (_shopTowerPrefabs.Count == 0)
            return result;

        // Find the purchased tower's tags
        List<Tower.Tag> purchasedTowerTags = null;
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null || tower.id != purchasedTowerId) continue;

            purchasedTowerTags = tower.tags;
            break;
        }

        if (purchasedTowerTags == null || purchasedTowerTags.Count == 0)
        {
            // If tower has no tags, probabilities won't change - just return current ones
            foreach (var kvp in _towerProbabilityById)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        // Build list of unique tower IDs and calculate their weights with simulated tags added
        var uniqueTowerIds = new List<Tower.ID>(_shopTowerPrefabs.Count);
        var towerWeights = new Dictionary<Tower.ID, float>();
        var seenIds = new HashSet<Tower.ID>();

        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;
            if (!seenIds.Add(tower.id)) continue;

            uniqueTowerIds.Add(tower.id);
            float weight = CalculateTowerProbabilityWeightWithSimulatedTags(tower.id, purchasedTowerTags);
            towerWeights[tower.id] = weight;
        }

        if (uniqueTowerIds.Count == 0)
            return result;

        // Normalize weights to probabilities (sum = 1.0)
        float totalWeight = 0f;
        foreach (var weight in towerWeights.Values)
        {
            totalWeight += weight;
        }

        if (totalWeight <= 0f) totalWeight = 1f;

        float assignedTotal = 0f;
        for (int i = 0; i < uniqueTowerIds.Count; i++)
        {
            Tower.ID id = uniqueTowerIds[i];
            float normalizedProbability = towerWeights[id] / totalWeight;
            
            if (i == uniqueTowerIds.Count - 1)
            {
                normalizedProbability = Mathf.Max(0f, 1f - assignedTotal);
            }
            
            result[id] = normalizedProbability;
            assignedTotal += normalizedProbability;
        }

        return result;
    }

    /// <summary>
    /// Helper method for CalculateProbabilitiesIfTowerPurchased.
    /// Calculates weight for a tower as if additional tags are active.
    /// </summary>
    private float CalculateTowerProbabilityWeightWithSimulatedTags(Tower.ID towerId, List<Tower.Tag> simulatedTags)
    {
        Tower.Rarity rarity = TowerManager.GetTowerRarity(towerId);
        float baseWeight = rarity switch
        {
            Tower.Rarity.Legendary => 3.0f,
            Tower.Rarity.Rare => 2.0f,
            _ => 1.0f
        };

        float tagMatchMultiplier = CalculateTagMatchMultiplierWithSimulatedTags(towerId, simulatedTags);
        return baseWeight * tagMatchMultiplier;
    }

    /// <summary>
    /// Helper method for weight calculation with simulated tags.
    /// Counts matches including the simulated tags being added.
    /// </summary>
    private float CalculateTagMatchMultiplierWithSimulatedTags(Tower.ID towerId, List<Tower.Tag> simulatedTags)
    {
        if (TagManager.instance == null)
            return 1.0f;

        Tower towerComponent = null;
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            var tower = prefab.GetComponent<Tower>();
            if (tower == null || tower.id != towerId) continue;

            towerComponent = tower;
            break;
        }

        if (towerComponent == null || towerComponent.tags == null || towerComponent.tags.Count == 0)
            return 1.0f;

        // Count matches including simulated tags
        int tagMatches = 0;
        for (int i = 0; i < towerComponent.tags.Count; i++)
        {
            Tower.Tag towerTag = towerComponent.tags[i];
            int activeTagCount = TagManager.instance.GetTagCount(towerTag);
            
            // Check if this tag is also in the simulated tags being added
            bool isSimulatedTag = false;
            for (int j = 0; j < simulatedTags.Count; j++)
            {
                if (simulatedTags[j] == towerTag)
                {
                    isSimulatedTag = true;
                    break;
                }
            }

            // Tag is active if it has current count or will be active when simulated tower is added
            if (activeTagCount > 0 || isSimulatedTag)
            {
                tagMatches++;
            }
        }

        float multiplier = 1.0f + (tagMatches * 0.25f);
        return multiplier;
    }

    private List<GameObject> BuildDebugPriorityPrefabs(int slotCount)
    {
        var result = new List<GameObject>(Mathf.Max(0, slotCount));
        if (!debug || slotCount <= 0) return result;

        var prefabById = new Dictionary<Tower.ID, GameObject>();
        for (int i = 0; i < _shopTowerPrefabs.Count; i++)
        {
            GameObject prefab = _shopTowerPrefabs[i];
            if (prefab == null) continue;

            Tower tower = prefab.GetComponent<Tower>();
            if (tower == null) continue;

            if (!prefabById.ContainsKey(tower.id))
            {
                prefabById.Add(tower.id, prefab);
            }
        }

        if (priorityTowerIds != null)
        {
            var used = new HashSet<Tower.ID>();
            for (int i = 0; i < priorityTowerIds.Count && result.Count < slotCount; i++)
            {
                Tower.ID id = priorityTowerIds[i];
                if (used.Contains(id)) continue;
                if (!prefabById.TryGetValue(id, out var prefab)) continue;

                result.Add(prefab);
                used.Add(id);
            }
        }

        while (result.Count < slotCount)
        {
            result.Add(GetTowerShopPrefab());
        }

        return result;
    }

    private void BuildShop()
    {
        if (shopSlots == null || shopSlots.Count == 0) return;

        int enabledSlots = GetEnabledShopSlotCount();
        UpdateShopSlotVisuals();

        List<GameObject> debugPrefabsBySlot = debug
            ? BuildDebugPriorityPrefabs(enabledSlots)
            : null;

        // Clear each slot. Towers still in Shop state are destroyed; purchased/moved towers are detached.
        for (int i = 0; i < shopSlots.Count; i++)
        {
            var slot = shopSlots[i];
            if (slot == null) continue;

            var existingTower = slot.GetTower();
            if (existingTower != null && existingTower.CurrentState != Tower.State.Shop)
                slot.ReleaseOwnership();
            else
                slot.ClearTower();
        }

        int debugSlotCursor = 0;
        for (int i = 0; i < shopSlots.Count; i++)
        {
            var slot = shopSlots[i];
            if (slot == null) continue;

            if (IsShopSlotEnabled(i))
            {
                var prefab = debugPrefabsBySlot != null && debugSlotCursor < debugPrefabsBySlot.Count
                    ? debugPrefabsBySlot[debugSlotCursor]
                    : GetTowerShopPrefab();

                if (prefab != null)
                {
                    slot.SetTowerFromPrefab(prefab);

                    var tower = slot.GetTower();
                    if (tower != null)
                    {
                        tower.CurrentState = Tower.State.Shop;
                        if (TowerManager.instance != null) TowerManager.instance.OnTowerSpawnedInShop(tower);
                    }
                }

                debugSlotCursor++;
            }
        }

		UpdateShopTextsAndInteractables();
    }
}
