using System.Linq;
using UnityEngine;
using UID = UpgradeData.UID;
using System.Collections.Generic;

public class AOETower : Tower
{
    private readonly List<Enemy> _smartManualCandidates = new List<Enemy>(32);

    public enum Mode
    {
        OnEnemy,
        OnTower
    }
    public bool drawLaser;
    public Mode mode;
    private CustomDamageData data = new CustomDamageData();

    public float GetBlastRadius()
    {
        return Mathf.Max(0f, GetAOESize(GetBaseAOESize()));
    }

    public float GetEffectiveTargetingRange()
    {
        return mode == Mode.OnTower ? GetBlastRadius() : Mathf.Max(0f, GetRange());
    }
    public override string GetUpgradeDescription(UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }

    public override void ActivateUpgrade(UID uid)
    {
        if (uid == UID.AOESizeIncrease)
            SetBaseAOESize(GetBaseAOESize() + UpgradeData.AoeRangeIncrease);
        else if (uid == UID.SingleTargetAOEDamageIncrease)
            AddToolTipTag(CM.ColorType.Purple);
        base.ActivateUpgrade(uid);
        SyncRangeToMode();
    }

    protected override void OnUpgrade(int level)
    {
        base.OnUpgrade(level);
    }

    private void TryMoveManualTargetIndicatorToBestAoePosition()
    {
        if (!UpgradeActive(UID.SmartManualTargetting)) return;
        if (rangeManager == null || towerTool == null) return;
        if (QuadTree2D.instance == null) return;
        if (rangeManager.GetTargettingMode() != RangeManager.TargettingMode.Manual) return;
        if (mode != Mode.OnEnemy) return;

        float searchRadius = Mathf.Max(0f, GetRange());
        float blastRadius = GetBlastRadius();
        if (searchRadius <= 0.0001f || blastRadius <= 0.0001f) return;

        QuadTree2D.instance.QueryCircle(transform.position, searchRadius, _smartManualCandidates);
        if (_smartManualCandidates.Count == 0) return;

        Vector3 bestPosition = towerTool.transform.position;
        int bestCount = -1;
        float bestDistSqr = float.PositiveInfinity;

        for (int i = 0; i < _smartManualCandidates.Count; i++)
        {
            Enemy candidate = _smartManualCandidates[i];
            if (candidate == null) continue;

            Vector3 candidatePosition = candidate.transform.position;
            candidatePosition.z = towerTool.transform.position.z;
            if (!rangeManager.PointInsideRange(candidatePosition)) continue;

            int hitCount = 0;
            for (int j = 0; j < _smartManualCandidates.Count; j++)
            {
                Enemy other = _smartManualCandidates[j];
                if (other == null) continue;
                if (((Vector2)(other.transform.position - candidatePosition)).sqrMagnitude <= blastRadius * blastRadius)
                {
                    hitCount++;
                }
            }

            float distSqr = ((Vector2)(candidatePosition - transform.position)).sqrMagnitude;
            if (hitCount > bestCount || (hitCount == bestCount && distSqr < bestDistSqr))
            {
                bestCount = hitCount;
                bestDistSqr = distSqr;
                bestPosition = candidatePosition;
            }
        }

        if (bestCount > 0)
        {
            towerTool.transform.position = bestPosition;
        }
    }

    public override void Start()
    {
        base.Start();
        SyncRangeToMode();
    }

    public override void Update()
    {
        base.Update();
        SyncRangeToMode();
    }

    protected override float GetDamageModified(float damage, CustomDamageData data=null)
    {
        return base.GetDamageModified(damage, data);
    }
    protected override float GetDamageMultiplier(float damage, CustomDamageData data=null)
    {
        if (UpgradeActive(UID.SingleTargetAOEDamageIncrease) && rangeManager.GetAllEnemiesInRange().Count == 1)
        {
            damage *= 2;
        }

        return base.GetDamageMultiplier(damage, data);
    }
    
