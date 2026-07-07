using UnityEngine;

public class OnHitParticleEffect : MonoBehaviour
{
    public static OnHitParticleEffect instance;
    public ParticleSystem onHitPs;
    private ParticleSystem.ShapeModule shape;
    private ParticleSystem.MainModule _main;
    private ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule;
    private ParticleSystem.MinMaxGradient colorOverLifetimeGradient;

    [Tooltip("Half-angle of the emission cone in radians when useDirection is true.")]
    public float directionConeRadians = 0.4f;

    [Tooltip("Default number of particles to emit per hit when no explicit count is given.")]
    public int defaultParticleCount = 5;

    [Header("Count From Damage Scale")]
    [Tooltip("Damage-text scale value that maps to minParticlesAtScale.")]
    public float minDamageScale = 0.75f;
    [Tooltip("Damage-text scale value that maps to maxParticlesAtScale.")]
    public float maxDamageScale = 1.25f;
    [Min(1)]
    public int minParticlesAtScale = 2;
    [Min(1)]
    public int maxParticlesAtScale = 10;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        if (onHitPs != null)
        {
            shape = onHitPs.shape;
            _main = onHitPs.main;
            colorOverLifetimeModule = onHitPs.colorOverLifetime;
        }
    }

    /// <summary>
    /// Emits hit particles at <paramref name="position"/>.
    /// When <paramref name="useDirection"/> is true the emission cone is aimed along
    /// <paramref name="directionAngle"/> (world-space degrees, 0 = right).
    /// The cone spread is controlled by the <see cref="directionConeRadians"/> field.
    /// </summary>
    public void OnHitVfx(Vector3 position, int numParticles, Color color, bool useDirection = false, float directionAngle = 0f)
    {
        if (onHitPs == null) return;

        onHitPs.transform.position = position;
        _main.startColor = color;
        colorOverLifetimeGradient.colorMax = color;
        color.a = 0;
        colorOverLifetimeGradient.colorMin = color;

        if (useDirection)
        {
            shape.enabled = true;
            shape.angle = directionConeRadians * Mathf.Rad2Deg;
            // Unity's Cone shape emits along local +Z; rotate around Z so it faces directionAngle.
            shape.rotation = new Vector3(0f, 0f, directionAngle - 90f);
        }

        onHitPs.Emit(numParticles);
    }

    public int GetParticleCountFromDamageScale(float damageTextScale)
    {
        float loScale = Mathf.Min(minDamageScale, maxDamageScale);
        float hiScale = Mathf.Max(minDamageScale, maxDamageScale);
        float t = hiScale > loScale
            ? Mathf.InverseLerp(loScale, hiScale, damageTextScale)
            : 0f;

        int loParticles = Mathf.Min(minParticlesAtScale, maxParticlesAtScale);
        int hiParticles = Mathf.Max(minParticlesAtScale, maxParticlesAtScale);
        return Mathf.RoundToInt(Mathf.Lerp(loParticles, hiParticles, t));
    }

    /// <summary>Convenience overload using <see cref="defaultParticleCount"/>.</summary>
    public void OnHitVfx(Vector3 position, Color color, bool useDirection = false, float directionAngle = 0f)
        => OnHitVfx(position, defaultParticleCount, color, useDirection, directionAngle);
}
