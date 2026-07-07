using UnityEngine;

public class CRTJuice : MonoBehaviour
{
    public Juice redJuice;
    public Juice blueJuice;
    public Juice greenJuice;

    [Min(0f)] public float alphaIncrease = 0.2f;
    [Range(0f, 1f)] public float baseAlpha = 0.2f;
    [Min(0f)] public float alphaReturnSpeed = 8f;
    [SerializeField] private bool fixedRGBDirection = false;
    private SpriteRenderer redSr;
    private SpriteRenderer blueSr;
    private SpriteRenderer greenSr;
    private float _redAlpha;
    private float _greenAlpha;
    private float _blueAlpha;
    private Enemy _enemy;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
        redSr = redJuice != null ? redJuice.GetComponent<SpriteRenderer>() : null;
        blueSr = blueJuice != null ? blueJuice.GetComponent<SpriteRenderer>() : null;
        greenSr = greenJuice != null ? greenJuice.GetComponent<SpriteRenderer>() : null;

        _redAlpha = Mathf.Clamp01(baseAlpha);
        _greenAlpha = Mathf.Clamp01(baseAlpha);
        _blueAlpha = Mathf.Clamp01(baseAlpha);

        SyncSpriteAndColor(redSr, _redAlpha, CM.ColorType.Red);
        SyncSpriteAndColor(greenSr, _greenAlpha, CM.ColorType.Green);
        SyncSpriteAndColor(blueSr, _blueAlpha, CM.ColorType.Blue);
    }

    private void SyncSpriteAndColor(SpriteRenderer sr, float alpha, CM.ColorType colorType)
    {
        if (sr == null) return;
        if (_enemy != null && _enemy.enemyBodySR != null)
        {
            sr.sprite = _enemy.enemyBodySR.sprite;
        }
        if (CM.i != null)
        {
            Color c = CM.i.ColorTypeToColor(colorType);
            c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }
    }

    private void UpdateSpriteAndColor(SpriteRenderer sr, CM.ColorType colorType, float alpha)
    {
        if (sr == null) return;
        if (_enemy != null && _enemy.enemyBodySR != null && sr.sprite != _enemy.enemyBodySR.sprite)
        {
            sr.sprite = _enemy.enemyBodySR.sprite;
        }
        if (CM.i != null)
        {
            Color c = CM.i.ColorTypeToColor(colorType);
            c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }
    }

    void Update()
    {
        float t = Mathf.Clamp01(alphaReturnSpeed * Time.deltaTime);
        float targetAlpha = Mathf.Clamp01(baseAlpha);

        _redAlpha = Mathf.Lerp(_redAlpha, targetAlpha, t);
        _greenAlpha = Mathf.Lerp(_greenAlpha, targetAlpha, t);
        _blueAlpha = Mathf.Lerp(_blueAlpha, targetAlpha, t);

        UpdateSpriteAndColor(redSr, CM.ColorType.Red, _redAlpha);
        UpdateSpriteAndColor(greenSr, CM.ColorType.Green, _greenAlpha);
        UpdateSpriteAndColor(blueSr, CM.ColorType.Blue, _blueAlpha);
    }

    public void OnDamage(float dmgAmount, CM.ColorType damageType)
    {
        OnDamage(dmgAmount, damageType, transform.position);
    }

    public void OnDamage(float dmgAmount, CM.ColorType damageType, Vector3 sourcePosition)
    {
        if (dmgAmount <= 0f) return;

        Vector2 awayDirection = (Vector2)(transform.position - sourcePosition);
        if (awayDirection.sqrMagnitude > 0.000001f)
        {
            awayDirection.Normalize();
        }
        else
        {
            awayDirection = Vector2.zero;
        }

        Color c = CM.i != null ? CM.i.ColorTypeToColor(damageType) : Color.white;
        float r = Mathf.Max(0f, c.r);
        float g = Mathf.Max(0f, c.g);
        float b = Mathf.Max(0f, c.b);
        float rgbSum = r + g + b;
        if (rgbSum <= 0.0001f) return;

        float rRatio = r / rgbSum;
        float gRatio = g / rgbSum;
        float bRatio = b / rgbSum;

        ApplyChannel(redJuice, redSr, dmgAmount, rRatio, awayDirection, ref _redAlpha);
        ApplyChannel(greenJuice, greenSr, dmgAmount, gRatio, awayDirection, ref _greenAlpha);
        ApplyChannel(blueJuice, blueSr, dmgAmount, bRatio, awayDirection, ref _blueAlpha);
    }

    private Vector2 GetFixedRedDirection()
    {
        // Up-left 45 degrees in local space: (-1, 1) normalized
        return new Vector2(-Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f));
    }

    private Vector2 GetFixedGreenDirection()
    {
        // Up in local space: (0, 1)
        return Vector2.up;
    }

    private Vector2 GetFixedBlueDirection()
    {
        // Up-right 45 degrees in local space: (1, 1) normalized
        return new Vector2(Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f));
    }

    private void ApplyChannel(Juice juice, SpriteRenderer sr, float dmgAmount, float ratio, Vector2 awayDirection, ref float currentAlpha)
    {
        if (ratio <= 0f) return;

        float impulse = dmgAmount * ratio;
        if (juice != null)
        {
            juice.AddBounce(impulse);
            Vector2 forceDir = awayDirection;
            if (fixedRGBDirection)
            {
                if (juice == redJuice)
                    forceDir = GetFixedRedDirection();
                else if (juice == greenJuice)
                    forceDir = GetFixedGreenDirection();
                else if (juice == blueJuice)
                    forceDir = GetFixedBlueDirection();
            }
            if (forceDir.sqrMagnitude > 0f)
            {
                juice.AddForce(forceDir, impulse);
            }
        }

        if (sr != null)
        {
            float alphaDelta = alphaIncrease * impulse;
            currentAlpha = Mathf.Clamp01(currentAlpha + alphaDelta);
            SetAlpha(sr, currentAlpha);
        }
    }

    private static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = Mathf.Clamp01(alpha);
        sr.color = c;
    }
}
