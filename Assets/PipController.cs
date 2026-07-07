using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PipController : MonoBehaviour
{
    public CM.ColorType color;
    public Transform pipTransformParent;
    public float radius = 0.4f;
    [SerializeField] private Juice juice;

    [Header("Layout")]
    [SerializeField, Min(0.01f)] private float moveLerpSpeed = 12f;

    [Header("Remove Animations")]
    [SerializeField, Min(0.01f)] private float collideOutDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float collideInDuration = 0.14f;
    [SerializeField, Min(0f)] private float collideOutDistance = 0.25f;
    [SerializeField, Min(0f)] private float collideScaleBoost = 0.25f;
    [SerializeField, Min(0.01f)] private float floatAwayDuration = 0.35f;
    [SerializeField, Min(0f)] private float floatAwayDistance = 0.8f;

    private readonly List<Transform> pips = new List<Transform>();
    private List<Transform> animatingPips = new List<Transform>();

    public float CollideTotalDuration => Mathf.Max(0f, collideOutDuration + collideInDuration);

    public AnimationType animationType;
    public enum AnimationType
    {
        Collide,
        FloatAway
    }

    private void LateUpdate()
    {
        if (Input.GetMouseButtonDown(1))
        {
            AddPips(1);
        }
        if (Input.GetMouseButtonDown(0))
        {
            RemovePips(1);
        }
        UpdatePipLayout();
    }

    private void Awake()
    {
        if (juice == null)
        {
            Transform centerT = pipTransformParent != null ? pipTransformParent : transform;
            juice = centerT.GetComponent<Juice>();
            if (juice == null) juice = centerT.GetComponentInParent<Juice>();
        }
    }

    public void AddPips(int count)
    {
        if (count <= 0) return;

        Transform centerT = pipTransformParent != null ? pipTransformParent : transform;
        Vector3 center = centerT.position;

        for (int i = 0; i < count; i++)
        {
            var pool = PipObjectPool.instance;
            if (pool == null) return;

            GameObject go = pool.Get(center);
            if (go == null) continue;

            Transform pipT = go.transform;
            pips.Add(pipT);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && CM.i != null)
            {
                Color c = CM.i.ColorTypeToColor(color);
                c.a = 1f;
                sr.color = c;
            }
        }

        UpdatePipLayout(forceSnap: false);
    }

    public void RemovePips(int count)
    {
        if (count <= 0 || pips.Count == 0) return;

        int removeCount = Mathf.Min(count, pips.Count);
        for (int i = 0; i < removeCount; i++)
        {
            int idx = Random.Range(0, pips.Count);
            Transform pip = pips[idx];
            pips.RemoveAt(idx);

            if (pip == null) continue;

            animatingPips.Add(pip);
            if (animationType == AnimationType.FloatAway)
            {
                StartCoroutine(AnimateFloatAway(pip));
            }
            else
            {
                StartCoroutine(AnimateCollide(pip));
            }
        }

        UpdatePipLayout(forceSnap: false);
    }

    private void UpdatePipLayout(bool forceSnap = false)
    {
        if (pips.Count == 0) return;

        Transform centerT = pipTransformParent != null ? pipTransformParent : transform;
        Vector3 center = centerT.position;

        int n = pips.Count;
        float r = Mathf.Max(0f, radius);

        for (int i = 0; i < n; i++)
        {
            Transform pip = pips[i];
            if (pip == null) continue;
            if (animatingPips.Contains(pip)) continue;

            float angle = (Mathf.PI * 2f * i) / Mathf.Max(1, n);
            Vector3 target = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;

            if (forceSnap)
            {
                pip.position = target;
            }
            else
            {
                float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, moveLerpSpeed) * Time.deltaTime);
                pip.position = Vector3.Lerp(pip.position, target, t);
            }
        }
    }

    private IEnumerator AnimateCollide(Transform pip)
    {
        if (pip == null)
        {
            yield break;
        }

        Transform centerT = pipTransformParent != null ? pipTransformParent : transform;
        Vector3 center = centerT.position;
        Vector3 start = pip.position;
        Vector3 dir = (start - center);
        if (dir.sqrMagnitude <= 0.000001f) dir = Vector3.up;
        dir.Normalize();

        Vector3 outPos = start + dir * collideOutDistance;
        Vector3 baseScale = pip.localScale;

        float t = 0f;
        while (t < collideOutDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / collideOutDuration);
            pip.position = Vector3.Lerp(start, outPos, k);
            pip.localScale = Vector3.Lerp(baseScale, baseScale * (1f + collideScaleBoost), k);
            yield return null;
        }

        t = 0f;
        float inSpeed = Mathf.Max(0.0001f, Vector3.Distance(outPos, center) / Mathf.Max(0.0001f, collideInDuration));
        while (t < collideInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / collideInDuration);

            Vector3 movingCenter = centerT != null ? centerT.position : transform.position;
            pip.position = Vector3.MoveTowards(pip.position, movingCenter, inSpeed * Time.deltaTime);
            pip.localScale = Vector3.Lerp(baseScale * (1f + collideScaleBoost), baseScale, k);
            yield return null;
        }

        ReleasePip(pip);
    }

    private IEnumerator AnimateFloatAway(Transform pip)
    {
        if (pip == null)
        {
            yield break;
        }

        pip.SetParent(null, true);

        Transform centerT = pipTransformParent != null ? pipTransformParent : transform;
        Vector3 center = centerT.position;
        Vector3 start = pip.position;

        Vector3 dir = (start - center);
        if (dir.sqrMagnitude <= 0.000001f)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }
        dir.Normalize();

        Vector3 end = start + dir * floatAwayDistance;

        var sr = pip.GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;

        float t = 0f;
        while (t < floatAwayDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / floatAwayDuration);
            pip.position = Vector3.Lerp(start, end, k);

            if (sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, k);
                sr.color = c;
            }

            yield return null;
        }

        if (sr != null)
        {
            Color c = startColor;
            c.a = 1f;
            sr.color = c;
        }

        ReleasePip(pip);
    }

    private void ReleasePip(Transform pip)
    {
        if (pip == null) return;

        animatingPips.Remove(pip);

        var pool = PipObjectPool.instance;
        if (pool != null)
        {
            pool.Release(pip.gameObject);
        }
        else
        {
            Destroy(pip.gameObject);
        }
    }

    private static void ReleasePipImmediate(Transform pip)
    {
        if (pip == null) return;

        var pool = PipObjectPool.instance;
        if (pool != null)
        {
            pool.Release(pip.gameObject);
        }
        else
        {
            Object.Destroy(pip.gameObject);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        var seen = new HashSet<Transform>();

        for (int i = 0; i < pips.Count; i++)
        {
            var pip = pips[i];
            if (pip == null) continue;
            if (!seen.Add(pip)) continue;
            ReleasePipImmediate(pip);
        }

        for (int i = 0; i < animatingPips.Count; i++)
        {
            var pip = animatingPips[i];
            if (pip == null) continue;
            if (!seen.Add(pip)) continue;
            ReleasePipImmediate(pip);
        }

        pips.Clear();
        animatingPips.Clear();
    }

    public int PipCount => pips.Count;

    public void SetPipCount(int count)
    {
        int target = Mathf.Max(0, count);
        int current = pips.Count;

        if (target > current)
        {
            AddPips(target - current);
        }
        else if (target < current)
        {
            RemovePips(current - target);
        }
    }
}
