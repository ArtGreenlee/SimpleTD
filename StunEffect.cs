using UnityEngine;

public class StunEffect : Effect
{
    public float duration;

    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        enemy.StunEnemy(duration);
    }
}
