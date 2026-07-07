using UnityEngine;

public class Currency : MonoBehaviour
{
    [SerializeField] private float spawnImpulse = 1f;
    [SerializeField] private float spawnTorque = 50f;

    [Header("AllIn1 Glow Pulse")]
    [SerializeField] private bool animateAllIn1Glow = true;
    [SerializeField] private Material allIn1SpriteShaderMaterial;
    [SerializeField, Min(0f)] private float glowMin = 1.25f;
    [SerializeField, Min(0f)] private float glowMax = 4f;
    [SerializeField, Min(0.01f)] private float glowPulseSpeed = 4f;

    [Header("Passive Scale Swell")]
    [SerializeField] private bool animateScaleSwell = true;
    [SerializeField, Min(0f)] private float scaleSwellAmount = 0.06f;
    [SerializeField, Min(0.01f)] private float scaleSwellSpeed = 3f;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private SRC _src;
    private Vector3 _baseLocalScale;
    private int _value = 1;
    private MaterialPropertyBlock _mpb;
    private Material _activeAllIn1Material;
    private float _glowPhaseOffset;
    private float _scalePhaseOffset;
    private Vector3 _configuredLocalScale;

    private static readonly int GlowId = Shader.PropertyToID("_Glow");

    public int Value => Mathf.Max(1, _value);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _src = GetComponent<SRC>();
        _baseLocalScale = transform.localScale;
        _configuredLocalScale = _baseLocalScale;
        _mpb = new MaterialPropertyBlock();
        CacheAllIn1Material();
    }

    private void OnEnable()
    {
        if (_rb == null) return;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (spawnImpulse > 0f)
        {
            _rb.AddForce(Random.insideUnitCircle * spawnImpulse, ForceMode2D.Impulse);
        }

        if (spawnTorque > 0f)
        {
            _rb.AddTorque(Random.value * spawnTorque, ForceMode2D.Impulse);
        }

        _glowPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        _scalePhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        transform.localScale = _configuredLocalScale;
        ApplyGlowPulse(Time.time);
        ApplyScaleSwell(Time.time);
    }

    private void Update()
    {
        float now = Time.time;

        if (animateAllIn1Glow)
        {
            ApplyGlowPulse(now);
        }

        if (animateScaleSwell)
        {
            ApplyScaleSwell(now);
        }
        else
        {
            transform.localScale = _configuredLocalScale;
        }
    }

    private void OnDisable()
    {
        if (_spriteRenderer == null || _mpb == null) return;
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(GlowId, glowMin);
        _spriteRenderer.SetPropertyBlock(_mpb);
    }

    public void Flash(float duration)
    {
        if (_src == null) _src = GetComponent<SRC>();
        if (_src != null)
        {
            _src.Flash(duration);
        }
    }

    public void Configure(int value, CM.ColorType colorType, float scaleMultiplier)
    {
        _value = Mathf.Max(1, value);
        float appliedScale = Mathf.Max(0.01f, scaleMultiplier);
        _configuredLocalScale = _baseLocalScale * appliedScale;
        transform.localScale = _configuredLocalScale;

        CacheAllIn1Material();

        if (_src == null) _src = GetComponent<SRC>();
        if (_src != null)
        {
            _src.ApplyColorToAll(colorType);
            _src.Indicate();
        }
    }

    private void CacheAllIn1Material()
    {
        if (_spriteRenderer == null) return;

        if (allIn1SpriteShaderMaterial != null)
        {
            _activeAllIn1Material = allIn1SpriteShaderMaterial;
            if (_spriteRenderer.sharedMaterial != allIn1SpriteShaderMaterial)
            {
                _spriteRenderer.sharedMaterial = allIn1SpriteShaderMaterial;
            }
            return;
        }

        _activeAllIn1Material = _spriteRenderer.sharedMaterial;
    }

    private void ApplyGlowPulse(float timeNow)
    {
        if (_spriteRenderer == null || _mpb == null) return;
        if (_activeAllIn1Material == null || !_activeAllIn1Material.HasProperty(GlowId)) return;

        float minGlow = Mathf.Min(glowMin, glowMax);
        float maxGlow = Mathf.Max(glowMin, glowMax);
        float t = (Mathf.Sin(timeNow * glowPulseSpeed + _glowPhaseOffset) * 0.5f) + 0.5f;
        float glow = Mathf.Lerp(minGlow, maxGlow, t);

        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(GlowId, glow);
        _spriteRenderer.SetPropertyBlock(_mpb);
    }

    private void ApplyScaleSwell(float timeNow)
    {
        float swell = 1f + (Mathf.Sin(timeNow * scaleSwellSpeed + _scalePhaseOffset) * scaleSwellAmount);
        transform.localScale = _configuredLocalScale * Mathf.Max(0.01f, swell);
    }
}
