using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ProjectileObjectPool : MonoBehaviour
{
    public static ProjectileObjectPool instance { get; private set; }

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int GlowGlobalId = Shader.PropertyToID("_GlowGlobal");
    private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");
    private static readonly int ShineRotateId = Shader.PropertyToID("_ShineRotate");
    private static readonly int ShineGlowId = Shader.PropertyToID("_ShineGlow");

    [Header("Setup")]
    [Tooltip("Projectile prefab this pool manages.")]
    public GameObject projectilePrefab;
    [SerializeField] private int prewarmCount = 32;
    [SerializeField] private bool canGrow = true;

    [Header("Projectile Scale")]
    [Tooltip("Y scale applied to a projectile when its speed equals projectileYScaleSpeedReference. At speed 0 the Y scale matches the prefab's X scale.")]
    public float projectileYScaleMax = 1f;
    [Tooltip("The speed at which projectileYScaleMax is reached. Scale is linearly interpolated between 0 speed (Y = X scale) and this speed (Y = projectileYScaleMax).")]
    public float projectileYScaleSpeedReference = 10f;

    [Header("Projectile VFX")]
    [Tooltip("If true, projectiles spawned by ProjectileHelper use attached particle VFX by default.")]
    public bool enableParticleVfxByDefault = false;

    [Header("AllIn1 Shader")]
    [SerializeField] private bool enableAllIn1ShaderFeatures = true;
    [SerializeField, Min(0f)] private float allIn1GlowIntensity = 3f;
    [SerializeField, Min(0f)] private float allIn1GlowGlobal = 1f;
    [SerializeField, Range(0f, 1f)] private float allIn1HitEffectBlend = 0.9f;
    [SerializeField, Min(0f)] private float allIn1ShineGlow = 1f;
    [SerializeField] private bool allIn1RandomizeShineAngle = true;

    private ObjectPool<GameObject> _pool;
    private readonly HashSet<int> _pendingParticleReleaseIds = new HashSet<int>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Avoid nuking the shared managers GameObject.
            Destroy(this);
            return;
        }

        instance = this;

        if (projectilePrefab == null)
        {
            Debug.LogError("ProjectileObjectPool requires a projectilePrefab.", this);
            return;
        }

        int initialCapacity = Mathf.Max(1, prewarmCount);
        int maxSize = canGrow ? 10000 : initialCapacity;

        _pool = new ObjectPool<GameObject>(
            createFunc: CreateInstance,
            actionOnGet: OnTakeFromPool,
            actionOnRelease: OnReturnedToPool,
            actionOnDestroy: OnDestroyPooledObject,
            collectionCheck: false,
            defaultCapacity: initialCapacity,
            maxSize: maxSize);

        Prewarm();
    }

    private void Prewarm()
    {
        if (_pool == null) return;

        int count = Mathf.Max(0, prewarmCount);
        for (int i = 0; i < count; i++)
        {
            var go = _pool.Get();
            _pool.Release(go);
        }
    }

    private GameObject CreateInstance()
    {
        var go = Instantiate(projectilePrefab, transform);
        go.name = projectilePrefab.name + " (Pooled)";

        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.pool = this;
        }

        var allIn1 = go.GetComponent<PooledProjectileAllIn1>();
        if (allIn1 == null) allIn1 = go.AddComponent<PooledProjectileAllIn1>();
        allIn1.pool = this;
        allIn1.CacheRenderers();

        go.SetActive(false);
        return go;
    }

    private static void OnTakeFromPool(GameObject go)
    {
        // Intentionally no-op. Activation happens in Get() after transform is set,
        // which avoids TrailRenderer streaks from stale previous positions.
    }

    private void OnReturnedToPool(GameObject go)
    {
        if (go == null) return;

        if (go.TryGetComponent<Projectile>(out var projectile) && projectile != null)
        {
            projectile.OnReturnedToPool();
        }

        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
    }

    private static void OnDestroyPooledObject(GameObject go)
    {
        if (go != null) Destroy(go);
    }

    /// <summary>
    /// Gets a projectile from the pool and spawns it.
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        if (_pool == null || projectilePrefab == null) return null;

        if (!canGrow && _pool.CountInactive <= 0 && _pool.CountAll >= Mathf.Max(1, prewarmCount))
        {
            return null;
        }

        var go = _pool.Get();
        if (go == null) return null;

        go.transform.SetParent(null, worldPositionStays: false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        if (go.TryGetComponent<Projectile>(out var projectile) && projectile != null)
        {
            projectile.OnSpawnedFromPool();
        }

        return go;
    }

    public void RefreshAllIn1Properties(GameObject go)
    {
        if (go == null || !enableAllIn1ShaderFeatures) return;

        var allIn1 = go.GetComponent<PooledProjectileAllIn1>();
        if (allIn1 == null)
        {
            allIn1 = go.AddComponent<PooledProjectileAllIn1>();
            allIn1.CacheRenderers();
        }

        allIn1.pool = this;
        allIn1.Apply();
    }

    /// <summary>
    /// Returns a projectile to the pool.
    /// </summary>
    public void Release(GameObject go)
    {
        if (go == null) return;
        if (_pool == null)
        {
            Destroy(go);
            return;
        }

        int id = go.GetInstanceID();
        if (_pendingParticleReleaseIds.Contains(id)) return;

        if (go.activeInHierarchy
            && go.TryGetComponent<Projectile>(out var projectile)
            && projectile != null
            && projectile.BeginSoftReleaseForParticleVfx())
        {
            _pendingParticleReleaseIds.Add(id);
            StartCoroutine(ReleaseAfterParticleVfx(go, projectile, id));
            return;
        }

        _pool.Release(go);
    }

    private IEnumerator ReleaseAfterParticleVfx(GameObject go, Projectile projectile, int id)
    {
        while (go != null
               && projectile != null
               && go.activeInHierarchy
               && projectile.HasLiveParticleVfx())
        {
            yield return null;
        }

        _pendingParticleReleaseIds.Remove(id);

        if (go == null) yield break;

        if (_pool == null)
        {
            Destroy(go);
            yield break;
        }

        if (projectile != null)
        {
            projectile.RestoreAfterSoftRelease();
        }

        _pool.Release(go);
    }

    private sealed class PooledProjectileAllIn1 : MonoBehaviour
    {
        [HideInInspector] public ProjectileObjectPool pool;

        private SpriteRenderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        public void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        }

        public void Apply()
        {
            if (pool == null || !pool.enableAllIn1ShaderFeatures) return;
            if (_renderers == null || _renderers.Length == 0) CacheRenderers();
            if (_renderers == null || _renderers.Length == 0) return;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                var sr = _renderers[i];
                if (sr == null) continue;

                var mat = sr.sharedMaterial;
                if (mat == null) continue;

                bool supportsAllIn1 = mat.HasProperty(GlowId)
                    || mat.HasProperty(GlowColorId)
                    || mat.HasProperty(HitEffectBlendId)
                    || mat.HasProperty(ShineRotateId)
                    || mat.HasProperty(ShineGlowId);

                if (!supportsAllIn1) continue;

                sr.GetPropertyBlock(_mpb);

                Color c = sr.color;
                if (mat.HasProperty(ColorId))
                {
                    _mpb.SetColor(ColorId, c);
                }

                if (mat.HasProperty(GlowColorId))
                {
                    _mpb.SetColor(GlowColorId, c);
                }

                if (mat.HasProperty(GlowId))
                {
                    _mpb.SetFloat(GlowId, pool.allIn1GlowIntensity * Mathf.Clamp01(c.a));
                }

                if (mat.HasProperty(GlowGlobalId))
                {
                    _mpb.SetFloat(GlowGlobalId, pool.allIn1GlowGlobal);
                }

                if (mat.HasProperty(HitEffectBlendId))
                {
                    _mpb.SetFloat(HitEffectBlendId, pool.allIn1HitEffectBlend);
                }

                if (mat.HasProperty(ShineGlowId))
                {
                    _mpb.SetFloat(ShineGlowId, pool.allIn1ShineGlow);
                }

                if (pool.allIn1RandomizeShineAngle && mat.HasProperty(ShineRotateId))
                {
                    _mpb.SetFloat(ShineRotateId, Random.Range(0f, Mathf.PI * 2f));
                }

                sr.SetPropertyBlock(_mpb);
            }
        }
    }
}
