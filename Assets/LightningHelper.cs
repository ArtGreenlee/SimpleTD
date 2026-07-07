using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LightningHelper : MonoBehaviour
{
    private struct LightningSegment
    {
        public Transform start;
        public Transform end;
        public Enemy endEnemy;
    }

    private LayerMask enemyLayerMask;
    private Collider2D[] colliders = new Collider2D[20];
    public static LightningHelper instance { get; private set; }
    public CM.ColorType defaultShockColor;

    private ContactFilter2D _enemyFilter;
    public float enemySearchRange;
    public float lightningEchoVfxDelay;
    public float initialLaserWidth = 0.05f;
    public float initialLaserDuration = 0.15f;
    public float initialLaserAlpha = 1f;
    public float echoLaserWidth = 0.025f;
    public float echoLaserDuration = 0.2f;
    public float echoLaserAlpha = 1f;

    [Header("Segmented Lightning")]
    [Min(0.01f)] public float segmentLengthMin = 0.4f;
    [Min(0.01f)] public float segmentLengthMax = 0.8f;
    [Min(0f)] public float segmentSpawnDelay = 0.01f;
    [Min(0f)] public float segmentJitter = 0.2f;

    [Header("Cyan Laser Overlay")]
    [Min(0.01f)] [SerializeField] private float cyanLaserSegmentLength = 1.2f;
    [Min(0f)] [SerializeField] private float cyanLaserSegmentJitter = 0.1f;
    [Min(0.001f)] [SerializeField] private float cyanLaserWidth = 0.01f;
    [Min(0.001f)] [SerializeField] private float cyanLaserDuration = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float cyanLaserAlpha = 1f;

    [Header("Grid Viz")]
    [SerializeField] private bool gridVizAlphaIncrease = false;
    [SerializeField, Min(0f)] private float gridVizAlphaIncreaseAmount = 0.2f;
    [SerializeField, Min(1)] private int gridVizAlphaLineWidthCells = 1;
    [SerializeField] private bool gridVizColorLine = false;
    [SerializeField, Min(0f)] private float gridVizColorLineIncreaseAmount = 0.2f;
    [SerializeField, Min(1)] private int gridVizColorLineWidthCells = 1;

    private readonly HashSet<Enemy> _visited = new HashSet<Enemy>();
    private readonly HashSet<Enemy> _queued = new HashSet<Enemy>();
    private readonly Stack<Enemy> _stack = new Stack<Enemy>(16);
    private readonly List<Enemy> _candidateEnemies = new List<Enemy>(20);
    private readonly List<Enemy> _quadtreeResults = new List<Enemy>(20);

    private ObjectPool<List<LightningSegment>> _segmentsPool;
    private ObjectPool<HashSet<Enemy>> _damagedSetPool;
    private ObjectPool<List<Vector3>> _segmentPointsPool;
    private GridViz _gridViz;

    private void Awake()
    {
        instance = this;

        _segmentsPool = new ObjectPool<List<LightningSegment>>(
            () => new List<LightningSegment>(16),
            null,
            list => list.Clear(),
            null,
            false,
            8,
            256);

        _damagedSetPool = new ObjectPool<HashSet<Enemy>>(
            () => new HashSet<Enemy>(),
            null,
            set => set.Clear(),
            null,
            false,
            8,
            256);

        _segmentPointsPool = new ObjectPool<List<Vector3>>(
            () => new List<Vector3>(16),
            null,
            list => list.Clear(),
            null,
            false,
            8,
            512);
    }

    private void Start()
    {
        enemyLayerMask = LayerMaskManager.instance.enemyLayerMask;

        _enemyFilter = new ContactFilter2D();
        _enemyFilter.useLayerMask = true;
        _enemyFilter.layerMask = enemyLayerMask;
        _enemyFilter.useTriggers = true;
    }

    public List<Enemy> Lightning(Tower tower, Enemy e, int cnt, bool applyEffects=false, bool visualize=false, bool includeTowerInVisualization=true, Tower.CustomDamageData data=null, float? fixedDamageOverride=null, Transform originOverride = null)
    {
        if (e == null || tower == null || cnt <= 0) return null;
        List<Enemy> hitEnemies = new List<Enemy>();

        float range = enemySearchRange > 0f ? enemySearchRange : tower.GetRange();
        if (range <= 0f) return null;

        _visited.Clear();
        _queued.Clear();
        _stack.Clear();
        _candidateEnemies.Clear();

        List<LightningSegment> segments = visualize ? _segmentsPool.Get() : null;

        _stack.Push(e);
        _queued.Add(e);

        if (visualize && includeTowerInVisualization)
        {
            segments.Add(new LightningSegment
            {
                start = originOverride != null ? originOverride : tower.transform,
                end = e.transform,
                endEnemy = e
            });
        }

        while (_stack.Count > 0 && _visited.Count < cnt)
        {
            Enemy current = _stack.Pop();
            if (current == null) continue;
            if (!_visited.Add(current)) continue;

            hitEnemies.Add(current);

            if (_visited.Count >= cnt) break;

            int found = GetEnemiesInLightningRange(current.transform.position, range, out var foundCols);
            _candidateEnemies.Clear();

            for (int i = 0; i < found && _visited.Count + _stack.Count + _candidateEnemies.Count < cnt; i++)
            {
                var c = foundCols[i];
                if (c == null) continue;

                var next = c.GetComponentInParent<Enemy>();
                if (next == null || next == current || _visited.Contains(next) || _queued.Contains(next)) continue;

                _candidateEnemies.Add(next);
                _queued.Add(next);
            }

            _candidateEnemies.Sort((a, b) =>
            {
                float da = ((Vector2)(a.transform.position - current.transform.position)).sqrMagnitude;
                float db = ((Vector2)(b.transform.position - current.transform.position)).sqrMagnitude;
                return da.CompareTo(db);
            });

            if (_candidateEnemies.Count > 2)
                _candidateEnemies.RemoveRange(2, _candidateEnemies.Count - 2);

            for (int i = _candidateEnemies.Count - 1; i >= 0; i--)
            {
                Enemy next = _candidateEnemies[i];
                _stack.Push(next);

                if (visualize)
                {
                    segments.Add(new LightningSegment
                    {
                        start = current.transform,
                        end = next.transform,
                        endEnemy = next
                    });
                }
            }
        }

        if (hitEnemies.Count == 0)
        {
            if (segments != null) _segmentsPool.Release(segments);
            return hitEnemies;
        }

        if (data == null)
        {
            data = tower.towerDamageData;
        }

        CM.ColorType damageType = tower.GetDamageType(data, true);

        int critCount = fixedDamageOverride.HasValue ? 0 : tower.RollCritCount();
        bool crit = critCount > 0;

        if (visualize && segments != null && segments.Count > 0)
        {
            StartCoroutine(LightningVisualizationAndDamageRoutine(
                segments,
                hitEnemies,
                tower,
                applyEffects,
                damageType,
                critCount,
                crit,
                fixedDamageOverride,
                data));
        }
        else
        {
            if (segments != null) _segmentsPool.Release(segments);
            for (int i = 0; i < hitEnemies.Count; i++)
            {
                ApplyLightningDamageToEnemy(hitEnemies[i], tower, hitEnemies.Count, applyEffects, damageType, critCount, crit, fixedDamageOverride, data);
            }
        }

        return hitEnemies;
    }

    private IEnumerator LightningVisualizationAndDamageRoutine(
        List<LightningSegment> segments,
        List<Enemy> hitEnemies,
        Tower tower,
        bool applyEffects,
        CM.ColorType damageType,
        int critCount,
        bool crit,
        float? fixedDamageOverride,
        Tower.CustomDamageData dataTemplate)
    {
        var damaged = _damagedSetPool.Get();

        Color initialColor = GetDamageTypeColor(damageType);

        Color echoColor = Color.white;

        for (int i = 0; i < segments.Count; i++)
        {
            Transform start = segments[i].start;
            Transform end = segments[i].end;
            if (start == null || end == null) continue;

            Enemy reachedEnemy = segments[i].endEnemy;

            StartCoroutine(PlaySegmentedBoltDelayed(
                start,
                end,
                reachedEnemy,
                echoColor,
                echoLaserWidth,
                echoLaserDuration,
                echoLaserAlpha,
                lightningEchoVfxDelay));

            yield return PlaySegmentedBolt(
                start,
                end,
                reachedEnemy,
                initialColor,
                initialLaserWidth,
                initialLaserDuration,
                initialLaserAlpha);

            if (reachedEnemy != null && damaged.Add(reachedEnemy))
            {
                ApplyLightningDamageToEnemy(reachedEnemy, tower, hitEnemies.Count, applyEffects, damageType, critCount, crit, fixedDamageOverride, dataTemplate);
            }
        }

        for (int i = 0; i < hitEnemies.Count; i++)
        {
            Enemy enemy = hitEnemies[i];
            if (enemy == null) continue;
            if (!damaged.Add(enemy)) continue;
            ApplyLightningDamageToEnemy(enemy, tower, hitEnemies.Count, applyEffects, damageType, critCount, crit, fixedDamageOverride, dataTemplate);
        }

        _damagedSetPool.Release(damaged);
        _segmentsPool.Release(segments);
    }

    private static Color GetDamageTypeColor(CM.ColorType damageType)
    {
        if (damageType == CM.ColorType.None)
        {
            return Color.white;
        }

        return CM.i != null ? CM.i.ColorTypeToColor(damageType) : Color.white;
    }

    private IEnumerator PlaySegmentedBoltDelayed(Transform start, Transform end, Enemy trackedEnemy, Color color, float width, float duration, float alpha, float startDelay)
    {
        float d = Mathf.Max(0f, startDelay);
        if (d > 0f)
        {
            yield return new WaitForSeconds(GetVisualScaledDuration(d));
        }

        yield return PlaySegmentedBolt(start, end, trackedEnemy, color, width, duration, alpha);
    }

    private void ApplyLightningDamageToEnemy(
        Enemy enemy,
        Tower tower,
        int totalHits,
        bool applyEffects,
        CM.ColorType damageType,
        int critCount,
        bool crit,
        float? fixedDamageOverride,
        Tower.CustomDamageData dataTemplate)
    {
        if (enemy == null || tower == null) return;

        Health health = enemy.health != null ? enemy.health : enemy.GetComponent<Health>();
        if (health == null) return;

        var data = new Tower.CustomDamageData
        {
            numHit = Mathf.Max(1, totalHits),
            enemyHit = enemy,
            hitColliders = null,
            critCount = critCount,
            crit = crit,
            isAOE = dataTemplate != null && dataTemplate.isAOE,
            damageType = damageType,
            baseDamageRatio = dataTemplate != null ? dataTemplate.baseDamageRatio : 1f,
        };

        if (tower.UpgradeActive(UpgradeData.UID.MegaShockOnFullyChargedLightning) && tower.IsAtMaxCharge())
        {
            health.ApplyShock(UpgradeData.MegaShockOnFullyChargedLightningStacks, tower);
        }

        float damage = fixedDamageOverride.HasValue
            ? Mathf.Max(0f, fixedDamageOverride.Value)
            : tower.GetDamage(data, false);

        if (OnHitParticleEffect.instance != null)
        {
            OnHitParticleEffect.instance.OnHitVfx(enemy.transform.position, GetDamageTypeColor(damageType));
        }

        enemy.Flash();
        health.TakeDamage(damage, tower, damageType, data);

        if (applyEffects)
        {
            tower.ApplyEffects(enemy);
        }
    }

    public void LightningVisualization(List<(Transform start, Transform end)> segments, CM.ColorType damageType=CM.ColorType.None)
    {
        if (segments == null || segments.Count == 0) return;
        if (LaserObjectPool.instance == null) return;

        StartCoroutine(LightningVisualizationEchoRoutine(segments, damageType));
    }

    private IEnumerator LightningVisualizationEchoRoutine(List<(Transform start, Transform end)> segments, CM.ColorType damageType)
    {
        Color initialColor = GetDamageTypeColor(damageType);

        Color echoColor = Color.white;

        for (int i = 0; i < segments.Count; i++)
        {
            Transform start = segments[i].start;
            Transform end = segments[i].end;
            if (start == null || end == null) continue;

            StartCoroutine(PlaySegmentedBoltDelayed(
                start,
                end,
                null,
                echoColor,
                echoLaserWidth,
                echoLaserDuration,
                echoLaserAlpha,
                lightningEchoVfxDelay));

            yield return PlaySegmentedBolt(
                start,
                end,
                null,
                initialColor,
                initialLaserWidth,
                initialLaserDuration,
                initialLaserAlpha);
        }
    }

    private static float GetVisualScaledDuration(float duration)
    {
        if (duration <= 0f)
        {
            return 0f;
        }

        return duration * Mathf.Max(Time.timeScale, 0.01f);
    }

    private IEnumerator PlaySegmentedBolt(Transform startTransform, Transform endTransform, Enemy trackedEnemy, Color color, float width, float duration, float alpha)
    {
        if (startTransform == null)
            yield break;

        Vector3 startPos = startTransform.position;
        Vector3 endPos = ResolveCurrentEndPosition(endTransform, trackedEnemy);
        if (endTransform == null && trackedEnemy == null)
            yield break;

        startPos.z = 0f;
        endPos.z = 0f;

        float randomSegmentLength = Random.Range(Mathf.Max(0.01f, segmentLengthMin), Mathf.Max(0.01f, segmentLengthMax));
        float initialLen = (endPos - startPos).magnitude;
        int segCount = Mathf.Max(1, Mathf.CeilToInt(initialLen / Mathf.Max(0.01f, randomSegmentLength)));

        int jitterCount = Mathf.Max(0, segCount - 1);
        float[] jitterSamples = new float[jitterCount];
        for (int i = 1; i < segCount; i++)
        {
            jitterSamples[i - 1] = Random.Range(-segmentJitter, segmentJitter);
        }

        float delay = Mathf.Max(0f, segmentSpawnDelay);
        float visualDuration = GetVisualScaledDuration(duration);

        for (int i = 0; i < segCount; i++)
        {
            if (startTransform == null)
                break;

            Vector3 currentStart = startTransform.position;
            Vector3 currentEnd = ResolveCurrentEndPosition(endTransform, trackedEnemy);
            currentStart.z = 0f;
            currentEnd.z = 0f;

            Vector3 segStart = EvaluateSegmentPoint(currentStart, currentEnd, segCount, i, jitterSamples);
            Vector3 segEnd = EvaluateSegmentPoint(currentStart, currentEnd, segCount, i + 1, jitterSamples);

            LaserObjectPool.instance.ShowLaser(segStart, segEnd, color, width, visualDuration, alpha);
            TryApplyGridVizAlphaLine(segStart, segEnd);
            TryApplyGridVizColorLine(segStart, segEnd, color);
            if (delay > 0f && i < segCount - 1)
            {
                yield return new WaitForSeconds(delay);
            }
        }

    }

    private static Vector3 ResolveCurrentEndPosition(Transform endTransform, Enemy trackedEnemy)
    {
        if (trackedEnemy != null)
            return trackedEnemy.transform.position;

        if (endTransform != null)
            return endTransform.position;

        return Vector3.zero;
    }

    private static Vector3 EvaluateSegmentPoint(Vector3 start, Vector3 end, int segCount, int pointIndex, float[] jitterSamples)
    {
        int clampedCount = Mathf.Max(1, segCount);
        int clampedIndex = Mathf.Clamp(pointIndex, 0, clampedCount);
        float t = clampedCount <= 0 ? 0f : clampedIndex / (float)clampedCount;

        Vector3 p = Vector3.Lerp(start, end, t);
        if (clampedIndex <= 0 || clampedIndex >= clampedCount)
        {
            p.z = 0f;
            return p;
        }

        Vector3 dir = end - start;
        float length = dir.magnitude;
        if (length <= 0.0001f)
        {
            p.z = 0f;
            return p;
        }

        Vector3 dirN = dir / length;
        Vector3 perp = new Vector3(-dirN.y, dirN.x, 0f);

        int jitterIndex = Mathf.Clamp(clampedIndex - 1, 0, jitterSamples.Length - 1);
        float envelope = Mathf.Sin(t * Mathf.PI);
        float offset = jitterSamples.Length > 0 ? jitterSamples[jitterIndex] * envelope : 0f;
        p += perp * offset;
        p.z = 0f;
        return p;
    }

    private static void BuildSegmentPoints(Vector3 start, Vector3 end, float desiredSegmentLength, float jitter, List<Vector3> points)
    {
        points.Clear();

        start.z = 0f;
        end.z = 0f;

        Vector3 dir = end - start;
        float length = dir.magnitude;

        if (length <= 0.0001f)
        {
            points.Add(start);
            points.Add(end);
            return;
        }

        float safeSegmentLen = Mathf.Max(0.01f, desiredSegmentLength);
        int segCount = Mathf.Max(1, Mathf.CeilToInt(length / safeSegmentLen));

        Vector3 dirN = dir / length;
        Vector3 perp = new Vector3(-dirN.y, dirN.x, 0f);

        points.Add(start);

        for (int i = 1; i < segCount; i++)
        {
            float t = i / (float)segCount;
            Vector3 p = Vector3.Lerp(start, end, t);

            float envelope = Mathf.Sin(t * Mathf.PI);
            float offset = Random.Range(-jitter, jitter) * envelope;
            p += perp * offset;
            p.z = 0f;

            points.Add(p);
        }

        points.Add(end);
    }

    public void PlayCyanLaserBounceLightning(Vector3 start, Vector3 end)
    {
        if (LaserObjectPool.instance == null) return;

        StartCoroutine(PlaySegmentedBoltNoDelay(
            start,
            end,
            GetDamageTypeColor(CM.ColorType.Cyan),
            cyanLaserWidth,
            cyanLaserDuration,
            cyanLaserAlpha,
            cyanLaserSegmentLength,
            cyanLaserSegmentJitter));
    }

    private IEnumerator PlaySegmentedBoltNoDelay(
        Vector3 start,
        Vector3 end,
        Color color,
        float width,
        float duration,
        float alpha,
        float customSegmentLength,
        float customSegmentJitter)
    {
        var segmentPoints = _segmentPointsPool.Get();
        BuildSegmentPoints(start, end, customSegmentLength, customSegmentJitter, segmentPoints);
        if (segmentPoints.Count < 2)
        {
            _segmentPointsPool.Release(segmentPoints);
            yield break;
        }

        for (int i = 0; i < segmentPoints.Count - 1; i++)
        {
            Vector3 segStart = segmentPoints[i];
            Vector3 segEnd = segmentPoints[i + 1];

            LaserObjectPool.instance.ShowLaser(segStart, segEnd, color, width, GetVisualScaledDuration(duration), alpha);
            TryApplyGridVizAlphaLine(segStart, segEnd);
            TryApplyGridVizColorLine(segStart, segEnd, color);
        }

        _segmentPointsPool.Release(segmentPoints);
    }

    private void TryApplyGridVizAlphaLine(Vector3 start, Vector3 end)
    {
        if (!gridVizAlphaIncrease) return;

        if (_gridViz == null)
        {
            _gridViz = FindFirstObjectByType<GridViz>();
        }

        if (_gridViz == null) return;
        if (!_gridViz.AllowLaserPoolAlphaLine) return;

        _gridViz.AlphaLine(gridVizAlphaIncreaseAmount, start, end, gridVizAlphaLineWidthCells);
    }

    private void TryApplyGridVizColorLine(Vector3 start, Vector3 end, Color color)
    {
        if (!gridVizColorLine) return;

        if (_gridViz == null)
        {
            _gridViz = FindFirstObjectByType<GridViz>();
        }

        if (_gridViz == null) return;

        _gridViz.ColorLine(color, gridVizColorLineIncreaseAmount, start, end, gridVizColorLineWidthCells);
    }

    public int GetEnemiesInLightningRange(Vector3 position, float range, out Collider2D[] cols)
    {
        cols = colliders;

        if (range <= 0f)
        {
            return 0;
        }

        if (QuadTree2D.instance != null)
        {
            QuadTree2D.instance.QueryCircle(position, range, _quadtreeResults);

            int count = 0;
            for (int i = 0; i < _quadtreeResults.Count && count < colliders.Length; i++)
            {
                Enemy enemy = _quadtreeResults[i];
                if (enemy == null) continue;

                Collider2D col = enemy.GetComponent<Collider2D>();
                if (col == null) col = enemy.GetComponentInChildren<Collider2D>();
                if (col == null) continue;

                colliders[count++] = col;
            }

            for (int i = count; i < colliders.Length; i++)
            {
                colliders[i] = null;
            }

            return count;
        }

        int hitCount = Physics2D.OverlapCircle(position, range, _enemyFilter, colliders);
        if (hitCount <= 0)
        {
            return 0;
        }

        return hitCount;
    }
}
