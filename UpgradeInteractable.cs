using UnityEngine;

public class UpgradeInteractable : Interactable
{
    private UpgradeItem _upgradeItem;
    public bool pickupable = true;

    public UpgradeItem GetUpgradeItem() => _upgradeItem;

    public override void Awake()
    {
        base.Awake();
        if (_upgradeItem == null) _upgradeItem = GetComponent<UpgradeItem>();
    }

    public override string GetCursorToolTipText()
    {
        if (_upgradeItem == null) _upgradeItem = GetComponent<UpgradeItem>();
        if (_upgradeItem == null) return string.Empty;
        return UpgradeData.GetUpgradeDescription(_upgradeItem.uid) ?? _upgradeItem.uid.ToString();
    }
}
