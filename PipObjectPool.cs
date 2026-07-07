using System.Collections.Generic;
using UnityEngine;

public class PipObjectPool : MonoBehaviour
{
    public static PipObjectPool instance;

    [Header("Setup")]
    public GameObject pipPrefab;
    [SerializeField] private int prewarmCount = 16;
    [SerializeField] private bool canGrow = true;

    private readonly Queue<GameObject> _available = new Queue<GameObject>(32);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (pipPrefab == null)
        {
            Debug.LogError("PipObjectPool requires a pipPrefab.", this);
            return;
        }

        Prewarm();
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
        var go = Instantiate(pipPrefab, transform);
        go.name = pipPrefab.name + " (Pooled)";
        go.SetActive(false);
        return go;
    }

    public GameObject Get(Vector3 position)
    {
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

        go.transform.SetParent(null, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        _available.Enqueue(go);
    }
}
