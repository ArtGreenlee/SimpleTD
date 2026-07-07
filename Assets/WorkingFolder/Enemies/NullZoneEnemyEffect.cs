using UnityEngine;

public class NullZoneEnemyEffect : EnemyEffect
{
    [Header("Null Zone")]
    [Min(0f)] public float zoneRadius = 2.5f;
    [Min(0f)] public float stunDuration = 0.2f;

    [Header("Indicator")]
    [SerializeField, Range(0f, 1f)] private float indicatorAlpha = 0.2f;
    [SerializeField] private int sortingOrderOffset = 1;
    [SerializeField, Min(0.01f)] private float indicatorTimeoutSeconds = 5f;
    [SerializeField] private float indicatorZOffset = -0.02f;

    private GameObject _zoneIndicator;

    public override TriggerCondition Condition => TriggerCondition.Interval;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureIndicator();
        SyncIndicatorTransform();
    }

    private void LateUpdate()
    {
        EnsureIndicator();
        SyncIndicatorTransform();
    }

    private void OnDisable()
    {
        DestroyIndicator();
    }

    private void OnDestroy()
    {
        DestroyIndicator();
    }

    public override void Trigger()
    {
        if (!isActiveAndEnabled)
        {
            base.Trigger();
            return;
        }

        if (TowerManager.instance == null)
        {
            base.Trigger();
            return;
        }

        float radius = Mathf.Max(0f, zoneRadius);
        if (radius <= 0f)
        {
            base.Trigger();
            return;
        }

        float sqrRadius = radius * radius;
        Vector3 center = transform.position;

        foreach (Tower tower in TowerManager.instance.EnumeratePlacedTowers())
        {
            if (tower == null) continue;
            if (!tower.isActiveAndEnabled) continue;

            Vector3 delta = tower.transform.position - center;
            if (delta.sqrMagnitude > sqrRadius) continue;

            tower.Stun(stunDuration);
        }

        base.Trigger();
    }

    private void EnsureIndicator()
    {
        if (_zoneIndicator != null) return;
        if (AOEObjectPool.instance == null) return;

        Color indicatorColor = Color.magenta;
        if (CM.i != null)
        {
            indicatorColor = CM.i.ColorTypeToColor(CM.ColorType.Purple);
        }

        indicatorColor.a = indicatorAlpha;

        _zoneIndicator = AOEObjectPool.instance.SpawnUnmanagedIndicator(
            transform.position,
            Mathf.Max(0.1f, zoneRadius * 2f),
            indicatorColor,
            GetIndicatorSortingOrder(),
            indicatorTimeoutSeconds,
            indicatorZOffset);
    }

    private void SyncIndicatorTransform()
    {
        if (_zoneIndicator == null) return;

        Vector3 p = transform.position;
        p.z += indicatorZOffset;
        _zoneIndicator.transform.position = p;
        _zoneIndicator.transform.localScale = new Vector3(
            Mathf.Max(0.1f, zoneRadius * 2f),
            Mathf.Max(0.1f, zoneRadius * 2f),
            1f);
    }

    private int GetIndicatorSortingOrder()
    {
        int maxOrder = 0;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            if (sr.sortingOrder > maxOrder) maxOrder = sr.sortingOrder;
        }

        return maxOrder + sortingOrderOffset;
    }

    private void DestroyIndicator()
    {
        if (_zoneIndicator == null) return;
        Destroy(_zoneIndicator);
        _zoneIndicator = null;
    }
}
