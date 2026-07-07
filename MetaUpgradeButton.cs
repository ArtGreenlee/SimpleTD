using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MetaUpgradeButton : ShopButton
{
    public Transform metaUpgradeValuePanel;
    public List<TextMeshProUGUI> metaUpgradeValueTexts;
    public List<UnityEngine.UI.Image> metaUpgradeValueImages;

    public void OnMouseEnter()
    {
        if (metaUpgradeValuePanel != null)
        {
            metaUpgradeValuePanel.gameObject.SetActive(true);
        }
    }

    public void OnMouseExit()
    {
        if (metaUpgradeValuePanel != null)
        {
            metaUpgradeValuePanel.gameObject.SetActive(false);
        }
    }
}
