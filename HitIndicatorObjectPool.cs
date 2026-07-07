using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class HitIndicatorObjectPool : MonoBehaviour
{
    private sealed class ActiveIndicator
    {
        public GameObject go;
        public SRC src;
        public float alpha;
    }

    public static HitIndicatorObjectPool instance;

    [Header("Pool")]
    public GameObject hitIndicatorPrefab;
    [SerializeField] private int prewarmCount = 24;
    [SerializeField] private bool canGrow = true;

    [Header("Fade")]
    public float startingAlpha = .8f;
    [Min(0f)] public float alphaDecayRate = 3f;

    private ObjectPool<GameObject> _pool;
    private readonly List<ActiveIndicator> _active = new List<ActiveIndicator>(64);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (hitIndicatorPrefab == null)
        {
            Debug.LogError("HitIndicatorObjectPool requires a hitIndicatorPrefab.", this);
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

    private void Update()
    {
        if (_active.Count == 0) return;

        float decayPerSecond = Mathf.Max(0f, alphaDecayRate);
        float decay = decayPerSecond * Time.deltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var entry = _active[i];
            if (entry == null || entry.go == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            entry.alpha = Mathf.Clamp01(entry.alpha - decay);

            if (entry.src != null)
            {
                entry.src.SetSpriteRendererAlpha(entry.alpha);
            }

            if (entry.alpha <= 0f)
            {
                Release(entry.go);
                _active.RemoveAt(i);
            }
        }
    }

    public void IndicateProjectileHit(Vector3 position, CM.ColorType color)
    {
        if (hitIndicatorPrefab == null || _pool == null) return;

        if (!canGrow && _pool.CountInactive <= 0 && _pool.CountAll >= Mathf.Max(1, prewarmCount))
        {
            return;
        }

        GameObject go = _pool.Get();
        if (go == null) return;

        go.transform.SetParent(null, worldPositionStays: false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;

        SRC src = go.GetComponent<SRC>();
        if (src == null) src = go.GetComponentInChildren<SRC>();
        if (src != null)
        {
            src.ApplyColorToAll(color);
            src.SetSpriteRendererAlpha(startingAlpha);
        }

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] == null || _active[i].go == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            if (_active[i].go == go)
            {
                _active[i].src = src;
                _active[i].alpha = Mathf.Clamp01(startingAlpha);
                return;
            }
        }

        _active.Add(new ActiveIndicator
        {
            go = go,
            src = src,
            alpha = Mathf.Clamp01(startingAlpha)
        });
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
        GameObject go = Instantiate(hitIndicatorPrefab, transform);
        go.name = hitIndicatorPrefab.name + " (Pooled)";
        go.SetActive(false);
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
        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
    }

    private static void OnDestroyPooledObject(GameObject go)
    {
        if (go != null) Destroy(go);
    }

    private void Release(GameObject go)
    {
        if (go == null) return;

        if (_pool == null)
        {
            Destroy(go);
            return;
        }

        _pool.Release(go);
    }
}
