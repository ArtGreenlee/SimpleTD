using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserHelper : MonoBehaviour
{
    public static LaserHelper instance;

    [Tooltip("Width of the damage capsule when no explicit width is passed.")]
    [SerializeField] private float defaultLaserWidth = 0.5f;
    [SerializeField] private bool laserPassThroughRod = true;

    private ContactFilter2D _filter;
    private readonly HashSet<int> _uniqueIds = new HashSet<int>(64);
    private readonly HashSet<LensTower> _activatedRods = new HashSet<LensTower>(8);
    private Collider2D[] _helperCols = new Collider2D[64];
    private bool _initialized;
    [Header("Grid Viz")]
    [SerializeField] private bool gridVizColorLine = false;
    [SerializeField, Min(0f)] private float gridVizColorLineIncreaseAmount = 0.2f;
    [SerializeField, Min(1)] private int gridVizColorLineWidthCells = 1;
    private GridViz _gridViz;

    private void Awake()
    {
        instance = this;
        InitializeFilter();
    }

    private void OnEnable()
    {
        if (!_initialized) InitializeFilter();
    }

    private void InitializeFilter()
    {
        LayerMask m = (LayerMaskManager.instance != null)
            ? LayerMaskManager.instance.laserLayerMask : (LayerMask)~0;
        _filter = new ContactFilter2D();
        _filter.useLayerMask = true;
        _filter.layerMask = m;
        _filter.useTriggers = true;
        _initialized = true;
    }

    public void RefreshMask()
    {
        _initialized = false;
        InitializeFilter();
    }

    public void LaserAttackHelper(Tower tower, Vector3 start, Vector3 end,
                                  float width = -1f, bool applyEffects = false,
                                  Tower.CustomDamageData data = null,
                                  int bounceIndex = 0,
                                  List<Effect> effectOverrideList = null)
    {
        float w = width < 0f ? defaultLaserWidth : width;
        int hit = GetHitsOnLine(start, end, w, _helperCols);
        if (data == null) data = new Tower.CustomDamageData();
        tower.GetDamageType(data, true);
        data.numHit = hit;
        data.critCount = tower.RollCritCount();
        data.crit = data.critCount > 0;
        data.hitColliders = _helperCols;

        float bounceMultiplier = 1f;
        if (bounceIndex > 0 && tower.UpgradeActive(UpgradeData.UID.IncreaseLaserDamagePerBounce))
        {
            float capPercent = UpgradeData.IncreaseLaserDamagePerBounceCap;
            float perBouncePercent = UpgradeData.IncreaseLaserDamagePerBouncePercent;
            float totalBoostPercent = Mathf.Min(bounceIndex * perBouncePercent, capPercent);
            bounceMultiplier = 1f + totalBoostPercent / 100f;
        }

        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyLaserEnemiesHit(hit);
        }

        _activatedRods.Clear();

        for (int i = 0; i < hit; i++)
        {
            Collider2D col = _helperCols[i];
            if (col == null) continue;

            LensTower rodTower = GetLensTowerFromRodCollider(col);
            if (rodTower != null)
            {
                if (_activatedRods.Add(rodTower))
                {
                    Vector3 rodHitPosition = col.ClosestPoint(start);
                    rodTower.OnLaserTriggerEnterRod(tower, rodHitPosition, data);
                }
                continue;
            }

            Enemy e = col.GetComponent<Enemy>();
            if (e == null) e = col.GetComponentInParent<Enemy>();
            if (e == null) continue;
            data.enemyHit = e;
            e.health.TakeDamage(tower.GetDamage(data, false) * bounceMultiplier, tower, tower.GetDamageType(data), data);
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

            if (RM.i != null && RM.i.Active(RM.ID.laserStun) && Random.value <= 0.01f)
            {
                e.StunEnemy(2f);
            }
        }
    }

    public int GetHitsOnLine(Vector2 start, Vector2 end, float width, Collider2D[] results)
    {
        if (!_initialized) InitializeFilter();
        Vector2 dir = end - start;
        float length = dir.magnitude;
        if (length < 0.001f) return 0;
        Vector2 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 size = new Vector2(length, Mathf.Max(0.001f, width));
        _uniqueIds.Clear();
        int count = Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Horizontal, angle, _filter, results);
        if (count <= 1) return count;
        int write = 0;
        for (int read = 0; read < count; read++)
        {
            Collider2D col = results[read];
            if (col == null) continue;
            Rigidbody2D rb = col.attachedRigidbody;
            int id = rb != null ? rb.GetInstanceID() : col.gameObject.GetInstanceID();
            if (_uniqueIds.Add(id)) results[write++] = col;
        }
        for (int i = write; i < count; i++) results[i] = null;
        return write;
    }

    private LensTower GetLensTowerFromRodCollider(Collider2D col)
    {
        if (col == null) return null;

        Rod rod = col.GetComponent<Rod>();
        if (rod == null) rod = col.GetComponentInParent<Rod>();
        if (rod == null) return null;

        Lens lens = rod.GetComponentInParent<Lens>();
        if (lens == null) return null;

        return lens.GetComponentInParent<LensTower>();
    }

    #region Bounce Logic

    private struct LaserSegment
    {
        public Vector2 Start;
        public Vector2 End;
        public bool EndsOnWall;

        public LaserSegment(Vector2 start, Vector2 end, bool endsOnWall)
        {
            Start = start;
            End = end;
            EndsOnWall = endsOnWall;
        }
    }

    public void LaserAttackHelper(
        Tower tower, Vector3 start, Vector3 direction, float width,
        Color color,
        int? bounceCountOverride = null,
        bool applyEffects = true,
        List<Effect> effectOverrideList = null,
        float segmentMaxDistance = 50f,
        float bounceSurfaceOffset = 0.01f,
        float bounceAngleRandomRange = 0f,
        float bounceWidthFalloffPerBounce = 0.05f,
        float bounceDelayPerUnit = 0.01f,
        int wallHitMinParticles = 2,
        int wallHitMaxParticles = 5)
    {
        Vector2 origin = start;
        Vector2 dir = ((Vector2)direction).normalized;

        int effectiveBounces = GetEffectiveBounceCount(tower, bounceCountOverride);

        var segments = BuildLaserSegments(origin, dir, effectiveBounces, segmentMaxDistance, bounceSurfaceOffset, bounceAngleRandomRange);
        if (segments.Count == 0) return;

        int actualBounces = segments.Count - 1;
        if (SaveDataManager.instance != null)
        {
            SaveDataManager.instance.NotifyLaserBounceCount(actualBounces);
        }

        FireLaserSegment(tower, segments[0], width, color, applyEffects, wallHitMinParticles, wallHitMaxParticles, effectOverrideList: effectOverrideList);
        if (segments.Count > 1)
        {
            StartCoroutine(FireBouncedLaserSegments(tower, segments, width, color, applyEffects,
                effectOverrideList,
                bounceDelayPerUnit, bounceWidthFalloffPerBounce, wallHitMinParticles, wallHitMaxParticles));
        }
    }

    public int GetEffectiveBounceCount(Tower tower, int? bounceCountOverride = null)
    {
        int baseBounceCount = bounceCountOverride ?? (tower != null ? tower.GetBaseBounceCount() : 1);
        int bounceCount = Mathf.Max(0, baseBounceCount);

        if (TagManager.instance != null)
        {
            int purpleTagCount = TagManager.instance.GetTagCount(Tower.Tag.Purple);
            int purpleBounceBonus = TagManager.instance.GetPurpleTagBounceBonus();

            bool isPurpleTower = tower != null && tower.tags != null && tower.tags.Contains(Tower.Tag.Purple);

            // At 2-5 purple towers: bonus applies only to purple towers.
            if (purpleTagCount >= 2 && purpleTagCount < 6 && isPurpleTower)
            {
                bounceCount += purpleBounceBonus;
            }
            // At 6+ purple towers: bonus applies globally.
            else if (purpleTagCount >= 6)
            {
                bounceCount += purpleBounceBonus;
            }
        }

        if (RM.i != null && RM.i.Active(RM.ID.LaserBounce))
        {
            bounceCount += 2;
        }

        return Mathf.Max(0, bounceCount);
    }

    private List<LaserSegment> BuildLaserSegments(Vector2 origin, Vector2 direction, int maxBounces, float segmentMaxDistance, float bounceSurfaceOffset, float bounceAngleRandomRange)
    {
        var segments = new List<LaserSegment>();

        int wallMask = LayerMaskManager.instance != null ? LayerMaskManager.instance.wallLayerMask : 0;
        int rodMask = 0;
        if (!laserPassThroughRod && LayerMaskManager.instance != null)
        {
            int combinedLaserMask = LayerMaskManager.instance.laserLayerMask;
            int enemyMask = LayerMaskManager.instance.enemyLayerMask;
            rodMask = combinedLaserMask & ~enemyMask;
        }

        Vector2 currentOrigin = origin;
        Vector2 currentDirection = direction.normalized;
        int bouncesRemaining = (maxBounces > 0 && wallMask != 0) ? maxBounces : 0;

        while (true)
        {
            RaycastHit2D wallHit = wallMask != 0
                ? Physics2D.Raycast(currentOrigin, currentDirection, segmentMaxDistance, wallMask)
                : default;
            RaycastHit2D rodHit = rodMask != 0
                ? Physics2D.Raycast(currentOrigin, currentDirection, segmentMaxDistance, rodMask)
                : default;

            bool hasWallHit = wallHit.collider != null;
            bool hasRodHit = rodHit.collider != null;
            bool stopAtRod = hasRodHit && (!hasWallHit || rodHit.distance < wallHit.distance);

            if (stopAtRod)
            {
                segments.Add(new LaserSegment(currentOrigin, rodHit.point, false));
                break;
            }

            if (hasWallHit)
            {
                segments.Add(new LaserSegment(currentOrigin, wallHit.point, true));

                if (bouncesRemaining <= 0) break;

                Vector2 reflected = Vector2.Reflect(currentDirection, wallHit.normal).normalized;
                if (bounceAngleRandomRange > 0f)
                {
                    float angleOffset = Random.Range(-bounceAngleRandomRange, bounceAngleRandomRange);
                    reflected = (Quaternion.Euler(0f, 0f, angleOffset) * reflected).normalized;
                }

                currentOrigin = wallHit.point + reflected * bounceSurfaceOffset;
                currentDirection = reflected;
                bouncesRemaining--;
            }
            else
            {
                segments.Add(new LaserSegment(currentOrigin, currentOrigin + currentDirection * segmentMaxDistance, false));
                break;
            }
        }

        return segments;
    }

    private void FireLaserSegment(Tower tower, LaserSegment segment, float width, Color color, bool applyEffects, int wallHitMinParticles, int wallHitMaxParticles, int bounceIndex = 0, List<Effect> effectOverrideList = null)
    {
        Vector3 start = new Vector3(segment.Start.x, segment.Start.y, 0f);
        Vector3 end = new Vector3(segment.End.x, segment.End.y, 0f);

        LaserAttackHelper(tower, start, end, width, applyEffects, bounceIndex: bounceIndex, effectOverrideList: effectOverrideList);
        ShowLaserVisual(segment.Start, segment.End, width, color);

        if (IsCyanLaserColor(color) && LightningHelper.instance != null)
        {
            LightningHelper.instance.PlayCyanLaserBounceLightning(start, end);
        }

        if (segment.EndsOnWall)
        {
            SpawnWallHitVfx(segment.End, segment.Start, color, wallHitMinParticles, wallHitMaxParticles);
        }
    }

    private static bool IsCyanLaserColor(Color color)
    {
        if (CM.i != null)
        {
            Color cyan = CM.i.ColorTypeToColor(CM.ColorType.Cyan);
            const float tolerance = 0.02f;
            return Mathf.Abs(color.r - cyan.r) <= tolerance
                && Mathf.Abs(color.g - cyan.g) <= tolerance
                && Mathf.Abs(color.b - cyan.b) <= tolerance;
        }

        return color == Color.cyan;
    }

    private static void SpawnWallHitVfx(Vector2 hitPoint, Vector2 segmentStart, Color color, int minParticles, int maxParticles)
    {
        var onHit = OnHitParticleEffect.instance;
        if (onHit == null) return;

        int minCount = Mathf.Max(1, minParticles);
        int maxCount = Mathf.Max(minCount, maxParticles);
        int particleCount = Random.Range(minCount, maxCount + 1);

        Vector2 travelDir = (hitPoint - segmentStart).normalized;
        float angle = Mathf.Atan2(travelDir.y, travelDir.x) * Mathf.Rad2Deg;
        onHit.OnHitVfx(new Vector3(hitPoint.x, hitPoint.y, 0f), particleCount, color, useDirection: true, directionAngle: angle);
    }

    private static float GetSegmentWidth(float baseWidth, int bounceIndex, float widthFalloffPerBounce)
    {
        if (bounceIndex <= 0) return Mathf.Max(0.001f, baseWidth);

        float t = Mathf.Clamp01(widthFalloffPerBounce);
        float widthMultiplier = Mathf.Pow(1f - t, bounceIndex);
        return Mathf.Max(0.001f, baseWidth * widthMultiplier);
    }

    private IEnumerator FireBouncedLaserSegments(Tower tower, List<LaserSegment> segments, float width, Color color, bool applyEffects, List<Effect> effectOverrideList, float delayPerUnit, float widthFalloffPerBounce, int wallHitMinParticles, int wallHitMaxParticles)
    {
        for (int i = 1; i < segments.Count; i++)
        {
            float segmentLength = Vector2.Distance(segments[i - 1].Start, segments[i - 1].End);
            float delay = segmentLength * delayPerUnit;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            float segmentWidth = GetSegmentWidth(width, i, widthFalloffPerBounce);
            FireLaserSegment(tower, segments[i], segmentWidth, color, applyEffects, wallHitMinParticles, wallHitMaxParticles, bounceIndex: i, effectOverrideList: effectOverrideList);
        }
    }

    private void ShowLaserVisual(Vector2 start, Vector2 end, float width, Color color)
    {
        if (LaserObjectPool.instance != null)
        {
            Vector3 start3 = new Vector3(start.x, start.y, 0f);
            Vector3 end3   = new Vector3(end.x,   end.y,   0f);
            LaserObjectPool.instance.ShowLaser(start3, end3, color, width, fadeDurationOverride: null, alphaOverride: null, applyGridVizAlphaIncrease: true);
            TryApplyGridVizColorLine(start3, end3, color);
        }
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

    #endregion
}
