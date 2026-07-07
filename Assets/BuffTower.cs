using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Direction = GridManager.Direction;
using UID = UpgradeData.UID;
public class BuffTower : Tower
{
    public enum Mode
    {
        Charge,
        Buff,
    }

    private Collider2D[] cols = new Collider2D[64];

    [SerializeField] private Mode buffMode = Mode.Charge;
    [SerializeField] private bool displayText = false;
    [SerializeField] private bool drawLaserOnlyInActiveDirections = false;
    [SerializeField, Min(0.1f)] private float buffTextRadius = 1f;
    [SerializeField, Min(0f)] private float buffChargeAmount = 0.5f;
    [SerializeField, Min(0f)] private float buffModeAmount = 0.5f;
    private float _runtimeChargeAmountModifier = 0f;

    // Always use 4 cardinal directions
    private List<Direction> enabledDirections = new List<Direction>
    {
        Direction.Up,
        Direction.Down,
        Direction.Left,
        Direction.Right
    };

    // Expose read-only view of enabled directions
    public IReadOnlyList<Direction> EnabledDirections => enabledDirections;

    // Central helper for checking if a direction is enabled
    public bool IsDirectionEnabled(Direction direction)
    {
        return enabledDirections.Contains(direction);
    }

    public override void Awake()
    {
        base.Awake();
    }

    public override string GetUpgradeDescription(UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }

    public override void ActivateUpgrade(UID uid)
    {
        base.ActivateUpgrade(uid);
    }

    protected override void OnUpgrade(int level)
    {
        base.OnUpgrade(level);
    }

    public Mode BuffMode => buffMode;

    public override void Attack()
    {
        switch (buffMode)
        {
            case Mode.Charge:
                AttackCharge();
                break;
            case Mode.Buff:
                AttackBuff();
                break;
        }
        base.Attack();
    }

    private void AttackCharge()
    {
        foreach (var direction in enabledDirections)
        {
            var target = GridManager.instance.GetFirstTowerInDirection(transform.position, direction);
            BuffVisualization(direction, target);
            if (target != null && !target.IsAtMaxCharge())
            {
                ApplyBuff(target);
            }
        }
    }

    protected virtual void AttackBuff()
    {
        foreach (var direction in enabledDirections)
        {
            var target = GridManager.instance.GetFirstTowerInDirection(transform.position, direction);
            BuffVisualization(direction, target);
            if (target != null && !target.IsAtMaxBuff())
            {
                ApplyBuffMode(target);
            }
        }
    }

    public virtual void ApplyBuffMode(Tower tower)
    {
        float oldBuff = tower.GetBuffAmount();
        tower.AddBuff(buffModeAmount);
        float newBuff = tower.GetBuffAmount();
        float buffAdded = newBuff - oldBuff;

        if (displayText && buffAdded > 0f)
        {
            Vector3 textPosition = tower.transform.position + Random.insideUnitSphere * buffTextRadius;
            Color textColor = CM.i.ColorTypeToColor(CM.ColorType.Yellow);
            TextObjectPool.instance.PlayDamageText(textPosition, buffAdded, textColor, 1f);
        }
    }


    //public override void Update()
    //{
    //    base.Update();
    //    //Debug.Log("holding tower:" + PIC.instance.isHoldingTower());
    //    //Debug.Log("attached to valid cell:" + IsAttachedToValidCell());
    //    //if (PIC.instance.isHoldingTower() && IsAttachedToValidCell())   
    //    //{
    //    //    foreach (var direction in directionToIndicator.Keys)
    //    //    {
    //    //        if (IsDirectionEnabled(direction))
    //    //        {
    //    //            directionVisualizers[direction].enabled = true;
    //    //            LineRenderer lr = directionVisualizers[direction];
    //    //            lr.positionCount = 2;
    //    //            Vector3 start = transform.position;
    //    //            Vector3 end = GridManager.instance.GetEdgeOfGridInDirection(transform.position, direction);
    //    //            Tower firstTower = GridManager.instance.GetFirstTowerInDirection(transform.position, direction);
    //    //            if (firstTower != null)
    //    //            {
    //    //                end = firstTower.transform.position;
    //    //            }
    //    //            lr.SetPosition(0, start);
    //    //            lr.SetPosition(1, end); 
    //    //        }
    //    //        else
    //    //        {
    //    //            directionVisualizers[direction].enabled = false;
    //    //        }
    //    //    }
    //    //}
    //    //else
    //    //{
    //    //    foreach (var direction in directionToIndicator.Keys)
    //    //    {
    //    //        directionVisualizers[direction].enabled = false;
    //    //    }
    //    //}

