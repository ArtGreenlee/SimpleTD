using UnityEngine;

public class ExposeOnHitEffect : Effect
{
    public float time = 5;
    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (!ShouldApplyEffect())
        {
            return;
        }
        enemy.Expose(time);
        base.ApplyEffect(enemy, projectile);
    }
}
