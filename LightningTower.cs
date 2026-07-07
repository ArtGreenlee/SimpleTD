using System.Collections.Generic;
using System.Text;
using UnityEngine;
public class LightningTower : Tower
{
    [SerializeField, Min(1)] private int simultaneousTargetsInRange = 1;

    public override string GetUpgradeDescription(UpgradeData.UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }

    public int GetLightningTargetsInRangeCount()
    {
        int count = Mathf.Max(1, simultaneousTargetsInRange);
        if (UpgradeActive(UpgradeData.UID.IncreaseLightningTargets))
        {
            count += UpgradeData.IncreaseLightningTargetsAmount;
        }
        return count;
    }

    public override string GetDescription()
    {
        int targets = GetLightningTargetsInRangeCount();
        int chains = GetLightningChainCount();

        StringBuilder sb = new StringBuilder();
        sb.Append("Strikes enemies with lightning");
        if (targets > 1)
        {
            sb.Append(" across ");
            sb.Append(targets);
            sb.Append(" targets");
        }
        sb.Append(" and ");
        sb.Append(chains);
        sb.Append(chains == 1 ? " chain" : " chains");
        return sb.ToString();
    }

    public override void Attack()
    {
        base.Attack();
        if (targettedEnemy == null || rangeManager == null || LightningHelper.instance == null)
        {
            return;
        }

        int maxTargets = GetLightningTargetsInRangeCount();
        List<Enemy> targets = rangeManager.GetTopTargets(maxTargets);
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        Enemy primaryTarget = targets[0];

        // Vector3 toTarget = primaryTarget.transform.position - transform.position;
        // toTarget.z = 0f;
        // if (toTarget.sqrMagnitude > 0.000001f && !rangeManager.IsSingleRay())
        // {
        //     float angleDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        //     towerBodyTransform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        // }
        // else if (rangeManager.IsSingleRay())
        // {
        //     Vector2 rayDir = rangeManager.GetSingleRayDirectionWorld();
        //     if (rayDir.sqrMagnitude > 0.000001f)
        //     {
        //         float angleDeg = Mathf.Atan2(rayDir.y, rayDir.x) * Mathf.Rad2Deg - 90f;
        //         towerBodyTransform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        //     }
        // }

        int effectiveCnt = GetLightningChainCount();
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null) continue;

            LightningHelper.instance.Lightning(this, enemy, effectiveCnt, true, true, true, towerDamageData);
        }
    }
}