    public override void Attack()
    {
        base.Attack();

        if (AOEHelper.instance == null) return;

        SyncRangeToMode();
        TryMoveManualTargetIndicatorToBestAoePosition();

        // Use AOEHelper to handle all enemy detection, damage, and effects
        // Get the array of enemies hit
        Vector3 targetPosition = transform.position;
        if (mode == Mode.OnEnemy)
        {
            if (rangeManager.GetTargettingMode() == RangeManager.TargettingMode.Manual && towerTool != null)
            {
                targetPosition = towerTool.transform.position;
            }
            else if (targettedEnemy != null)
            {
                targetPosition = targettedEnemy.transform.position;
            }
        }

        var hitEnemies = AOEHelper.instance.AOEAttackHelper(this, targetPosition, GetBlastRadius(), applyEffects: true, data);
        if (drawLaser && mode == Mode.OnEnemy)
        {
            LaserObjectPool.instance.ShowLaser(transform.position, targetPosition, CM.i.ColorTypeToColor(GetDamageType()), 0.05f, .3f, .5f);
            //LaserHelper.instance.LaserAttackHelper(this, targettedEnemy.transform.position, (Vector3)Random.insideUnitCircle, .1f, CM.i.ColorTypeToColor(damageType), 1);
        }
        if (UpgradeActive(UID.AOEAddProjectiles) && ProjectileHelper.instance != null)
        {
            FireRingProjectiles(targetPosition);
        }
        if (hitEnemies == null || hitEnemies.Length == 0) return;

        if (id == ID.IceTower)
        {
            if (UpgradeActive(UID.AOESingleTargetSlowBoost) && hitEnemies.Length == 1)
            {
                hitEnemies[0].ApplySlow(.1f);
            }
            if (UpgradeActive(UID.MaxSlowIncreaseOnHit))
            {
                foreach (var enemy in hitEnemies)
                {
                    if (enemy != null)
                        enemy.GetMovement().SetMaxSlow(enemy.GetMovement().GetMaxSlow() + .01f);
                }
            }
        }

        if (UpgradeActive(UID.AOEExposeHighestHealth))
        {
            float maxHealth = float.MinValue;
            Enemy highestHealthEnemy = null;
            foreach (var enemy in hitEnemies)
            {
                if (enemy == null) continue;
                var h = enemy.GetComponent<Health>();
                if (h == null) continue;
                if (h.GetCurrentHealth() > maxHealth)
                {
                    maxHealth = h.GetCurrentHealth();
                    highestHealthEnemy = enemy;
                }
            }
            if (highestHealthEnemy != null)
            {
                highestHealthEnemy.Expose(5f);
            }
        }

        if (UpgradeActive(UID.ShockIfAllExposed))
        {
            bool allExposed = true;
            foreach (var enemy in hitEnemies)
            {
                if (enemy == null) continue;
                enemy.Expose(1);
                if (!enemy.HasStatusEffect(Enemy.StatusEffect.Exposed))
                {
                    allExposed = false;
                    break;
                }
            }
            if (allExposed)
            {
                //foreach (var enemy in hitEnemies)
                //{
                //    LaserHelper.instance.LaserAttackHelper(this, enemy.transform.position, (Vector3)Random.insideUnitCircle, .1f, CM.i.ColorTypeToColor(damageType), 1);
                //}
            }
        }

        
    }

    private void SyncRangeToMode()
    {
        if (rangeManager == null) return;
        rangeManager.SetRange(GetEffectiveTargetingRange());
    }

    private void FireRingProjectiles(Vector3 origin)
    {
        int count = UpgradeData.AOEAddProjectilesCount;
        float speed = UpgradeData.AOEAddProjectilesSpeed;
        Color color = CM.i != null ? CM.i.ColorTypeToColor(GetDamageType()) : Color.white;
        float aoeRadius = GetBlastRadius();
        float travelDistance = Mathf.Max(0f, aoeRadius);
        float lifetime = speed > 0.0001f ? travelDistance / speed : 1f;
        float angleStep = 360f / count;
        Vector3 dir = new Vector3(0f, 0f, 0f);

        for (int i = 0; i < count; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            dir.x = Mathf.Cos(rad);
            dir.y = Mathf.Sin(rad);

            ProjectileHelper.instance.FireProjectile(
                start: origin,
                direction: dir,
                mode: Projectile.ProjectileType.Constant,
                speed: speed,
                source: this,
                target: null,
                size: 1f,
                color: color,
                projectileAcceleration: 0f,
                lifetime: lifetime,
                pierceCount: GetPierceCount(),
                baseDamageRatio: towerDamageData.baseDamageRatio,
                triggerOnHitVfx: true);
        }
    }

    //private void FireLaserFromEnemy(Enemy enemy)
    //{
    //    if (enemy == null || LaserHelper.instance == null) return;

    //    // Generate a random direction
    //    Vector2 randomDirection = Random.insideUnitCircle.normalized;
        
    //    // Calculate laser start and end points
    //    Vector3 laserStart = enemy.transform.position;
    //    Vector3 laserEnd = laserStart + (Vector3)randomDirection * GetRange();
        
    //    // Create laser data
    //    var laserData = new CustomDamageData();
    //    laserData.crit = RollCrit();
    //    laserData.numHit = 0;
        
    //    // Use LaserHelper to fire the laser and deal damage to all enemies in its path
    //    LaserHelper.instance.LaserAttackHelper(
    //        this, 
    //        laserStart, 
    //        laserEnd, 
    //        width: 0.5f, // Laser width
    //        applyEffects: true, 
    //        data: laserData
    //    );

    //    //LaserHelper.instance.LaserAttackHelper()

    //    // Visual feedback - could be enhanced with actual laser visuals
    //    if (AOEObjectPool.instance != null)
    //    {
    //        AOEObjectPool.instance.PlayPulse((Vector3)((Vector2)laserStart + (Vector2)laserEnd) * 0.5f, 0.5f, GetColor());
    //    }
    //}
}
