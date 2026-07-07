using UnityEngine;

public class GhostEnemyEffect : EnemyEffect
{
    public float ghostDuration;
    public float ghostHealthPercentage;
    public float goalUnghostDistance;
    public float ghostOpacity = 0.35f;
    [SerializeField] private float goalDistanceCheckInterval = 0.25f;

    private bool ghostTriggered;
    private bool isGhosted;
    private float ghostUntil = -1f;
    private SpriteRenderer[] renderers;
    private float[] originalAlphas;
    private float nextGoalDistanceCheckTime;
    private Pathfinding pathfinding;
    private bool runtimeAgentEngagementBlockApplied;

    public override TriggerCondition Condition => TriggerCondition.HealthBelowThreshold;
    public float GoalDistanceCheckInterval
    {
        get => goalDistanceCheckInterval;
        set => goalDistanceCheckInterval = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // Populate the threshold list with the single ghost health percentage.
        healthThresholds.Clear();
        healthThresholds.Add(ghostHealthPercentage);
        ResetGhostState();
    }

    private void OnDisable()
    {
        RestoreDefaultState();
    }

    public override void Trigger()
    {
        Ghost();
        base.Trigger();
    }

    private void Update()
    {
        if (!isGhosted) return;

        if (goalUnghostDistance > 0f && Time.time >= nextGoalDistanceCheckTime)
        {
            nextGoalDistanceCheckTime = Time.time + Mathf.Max(0.01f, goalDistanceCheckInterval);

            var owner = Enemy;
            var movement = owner != null ? owner.GetMovement() : null;
            var goal = movement != null ? movement.Goal : null;
            if (goal != null)
            {
                if (pathfinding == null) pathfinding = FindFirstObjectByType<Pathfinding>();

                float distanceToGoal = pathfinding != null
                    ? pathfinding.GetPathDistanceToGoal(owner.transform.position, goal)
                    : Vector2.Distance(owner.transform.position, goal.position);

                if (distanceToGoal <= goalUnghostDistance)
                {
                    UnGhost();
                    return;
                }
            }
        }

        if (ghostUntil > 0f && Time.time >= ghostUntil)
        {
            UnGhost();
        }
    }

    private void Ghost()
    {
        if (ghostTriggered) return;

        var owner = Enemy;
        if (owner == null) return;

        ghostTriggered = true;
        isGhosted = true;
        ghostUntil = ghostDuration > 0f ? Time.time + ghostDuration : -1f;

        owner.SetRuntimePreventAgentEngagement(true);
        runtimeAgentEngagementBlockApplied = true;

        var collider = owner.GetDamageCollider();
        if (collider != null)
        {
            collider.enabled = false;
        }

        CacheRenderersIfNeeded(owner);
        SetOpacity(Mathf.Clamp01(ghostOpacity));
    }

    private void UnGhost()
    {
        if (!isGhosted) return;

        isGhosted = false;

        var owner = Enemy;
        if (owner != null)
        {
            var collider = owner.GetDamageCollider();
            if (collider != null)
            {
                collider.enabled = true;
            }
        }

        ReleaseRuntimeAgentEngagementBlock();
        RestoreOpacity();
    }

    private void ResetGhostState()
    {
        ghostTriggered = false;
        isGhosted = false;
        ghostUntil = -1f;
        nextGoalDistanceCheckTime = 0f;
        pathfinding = null;
        RestoreDefaultState();
    }

    private void RestoreDefaultState()
    {
        var owner = Enemy;
        if (owner != null)
        {
            var collider = owner.GetDamageCollider();
            if (collider != null)
            {
                collider.enabled = true;
            }
        }

        ReleaseRuntimeAgentEngagementBlock();
        RestoreOpacity();
    }

    private void ReleaseRuntimeAgentEngagementBlock()
    {
        if (!runtimeAgentEngagementBlockApplied) return;

        var owner = Enemy;
        if (owner != null)
        {
            owner.SetRuntimePreventAgentEngagement(false);
        }

        runtimeAgentEngagementBlockApplied = false;
    }

    private void CacheRenderersIfNeeded(Enemy owner)
    {
        if (renderers != null && originalAlphas != null && renderers.Length == originalAlphas.Length) return;

        renderers = owner.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        originalAlphas = new float[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            var currentRenderer = renderers[i];
            originalAlphas[i] = currentRenderer != null ? currentRenderer.color.a : 1f;
        }
    }

    private void SetOpacity(float alpha)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var currentRenderer = renderers[i];
            if (currentRenderer == null) continue;

            var color = currentRenderer.color;
            color.a = alpha;
            currentRenderer.color = color;
        }
    }

    private void RestoreOpacity()
    {
        if (renderers == null || originalAlphas == null) return;

        int rendererCount = Mathf.Min(renderers.Length, originalAlphas.Length);
        for (int i = 0; i < rendererCount; i++)
        {
            var currentRenderer = renderers[i];
            if (currentRenderer == null) continue;

            var color = currentRenderer.color;
            color.a = originalAlphas[i];
            currentRenderer.color = color;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        triggerValue = ghostHealthPercentage;
        unlimited = false;
        triggerLimit = 1;
    }

    private void OnValidate()
    {
        triggerValue = ghostHealthPercentage;
        unlimited = false;
        triggerLimit = 1;
    }
#endif
}