    //}

    //public override void Attack()
    //{
    //    //foreach (var direction in directionToIndicator.Keys)
    //    //{
    //    //    var target = GridManager.instance.GetFirstTowerInDirection(transform.position, direction);

    //    //    if (IsDirectionEnabled(direction) && target != null)
    //    //    {
    //    //        if (target.IsCharged() && upgradeLevel >= 3 && id == ID.WhiteChargeTowerTierTwo && target.HasEnemiesInRange())
    //    //        {
    //    //            AOEHelper.instance.AOEAttackHelper(this, target.transform.position, GetAOESize());
    //    //        }
    //    //        if (!target.IsCharged())
    //    //        {
    //    //            ApplyBuff(target);
    //    //        }
    //    //    }
    //    //    else if (!IsDirectionEnabled(direction))
    //    //    {
    //    //        directionToIndicator[direction].SetActive(false);
    //    //    }
    //    //}
    //    foreach (var direction in enabledDirections)
    //    {
    //        var target = GridManager.instance.GetFirstTowerInDirection(transform.position, direction);
    //        if (target != null)
    //        {
    //            if (target.IsCharged() && upgradeLevel >= 3 && id == ID.WhiteChargeTowerTierTwo && target.HasEnemiesInRange())
    //            {
    //                AOEHelper.instance.AOEAttackHelper(this, target.transform.position, GetAOESize());
    //            }
    //            if (!target.IsCharged())
    //            {
    //                ApplyBuff(target);
    //            }
    //        }
    //    }
    //    base.Attack();
    //}

    public virtual void BuffVisualization(Tower tower)
    {
        BuffVisualization(Direction.Up, tower);
    }

    private void BuffVisualization(Direction direction, Tower tower)
    {
        // Only draw laser if a tower exists in this direction, when the filter is enabled
        if (drawLaserOnlyInActiveDirections && tower == null)
        {
            return;
        }

        Color color = GetColor();
        Vector3 endPosition = tower != null
            ? tower.transform.position
            : GridManager.instance != null
                ? GridManager.instance.GetEdgeOfGridInDirection(transform.position, direction)
                : transform.position;

        if (tower != null)
        {
            tower.Indicate(color);
        }

        LaserObjectPool.instance.ShowLaser(transform.position, endPosition, color, .05f);
    }

    public virtual void ApplyBuff(Tower tower)
    {
        float effectiveChargeAmount = buffChargeAmount + _runtimeChargeAmountModifier;
        float oldChargeAmount = tower.GetChargeAmount();
        float baseCooldown = tower.GetBaseCooldown();
        float minCooldown = tower.GetMinCooldown();
        
        tower.Charge(effectiveChargeAmount);
        
        float newChargeAmount = tower.GetChargeAmount();
        
        // Calculate how much cooldown reduction was provided by this buff
        float oldReduction = Mathf.Min(oldChargeAmount, baseCooldown - minCooldown);
        float newReduction = Mathf.Min(newChargeAmount, baseCooldown - minCooldown);
        float cooldownReduced = newReduction - oldReduction;
        
        if (displayText && cooldownReduced > 0)
        {
            Vector3 textPosition = tower.transform.position + Random.insideUnitSphere * buffTextRadius;
            Color textColor = CM.i.ColorTypeToColor(CM.ColorType.White);
            TextObjectPool.instance.PlayDamageText(textPosition, cooldownReduced, textColor, 1f);
        }

        if (UpgradeActive(UID.HomingOnCharge) && tower is ProjectileTower projectileTower)
        {
            projectileTower.EnableNextAttackHoming();
        }
    }

    public void SetRuntimeChargeAmountModifier(float modifier)
    {
        if (!UpgradeActive(UID.ChargeCanCrit)) return;
        _runtimeChargeAmountModifier = modifier;
    }

    public float GetRuntimeChargeAmountModifier()
    {
        return _runtimeChargeAmountModifier;
    }

    protected override void OnCriticalHit(CustomDamageData data)
    {
        base.OnCriticalHit(data);
        if (UpgradeActive(UID.ChargeCanCrit))
        {
            _runtimeChargeAmountModifier += buffChargeAmount;
        }
    }
}
