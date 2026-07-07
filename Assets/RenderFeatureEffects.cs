using System.Collections;
using System.Reflection;
using CRTFilter;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RenderFeatureEffects : MonoBehaviour
{
    public static RenderFeatureEffects instance { get; private set; }

    public CRTRendererFeature CRTFeature { get; private set; }

    // Snapshot of all CRTRendererFeature parameters for lerping / restoring state.
    public struct CRTSnapshot
    {
        public float pixelResolutionX, pixelResolutionY;
        public float screenBend, screenOverscan;
        public float vignetteSize, vignetteSmooth, vignetteRound;
        public float blur, bleed, smidge;
        public float scanlinesStrength, apertureStrength;
        public float shadowlines, shadowlinesSpeed, shadowlinesAlpha;
        public float noiseSize, noiseSpeed, noiseAlpha;
        public float glitchFrequency, glitchPosition, glitchPositionFlicker;
        public float glitchMovementSpeed, glitchStrength, glitchNoise, glitchNoiseSpeed;
        public int   glitchBands;
        public float brightness, contrast, gamma, red, green, blue, chromaticAberration;
        public Vector2 redOffset, blueOffset, greenOffset;

        public static CRTSnapshot Capture(CRTRendererFeature f) => new CRTSnapshot
        {
            pixelResolutionX     = f.pixelResolutionX,
            pixelResolutionY     = f.pixelResolutionY,
            screenBend           = f.screenBend,
            screenOverscan       = f.screenOverscan,
            vignetteSize         = f.vignetteSize,
            vignetteSmooth       = f.vignetteSmooth,
            vignetteRound        = f.vignetteRound,
            blur                 = f.blur,
            bleed                = f.bleed,
            smidge               = f.smidge,
            scanlinesStrength    = f.scanlinesStrength,
            apertureStrength     = f.apertureStrength,
            shadowlines          = f.shadowlines,
            shadowlinesSpeed     = f.shadowlinesSpeed,
            shadowlinesAlpha     = f.shadowlinesAlpha,
            noiseSize            = f.noiseSize,
            noiseSpeed           = f.noiseSpeed,
            noiseAlpha           = f.noiseAlpha,
            glitchFrequency      = f.glitchFrequency,
            glitchBands          = f.glitchBands,
            glitchPosition       = f.glitchPosition,
            glitchPositionFlicker = f.glitchPositionFlicker,
            glitchMovementSpeed  = f.glitchMovementSpeed,
            glitchStrength       = f.glitchStrength,
            glitchNoise          = f.glitchNoise,
            glitchNoiseSpeed     = f.glitchNoiseSpeed,
            brightness           = f.brightness,
            contrast             = f.contrast,
            gamma                = f.gamma,
            red                  = f.red,
            green                = f.green,
            blue                 = f.blue,
            chromaticAberration  = f.chromaticAberration,
            redOffset            = f.redOffset,
            blueOffset           = f.blueOffset,
            greenOffset          = f.greenOffset,
        };

        public static CRTSnapshot Lerp(in CRTSnapshot a, in CRTSnapshot b, float t) => new CRTSnapshot
        {
            pixelResolutionX     = Mathf.Lerp(a.pixelResolutionX,     b.pixelResolutionX,     t),
            pixelResolutionY     = Mathf.Lerp(a.pixelResolutionY,     b.pixelResolutionY,     t),
            screenBend           = Mathf.Lerp(a.screenBend,           b.screenBend,           t),
            screenOverscan       = Mathf.Lerp(a.screenOverscan,       b.screenOverscan,       t),
            vignetteSize         = Mathf.Lerp(a.vignetteSize,         b.vignetteSize,         t),
            vignetteSmooth       = Mathf.Lerp(a.vignetteSmooth,       b.vignetteSmooth,       t),
            vignetteRound        = Mathf.Lerp(a.vignetteRound,        b.vignetteRound,        t),
            blur                 = Mathf.Lerp(a.blur,                 b.blur,                 t),
            bleed                = Mathf.Lerp(a.bleed,                b.bleed,                t),
            smidge               = Mathf.Lerp(a.smidge,               b.smidge,               t),
            scanlinesStrength    = Mathf.Lerp(a.scanlinesStrength,    b.scanlinesStrength,    t),
            apertureStrength     = Mathf.Lerp(a.apertureStrength,     b.apertureStrength,     t),
            shadowlines          = Mathf.Lerp(a.shadowlines,          b.shadowlines,          t),
            shadowlinesSpeed     = Mathf.Lerp(a.shadowlinesSpeed,     b.shadowlinesSpeed,     t),
            shadowlinesAlpha     = Mathf.Lerp(a.shadowlinesAlpha,     b.shadowlinesAlpha,     t),
            noiseSize            = Mathf.Lerp(a.noiseSize,            b.noiseSize,            t),
            noiseSpeed           = Mathf.Lerp(a.noiseSpeed,           b.noiseSpeed,           t),
            noiseAlpha           = Mathf.Lerp(a.noiseAlpha,           b.noiseAlpha,           t),
            glitchFrequency      = Mathf.Lerp(a.glitchFrequency,      b.glitchFrequency,      t),
            glitchBands          = Mathf.RoundToInt(Mathf.Lerp(a.glitchBands, b.glitchBands, t)),
            glitchPosition       = Mathf.Lerp(a.glitchPosition,       b.glitchPosition,       t),
            glitchPositionFlicker = Mathf.Lerp(a.glitchPositionFlicker, b.glitchPositionFlicker, t),
            glitchMovementSpeed  = Mathf.Lerp(a.glitchMovementSpeed,  b.glitchMovementSpeed,  t),
            glitchStrength       = Mathf.Lerp(a.glitchStrength,       b.glitchStrength,       t),
            glitchNoise          = Mathf.Lerp(a.glitchNoise,          b.glitchNoise,          t),
            glitchNoiseSpeed     = Mathf.Lerp(a.glitchNoiseSpeed,     b.glitchNoiseSpeed,     t),
            brightness           = Mathf.Lerp(a.brightness,           b.brightness,           t),
            contrast             = Mathf.Lerp(a.contrast,             b.contrast,             t),
            gamma                = Mathf.Lerp(a.gamma,                b.gamma,                t),
            red                  = Mathf.Lerp(a.red,                  b.red,                  t),
            green                = Mathf.Lerp(a.green,                b.green,                t),
            blue                 = Mathf.Lerp(a.blue,                 b.blue,                 t),
            chromaticAberration  = Mathf.Lerp(a.chromaticAberration,  b.chromaticAberration,  t),
            redOffset            = Vector2.Lerp(a.redOffset,           b.redOffset,            t),
            blueOffset           = Vector2.Lerp(a.blueOffset,          b.blueOffset,           t),
            greenOffset          = Vector2.Lerp(a.greenOffset,         b.greenOffset,          t),
        };

        public void Apply(CRTRendererFeature f)
        {
            f.pixelResolutionX     = pixelResolutionX;
            f.pixelResolutionY     = pixelResolutionY;
            f.screenBend           = screenBend;
            f.screenOverscan       = screenOverscan;
            f.vignetteSize         = vignetteSize;
            f.vignetteSmooth       = vignetteSmooth;
            f.vignetteRound        = vignetteRound;
            f.blur                 = blur;
            f.bleed                = bleed;
            f.smidge               = smidge;
            f.scanlinesStrength    = scanlinesStrength;
            f.apertureStrength     = apertureStrength;
            f.shadowlines          = shadowlines;
            f.shadowlinesSpeed     = shadowlinesSpeed;
            f.shadowlinesAlpha     = shadowlinesAlpha;
            f.noiseSize            = noiseSize;
            f.noiseSpeed           = noiseSpeed;
            f.noiseAlpha           = noiseAlpha;
            f.glitchFrequency      = glitchFrequency;
            f.glitchBands          = glitchBands;
            f.glitchPosition       = glitchPosition;
            f.glitchPositionFlicker = glitchPositionFlicker;
            f.glitchMovementSpeed  = glitchMovementSpeed;
            f.glitchStrength       = glitchStrength;
            f.glitchNoise          = glitchNoise;
            f.glitchNoiseSpeed     = glitchNoiseSpeed;
            f.brightness           = brightness;
            f.contrast             = contrast;
            f.gamma                = gamma;
            f.red                  = red;
            f.green                = green;
            f.blue                 = blue;
            f.chromaticAberration  = chromaticAberration;
            f.redOffset            = redOffset;
            f.blueOffset           = blueOffset;
            f.greenOffset          = greenOffset;
        }
    }

    private Camera _camera;
    private Coroutine _lerpCoroutine;
    private Coroutine _glitchCoroutine;
    private Coroutine _noiseCoroutine;
    private Coroutine _chromaticCoroutine;
    private Coroutine _chromaticPulseCoroutine;

    [Header("Blur decay")]
    [Tooltip("Maximum blur value that Blur() can push up to.")]
    [SerializeField] private float blurMax = 10f;
    [Tooltip("Units per second the blur decays back to its baseline.")]
    [SerializeField] private float blurDecaySpeed = 3f;

    [Header("Test Mode")]
    [Tooltip("Enables click-to-test render feature effects. Auto-disabled in application builds.")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private bool showTestOverlay = true;
    [Range(0f, 1f)]
    [SerializeField] private float testIntensity = 0.5f;

    [Header("Enemy Damage Chromatic")]
    [Tooltip("If enabled, enemy damage adds a spring-driven chromatic aberration impulse.")]
    [SerializeField] private bool enableEnemyDamageChromatic = true;
    [SerializeField] private float enemyDamageChromaticMin = 0f;
    [SerializeField] private float enemyDamageChromaticMax = 4f;
    [SerializeField] private float enemyDamageChromaticPerHit = 0.5f;
    [Tooltip("Higher values reduce per-hit gain more aggressively as value approaches max.")]
    [SerializeField] private float enemyDamageChromaticNearMaxFalloff = 1.5f;
    [SerializeField] private float enemyDamageChromaticTargetReturnSpeed = 6f;
    [SerializeField] private float enemyDamageChromaticSpring = 70f;
    [SerializeField] private float enemyDamageChromaticDamping = 14f;

    private enum TestEffectMode
    {
        BlurPulse,
        GlitchFlash,
        NoiseFlash,
        ChromaticFlash,
        ChromaticPulse,
        CombinedImpact,
    }

    [SerializeField] private TestEffectMode selectedTestEffect = TestEffectMode.BlurPulse;

    private float _blurBaseline;

    private static readonly FieldInfo RendererDataListField =
        typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);

    private float _enemyDamageChromaticCurrent;
    private float _enemyDamageChromaticTarget;
    private float _enemyDamageChromaticVelocity;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        _camera = Camera.main;
        CRTFeature = FindCRTFeature();

    #if !UNITY_EDITOR
        testMode = false;
    #endif

        if (!Application.isEditor)
        {
            testMode = false;
        }

        if (CRTFeature == null)
            Debug.LogWarning($"[RenderFeatureEffects] No CRTRendererFeature found on the renderer used by camera '{(_camera != null ? _camera.name : gameObject.name)}'.");
        else
            _blurBaseline = CRTFeature.blur;

        _enemyDamageChromaticCurrent = Mathf.Clamp(enemyDamageChromaticMin, enemyDamageChromaticMin, enemyDamageChromaticMax);
        _enemyDamageChromaticTarget = _enemyDamageChromaticCurrent;
        _enemyDamageChromaticVelocity = 0f;
    }

    private void Update()
    {
        if (CRTFeature == null) return;

        if (!Mathf.Approximately(CRTFeature.blur, _blurBaseline))
        {
            CRTFeature.blur = Mathf.MoveTowards(CRTFeature.blur, _blurBaseline, blurDecaySpeed * Time.deltaTime);
        }

        UpdateEnemyDamageChromatic();
        HandleTestModeInput();
    }

    private void UpdateEnemyDamageChromatic()
    {
        if (!enableEnemyDamageChromatic) return;

        float min = Mathf.Min(enemyDamageChromaticMin, enemyDamageChromaticMax);
        float max = Mathf.Max(enemyDamageChromaticMin, enemyDamageChromaticMax);
        float dt = Time.deltaTime;

        _enemyDamageChromaticTarget = Mathf.MoveTowards(
            _enemyDamageChromaticTarget,
            min,
            Mathf.Max(0f, enemyDamageChromaticTargetReturnSpeed) * dt);

        float spring = Mathf.Max(0f, enemyDamageChromaticSpring);
        float damping = Mathf.Max(0f, enemyDamageChromaticDamping);
        float displacement = _enemyDamageChromaticTarget - _enemyDamageChromaticCurrent;
        float accel = displacement * spring - _enemyDamageChromaticVelocity * damping;
        _enemyDamageChromaticVelocity += accel * dt;
        _enemyDamageChromaticCurrent += _enemyDamageChromaticVelocity * dt;
        _enemyDamageChromaticCurrent = Mathf.Clamp(_enemyDamageChromaticCurrent, min, max);

        if (_enemyDamageChromaticCurrent >= max && _enemyDamageChromaticVelocity > 0f)
        {
            _enemyDamageChromaticVelocity = 0f;
        }

        if (CRTFeature.chromaticAberration < _enemyDamageChromaticCurrent)
        {
            CRTFeature.chromaticAberration = _enemyDamageChromaticCurrent;
        }
    }

    public void OnEnemyDamagedChromaticImpulse()
    {
        if (CRTFeature == null) return;
        if (!enableEnemyDamageChromatic) return;

        float min = Mathf.Min(enemyDamageChromaticMin, enemyDamageChromaticMax);
        float max = Mathf.Max(enemyDamageChromaticMin, enemyDamageChromaticMax);

        _enemyDamageChromaticCurrent = Mathf.Clamp(_enemyDamageChromaticCurrent, min, max);
        _enemyDamageChromaticTarget = Mathf.Clamp(_enemyDamageChromaticTarget, min, max);

        float range = Mathf.Max(0.0001f, max - min);
        float normalized = Mathf.InverseLerp(min, max, _enemyDamageChromaticTarget);
        float remaining = Mathf.Clamp01(1f - normalized);
        float falloffPower = Mathf.Max(0.01f, enemyDamageChromaticNearMaxFalloff);
        float gainScale = Mathf.Pow(remaining, falloffPower);

        float gain = Mathf.Max(0f, enemyDamageChromaticPerHit) * gainScale;
        _enemyDamageChromaticTarget = Mathf.Clamp(_enemyDamageChromaticTarget + gain, min, max);

        if (_enemyDamageChromaticCurrent < min)
        {
            _enemyDamageChromaticCurrent = min;
        }
    }

    private void HandleTestModeInput()
    {
        if (!testMode) return;

        if (Input.GetMouseButtonDown(1))
        {
            selectedTestEffect = CycleEffect(selectedTestEffect, step: 1);
        }

        if (Input.GetMouseButtonDown(2))
        {
            selectedTestEffect = CycleEffect(selectedTestEffect, step: -1);
        }

        if (Input.GetMouseButtonDown(0))
        {
            TriggerSelectedTestEffect();
        }
    }

    private static TestEffectMode CycleEffect(TestEffectMode current, int step)
    {
        int count = System.Enum.GetValues(typeof(TestEffectMode)).Length;
        int idx = (int)current;
        idx = (idx + step) % count;
        if (idx < 0) idx += count;
        return (TestEffectMode)idx;
    }

    private void TriggerSelectedTestEffect()
    {
        float intensity = Mathf.Clamp01(testIntensity);

        switch (selectedTestEffect)
        {
            case TestEffectMode.BlurPulse:
                Blur(Mathf.Lerp(0.4f, 5f, intensity));
                break;

            case TestEffectMode.GlitchFlash:
                GlitchFlash(
                    duration: Mathf.Lerp(0.04f, 0.35f, intensity),
                    strength: Mathf.Lerp(0.1f, 1f, intensity),
                    bands: Mathf.RoundToInt(Mathf.Lerp(1f, 8f, intensity)),
                    movementSpeed: Mathf.Lerp(0f, 6f, intensity));
                break;

            case TestEffectMode.NoiseFlash:
                NoiseFlash(
                    duration: Mathf.Lerp(0.05f, 0.5f, intensity),
                    alpha: Mathf.Lerp(0.05f, 0.8f, intensity),
                    size: Mathf.Lerp(10f, 120f, intensity),
                    speed: Mathf.Lerp(0.5f, 15f, intensity));
                break;

            case TestEffectMode.ChromaticFlash:
                ChromaticAberrationFlash(
                    duration: Mathf.Lerp(0.05f, 0.4f, intensity),
                    strength: Mathf.Lerp(0.5f, 5f, intensity));
                break;

            case TestEffectMode.ChromaticPulse:
                ChromaticAberrationPulse(
                    duration: Mathf.Lerp(0.2f, 1f, intensity),
                    peakStrength: Mathf.Lerp(0.8f, 6f, intensity),
                    pulses: Mathf.RoundToInt(Mathf.Lerp(1f, 4f, intensity)));
                break;

            case TestEffectMode.CombinedImpact:
                Blur(Mathf.Lerp(0.25f, 2.5f, intensity));
                GlitchFlash(
                    duration: Mathf.Lerp(0.03f, 0.18f, intensity),
                    strength: Mathf.Lerp(0.08f, 0.7f, intensity),
                    bands: Mathf.RoundToInt(Mathf.Lerp(2f, 6f, intensity)),
                    movementSpeed: Mathf.Lerp(0f, 4f, intensity));
                NoiseFlash(
                    duration: Mathf.Lerp(0.03f, 0.2f, intensity),
                    alpha: Mathf.Lerp(0.05f, 0.35f, intensity),
                    size: Mathf.Lerp(15f, 65f, intensity),
                    speed: Mathf.Lerp(2f, 9f, intensity));
                ChromaticAberrationFlash(
                    duration: Mathf.Lerp(0.03f, 0.2f, intensity),
                    strength: Mathf.Lerp(0.5f, 3f, intensity));
                break;
        }
    }

    private void OnGUI()
    {
        if (!testMode || !showTestOverlay) return;

        const float width = 360f;
        const float height = 122f;
        Rect area = new Rect(12f, 12f, width, height);

        GUILayout.BeginArea(area, "Render Feature Test", GUI.skin.window);
        GUILayout.Label("Effect: " + selectedTestEffect);
        GUILayout.Label("Intensity: " + Mathf.RoundToInt(testIntensity * 100f) + "%");
        testIntensity = GUILayout.HorizontalSlider(testIntensity, 0f, 1f);
        GUILayout.Label("Mouse0: trigger | Mouse1: next effect | Mouse2: previous effect");
        GUILayout.EndArea();
    }

    // Resolves the CRTRendererFeature from the URP renderer assigned to this camera.
    private CRTRendererFeature FindCRTFeature()
    {
        if (_camera == null) return null;

        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return null;

        var rendererDataList = RendererDataListField?.GetValue(urpAsset) as ScriptableRendererData[];
        if (rendererDataList == null) return null;

        foreach (var rendererData in rendererDataList)
        {
            if (rendererData == null) continue;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is CRTRendererFeature crt)
                    return crt;
            }
        }
        return null;
    }

    // Returns a snapshot of what a given preset looks like without permanently changing state.
    private CRTSnapshot GetPresetSnapshot(CRTRendererFeature.Presets preset)
    {
        var saved = CRTSnapshot.Capture(CRTFeature);
        CRTFeature.preset = preset;
        CRTFeature.OnValidate();
        var snap = CRTSnapshot.Capture(CRTFeature);
        saved.Apply(CRTFeature);
        CRTFeature.preset = CRTRendererFeature.Presets.custom;
        return snap;
    }

    // -------------------------------------------------------------------------
    // Instant preset / snapshot
    // -------------------------------------------------------------------------

    // Instantly applies a named preset.
    public void ApplyPreset(CRTRendererFeature.Presets preset)
    {
        if (CRTFeature == null) return;
        CRTFeature.preset = preset;
        CRTFeature.OnValidate();
    }

    // Instantly applies a previously captured snapshot.
    public void ApplySnapshot(in CRTSnapshot snapshot)
    {
        if (CRTFeature == null) return;
        snapshot.Apply(CRTFeature);
        CRTFeature.preset = CRTRendererFeature.Presets.custom;
    }

    // Captures and returns the current state.
    public CRTSnapshot CaptureSnapshot() => CRTSnapshot.Capture(CRTFeature);

    // -------------------------------------------------------------------------
    // Smooth transitions
    // -------------------------------------------------------------------------

    // Smoothly lerps all parameters to a named preset over <duration> seconds.
    public void LerpToPreset(CRTRendererFeature.Presets preset, float duration)
    {
        if (CRTFeature == null) return;
        var target = GetPresetSnapshot(preset);
        LerpToSnapshot(target, duration);
    }

    // Smoothly lerps all parameters toward a target snapshot over <duration> seconds.
    public void LerpToSnapshot(CRTSnapshot target, float duration)
    {
        if (CRTFeature == null) return;
        if (_lerpCoroutine != null) StopCoroutine(_lerpCoroutine);
        _lerpCoroutine = StartCoroutine(LerpCoroutine(CRTSnapshot.Capture(CRTFeature), target, duration));
    }

    private IEnumerator LerpCoroutine(CRTSnapshot from, CRTSnapshot to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            CRTSnapshot.Lerp(from, to, Mathf.Clamp01(elapsed / duration)).Apply(CRTFeature);
            yield return null;
        }
        to.Apply(CRTFeature);
        _lerpCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // One-shot VFX helpers (spike and restore)
    // -------------------------------------------------------------------------

    // Briefly spikes glitch parameters then restores them. Good for hit reactions.
    public void GlitchFlash(float duration, float strength = 0.5f, int bands = 4, float movementSpeed = 0f)
    {
        if (CRTFeature == null) return;
        if (_glitchCoroutine != null) StopCoroutine(_glitchCoroutine);
        _glitchCoroutine = StartCoroutine(GlitchFlashCoroutine(duration, strength, bands, movementSpeed));
    }

    private IEnumerator GlitchFlashCoroutine(float duration, float strength, int bands, float movementSpeed)
    {
        float prevStrength      = CRTFeature.glitchStrength;
        int   prevBands         = CRTFeature.glitchBands;
        float prevFrequency     = CRTFeature.glitchFrequency;
        float prevMovementSpeed = CRTFeature.glitchMovementSpeed;

        CRTFeature.glitchStrength      = strength;
        CRTFeature.glitchBands         = bands;
        CRTFeature.glitchFrequency     = 1f;
        CRTFeature.glitchMovementSpeed = movementSpeed;

        yield return new WaitForSeconds(duration);

        CRTFeature.glitchStrength      = prevStrength;
        CRTFeature.glitchBands         = prevBands;
        CRTFeature.glitchFrequency     = prevFrequency;
        CRTFeature.glitchMovementSpeed = prevMovementSpeed;
        _glitchCoroutine = null;
    }

    // Briefly spikes noise then restores it. Good for static / interference effects.
    public void NoiseFlash(float duration, float alpha = 0.3f, float size = 50f, float speed = 5f)
    {
        if (CRTFeature == null) return;
        if (_noiseCoroutine != null) StopCoroutine(_noiseCoroutine);
        _noiseCoroutine = StartCoroutine(NoiseFlashCoroutine(duration, alpha, size, speed));
    }

    private IEnumerator NoiseFlashCoroutine(float duration, float alpha, float size, float speed)
    {
        float prevAlpha = CRTFeature.noiseAlpha;
        float prevSize  = CRTFeature.noiseSize;
        float prevSpeed = CRTFeature.noiseSpeed;

        CRTFeature.noiseAlpha = alpha;
        CRTFeature.noiseSize  = size;
        CRTFeature.noiseSpeed = speed;

        yield return new WaitForSeconds(duration);

        CRTFeature.noiseAlpha = prevAlpha;
        CRTFeature.noiseSize  = prevSize;
        CRTFeature.noiseSpeed = prevSpeed;
        _noiseCoroutine = null;
    }

    // Briefly spikes chromatic aberration then restores it. Good for impacts / explosions.
    public void ChromaticAberrationFlash(float duration, float strength = 3f)
    {
        if (CRTFeature == null) return;
        if (_chromaticCoroutine != null) StopCoroutine(_chromaticCoroutine);
        _chromaticCoroutine = StartCoroutine(ChromaticFlashCoroutine(duration, strength));
    }

    private IEnumerator ChromaticFlashCoroutine(float duration, float strength)
    {
        float prev = CRTFeature.chromaticAberration;

        CRTFeature.chromaticAberration = strength;
        CRTFeature.OnValidate();

        yield return new WaitForSeconds(duration);

        CRTFeature.chromaticAberration = prev;
        CRTFeature.OnValidate();
        _chromaticCoroutine = null;
    }

    // Pulses chromatic aberration up/down a number of times, then restores baseline.
    public void ChromaticAberrationPulse(float duration, float peakStrength = 3f, int pulses = 2)
    {
        if (CRTFeature == null) return;
        if (_chromaticPulseCoroutine != null) StopCoroutine(_chromaticPulseCoroutine);
        _chromaticPulseCoroutine = StartCoroutine(ChromaticPulseCoroutine(duration, peakStrength, pulses));
    }

    private IEnumerator ChromaticPulseCoroutine(float duration, float peakStrength, int pulses)
    {
        float prev = CRTFeature.chromaticAberration;
        float dur = Mathf.Max(0.01f, duration);
        float peak = Mathf.Max(0f, peakStrength);
        int pulseCount = Mathf.Max(1, pulses);

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / dur);
            float wave = Mathf.Sin(normalized * Mathf.PI * 2f * pulseCount);
            float envelope = 1f - normalized;
            float value = prev + Mathf.Abs(wave) * peak * envelope;

            CRTFeature.chromaticAberration = value;
            CRTFeature.OnValidate();
            yield return null;
        }

        CRTFeature.chromaticAberration = prev;
        CRTFeature.OnValidate();
        _chromaticPulseCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // Blur impulse
    // -------------------------------------------------------------------------

    // Pushes the blur up by <amount>, clamped to blurMax. Decays back to baseline automatically.
    public void Blur(float amount)
    {
        if (CRTFeature == null) return;
        CRTFeature.blur = Mathf.Min(CRTFeature.blur + amount, blurMax);
    }

    // -------------------------------------------------------------------------
    // Direct parameter setters
    // -------------------------------------------------------------------------

    public void SetGlitch(float strength, int bands, float movementSpeed = 0f)
    {
        if (CRTFeature == null) return;
        CRTFeature.glitchStrength      = strength;
        CRTFeature.glitchBands         = bands;
        CRTFeature.glitchMovementSpeed = movementSpeed;
    }

    public void SetNoise(float alpha, float size = 50f, float speed = 2f)
    {
        if (CRTFeature == null) return;
        CRTFeature.noiseAlpha = alpha;
        CRTFeature.noiseSize  = size;
        CRTFeature.noiseSpeed = speed;
    }

    public void SetScreenBend(float bend)
    {
        if (CRTFeature == null) return;
        CRTFeature.screenBend = bend;
    }

    public void SetVignette(float size, float smooth = 2f, float round = 63f)
    {
        if (CRTFeature == null) return;
        CRTFeature.vignetteSize   = size;
        CRTFeature.vignetteSmooth = smooth;
        CRTFeature.vignetteRound  = round;
    }

    public void SetChromaticAberration(float amount)
    {
        if (CRTFeature == null) return;
        CRTFeature.chromaticAberration = amount;
        CRTFeature.OnValidate();
    }

    public void SetBrightness(float brightness)
    {
        if (CRTFeature == null) return;
        CRTFeature.brightness = brightness;
    }

    public void SetColorGrade(float brightness = 1f, float contrast = 1f, float gamma = 1f,
                              float red = 1f, float green = 1f, float blue = 1f)
    {
        if (CRTFeature == null) return;
        CRTFeature.brightness = brightness;
        CRTFeature.contrast   = contrast;
        CRTFeature.gamma      = gamma;
        CRTFeature.red        = red;
        CRTFeature.green      = green;
        CRTFeature.blue       = blue;
    }
}

