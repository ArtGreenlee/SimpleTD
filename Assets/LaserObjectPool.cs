using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserObjectPool : MonoBehaviour
{
    public static LaserObjectPool instance { get; private set; }

    [Header("Setup")]
    public GameObject laserPrefab;
    [SerializeField] private int prewarmCount = 16;
    [SerializeField] private bool canGrow = true;

    [Header("Visualization")]
    [Tooltip("How quickly the laser line alpha decays to zero (seconds).")]
    [Min(0.001f)]
    public float fadeDuration = 0.15f;

    [Tooltip("Extra small offset in Z to avoid z-fighting in2D sorting setups.")]
    public float zOffset = 0f;

    [Tooltip("Visual width of the laser in world units.")]
    [Min(0.001f)]
    public float laserWidth = 0.2f;

    [Tooltip("Minimum visual width the laser can be clamped to, regardless of any width override.")]
    [Min(0.001f)]
    public float laserMinWidth = 0.05f;

    [Tooltip("Starting alpha of the laser when fired.")]
    [Range(0f, 1f)]
    public float laserAlpha = 1f;

    [Header("AllIn1 Shader")]
    [SerializeField] private bool enableAllIn1ShaderFeatures = true;
    [SerializeField, Min(0f)] private float allIn1GlowIntensity = 6f;
    [SerializeField, Min(0f)] private float allIn1GlowGlobal = 1f;
    [SerializeField, Range(0f, 1f)] private float allIn1HitEffectBlend = 0.9f;
    [SerializeField, Min(0f)] private float allIn1ShineGlow = 1.25f;
    [SerializeField] private bool allIn1RandomizeShineAngle = true;

    [Header("Grid Viz")]
    [SerializeField] private bool gridVizAlphaIncrease = false;
    [SerializeField, Min(0f)] private float gridVizAlphaIncreaseAmount = 0.2f;
    [SerializeField, Min(1)] private int gridVizAlphaLineWidthCells = 1;

    [Header("Performance")]
    [Tooltip("Maximum number of pooled laser spawn requests processed in a single frame. Extra requests are deferred.")]
    [SerializeField, Min(1)] private int maxLaserSpawnsPerFrame = 64;

    private readonly Queue<GameObject> _available = new Queue<GameObject>(32);
    private readonly Queue<LaserSpawnRequest> _pendingSpawnRequests = new Queue<LaserSpawnRequest>(128);
    private GridViz _gridViz;
    private int _budgetFrame = -1;
    private int _spawnsUsedThisFrame;

    private struct LaserSpawnRequest
    {
        public bool followTransforms;
        public Transform startTransform;
        public Transform endTransform;
        public Vector3 start;
        public Vector3 end;
        public Color color;
        public float? widthOverride;
        public float? fadeDurationOverride;
        public float? alphaOverride;
        public bool applyGridVizAlpha;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Avoid nuking the shared managers GameObject.
            Destroy(this);
            return;
        }

        instance = this;

        if (laserPrefab == null)
        {
            Debug.LogError("LaserObjectPool requires a laserPrefab.", this);
            return;
        }

        Prewarm();
    }

    private void Update()
    {
        ProcessPendingSpawnRequests();
    }

    private void OnDisable()
    {
        _pendingSpawnRequests.Clear();
    }

    private void Prewarm()
    {
        int count = Mathf.Max(0, prewarmCount);
        for (int i = 0; i < count; i++)
        {
            var go = CreateInstance();
            Release(go);
        }
    }

    private GameObject CreateInstance()
    {
        var go = Instantiate(laserPrefab, transform);
        go.name = laserPrefab.name + " (Pooled)";
        go.SetActive(false);

        var ctrl = go.GetComponent<PooledLaserVisualizer>();
        if (ctrl == null) ctrl = go.AddComponent<PooledLaserVisualizer>();
        ctrl.pool = this;

        // Ensure a LineRenderer exists.
        var lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.enabled = false;

        return go;
    }

    private GameObject Get(Vector3 position, Quaternion rotation)
    {
        if (laserPrefab == null) return null;

        GameObject go = null;
        while (_available.Count > 0 && go == null)
        {
            go = _available.Dequeue();
        }

        if (go == null)
        {
            if (!canGrow) return null;
            go = CreateInstance();
        }

        go.transform.SetParent(null, worldPositionStays: false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        return go;
    }

    private void Release(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
        _available.Enqueue(go);
    }

    public void ShowLaser(Vector3 start, Vector3 end, Color color)
    {
        ShowLaser(start, end, color, laserWidthOverride: null, fadeDurationOverride: null, alphaOverride: null);
    }

    public void ShowLaser(Vector3 start, Vector3 end, Color color, float? laserWidthOverride)
    {
        ShowLaser(start, end, color, laserWidthOverride, fadeDurationOverride: null, alphaOverride: null);
    }

    public void ShowLaser(Vector3 start, Vector3 end, Color color, float? laserWidthOverride, float? fadeDurationOverride, float? alphaOverride)
    {
        ShowLaser(start, end, color, laserWidthOverride, fadeDurationOverride, alphaOverride, applyGridVizAlphaIncrease: false);
    }

    public void ShowLaser(Vector3 start, Vector3 end, Color color, float? laserWidthOverride, float? fadeDurationOverride, float? alphaOverride, bool applyGridVizAlphaIncrease)
    {
        if (laserPrefab == null) return;

        var request = new LaserSpawnRequest
        {
            followTransforms = false,
            start = start,
            end = end,
            color = color,
            widthOverride = laserWidthOverride,
            fadeDurationOverride = fadeDurationOverride,
            alphaOverride = alphaOverride,
            applyGridVizAlpha = applyGridVizAlphaIncrease,
        };

        TrySpawnOrEnqueue(request);
    }

    public void ShowLaser(Transform start, Transform end, Color color, float? laserWidthOverride)
    {
        ShowLaser(start, end, color, laserWidthOverride, fadeDurationOverride: null, alphaOverride: null);
    }

    public void ShowLaser(Transform start, Transform end, Color color, float? laserWidthOverride, float? fadeDurationOverride, float? alphaOverride)
    {
        if (laserPrefab == null) return;
        if (start == null || end == null) return;

        var request = new LaserSpawnRequest
        {
            followTransforms = true,
            startTransform = start,
            endTransform = end,
            color = color,
            widthOverride = laserWidthOverride,
            fadeDurationOverride = fadeDurationOverride,
            alphaOverride = alphaOverride,
            applyGridVizAlpha = false,
        };

        TrySpawnOrEnqueue(request);
    }

    private void TrySpawnOrEnqueue(LaserSpawnRequest request)
    {
        if (TryConsumeSpawnBudget())
        {
            SpawnFromRequest(request);
            return;
        }

        _pendingSpawnRequests.Enqueue(request);
    }

    private void ProcessPendingSpawnRequests()
    {
        while (_pendingSpawnRequests.Count > 0 && TryConsumeSpawnBudget())
        {
            LaserSpawnRequest request = _pendingSpawnRequests.Dequeue();
            SpawnFromRequest(request);
        }
    }

    private bool TryConsumeSpawnBudget()
    {
        int frame = Time.frameCount;
        if (_budgetFrame != frame)
        {
            _budgetFrame = frame;
            _spawnsUsedThisFrame = 0;
        }

        int budget = Mathf.Max(1, maxLaserSpawnsPerFrame);
        if (_spawnsUsedThisFrame >= budget)
        {
            return false;
        }

        _spawnsUsedThisFrame++;
        return true;
    }

    private void SpawnFromRequest(LaserSpawnRequest request)
    {
        if (laserPrefab == null) return;

        Vector3 start = request.start;
        Vector3 end = request.end;

        if (request.followTransforms)
        {
            if (request.startTransform == null || request.endTransform == null)
            {
                return;
            }

            start = request.startTransform.position;
            end = request.endTransform.position;
        }

        var go = Get(new Vector3(start.x, start.y, start.z + zOffset), Quaternion.identity);
        if (go == null) return;

        var ctrl = go.GetComponent<PooledLaserVisualizer>();
        if (ctrl == null) ctrl = go.AddComponent<PooledLaserVisualizer>();
        ctrl.pool = this;

        float w = request.widthOverride.HasValue ? Mathf.Max(0.001f, request.widthOverride.Value) : laserWidth;
        w = Mathf.Max(w, laserMinWidth);
        float a = request.alphaOverride.HasValue ? Mathf.Clamp01(request.alphaOverride.Value) : laserAlpha;
        float d = request.fadeDurationOverride.HasValue ? Mathf.Max(0.01f, request.fadeDurationOverride.Value) : fadeDuration;

        if (request.followTransforms)
        {
            ctrl.PlayFollowing(request.startTransform, request.endTransform, request.color, w, a, d, zOffset);
        }
        else
        {
            ctrl.Play(start, end, request.color, w, a, d, zOffset);
            if (request.applyGridVizAlpha)
            {
                TryApplyGridVizAlphaIncrease(start, end);
            }
        }
    }

    public void TryApplyGridVizAlphaIncrease(Vector3 start, Vector3 end)
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

    private sealed class PooledLaserVisualizer : MonoBehaviour
    {
        [HideInInspector] public LaserObjectPool pool;

        [SerializeField] private LineRenderer lr;
        private Coroutine _routine;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int GlowGlobalId = Shader.PropertyToID("_GlowGlobal");
        private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");
        private static readonly int ShineRotateId = Shader.PropertyToID("_ShineRotate");
        private static readonly int ShineGlowId = Shader.PropertyToID("_ShineGlow");

        private MaterialPropertyBlock _mpb;
        private bool _supportsAllIn1;

        private Transform _startTransform;
        private Transform _endTransform;
        private bool _followTransforms;
        private float _zOffset;

        private void Awake()
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.positionCount = 2;
                lr.enabled = false;
                CacheShaderSupport();
            }
        }

        private void CacheShaderSupport()
        {
            _supportsAllIn1 = false;
            if (lr == null) return;

            var mat = lr.sharedMaterial;
            if (mat == null) return;

            _supportsAllIn1 = mat.HasProperty(GlowId)
                || mat.HasProperty(GlowColorId)
                || mat.HasProperty(HitEffectBlendId)
                || mat.HasProperty(ShineRotateId)
                || mat.HasProperty(ShineGlowId);

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _startTransform = null;
            _endTransform = null;
            _followTransforms = false;
        }

        public void Play(Vector3 start, Vector3 end, Color rgb, float width, float alpha, float fadeDuration, float zOffset)
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            if (lr == null)
            {
                if (pool != null) pool.Release(gameObject);
                return;
            }

            CacheShaderSupport();

            _startTransform = null;
            _endTransform = null;
            _followTransforms = false;
            _zOffset = zOffset;

            lr.enabled = true;
            lr.positionCount = 2;

            lr.SetPosition(0, new Vector3(start.x, start.y, zOffset));
            lr.SetPosition(1, new Vector3(end.x, end.y, zOffset));

            float w = Mathf.Max(0.001f, width);
            lr.startWidth = w;
            lr.endWidth = w;

            float a = Mathf.Clamp01(alpha);
            Color startColor = new Color(rgb.r, rgb.g, rgb.b, a);
            lr.startColor = startColor;
            lr.endColor = startColor;
            ApplyAllIn1ShaderProperties(startColor, 1f);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FadeRoutine(fadeDuration));
        }

        public void PlayFollowing(Transform start, Transform end, Color rgb, float width, float alpha, float fadeDuration, float zOffset)
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            if (lr == null)
            {
                if (pool != null) pool.Release(gameObject);
                return;
            }

            CacheShaderSupport();

            _startTransform = start;
            _endTransform = end;
            _followTransforms = true;
            _zOffset = zOffset;

            lr.enabled = true;
            lr.positionCount = 2;

            Vector3 s = start.position;
            Vector3 e = end.position;
            lr.SetPosition(0, new Vector3(s.x, s.y, zOffset));
            lr.SetPosition(1, new Vector3(e.x, e.y, zOffset));

            float w = Mathf.Max(0.001f, width);
            lr.startWidth = w;
            lr.endWidth = w;

            float a = Mathf.Clamp01(alpha);
            Color startColor = new Color(rgb.r, rgb.g, rgb.b, a);
            lr.startColor = startColor;
            lr.endColor = startColor;
            ApplyAllIn1ShaderProperties(startColor, 1f);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FadeRoutine(fadeDuration));
        }

        private IEnumerator FadeRoutine(float fadeDuration)
        {
            if (lr == null) yield break;

            float dur = Mathf.Max(0.01f, fadeDuration);
            float t = 0f;

            Color sc0 = lr.startColor;
            Color ec0 = lr.endColor;
            float a0s = sc0.a;
            float a0e = ec0.a;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = t / dur;

                if (_followTransforms)
                {
                    if (_startTransform != null && _startTransform.gameObject.activeInHierarchy)
                    {
                        Vector3 s = _startTransform.position;
                        lr.SetPosition(0, new Vector3(s.x, s.y, _zOffset));
                    }

                    if (_endTransform != null && _endTransform.gameObject.activeInHierarchy)
                    {
                        Vector3 e = _endTransform.position;
                        lr.SetPosition(1, new Vector3(e.x, e.y, _zOffset));
                    }
                }

                Color sc = lr.startColor;
                Color ec = lr.endColor;
                sc.a = Mathf.Lerp(a0s, 0f, u);
                ec.a = Mathf.Lerp(a0e, 0f, u);
                lr.startColor = sc;
                lr.endColor = ec;

                ApplyAllIn1ShaderProperties(sc, 1f - Mathf.Clamp01(u));

                yield return null;
            }

            var scF = lr.startColor;
            var ecF = lr.endColor;
            scF.a = 0f;
            ecF.a = 0f;
            lr.startColor = scF;
            lr.endColor = ecF;
            ApplyAllIn1ShaderProperties(scF, 0f);
            lr.enabled = false;

            if (pool != null) pool.Release(gameObject);
        }

        private void ApplyAllIn1ShaderProperties(Color c, float life01)
        {
            if (!_supportsAllIn1 || pool == null || !pool.enableAllIn1ShaderFeatures || lr == null)
            {
                return;
            }

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }

            lr.GetPropertyBlock(_mpb);

            Material mat = lr.sharedMaterial;
            if (mat == null)
            {
                lr.SetPropertyBlock(_mpb);
                return;
            }

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
                _mpb.SetFloat(HitEffectBlendId, pool.allIn1HitEffectBlend * Mathf.Clamp01(life01));
            }

            if (mat.HasProperty(ShineGlowId))
            {
                _mpb.SetFloat(ShineGlowId, pool.allIn1ShineGlow * Mathf.Clamp01(life01));
            }

            if (pool.allIn1RandomizeShineAngle && mat.HasProperty(ShineRotateId))
            {
                _mpb.SetFloat(ShineRotateId, Random.Range(0f, Mathf.PI * 2f));
            }

            lr.SetPropertyBlock(_mpb);
        }
    }
}
