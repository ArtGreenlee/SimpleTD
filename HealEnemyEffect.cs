using UnityEngine;

public class HealEnemyEffect : EnemyEffect
{
    public float healAmount;
    public float healMaxDistance = 3;
    public override void Trigger()
    {
        Enemy e = enemy.GetNearestEnemy(healMaxDistance);
        if (e != null)
        {
            LaserObjectPool.instance.ShowLaser(transform.position, e.transform.position, CM.i.ColorTypeToColor(CM.ColorType.Green), .05f);
            e.health.Heal(healAmount, null, CM.ColorType.Green, null);
        }
        base.Trigger();
    }
}
