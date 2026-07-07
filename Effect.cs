using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Base class for all tower effects. Effects are components that apply gameplay mechanics when triggered.
/// 
/// Adding/Removing Effects at Runtime:
/// - Use RegisterEffectIfMissing() or EnsureActiveEffect<T>() to add effects at runtime
/// - Set active = false to disable an effect without removing it
/// - Effects that are null or inactive won't be applied via ApplyEffects()
/// </summary>
public class Effect : MonoBehaviour
{
    protected Tower tower;
    public bool active = true;
    [Range(0f, 1f)] public float effectProbability = 1f;

    public virtual void Awake()
    {
        tower = GetComponent<Tower>();
    }

    protected bool ShouldApplyEffect()
    {
        return active && Random.value <= Mathf.Clamp01(effectProbability);
    }

    public virtual void ApplyEffect(Enemy enemy, Projectile projectile=null)
    {

    }

    public virtual void OnAddedToTower()
    {
        
    }
}
