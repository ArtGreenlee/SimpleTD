using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class AOEObjectPool : MonoBehaviour
{
    public static AOEObjectPool instance { get; private set; }

    [Header("Setup")]
    [Tooltip("Prefab for an AOE circle visualization (e.g., a SpriteRenderer circle).")]
    public GameObject aoePrefab;
    [SerializeField] private int prewarmCount = 16;
    [SerializeField] private bool canGrow = true;

    [Header("Defaults")]
    [Tooltip("Default fade duration (seconds) used when calling PlayPulse without a fadeDuration.")]
    [Min(0.001f)]
    [SerializeField] private float defaultFadeDuration = 0.25f;

    [Tooltip("Default Z offset applied when calling PlayPulse without a zOffset.")]
    [SerializeField] private float defaultVisualDepthOffset = -0.01f;

    [Header("Fade")]
    [Tooltip("Starting alpha applied each time an AOE pulse is played.")]
    public float startingAlpha = 1f;

    [Header("Grid Viz")]
    [SerializeField] private bool gridVizAlphaIncrease = false;
    [SerializeField, Min(0f)] private float gridVizAlphaIncreaseAmount = 0.2f;

    [Header("Unmanaged Spawn")]
    [Tooltip("Fallback timeout (seconds) for unmanaged indicators when no timeout is specified.")]
    [Min(0.01f)]
    [SerializeField] private float defaultUnmanagedTimeout = 5f;

    private ObjectPool<GameObject> _pool;
    private GridViz _gridViz;
    private readonly Dictionary<GameObject, Coroutine> _timerRoutines = new Dictionary<GameObject, Coroutine>(32);
    private readonly Dictionary<GameObject, Coroutine> _unmanagedTimeoutRoutines = new Dictionary<GameObject, Coroutine>(16);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (aoePrefab == null)
        {
            Debug.LogError("AOEObjectPool requires an aoePrefab.", this);
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
        var go = Instantiate(aoePrefab, transform);
        go.name = aoePrefab.name + " (Pooled)";
        go.SetActive(false);

        var viz = go.GetComponent<AOEVisualizer>();
        if (viz != null) viz.pool = this;

        return go;
    }

    private static void OnTakeFromPool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);
    }

    private void OnReturnedToPool(GameObject go)
    {
        if (go == null) return;

        if (_timerRoutines.TryGetValue(go, out var routine) && routine != null)
        {
            StopCoroutine(routine);
            _timerRoutines.Remove(go);
        }

        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
    }

    private static void OnDestroyPooledObject(GameObject go)
    {
        if (go != null) Destroy(go);
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        if (aoePrefab == null || _pool == null) return null;

        if (!canGrow && _pool.CountInactive <= 0 && _pool.CountAll >= Mathf.Max(1, prewarmCount))
        {
            return null;
        }

        var go = _pool.Get();
        if (go == null) return null;

        go.transform.SetParent(null, worldPositionStays: false);
        go.transform.SetPositionAndRotation(position, rotation);

        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        if (_pool == null)
        {
            Destroy(go);
            return;
        }

        _pool.Release(go);
    }

    public void PlayTimerIndicator(Vector3 position, float size, Color color, float time)
    {
        if (aoePrefab == null) return;

        float duration = Mathf.Max(0.01f, time);
        var go = Get(new Vector3(position.x, position.y, position.z + defaultVisualDepthOffset), Quaternion.identity);
        if (go == null) return;

        SetMaskInteraction(go, SpriteMaskInteraction.VisibleOutsideMask);

        go.transform.localScale = new Vector3(size, size, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = color;
            c.a = Mathf.Clamp01(startingAlpha);
            sr.color = c;

            var lr = go.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        if (_timerRoutines.TryGetValue(go, out var oldRoutine) && oldRoutine != null)
        {
            StopCoroutine(oldRoutine);
        }

        _timerRoutines[go] = StartCoroutine(TimerIndicatorRoutine(go, duration));
    }

    private IEnumerator TimerIndicatorRoutine(GameObject go, float duration)
    {
        if (go == null) yield break;

        Vector3 startScale = go.transform.localScale;
        Vector3 endScale = startScale * 0.01f;

        float t = 0f;
        while (t < duration)
        {
            if (go == null || !go.activeInHierarchy) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            go.transform.localScale = Vector3.Lerp(startScale, endScale, u);
            yield return null;
        }

        if (go != null)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var c = sr.color;
                c.a = 0f;
                sr.color = c;

                var lr = go.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }

            Release(go);
        }
    }

    public void PlayPulse(Vector3 position, float diameter, Color color)
    {
        PlayPulse(position, diameter, defaultFadeDuration, color, defaultVisualDepthOffset);
    }

    public void PlayPulse(Vector3 position, float diameter, float fadeDuration, Color color)
    {
        PlayPulse(position, diameter, fadeDuration, color, defaultVisualDepthOffset);
    }

    public void PlayPulse(Vector3 position, float diameter, float fadeDuration, Color color, float zOffset)
    {
        PlayPulse(position, diameter, fadeDuration, color, zOffset, SpriteMaskInteraction.VisibleOutsideMask);
    }

    public void Indicate(Vector3 position, float diameter, Color color)
    {
        PlayPulse(position, diameter, defaultFadeDuration, color, defaultVisualDepthOffset, SpriteMaskInteraction.None);
    }

    public void Indicate(Vector3 position, float diameter, float fadeDuration, Color color)
    {
        PlayPulse(position, diameter, fadeDuration, color, defaultVisualDepthOffset, SpriteMaskInteraction.None);
    }

    public void Indicate(Vector3 position, float diameter, float fadeDuration, Color color, float zOffset)
    {
        PlayPulse(position, diameter, fadeDuration, color, zOffset, SpriteMaskInteraction.None);
    }

    public GameObject SpawnUnmanagedIndicator(Vector3 position, float diameter, Color color, int sortingOrder)
    {
        return SpawnUnmanagedIndicator(position, diameter, color, sortingOrder, defaultUnmanagedTimeout, defaultVisualDepthOffset);
    }

    public GameObject SpawnUnmanagedIndicator(Vector3 position, float diameter, Color color, int sortingOrder, float timeoutSeconds)
    {
        return SpawnUnmanagedIndicator(position, diameter, color, sortingOrder, timeoutSeconds, defaultVisualDepthOffset);
    }

    public GameObject SpawnUnmanagedIndicator(Vector3 position, float diameter, Color color, int sortingOrder, float timeoutSeconds, float zOffset)
    {
        if (aoePrefab == null) return null;

        var spawnPos = new Vector3(position.x, position.y, position.z + zOffset);
        var go = Instantiate(aoePrefab, spawnPos, Quaternion.identity);
        if (go == null) return null;

        go.name = aoePrefab.name + " (Unmanaged)";
        go.transform.localScale = new Vector3(diameter, diameter, 1f);

        var viz = go.GetComponent<AOEVisualizer>();
        if (viz != null)
        {
            // Unmanaged indicators are static; no pool fade/release behavior.
            viz.enabled = false;
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = color;
            if (c.a <= 0f) c.a = Mathf.Clamp01(startingAlpha);
            sr.color = c;
            sr.sortingOrder = sortingOrder;
            sr.maskInteraction = SpriteMaskInteraction.None;
        }

        var lr = go.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.sortingOrder = sortingOrder;
            lr.maskInteraction = SpriteMaskInteraction.None;
            lr.startColor = color;
            lr.endColor = color;
        }

        float timeout = timeoutSeconds > 0f ? timeoutSeconds : defaultUnmanagedTimeout;
        if (_unmanagedTimeoutRoutines.TryGetValue(go, out var existingRoutine) && existingRoutine != null)
        {
            StopCoroutine(existingRoutine);
        }
        _unmanagedTimeoutRoutines[go] = StartCoroutine(DestroyAfterTimeoutRoutine(go, timeout));

        return go;
    }

    private void PlayPulse(Vector3 position, float diameter, float fadeDuration, Color color, float zOffset, SpriteMaskInteraction maskInteraction)
    {
        if (aoePrefab == null) return;

        TryApplyGridVizAlphaIncrease(position, diameter);

        var go = Get(new Vector3(position.x, position.y, position.z + zOffset), Quaternion.identity);
        if (go == null) return;

        SetMaskInteraction(go, maskInteraction);

        go.transform.localScale = new Vector3(diameter, diameter, 1f);

        var viz = go.GetComponent<AOEVisualizer>();
        if (viz == null)
        {
            viz = go.AddComponent<AOEVisualizer>();
        }

        viz.pool = this;
        viz.Play(Mathf.Clamp01(startingAlpha), fadeDuration, color);
    }

    private void TryApplyGridVizAlphaIncrease(Vector3 position, float diameter)
    {
        if (!gridVizAlphaIncrease) return;

        if (_gridViz == null)
        {
            _gridViz = FindFirstObjectByType<GridViz>();
        }

        if (_gridViz == null) return;
        if (!_gridViz.AllowAOEPoolAlphaCircle) return;

        float radius = Mathf.Max(0f, diameter * 0.5f);
        if (radius <= 0f) return;

        _gridViz.AlphaCircle(gridVizAlphaIncreaseAmount, radius, position);
    }

    private static void SetMaskInteraction(GameObject go, SpriteMaskInteraction maskInteraction)
    {
        if (go == null) return;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.maskInteraction = maskInteraction;
        }

        var lr = go.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.maskInteraction = maskInteraction;
        }
    }

    private IEnumerator DestroyAfterTimeoutRoutine(GameObject go, float timeoutSeconds)
    {
        if (go == null) yield break;

        float t = Mathf.Max(0.01f, timeoutSeconds);
        yield return new WaitForSeconds(t);

        if (go != null)
        {
            Destroy(go);
        }

        _unmanagedTimeoutRoutines.Remove(go);
    }
}
