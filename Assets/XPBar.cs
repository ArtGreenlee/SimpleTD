using UnityEngine;

public class XPBar : MonoBehaviour
{
    [Header("Renderers")]
    public SpriteRenderer blueBox;
    public SpriteRenderer blackBox;
    public SpriteRenderer whiteBox;

    [Header("Behavior")]
    [Tooltip("Seconds to wait before the buffer starts catching up.")]
    public float bufferWait = 0.15f;
    [Tooltip("How fast the buffer catches up to the current XP percentage.")]
    public float bufferSpeed = 2f;
    [Tooltip("Seconds to keep the bar visible since the last XP update.")]
    public float deactivateDuration = 4f;

    [Header("References")]
    public GameObject xpBarRendererParent;

    private float currentPercentage = 0f;
    private float bufferPercentage = 0f;
    private float bufferTimer = 0f;
    private float deactivateTimer = 0f;

    private void Start()
    {
        ApplyPercent(currentPercentage, bufferPercentage);
        HideXPBar();
    }

    public void SetPercentage(float percentage)
    {
        currentPercentage = Mathf.Clamp01(percentage);
        bufferTimer = Time.time + Mathf.Max(0f, bufferWait);
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
        ShowXPBar();
    }

    public void SetPercentageImmediate(float percentage)
    {
        currentPercentage = Mathf.Clamp01(percentage);
        bufferPercentage = currentPercentage;
        deactivateTimer = Time.time + Mathf.Max(0f, deactivateDuration);
        ApplyPercent(currentPercentage, bufferPercentage);
        ShowXPBar();
    }

    public void RegisterChange(float percentage)
    {
        SetPercentage(percentage);
    }

    private void SetBoxSize(SpriteRenderer box, float percentage)
    {
        if (box == null) return;
        percentage = Mathf.Clamp01(percentage);
        box.transform.localScale = new Vector2(percentage, box.transform.localScale.y);
        box.transform.localPosition = new Vector2(-(.5f - percentage / 2f), box.transform.localPosition.y);
    }

    private void ApplyPercent(float current, float buffer)
    {
        SetBoxSize(blueBox, current);
        SetBoxSize(whiteBox, buffer);
        SetBoxSize(blackBox, 1f);
    }

    public void HideXPBar()
    {
        if (xpBarRendererParent != null) xpBarRendererParent.SetActive(false);
    }

    public void ShowXPBar()
    {
        if (xpBarRendererParent != null) xpBarRendererParent.SetActive(true);
    }

    private void Update()
    {
        if (Time.time >= bufferTimer)
        {
            float spd = Mathf.Max(0.01f, bufferSpeed);
            if (bufferPercentage > currentPercentage)
                bufferPercentage = Mathf.Max(currentPercentage, bufferPercentage - Time.deltaTime * spd);
            else if (bufferPercentage < currentPercentage)
                bufferPercentage = Mathf.Min(currentPercentage, bufferPercentage + Time.deltaTime * spd);
        }

        ApplyPercent(currentPercentage, bufferPercentage);

        if (Time.time > deactivateTimer)
        {
            HideXPBar();
        }
    }
}
