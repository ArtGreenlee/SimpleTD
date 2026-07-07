using UnityEngine;

public class PercentHealthOnHitEffect : Effect
{
    public enum Mode
    {
        Max,
        Current
    }
    public Mode mode;
    public float percentHealthDamage;
    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        float damage = 0;
        if (mode == Mode.Max)
        {
            damage = enemy.health.GetMaxHealth() * percentHealthDamage;
        }
        else if (mode == Mode.Current)
        {
            damage = enemy.health.GetCurrentHealth() * percentHealthDamage;
        }
        Tower.CustomDamageData damageData = projectile != null ? projectile.data : (tower != null ? tower.towerDamageData : null);
        enemy.health.TakeDamage(damage, tower, CM.ColorType.None, damageData);
        base.ApplyEffect(enemy, projectile);
    }
}
