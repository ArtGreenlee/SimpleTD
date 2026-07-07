using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RelicInformationDisplay : MonoBehaviour
{
    public static RelicInformationDisplay instance;

    public GameObject relicInformationPanel;
    public TextMeshProUGUI relicNameText;
    public TextMeshProUGUI relicDescriptionText;
    public TextMeshProUGUI relicCostText;
    public TextMeshProUGUI relicRarityText;
    public TextMeshProUGUI relicUnlockText;
    [HideInInspector] public Relic displayedRelic;
    public Image lockIcon;

    private PanelFollower panelFollower;
    private Outline panelOutline;
    private bool panelHideInProgress;
    private bool playerMouseHovering;

    private void Awake()
    {
        instance = this;
        if (relicInformationPanel != null)
        {
            panelFollower = relicInformationPanel.GetComponent<PanelFollower>();
            panelOutline = relicInformationPanel.GetComponent<Outline>();
            // Relic info panel should not animate with swell.
            var panelSwell = relicInformationPanel.GetComponent<SwellAnimation>();
            if (panelSwell != null)
            {
                panelSwell.swellInOnEnable = false;
                panelSwell.enabled = false;
            }
            relicInformationPanel.SetActive(false);
        }
    }

    public string GetRarityString(int r)
    {
        if (CM.i == null) return "Unknown";

        switch (r)
        {
            case 0:
            case 1:
                return CM.i.RTC(CM.ColorType.RarityTier1, "Common");
            case 2:
                return CM.i.RTC(CM.ColorType.RarityTier2, "Uncommon");
            case 3:
                return CM.i.RTC(CM.ColorType.RarityTier3, "Rare");
            default:
                return CM.i.RTC(CM.ColorType.RarityTier1, "Unknown");
        }
    }

    private void Update()
    {
        if (displayedRelic == null)
        {
            if (relicInformationPanel != null && relicInformationPanel.activeSelf && !panelHideInProgress)
            {
                HideRelicInformation();
            }
            return;
        }

        if (panelFollower != null)
        {
            panelFollower.SetFollowTransform(displayedRelic.transform);
        }
    }

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
    }

    public void DisplayRelicInformation(Relic relicInteractable)
    {
        if (relicInteractable == null)
        {
            HideRelicInformation();
            return;
        }

        panelHideInProgress = false;

        displayedRelic = relicInteractable;

        if (RM.i != null)
        {
            if (relicNameText != null)
            {
                relicNameText.text = RM.i.GetName(relicInteractable.id);
            }
            if (relicDescriptionText != null)
            {
                relicDescriptionText.text = RM.i.GetDescription(relicInteractable.id);
            }
            if (relicCostText != null)
            {
                relicCostText.text = "$" + RM.i.GetRelicCost(relicInteractable.id).ToString();
            }
            if (relicRarityText != null)
            {
                relicRarityText.text = GetRarityString(relicInteractable.rarity);
            }
            if (relicUnlockText != null)
            {
                relicUnlockText.text = RM.i.GetRelicUnlockDescription(relicInteractable.id);
            }

            if (panelOutline != null && CM.i != null)
            {
                CM.ColorType rarityColorType = RM.i.GetRelicRarityColorType(relicInteractable.rarity);
                panelOutline.effectColor = CM.i.ColorTypeToColor(rarityColorType);
            }
            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(RM.i.IsUnlocked(relicInteractable.id));
            }
        }

        if (relicInformationPanel != null)
        {
            if (!relicInformationPanel.activeSelf)
            {
                relicInformationPanel.SetActive(true);
            }

            if (panelFollower != null)
            {
                panelFollower.SetFollowTransform(relicInteractable.transform);
                panelFollower.SnapToFollowTarget();
            }
            else
            {
                PositionPanelNearRelic(relicInteractable.transform.position);
            }
        }

    }

    public void HideRelicInformation()
    {
        playerMouseHovering = false;
        displayedRelic = null;

        if (panelFollower != null)
        {
            panelFollower.SetFollowTransform(null);
        }

        if (relicInformationPanel == null || !relicInformationPanel.activeSelf)
        {
            ClearDisplayedRelicData();
            panelHideInProgress = false;
            return;
        }

        relicInformationPanel.SetActive(false);
        ClearDisplayedRelicData();
        panelHideInProgress = false;
    }

    private void ClearDisplayedRelicData()
    {
        if (relicNameText != null) relicNameText.text = string.Empty;
        if (relicDescriptionText != null) relicDescriptionText.text = string.Empty;
        if (relicCostText != null) relicCostText.text = string.Empty;
        if (relicRarityText != null) relicRarityText.text = string.Empty;
        if (relicUnlockText != null) relicUnlockText.text = string.Empty;
        if (lockIcon != null) lockIcon.gameObject.SetActive(false);
    }

    private void PositionPanelNearRelic(Vector3 relicWorldPos)
    {
        // InformationPanelPositioner.PositionPanel(_panelRect, _canvasRect, canvas, relicWorldPos, worldCamera);
    }

}
