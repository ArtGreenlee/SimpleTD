using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Renderers")]
    public SpriteRenderer redBox;
    public SpriteRenderer whiteBox; // damage buffer
    public SpriteRenderer greenBox; // pending poison damage
    public SpriteRenderer orangeBox; // pending burn damage
    public SpriteRenderer cyanBox; // pending shock damage

    [Tooltip("Optional: shows the slow percentage (0..1) as a separate bar.")]
    public SpriteRenderer blueBox;

    [Header("Behavior")]
    [Tooltip("Seconds to wait before the buffer starts catching up.")]
    public float bufferWait = 0.15f;
    [Tooltip("How fast the buffer catches up to the current health percentage.")]
    public float bufferSpeed = 2f;
    [Tooltip("Seconds to keep the bar visible after it finishes animating.")]
    public float deactivateDuration = 4f;

    [Header("References")]
    public GameObject healthBarRendererParent;

    private float currentPercentage = 1f;
    private float bufferPercentage = 1f;
    private float bufferTimer = 0f;
    private float deactivateTimer = 0f;

    private float slowPercentage = 0f;
    private float poisonPercentage = 0f;
    private float burnPercentage = 0f;
    private float shockPercentage = 0f;

    [Header("Aux Bars")]
    [Tooltip("If the registered slow/burn/poison/shock percentage is below this, the bar is hidden to avoid tiny slivers.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float minAuxPercentageVisible = 0.0025f;

    private void Start()
    {
        ApplyPercent(currentPercentage, bufferPercentage, true);
        ApplySlowPercent(slowPercentage);
        HideHealthBar();
    }

    private void OnEnable()
    {
        // No-op: keep authored local position.
    }

    /// <summary>
    /// Drive the health bar externally by setting the current health percentage [0..1].
    /// </summary>
    public virtual void RegisterChange(float percentage)
    {
        currentPercentage = Mathf.Clamp01(percentage);

        // If this is the first update, initialize buffer to current.
        if (bufferPercentage > 1f || bufferPercentage < 0f)
            bufferPercentage = currentPercentage;

        bufferTimer = Time.time + Mathf.Max(0f, bufferWait);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
        ShowHealthBar();
    }

    /// <summary>
    /// Drive the slow bar externally by setting slow percentage [0..1].
    /// 0 = not slowed, 1 = fully slowed.
    /// </summary>
    public virtual void RegisterSlowChange(float percentage)
    {
        slowPercentage = Mathf.Clamp01(percentage);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
        ApplySlowPercent(slowPercentage);
    }

    /// <summary>
    /// Drive the poison bar externally by setting poison percentage [0..1].
    /// 0 = not poisoned, 1 = fully poisoned.
    /// </summary>
    public virtual void RegisterPoisonChange(float percentage)
    {
        poisonPercentage = Mathf.Clamp01(percentage);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
    }

    /// <summary>
    /// Drive the burn bar externally by setting burn percentage [0..1].
    /// 0 = not burning, 1 = fully burning.
    /// </summary>
    public virtual void RegisterBurnChange(float percentage)
    {
        burnPercentage = Mathf.Clamp01(percentage);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
    }

    /// <summary>
    /// Drive the shock bar externally by setting shock percentage [0..1].
    /// 0 = not shocked, 1 = fully shocked.
    /// </summary>
    public virtual void RegisterShockChange(float percentage)
    {
        shockPercentage = Mathf.Clamp01(percentage);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
    }

    private void SetSegment(SpriteRenderer box, float width, ref float cursor, bool hideWhenSmall)
    {
        if (box == null) return;

        width = Mathf.Clamp01(width);
        if (hideWhenSmall && width <= minAuxPercentageVisible)
        {
            box.enabled = false;
            return;
        }

        if (width <= 0.00001f)
        {
            box.enabled = false;
            return;
        }

        box.enabled = true;

        Vector3 scale = box.transform.localScale;
        scale.x = width;
        box.transform.localScale = scale;

        Vector3 pos = box.transform.localPosition;
        pos.x = cursor + (width * 0.5f);
        box.transform.localPosition = pos;

        cursor += width;
    }

    private void SetBoxSize(SpriteRenderer box, float percentage, bool hideWhenSmall = false)
    {
        if (box == null) return;
        percentage = Mathf.Clamp01(percentage);

        if (hideWhenSmall && percentage <= minAuxPercentageVisible)
        {
            box.enabled = false;
            percentage = 0f;
        }
        else
        {
            box.enabled = true;
        }

        box.transform.localScale = new Vector2(percentage, box.transform.localScale.y);
        box.transform.localPosition = new Vector2(-(.5f - percentage / 2f), box.transform.localPosition.y);
    }

    private void ApplyPercent(float current, float buffer, bool forceAll)
    {
        float currentClamped = Mathf.Clamp01(current);

        // Pending damage is represented as portions of CURRENT health, not extra width beyond it.
        float pendingPoison = Mathf.Clamp01(poisonPercentage);
        float pendingBurn = Mathf.Clamp01(burnPercentage);
        float pendingShock = Mathf.Clamp01(shockPercentage);

        float pendingSum = pendingPoison + pendingBurn + pendingShock;
        if (pendingSum > currentClamped && pendingSum > 0f)
        {
            float scale = currentClamped / pendingSum;
            pendingPoison *= scale;
            pendingBurn *= scale;
            pendingShock *= scale;
        }

        float red = Mathf.Max(0f, currentClamped - (pendingPoison + pendingBurn + pendingShock));

        // White buffer remains the extra portion beyond current health.
        float white = Mathf.Clamp01(Mathf.Max(0f, buffer - currentClamped));

        float remaining = 1f;
        red = Mathf.Min(red, remaining); remaining -= red;
        pendingPoison = Mathf.Min(pendingPoison, remaining); remaining -= pendingPoison;
        pendingBurn = Mathf.Min(pendingBurn, remaining); remaining -= pendingBurn;
        pendingShock = Mathf.Min(pendingShock, remaining); remaining -= pendingShock;
        white = Mathf.Min(white, remaining);

        float cursor = -0.5f;
        SetSegment(redBox, red, ref cursor, hideWhenSmall: false);
        SetSegment(greenBox, pendingPoison, ref cursor, hideWhenSmall: true);
        SetSegment(orangeBox, pendingBurn, ref cursor, hideWhenSmall: true);
        SetSegment(cyanBox, pendingShock, ref cursor, hideWhenSmall: true);
        SetSegment(whiteBox, white, ref cursor, hideWhenSmall: false);
    }

    private void ApplySlowPercent(float slow)
    {
        SetBoxSize(blueBox, slow, hideWhenSmall: true);
    }

    public void HideHealthBar()
    {
        if (healthBarRendererParent != null) healthBarRendererParent.SetActive(false);
    }

    public void ShowHealthBar()
    {
        if (healthBarRendererParent != null) healthBarRendererParent.SetActive(true);
    }

    private void Update()
    {
        // Animate buffer towards current.
        if (Time.time >= bufferTimer)
        {
            float spd = Mathf.Max(0.01f, bufferSpeed);
            if (bufferPercentage > currentPercentage)
                bufferPercentage = Mathf.Max(currentPercentage, bufferPercentage - Time.deltaTime * spd);
            else if (bufferPercentage < currentPercentage)
                bufferPercentage = Mathf.Min(currentPercentage, bufferPercentage + Time.deltaTime * spd);
        }

        ApplyPercent(currentPercentage, bufferPercentage, false);

        // Auto-hide.
        if (Time.time > deactivateTimer && Mathf.Approximately(bufferPercentage, currentPercentage))
        {
            HideHealthBar();
        }
    }
}
