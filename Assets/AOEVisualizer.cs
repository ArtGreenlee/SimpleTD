using System.Collections;
using UnityEngine;

public class AOEVisualizer : MonoBehaviour
{
    [HideInInspector] public AOEObjectPool pool;

    [Header("References")]
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private LineRenderer lr;

    [Header("Outline")]
    [Tooltip("Alpha added on top of the sprite alpha for the outline. The outline alpha is clamped to [0,1].")]
    [Range(0f, 1f)]
    [SerializeField] private float outlineAlphaBoost = 0.25f;

    [Header("Spawn Bounce")]
    [Tooltip("Initial scale multiplier when the pulse spawns (e.g.,0.9 =90% size).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float spawnScaleMultiplier = 0.9f;

    [Tooltip("Optional overshoot scale multiplier during the bounce (e.g.,1.05 =105% size).")]
    [Range(1f, 1.5f)]
    [SerializeField] private float bounceOvershootMultiplier = 1.05f;

    [Tooltip("Duration of the bounce animation in seconds.")]
    [Min(0f)]
    [SerializeField] private float bounceDuration = 0.06f;

    private const int CircleSegments = 50;

    private Coroutine _routine;
    private bool _lineInitialized;
    private Vector3 _finalLocalScale = Vector3.one;

    private void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (lr == null) lr = GetComponent<LineRenderer>();
    }

    private void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }

    public void Play(float startAlpha, float fadeDuration, Color rgb)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (lr == null) lr = GetComponent<LineRenderer>();

        if (sr == null)
        {
            if (pool != null) pool.Release(gameObject);
            return;
        }

        // Pool sets the desired final local scale before calling Play.
        _finalLocalScale = transform.localScale;

        // Apply RGB, keep existing alpha for now (fade routine sets it).
        var c = sr.color;
        c.r = rgb.r;
        c.g = rgb.g;
        c.b = rgb.b;
        sr.color = c;

        SetupLineRenderer();
        SyncLineColorToSprite();

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine(Mathf.Clamp01(startAlpha), fadeDuration));
    }

    private void SetupLineRenderer()
    {
        if (lr == null) return;

        lr.useWorldSpace = false;
        lr.loop = true;

        // Define a unit-diameter circle once (radius0.5). Transform scaling handles final size.
        if (_lineInitialized && lr.positionCount == CircleSegments) return;

        lr.positionCount = CircleSegments;
        const float unitRadius = 0.5f;
        float step = Mathf.PI * 2f / CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = step * i;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * unitRadius, Mathf.Sin(a) * unitRadius, 0f));
        }

        _lineInitialized = true;
    }

    private void SyncLineColorToSprite()
    {
        if (lr == null || sr == null) return;

        Color c = sr.color;
        c.a = Mathf.Clamp01(c.a + Mathf.Max(0f, outlineAlphaBoost));
        lr.startColor = c;
        lr.endColor = c;
    }

    private IEnumerator PlayRoutine(float startAlpha, float fadeDuration)
    {
        // Quick bounce to final size, then fade.
        float dur = Mathf.Max(0f, bounceDuration);

        float startMult = Mathf.Clamp01(spawnScaleMultiplier);
        float overMult = Mathf.Max(1f, bounceOvershootMultiplier);

        transform.localScale = _finalLocalScale * startMult;

        if (dur > 0.0001f)
        {
            float half = dur * 0.5f;

            // Phase1: start -> overshoot.
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                transform.localScale = Vector3.LerpUnclamped(_finalLocalScale * startMult, _finalLocalScale * overMult, eased);
                yield return null;
            }

            // Phase2: overshoot -> final.
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / half);
                float eased = u * u;
                transform.localScale = Vector3.LerpUnclamped(_finalLocalScale * overMult, _finalLocalScale, eased);
                yield return null;
            }
        }

        transform.localScale = _finalLocalScale;

        yield return FadeRoutine(startAlpha, fadeDuration);
    }

    private IEnumerator FadeRoutine(float startAlpha, float fadeDuration)
    {
        float dur = Mathf.Max(0.01f, fadeDuration);

        // Reset alpha.
        Color c = sr.color;
        c.a = startAlpha;
        sr.color = c;
        SyncLineColorToSprite();

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, 0f, t / dur);
            c = sr.color;
            c.a = a;
            sr.color = c;
            SyncLineColorToSprite();
            yield return null;
        }

        c = sr.color;
        c.a = 0f;
        sr.color = c;
        SyncLineColorToSprite();

        if (pool != null) pool.Release(gameObject);
    }
}
