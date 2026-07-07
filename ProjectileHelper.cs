using UnityEngine;

public class ProjectileHelper : MonoBehaviour
{
    public static ProjectileHelper instance;

    private void Awake()
    {
        instance = this;
    }

    // start, direction, mode, speed, source, target, size, color, projectileAcceleration, lifetime, pierceCount, onHitAction, onExpireAction
    public Projectile FireProjectile(
        Vector3 start,
        Vector3 direction,
        Projectile.ProjectileType mode,
        float speed,
        Tower source,
        Enemy target,
        float size,
        Color color,
        float projectileAcceleration,
        float lifetime,
        int pierceCount,
        float baseDamageRatio = 1f,
        System.Collections.Generic.List<Effect> effectOverrideList = null,
        System.Action<Projectile> onHitAction = null,
        System.Action<Projectile> onExpireAction = null,
        bool triggerOnHitVfx = true,
        bool? enableParticleVfx = null,
        float steeringSpeed = 360f,
        bool homingRetargetting = false,
        bool lerpScaleToIntendedScaleOnSpawn = false)
    {
        var pool = ProjectileObjectPool.instance;
        if (pool == null || pool.projectilePrefab == null) return null;

        Vector3 dir = direction;
        dir.z = 0f;
        if (dir.sqrMagnitude <= 0.000001f)
        {
            dir = Vector3.up;
        }

        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, dir);
        GameObject bulletObj = pool.Get(start, rotation);
        if (bulletObj == null)
        {
            bulletObj = Instantiate(pool.projectilePrefab, start, rotation);
        }
        if (bulletObj == null) return null;

        var proj = bulletObj.GetComponent<Projectile>();
        if (proj == null) return null;

        proj.sourceTower = source;
        proj.pool = pool;
        proj.triggerOnHitVfx = triggerOnHitVfx;
        proj.data.enemyHit = null;
        proj.data.hitColliders = null;
        proj.data.numHit = 1;
        proj.data.critCount = source != null ? source.RollCritCount() : 0;
        proj.data.crit = proj.data.critCount > 0;
        proj.data.baseDamageRatio = baseDamageRatio;
        proj.effectOverrideList = effectOverrideList;

        if (source != null)
        {
            source.GetDamageType(proj.data, true);
        }

        proj.SetPierceCount(Mathf.Max(1, pierceCount));
        proj.ApplyColor(color);
        bool useParticleVfx = enableParticleVfx ?? pool.enableParticleVfxByDefault;
        proj.ConfigureParticleVfx(useParticleVfx, color);
        pool.RefreshAllIn1Properties(bulletObj);
        proj.Configure(mode, target, speed, source, projectileAcceleration, homingRetargetting);
        proj.SetSteeringSpeed(steeringSpeed);
        proj.SetSizeMultiplier(size, lerpScaleToIntendedScaleOnSpawn);
        proj.SetChargeEffectActive(source != null && source.UsedChargeForLastAttack());
        proj.SetDamageBuffEffectActive(source != null && source.UsedBuffForLastAttack());

        if (lifetime >= 0f)
        {
            proj.ArmLifetimeDirect(lifetime);
        }

        if (proj.rb != null)
        {
            proj.rb.linearVelocity = dir.normalized * speed;
        }

        _ = onHitAction;
        _ = onExpireAction;

        return proj;
    }
}
