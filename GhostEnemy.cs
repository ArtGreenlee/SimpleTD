using UnityEngine;

public class GhostEnemy : Enemy
{
    public float ghostDuration;
    public float ghostHealthPercentage;
    public float goalUnghostDistance;
    public float ghostOpacity;
	[SerializeField] private float goalDistanceCheckInterval = 0.25f;
    private GhostEnemyEffect ghostEffect;

    public override void OnHealthChange()
    {
        EnsureEffectConfigured();
        base.OnHealthChange();
    }

    public override void OnDamageTaken(float damageAmount)
    {
        EnsureEffectConfigured();
        base.OnDamageTaken(damageAmount);
    }

    public override void Update()
    {
        EnsureEffectConfigured();
        base.Update();
    }

    private GhostEnemyEffect EnsureEffectConfigured()
    {
        if (ghostEffect == null)
        {
            ghostEffect = GetComponent<GhostEnemyEffect>();
        }

        if (ghostEffect == null)
        {
            ghostEffect = gameObject.AddComponent<GhostEnemyEffect>();
            RefreshEnemyEffects();
        }

        ghostEffect.ghostDuration = ghostDuration;
        ghostEffect.ghostHealthPercentage = ghostHealthPercentage;
        ghostEffect.goalUnghostDistance = goalUnghostDistance;
        ghostEffect.ghostOpacity = ghostOpacity;
        ghostEffect.GoalDistanceCheckInterval = goalDistanceCheckInterval;
        ghostEffect.triggerValue = ghostHealthPercentage;

        return ghostEffect;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (ghostEffect == null)
        {
            ghostEffect = GetComponent<GhostEnemyEffect>();
        }

        if (ghostEffect != null)
        {
            ghostEffect.ghostDuration = ghostDuration;
            ghostEffect.ghostHealthPercentage = ghostHealthPercentage;
            ghostEffect.goalUnghostDistance = goalUnghostDistance;
            ghostEffect.ghostOpacity = ghostOpacity;
            ghostEffect.GoalDistanceCheckInterval = goalDistanceCheckInterval;
            ghostEffect.triggerValue = ghostHealthPercentage;
        }
    }
#endif
}
