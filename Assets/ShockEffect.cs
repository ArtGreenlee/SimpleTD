using UnityEngine;

public class ShockEffect : Effect
{
    public int shockStacks;
    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        enemy.health.ApplyShock(shockStacks, tower);
        base.ApplyEffect(enemy, projectile);
    }
}
