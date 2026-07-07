using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class XPObjectPool : MonoBehaviour
{
    public static XPObjectPool instance { get; private set; }

    [Header("Setup")]
    public GameObject xpPrefab;
    [SerializeField] private int prewarmCount = 32;
    [SerializeField] private bool canGrow = true;

    private ObjectPool<GameObject> _pool;
    private readonly Dictionary<Transform, Tower> activeXp = new Dictionary<Transform, Tower>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (xpPrefab == null)
        {
            Debug.LogError("XPObjectPool requires xpPrefab.", this);
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
        var go = Instantiate(xpPrefab, transform);
        go.name = xpPrefab.name + " (Pooled)";
        go.SetActive(false);

        var xp = go.GetComponent<XP>();
        if (xp != null)
        {
            xp.SetPool(this);
        }

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
        if (go.transform != null) activeXp.Remove(go.transform);

        go.SetActive(false);
        go.transform.SetParent(transform, worldPositionStays: false);
    }

    private static void OnDestroyPooledObject(GameObject go)
    {
        if (go != null) Destroy(go);
    }

    public void SpawnXP(Vector3 position, Tower tower, int count = 1)
    {
        if (_pool == null || xpPrefab == null) return;
        if (tower == null) return;

        for (int n = 0; n < count; n++)
        {
            if (!canGrow && _pool.CountInactive <= 0 && _pool.CountAll >= Mathf.Max(1, prewarmCount))
            {
                return;
            }

            var go = _pool.Get();
            if (go == null) return;

            go.transform.SetParent(null, worldPositionStays: false);
            go.transform.position = position;

            activeXp[go.transform] = tower;

            var xp = go.GetComponent<XP>();
            if (xp != null)
            {
                xp.SetPool(this);
                xp.SetTargetTower(tower);
            }
        }
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
}
