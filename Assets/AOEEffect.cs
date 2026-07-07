using System.Collections.Generic;
using UnityEngine;

public class AOEEffect : ChainEffect
{
    public float aoeRatio = .5f;

    private Collider2D[] s_overlap = new Collider2D[64];

    public override void ApplyEffect(Enemy enemy, Projectile projectile)
    {
        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        if (enemy == null)
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        List<Effect> nonChainEffects = GetNonChainEffectsFromTower();
        Tower.CustomDamageData data = tower.towerDamageData;
        data.enemyHit = enemy;
        AOEHelper.instance.AOEAttackHelper(
            tower,
            enemy.transform.position,
            tower.GetAOESize(tower.GetBaseAOESize()) * aoeRatio,
            applyEffects: nonChainEffects != null && nonChainEffects.Count > 0,
            data: data,
            effectOverrideList: nonChainEffects);
        if (data != null && data.crit && tower.UpgradeActive(UpgradeData.UID.ApplyBlueMarkOnCrit))
        {
            for (int i = 0; i < data.numHit; i++)
            {
                if (data.hitColliders[i] == null) continue;
                Enemy e = data.hitColliders[i].GetComponent<Enemy>();
                // e.ApplySlow(.1f);
                e.SetMark(CM.ColorType.Blue);
            }
        }
        // TODO modify this to use the AOEHElper.cs
        // if (enemy == null) return;
        // if (projectile == null) return;

        // float radius = Mathf.Max(0f, AOERadius);
        // if (radius <= 0f) return;

        // float baseDamage = tower.GetDamage();
        // float aoeDamage = Mathf.Max(0f, baseDamage * Mathf.Clamp01(AOEDamageRatio));
        // if (aoeDamage <= 0f) return;

        // Vector3 p3 = projectile.transform.position;
        // Vector2 center = new Vector2(p3.x, p3.y);

        // int hitCount;
        // hitCount = AOEHelper.instance.GetEnemiesInRadius(center, radius, s_overlap);

        // for (int i = 0; i < hitCount; i++)
        // {
        //     var c = s_overlap[i];
        //     if (c == null) continue;
        //     var e = c.GetComponent<Enemy>();
        //     if (e == null) continue;
        //     var h = e.GetComponent<Health>();
        //     if (h == null) continue;
        //     h.TakeDamage(aoeDamage, tower);
        //     tower.ApplyEffects(e, null, selfEffectSet);
        // }

        // var pool = AOEObjectPool.instance;
        // if (pool != null)
        // {
        //     float diameter = radius * 2f;
        //     pool.PlayPulse(new Vector3(center.x, center.y, 0f), diameter, aoeFadeDuration, tower.GetColor(), zOffset: p3.z);
        // }

        base.ApplyEffect(enemy, projectile);
    }
}
