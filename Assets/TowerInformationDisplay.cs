using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TowerInformationDisplay : MonoBehaviour
{
    public Vector3 topLeft = new Vector3(-6.42f, 2.78f, 0);
    public Vector3 topRight = new Vector3(-4.37f, 2.78f, 0);
    public Vector3 bottomRight = new Vector3(-4.37f, -3.75f, 0);
    public Vector3 bottomLeft = new Vector3(-6.42f, -3.75f, 0);

    public static TowerInformationDisplay instance;
    public KeyCode RotateRightKey;
    public KeyCode RotateLeftKey;
    public GameObject towerInformationPanel;
    public RecipeDisplay towerInformationRecipeDisplay;
    public List<RecipeDisplay> towerInformationRecipeDisplays;
    [SerializeField] public List<UnityEngine.UI.Image> tagImages;
    public TextMeshProUGUI towerNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI rangeText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI upgradeDescriptionText;
    public TextMeshProUGUI criticalHitText;
    public TextMeshProUGUI damageTypeText;
    public TextMeshProUGUI upgradeLabelText;
    public TowerInteractable currentTowerInteractable;
    public TMP_Dropdown targettingDropdown;
    public List<ShopButton> upgradeButtons;
    [Header("Buttons")]
    [Tooltip("Sell button root object to show/hide based on tower state.")]
    [SerializeField] private GameObject sellButton; 
    public TextMeshProUGUI sellButtonText;

    [Header("Recipe Display")]
    public bool towerInformationDisplayRecipeDisplayEnabled = true;

    [Header("Prefixes")]
    [SerializeField] private string namePrefix = "";
    [SerializeField] private string descriptionPrefix = "";
    [SerializeField] private string damagePrefix = "Damage: ";
    [SerializeField] private string rangePrefix = "Range: ";
    [SerializeField] private string cooldownPrefix = "Cooldown: ";
    [SerializeField] private string upgradeDescriptionPrefix = "Upgrade ";
    public bool showUnseenUpgradeDescriptions = false;

    private RectTransform panelRect;
    private RectTransform _canvasRect;
    private Outline panelOutline;
    public bool displayed = false;
    private Vector2 recipeDisplayPosition;


    public List<GameObject> toolTipPanels;
    public List<TextMeshProUGUI> toolTipTexts;

    [Header("Tooltips")]
    [SerializeField] private float toolTipPanelPaddingX = 24f;
    [SerializeField] private float toolTipPanelPaddingY = 12f;
    public LayerMask uiLayerMask;
    private bool playerMouseHovering;

    

    public void OnPlayerMouseEnter()
    {
        playerMouseHovering = true;
    }

    public void OnPlayerMouseExit()
    {
        playerMouseHovering = false;
    }
    public bool PlayerMouseHovering()
    {
        return playerMouseHovering;
        //Collider2D[] hit = Physics2D.OverlapPointAll(Input.mousePosition, uiLayerMask);
        //for (int i = 0; i < hit.Length; i++)
        //{
        //    Debug.Log(hit[i].gameObject.name);
        //    if (hit[i].gameObject.CompareTag("TID")) return true;
        //    //if (hit[i].gameObject == towerInformationPanel) return true;
        //}
        //return false;
    }

    private void Update()
    {
        if (!displayed) return;
        if (!IsDisplayedTowerValid())
        {
            HideTowerInformation();
            return;
        }

        if (towerInformationPanel == null || !towerInformationPanel.activeSelf) return;
        var t = currentTowerInteractable.GetTower();

        if (damageText != null)
        {
            bool showDamage = t != null && t.showDamageInfo;
            damageText.gameObject.SetActive(showDamage);
            damageText.text = showDamage ? BuildDamageText(t) : string.Empty;
        }
        if (rangeText != null)
        {
            bool showRange = t != null && t.showRangeInfo;
            rangeText.gameObject.SetActive(showRange);
            if (showRange)
            {
                float displayedRange = t.GetRange();
                if (t is AOETower aoeTower && aoeTower.mode == AOETower.Mode.OnTower)
                {
                    displayedRange = aoeTower.GetBlastRadius();
                }
                    rangeText.text = GetRangePrefix(t) + displayedRange.ToString("0.##");
            }
            else
            {
                rangeText.text = string.Empty;
            }
        }
        if (cooldownText != null)
        {
            bool showCooldown = t != null && t.showCooldownInfo;
            cooldownText.gameObject.SetActive(showCooldown);
                cooldownText.text = showCooldown ? GetCooldownPrefix(t) + t.GetCooldownPreview().ToString("0.##") : string.Empty;
        }
        if (sellButtonText != null) sellButtonText.text = CM.i.RTC(CM.ColorType.Red, "$" + t.GetSellCost().ToString());

        RefreshCriticalHitText(t);
    }

    private bool IsDisplayedTowerValid()
    {
        if (currentTowerInteractable == null) return false;
        if (!currentTowerInteractable.isActiveAndEnabled) return false;

        Tower tower = currentTowerInteractable.GetTower();
        if (tower == null) return false;
        if (!tower.isActiveAndEnabled) return false;

        return true;
    }

    public void OnTargettingModeChanged()
    {
        if (currentTowerInteractable == null) return;
        var t = currentTowerInteractable.GetTower();
        if (t == null) return;
        RangeManager rangeManager = t.GetRangeManager();
        if (rangeManager == null) return;
        int selectedIndex = targettingDropdown.value;
        // Build the same filtered list used when displaying, so indices match.
        var filteredModes = BuildFilteredModeNames(rangeManager);
        if (filteredModes.Count <= 1) return;
        if (selectedIndex < 0 || selectedIndex >= filteredModes.Count) return;
        var mode = (RangeManager.TargettingMode)System.Enum.Parse(typeof(RangeManager.TargettingMode), filteredModes[selectedIndex]);
        rangeManager.SetTargettingMode(mode);
    }

    private static List<string> BuildFilteredModeNames(RangeManager rangeManager)
    {
        var allValues = (RangeManager.TargettingMode[])System.Enum.GetValues(typeof(RangeManager.TargettingMode));
        var result = new List<string>(allValues.Length);
        foreach (var mode in allValues)
        {
            if (!rangeManager.IsModeAvailable(mode)) continue;
            result.Add(mode.ToString());
        }
        return result;
    }
    private void Awake()
    {
        instance = this;
        if (toolTipPanels.Count != toolTipTexts.Count)
        {
            Debug.LogError("TOOL TIP COUNT MISMATCH");
        }
        // Dropdown is populated per-tower in DisplayTowerInformation (options vary based on manualTargettingEnabled).

        if (towerInformationPanel != null)
        {
            panelRect = towerInformationPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                // Register the panel's authored starting position for recipe tree tower inspection.
                recipeDisplayPosition = panelRect.anchoredPosition;
            }

            panelOutline = towerInformationPanel.GetComponent<Outline>();
            towerInformationPanel.SetActive(false);
        }

        HideTowerRecipeDisplay();
        
    }

    public void OnUpgradeButtonPressed(int upgradeIndex)
    {
        if (currentTowerInteractable == null) return;
        var t = currentTowerInteractable.GetTower();
        if (t == null) return;

        int currentLevel = t.GetUpgradeLevel();
        var possibleUpgrades = UpgradeData.GetUpgradesForTower(t.id);
        if (possibleUpgrades == null || currentLevel >= possibleUpgrades.Count) return;

        var upgradesAtLevel = possibleUpgrades[currentLevel];
        if (upgradesAtLevel == null || upgradesAtLevel.upgrades == null || upgradeIndex < 0 || upgradeIndex >= upgradesAtLevel.upgrades.Count) return;

        UpgradeData.UID chosenUID = upgradesAtLevel.upgrades[upgradeIndex];

        int cost = upgradesAtLevel.GetCost(upgradeIndex);

        if (CurrencyManager.instance == null) return;
        if (CurrencyManager.instance.GetCurrency() < cost) return;

        CurrencyManager.instance.RemoveCurrency(cost);
        t.ApplyUpgrade(chosenUID);

        PIC.instance.pickupDropVfx.transform.position = t.transform.position;
        PIC.instance.pickupDropVfx.Play();
        DisplayTowerInformation(currentTowerInteractable);
    }

    public void OnSellButtonPressed()
    {
        if (currentTowerInteractable == null) return;
        var t = currentTowerInteractable.GetTower();
        if (t == null) return;
        // Refund half of the tower's cost.
        int refundAmount = t.GetSellCost();
        CurrencyManager.instance.AddCurrency(refundAmount);
        TowerManager.instance.OnTowerDestroyed(currentTowerInteractable.GetTower());
        Destroy(t.gameObject);      
        displayed = false;
        HideTowerInformation();
    }

    public void DisplayTowerInformation(TowerInteractable towerInteractable)
    {
        //if (displayed ) return; // Already displaying this tower's info.) 
        displayed = true;
        currentTowerInteractable = towerInteractable;

        if (towerInteractable == null)
        {
            HideTowerInformation();
            return;
        }

        bool isRecipeTreeTower = IsRecipeTreeTower(towerInteractable);

        // Ensure only one info panel is visible.
        if (EnemyInformationDisplay.instance != null)
        {
            EnemyInformationDisplay.instance.HideEnemyInformation();
        }

        if (towerInformationPanel != null)
        {
            towerInformationPanel.SetActive(false);
            if (isRecipeTreeTower)
            {
                PositionPanelAtRecipeDisplayPosition();
            }
            else
            {
                PositionPanelInOppositeQuadrant(towerInteractable.transform.position);
            }
        }

        Tower t = towerInteractable.GetTower();

        if (towerNameText != null)
        {
            string customName = towerInteractable.GetName();
            string n = !string.IsNullOrEmpty(customName)
                ? customName
                : (t != null ? t.name : string.Empty);
            towerNameText.text = (namePrefix ?? string.Empty) + (n ?? string.Empty);
        }

        if (descriptionText != null)
        {
            string d = towerInteractable.GetDescription() ?? string.Empty;
            descriptionText.text = (descriptionPrefix ?? string.Empty) + d;
        }

        if (t != null)
        {
            if (towerInformationDisplayRecipeDisplayEnabled)
            {
                ShowTowerRecipeDisplay(t);
            }
            else
            {
                HideTowerRecipeDisplay();
            }

            if (sellButton != null) sellButton.SetActive(t.CurrentState != Tower.State.Shop);
            bool showUpgrades = t.CurrentState != Tower.State.Shop;
            RefreshUpgradeButtons(t, showUpgrades);

            if (damageText != null)
            {
                bool showDamage = t.showDamageInfo;
                damageText.gameObject.SetActive(showDamage);
                damageText.text = showDamage ? BuildDamageText(t) : string.Empty;
            }
            if (rangeText != null)
            {
                bool showRange = t.showRangeInfo;
                rangeText.gameObject.SetActive(showRange);
                if (showRange)
                {
                    float displayedRange = t.GetRange();
                    if (t is AOETower aoeTower && aoeTower.mode == AOETower.Mode.OnTower)
                    {
                        displayedRange = aoeTower.GetBlastRadius();
                    }
                    rangeText.text = GetRangePrefix(t) + displayedRange.ToString("0.##");
                }
                else
                {
                    rangeText.text = string.Empty;
                }
            }
            if (cooldownText != null)
            {
                bool showCooldown = t.showCooldownInfo;
                cooldownText.gameObject.SetActive(showCooldown);
                cooldownText.text = showCooldown ? GetCooldownPrefix(t) + t.GetCooldownPreview().ToString("0.##") : string.Empty;
            }
            RefreshCriticalHitText(t);
            if (t.tags != null && t.tags.Count >0) DisplayTowerTags(t.tags);
            else HideTowerTags();

            if (targettingDropdown != null)
            {
                if (t.CurrentState == Tower.State.Placed)
                {
                    var rm = t.GetRangeManager();
                    var filteredModes = BuildFilteredModeNames(rm);
                    targettingDropdown.ClearOptions();
                    targettingDropdown.AddOptions(filteredModes);
                    int index = filteredModes.IndexOf(rm.GetTargettingMode().ToString());
                    targettingDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
                    targettingDropdown.gameObject.SetActive(filteredModes.Count > 1);
                }
                else
                {
                    targettingDropdown.gameObject.SetActive(false);
                }
            }

            int towerLevel = t.GetUpgradeLevel();
            int numLevels = t.GetNumberOfLevels();
            sellButtonText.text = CM.i.RTC(CM.ColorType.Red, "$" + t.GetSellCost().ToString());
            bool isPlacedTower = t.CurrentState == Tower.State.Placed;
            upgradeLabelText.gameObject.SetActive(false);
            if (towerLevel < numLevels)
            {
                var possibleUpgrades = UpgradeData.GetUpgradesForTower(t.id);
                var ul = possibleUpgrades[towerLevel];
                if (ul != null && ul.upgrades != null && ul.upgrades.Count > 0)
                {
                    upgradeLabelText.gameObject.SetActive(isPlacedTower);
                    string towerUpgradeDescription = t.GetUpgradeDescription(ul.upgrades[0]);
                    upgradeDescriptionText.text = upgradeDescriptionPrefix + (towerLevel + 1).ToString() + ": " + towerUpgradeDescription;
                }
                else
                {
                    upgradeDescriptionText.text = string.Empty;
                }
            }
            else
            {
                upgradeDescriptionText.text = string.Empty;
            }

            var configuredDamageTypes = t.GetConfiguredDamageTypes();
            if (configuredDamageTypes.Count <= 0)
            {
                damageTypeText.text = "Damage Type: None";
            }
            else if (configuredDamageTypes.Count == 1)
            {
                damageTypeText.text = "Damage Type: " + CM.i.RTC(configuredDamageTypes[0]);
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append("Damage Type: Random (");
                for (int i = 0; i < configuredDamageTypes.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(CM.i.RTC(configuredDamageTypes[i]));
                }
                sb.Append(")");
                damageTypeText.text = sb.ToString();
            }
            var tooltipDescriptions = TowerInteractable.TowerDescriptionUtility.GetTowerTooltipDescriptions(t);
            int tooltipIndex = 0;
            for (; tooltipIndex < tooltipDescriptions.Count && tooltipIndex < toolTipPanels.Count; tooltipIndex++)
            {
                toolTipPanels[tooltipIndex].SetActive(true);
                toolTipTexts[tooltipIndex].text = tooltipDescriptions[tooltipIndex];
            }

            for (int i = tooltipIndex; i < toolTipPanels.Count; i++)
            {
                toolTipPanels[i].SetActive(false);
            }
        }
        else
        {
            HideTowerRecipeDisplay();

            if (sellButton != null) sellButton.SetActive(false);
            if (damageText != null)
            {
                damageText.gameObject.SetActive(true);
                damageText.text = string.Empty;
            }
            if (rangeText != null)
            {
                rangeText.gameObject.SetActive(true);
                rangeText.text = string.Empty;
            }
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = string.Empty;
            }
            HideTowerTags();

            foreach (var btn in upgradeButtons) { if (btn != null) btn.gameObject.SetActive(false); }
        }

        if (panelOutline != null && CM.i != null && t != null)
        {
            panelOutline.effectColor = CM.i.ColorTypeToColor(RarityToColorType(TowerManager.GetTowerRarity(t.id)));
        }

        if (towerInformationPanel != null)
        {
            towerInformationPanel.SetActive(true);
        }

        displayed = true;
    }

    private static CM.ColorType RarityToColorType(Tower.Rarity rarity)
    {
        return rarity switch
        {
            Tower.Rarity.Legendary => CM.ColorType.RarityTier3,
            Tower.Rarity.Rare      => CM.ColorType.RarityTier2,
            _                      => CM.ColorType.RarityTier1,
        };
    }

    private static bool IsRecipeTreeTower(TowerInteractable towerInteractable)
    {
        if (towerInteractable == null) return false;

        string interactableName = towerInteractable.gameObject != null ? towerInteractable.gameObject.name : string.Empty;
        if (!string.IsNullOrEmpty(interactableName) && interactableName.StartsWith("RecipeTree_"))
        {
            return true;
        }

        Tower tower = towerInteractable.GetTower();
        if (tower == null || tower.gameObject == null) return false;

        string towerName = tower.gameObject.name;
        return !string.IsNullOrEmpty(towerName) && towerName.StartsWith("RecipeTree_");
    }

    private void RefreshUpgradeButtons(Tower t, bool visible)
    {
        int towerLevel = t.GetUpgradeLevel();
        int numLevels = t.GetNumberOfLevels();

        // Collect the choices available at the current level
        List<UpgradeData.UID> currentChoices = null;
        var possibleUpgrades = UpgradeData.GetUpgradesForTower(t.id);
        if (visible && towerLevel < numLevels && possibleUpgrades != null && towerLevel < possibleUpgrades.Count)
        {
            var ul = possibleUpgrades[towerLevel];
            if (ul != null && ul.upgrades != null && ul.upgrades.Count > 0)
                currentChoices = ul.upgrades;
        }

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            var btn = upgradeButtons[i];
            if (btn == null) continue;

            if (!visible || currentChoices == null || i >= currentChoices.Count)
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            btn.gameObject.SetActive(true);

            UpgradeData.UID uid = currentChoices[i];
            btn.SetItemCost(possibleUpgrades[towerLevel].GetCost(i));
            btn.SetDescription(t.GetUpgradeDescription(uid));
            btn.SetEligible(true);
        }
    }

    public void DisplayTowerTags(List<Tower.Tag> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            tagImages[i].enabled = true;
            tagImages[i].color = CM.i.TagToColor(tags[i]);
        }
        for (int i =0; i < tagImages.Count - tags.Count; i++)
        {
            tagImages[tagImages.Count -1 - i].enabled = false;
        }
    }

    public void HideTowerTags()
    {
        foreach (var img in tagImages)
        {
            img.enabled = false;
        }
    }

    public void HideTowerInformation()
    {
        playerMouseHovering = false;
        currentTowerInteractable = null;
        displayed = false;
        ClearPanelContent();
        HideTowerRecipeDisplay();
        if (towerInformationPanel == null) return;
        towerInformationPanel.SetActive(false);
    }

    private void ClearPanelContent()
    {
        if (towerNameText != null) towerNameText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (damageText != null)
        {
            damageText.gameObject.SetActive(true);
            damageText.text = string.Empty;
        }
        if (rangeText != null)
        {
            rangeText.gameObject.SetActive(true);
            rangeText.text = string.Empty;
        }
        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(true);
            cooldownText.text = string.Empty;
        }
        if (upgradeDescriptionText != null) upgradeDescriptionText.text = string.Empty;
        if (damageTypeText != null) damageTypeText.text = string.Empty;
        if (sellButtonText != null) sellButtonText.text = string.Empty;
        if (upgradeLabelText != null) upgradeLabelText.gameObject.SetActive(false);
        if (criticalHitText != null) criticalHitText.gameObject.SetActive(false);
        if (sellButton != null) sellButton.SetActive(false);
        if (targettingDropdown != null) targettingDropdown.gameObject.SetActive(false);

        HideTowerTags();

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            var btn = upgradeButtons[i];
            if (btn == null) continue;
            btn.gameObject.SetActive(false);
        }

        for (int i = 0; i < toolTipPanels.Count; i++)
        {
            if (toolTipPanels[i] != null) toolTipPanels[i].SetActive(false);
        }
    }

    private void ShowTowerRecipeDisplay(Tower tower)
    {
        HideTowerRecipeDisplay();

        if (!towerInformationDisplayRecipeDisplayEnabled)
        {
            return;
        }

        if (tower == null || RecipeManager.instance == null)
        {
            return;
        }

        if (tower.CurrentState == Tower.State.Shop)
        {
            return;
        }

        var recipeDictionary = RecipeManager.instance.RecipeDictionary;
        if (recipeDictionary == null || recipeDictionary.Count == 0)
        {
            return;
        }

        List<RecipeManager.Recipe> matchingRecipes = new List<RecipeManager.Recipe>();
        foreach (var kvp in recipeDictionary)
        {
            Tower.ID resultTowerId = kvp.Key;
            List<Tower.ID> required = kvp.Value;
            if (required == null || required.Count == 0) continue;

            bool containsTower = resultTowerId == tower.id;
            if (!containsTower)
            {
                for (int i = 0; i < required.Count; i++)
                {
                    if (required[i] == tower.id)
                    {
                        containsTower = true;
                        break;
                    }
                }
            }

            if (!containsTower) continue;

            var recipe = new RecipeManager.Recipe
            {
                resultTower = resultTowerId,
                requiredTowers = new List<Tower.ID>(required)
            };

            if (!CanRenderRecipe(recipe)) continue;

            matchingRecipes.Add(recipe);
        }

        if (matchingRecipes.Count == 0)
        {
            return;
        }

        matchingRecipes.Sort((a, b) =>
        {
            bool aResultMatch = a.resultTower == tower.id;
            bool bResultMatch = b.resultTower == tower.id;
            if (aResultMatch != bResultMatch)
            {
                return aResultMatch ? -1 : 1;
            }

            return a.resultTower.CompareTo(b.resultTower);
        });

        List<RecipeDisplay> displays = new List<RecipeDisplay>();
        if (towerInformationRecipeDisplays != null && towerInformationRecipeDisplays.Count > 0)
        {
            for (int i = 0; i < towerInformationRecipeDisplays.Count; i++)
            {
                RecipeDisplay display = towerInformationRecipeDisplays[i];
                if (display == null) continue;
                displays.Add(display);
            }
        }
        else if (towerInformationRecipeDisplay != null)
        {
            displays.Add(towerInformationRecipeDisplay);
        }

        if (displays.Count == 0)
        {
            return;
        }

        int displayCount = Mathf.Min(displays.Count, matchingRecipes.Count);
        for (int i = 0; i < displayCount; i++)
        {
            var display = displays[i];
            if (display == null) continue;
            display.gameObject.SetActive(true);
            display.DisplayRecipe(matchingRecipes[i], allowInteraction: false, highlightedTowerId: tower.id);
        }
    }

    private static bool CanRenderRecipe(RecipeManager.Recipe recipe)
    {
        if (TowerManager.instance == null) return false;
        var prefabs = TowerManager.instance.towerPrefabDictionary;
        if (prefabs == null) return false;

        if (!prefabs.TryGetValue(recipe.resultTower, out var resultPrefab) || resultPrefab == null)
        {
            return false;
        }

        if (recipe.requiredTowers == null || recipe.requiredTowers.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < recipe.requiredTowers.Count; i++)
        {
            Tower.ID id = recipe.requiredTowers[i];
            if (!prefabs.TryGetValue(id, out var ingredientPrefab) || ingredientPrefab == null)
            {
                return false;
            }
        }

        return true;
    }

    private void HideTowerRecipeDisplay()
    {
        if (towerInformationRecipeDisplay != null)
        {
            towerInformationRecipeDisplay.gameObject.SetActive(false);
        }

        if (towerInformationRecipeDisplays == null) return;

        for (int i = 0; i < towerInformationRecipeDisplays.Count; i++)
        {
            RecipeDisplay display = towerInformationRecipeDisplays[i];
            if (display == null) continue;
            display.gameObject.SetActive(false);
        }
    }

    private void PositionPanelInOppositeQuadrant(Vector3 towerWorldPos)
    {
        if (panelRect == null) return;

        bool towerOnLeft;
        bool towerOnTop;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(towerWorldPos);
            towerOnLeft = screenPoint.x < (Screen.width * 0.5f);
            towerOnTop = screenPoint.y >= (Screen.height * 0.5f);
        }
        else
        {
            // Fallback when no camera is available.
            towerOnLeft = towerWorldPos.x < 0f;
            towerOnTop = towerWorldPos.y >= 0f;
        }

        Vector3 target;
        if (towerOnLeft && towerOnTop)
        {
            target = bottomRight;
        }
        else if (!towerOnLeft && towerOnTop)
        {
            target = bottomLeft;
        }
        else if (!towerOnLeft && !towerOnTop)
        {
            target = topLeft;
        }
        else
        {
            target = topRight;
        }

        panelRect.anchoredPosition = new Vector2(target.x, target.y);
    }

    private void PositionPanelAtRecipeDisplayPosition()
    {
        if (panelRect == null) return;
        panelRect.anchoredPosition = recipeDisplayPosition;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    private string BuildDamageText(Tower t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        string prefix = GetDamagePrefix(t);
        string damageValue = t.GetDamage(rollCrit: false).ToString("0.##");
        CM.ColorType damageType = t.GetDamageType(t.towerDamageData);

        if (CM.i == null)
        {
            return prefix + damageValue;
        }

        return prefix + CM.i.RTC(damageType, damageValue);
    }

    private string GetDamagePrefix(Tower tower)
    {
        if (tower != null && !string.IsNullOrEmpty(tower.damageInfoPrefixOverride))
        {
            return tower.damageInfoPrefixOverride;
        }

        return damagePrefix ?? string.Empty;
    }

    private string GetRangePrefix(Tower tower)
    {
        if (tower != null && !string.IsNullOrEmpty(tower.rangeInfoPrefixOverride))
        {
            return tower.rangeInfoPrefixOverride;
        }

        return rangePrefix ?? string.Empty;
    }

    private string GetCooldownPrefix(Tower tower)
    {
        if (tower != null && !string.IsNullOrEmpty(tower.cooldownInfoPrefixOverride))
        {
            return tower.cooldownInfoPrefixOverride;
        }

        return cooldownPrefix ?? string.Empty;
    }

    private void RefreshCriticalHitText(Tower t)
    {
        if (criticalHitText == null) return;
        if (t == null)
        {
            criticalHitText.gameObject.SetActive(false);
            return;
        }

        float critChance = t.GetCriticalChance();
        if (critChance <= 0f)
        {
            criticalHitText.gameObject.SetActive(false);
            return;
        }

        criticalHitText.gameObject.SetActive(true);
        criticalHitText.text = "Crit Chance: " + (critChance * 100f).ToString("0.##") + "%";
    }
}

