using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class AOEHelper : MonoBehaviour
{
    public static AOEHelper instance;

    private ContactFilter2D _filter;
    private ContactFilter2D _agentFilter;
    private ContactFilter2D towerFilter;
    private bool _initialized;

    // Reused set to avoid per-call allocations; used to dedupe multi-collider enemies.
    private readonly HashSet<int> _uniqueIds = new HashSet<int>(128);

    private Collider2D[] helperCols = new Collider2D[64];
    [Header("Grid Viz")]
    [SerializeField] private bool gridVizColorCircle = false;
    [SerializeField, Min(0f)] private float gridVizColorCircleIncreaseAmount = 0.2f;
    private GridViz _gridViz;

    private void Awake()
    {
        instance = this;
        InitializeFilter();
        Physics2D.reuseCollisionCallbacks = true;
    }

    private void OnEnable()
    {
        if (!_initialized) InitializeFilter();
    }

    private void InitializeFilter()
    {
        // Build once; can be refreshed if LayerMaskManager comes online later.
        LayerMask m = (LayerMaskManager.instance != null) ? LayerMaskManager.instance.enemyLayerMask : (LayerMask)~0;

        _filter = new ContactFilter2D();
        _filter.useLayerMask = true;
        _filter.layerMask = m;
        _filter.useTriggers = true;

        LayerMask agentMask = (LayerMaskManager.instance != null) ? LayerMaskManager.instance.agentLayerMask : (LayerMask)~0;
        _agentFilter = new ContactFilter2D();
        _agentFilter.useLayerMask = true;
        _agentFilter.layerMask = agentMask;
        _agentFilter.useTriggers = true;

        _initialized = true;
    }

    public Enemy[] AOEAttackHelper(Tower tower, Vector3 position, float radius, bool applyEffects=false, Tower.CustomDamageData data=null, List<Effect> effectOverrideList = null)
    {
        //this shit straight up crashes unity
        int hit = GetEnemiesInRadius(position, radius, helperCols);
        if (data == null) data = new Tower.CustomDamageData();
        data.isAOE = true;
        tower.GetDamageType(data, true);
        Color aoeColor = tower.GetColor(data);
        AOEObjectPool.instance.PlayPulse(position, radius * 2, aoeColor);
        TryApplyGridVizColorCircle(position, radius, aoeColor);
        data.numHit = hit;
        data.critCount = tower.RollCritCount();
        data.crit = data.critCount > 0;
        data.hitColliders = helperCols;
        
        Enemy[] hitEnemies = new Enemy[hit];
        int enemyCount = 0;
        
        for (int i = 0; i < hit; i++)
        {
            Collider2D col = helperCols[i];
            if (col == null)
            {
                continue;
            }
            Enemy e = col.GetComponent<Enemy>();
            if (e == null)
            {
                continue;
            }
            data.enemyHit = e;
            float damage = tower.GetDamage(data, false);
            CM.ColorType damageType = tower.GetDamageType(data);
            e.health.TakeDamage(damage, tower, damageType, data);
            TryPlayOnHitVfx(e, damage, damageType);
            if (applyEffects)
            {
                if (effectOverrideList != null)
                {
                    for (int j = 0; j < effectOverrideList.Count; j++)
                    {
                        Effect effect = effectOverrideList[j];
                        if (effect == null) continue;
                        effect.ApplyEffect(e);
                    }
                }
                else
                {
                    tower.ApplyEffects(e);
                }
            }
            hitEnemies[enemyCount++] = e;
        }
        
        // Return only the actual enemies hit (trim excess nulls)
        if (enemyCount < hit)
        {
            System.Array.Resize(ref hitEnemies, enemyCount);
        }
        
        return hitEnemies;
    }

    private static void TryPlayOnHitVfx(Enemy enemy, float damage, CM.ColorType damageType)
    {
        if (enemy == null || damage <= 0f) return;

        var hitEffect = OnHitParticleEffect.instance;
        if (hitEffect == null) return;

        Color hitColor = Color.white;
        if (damageType != CM.ColorType.None && CM.i != null)
        {
            hitColor = CM.i.ColorTypeToColor(damageType);
        }

        float damageScale = TextObjectPool.instance != null
            ? TextObjectPool.instance.GetDamageTextScalePreview(enemy.transform, Mathf.Max(0.0001f, damage))
            : 1f;
        int particleCount = hitEffect.GetParticleCountFromDamageScale(damageScale);
        hitEffect.OnHitVfx(enemy.transform.position, particleCount, hitColor, false, 0f);
    }

    public int DamageEnemiesInRadiusNoSource(Vector3 position, float radius, float damage, Enemy ignoredEnemy = null, CM.ColorType damageTypeOverride = CM.ColorType.None)
    {
        if (damage <= 0f) return 0;

        int hit = GetEnemiesInRadius(position, radius, helperCols);
        int damaged = 0;

        for (int i = 0; i < hit; i++)
        {
            Collider2D col = helperCols[i];
            if (col == null) continue;

            Enemy e = col.GetComponent<Enemy>();
            if (e == null || e == ignoredEnemy || e.health == null) continue;

            e.health.TakeDamage(damage, null, damageTypeOverride, null);
            damaged++;
        }

        return damaged;
    }

    public int AgentHealAOE(Vector3 position, float radius, float amount, Tower source)
    {
        if (amount <= 0f) return 0;

        Color healColor = CM.i != null ? CM.i.ColorTypeToColor(CM.ColorType.Green) : Color.green;
        if (AOEObjectPool.instance != null)
        {
            AOEObjectPool.instance.PlayPulse(position, radius * 2f, healColor);
        }
        TryApplyGridVizColorCircle(position, radius, healColor);

        _uniqueIds.Clear();
        int hitCount = Physics2D.OverlapCircle(position, radius, _agentFilter, helperCols);
        int healedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = helperCols[i];
            if (col == null) continue;

            Agent agent = col.GetComponentInParent<Agent>();
            if (agent == null)
            {
                agent = col.GetComponent<Agent>();
            }
            if (agent == null) continue;

            int id = agent.GetInstanceID();
            if (!_uniqueIds.Add(id)) continue;

            Health health = agent.GetComponent<Health>();
            if (health == null)
            {
                health = agent.GetComponentInChildren<Health>();
            }
            if (health == null) continue;

            float before = health.GetCurrentHealth();
            health.Heal(amount, source, CM.ColorType.Green, null);
            float after = health.GetCurrentHealth();

            float healedAmount = after - before;
            if (healedAmount <= 0f) continue;

            if (TextObjectPool.instance != null)
            {
                string text = "+" + Mathf.RoundToInt(healedAmount).ToString();
                TextObjectPool.instance.PlayFloatingText(agent.transform.position, text, healColor, 0.15f, 1f);
            }

            healedCount++;
        }

        return healedCount;
    }

    /// <summary>
    /// Call if enemy layer mask changes at runtime.
    /// </summary>
    public void RefreshMask()
    {
        _initialized = false;
        InitializeFilter();
    }

    public int GetEnemiesInRadius(Vector2 center, float radius, Collider2D[] results)
    {
        if (!_initialized) InitializeFilter();

        // Physics2D can return the same enemy multiple times if it has multiple colliders.
        // Deduplicate by Rigidbody2D (if present) otherwise by GameObject instance.
        _uniqueIds.Clear();

        int count = Physics2D.OverlapCircle(center, radius, _filter, results);
        if (count <= 1) return count;

        int write = 0;
        for (int read = 0; read < count; read++)
        {
            var col = results[read];
            if (col == null) continue;

            int id;
            var rb = col.attachedRigidbody;
            if (rb != null)
                id = rb.GetInstanceID();
            else
                id = col.gameObject.GetInstanceID();

            if (_uniqueIds.Add(id))
            {
                results[write++] = col;
            }
        }

        // Clear remaining slots so callers iterating the full array don't see stale colliders.
        for (int i = write; i < count; i++)
            results[i] = null;

        return write;
    }

    private void TryApplyGridVizColorCircle(Vector3 position, float radius, Color color)
    {
        if (!gridVizColorCircle) return;

        if (_gridViz == null)
        {
            _gridViz = FindFirstObjectByType<GridViz>();
        }

        if (_gridViz == null) return;

        _gridViz.ColorCircle(color, gridVizColorCircleIncreaseAmount, radius, position);
    }

}
