using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileExplosionOnHitEffect : ChainEffect
{
    [Min(1)] public int radialProjectileCount = 8;
    [Min(0f)] public float projectileSpeed = 12f;
    [Min(0f)] public float projectileDistance = 12f;
    [Min(0f)] public float projectileSize = 1f;
    [Min(0f)] public float projectileAcceleration = 0f;
    [Min(1)] public int projectilePierceCount = 1;
    [Min(0f)] public float baseDamageRatio = 0.5f;
    [Min(0f)] public float explosionDelaySeconds = 0f;
    [Min(0.01f)] public float timerIndicatorSize = 1.25f;
    public bool triggerOnHitVfx = true;

    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (tower == null || enemy == null || ProjectileHelper.instance == null)
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        int count = Mathf.Max(1, radialProjectileCount);
        Vector3 origin = enemy.transform.position;

        float delay = Mathf.Max(0f, explosionDelaySeconds);
        if (delay > 0f)
        {
            if (AOEObjectPool.instance != null)
            {
                AOEObjectPool.instance.PlayTimerIndicator(origin, timerIndicatorSize, tower.GetColor(), delay);
            }

            StartCoroutine(DelayedExplosion(origin, count, delay));
        }
        else
        {
            FireRadialProjectiles(origin, count);
        }

        base.ApplyEffect(enemy, projectile);
    }

    private IEnumerator DelayedExplosion(Vector3 origin, int count, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (this == null || tower == null || ProjectileHelper.instance == null)
        {
            yield break;
        }

        FireRadialProjectiles(origin, count);
    }

    private void FireRadialProjectiles(Vector3 origin, int count)
    {
        float speed = Mathf.Max(0f, projectileSpeed);
        float distance = Mathf.Max(0f, projectileDistance);
        float lifetime = speed > 0.0001f ? distance / speed : 0f;
        int pierce = Mathf.Max(1, projectilePierceCount);

        List<Effect> nonChainEffects = GetNonChainEffectsFromTower();
        float randomAngleOffset = Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            float angle = randomAngleOffset + (360f * i) / count;
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;

            var data = new Tower.CustomDamageData
            {
                baseDamageRatio = Mathf.Max(0f, baseDamageRatio)
            };

            ProjectileHelper.instance.FireProjectile(
                start: origin,
                direction: direction,
                mode: Projectile.ProjectileType.Constant,
                speed: speed,
                source: tower,
                target: null,
                size: Mathf.Max(0f, projectileSize),
                color: tower.GetColor(data),
                projectileAcceleration: projectileAcceleration,
                lifetime: lifetime,
                pierceCount: pierce,
                baseDamageRatio: Mathf.Max(0f, baseDamageRatio),
                effectOverrideList: nonChainEffects,
                triggerOnHitVfx: triggerOnHitVfx);
        }
    }
}
