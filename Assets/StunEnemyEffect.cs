using UnityEngine;

public class StunEnemyEffect : EnemyEffect
{
    public float stunDuration;
    public float minDistance;
    public override void Trigger()
    {
        var tList = TowerManager.instance.GetNNearestPlacedTowers(1, transform.position);
        if (tList.Count > 0 && (tList[0].transform.position - transform.position).sqrMagnitude < minDistance * minDistance)
        {
            var t = tList[0];
            t.Stun(stunDuration);
            LaserObjectPool.instance.ShowLaser(transform.position, t.transform.position, CM.i.ColorTypeToColor(CM.ColorType.Purple), .05f);
        }
        
        base.Trigger();
    }
}
