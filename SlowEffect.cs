using UnityEngine;

public class SlowEffect : Effect
{
    [Tooltip("Increase applied to the target's slow percentage per hit (0..1).")]
    public float slowAmount = 0.1f;

    public override void ApplyEffect(Enemy e, Projectile p=null)
    {
        if (e == null) return;
        if (!ShouldApplyEffect()) return;
        e.ApplySlow(GetSlowAmount());
    }

    public float GetSlowAmount()
    {
        float s = slowAmount;
        if (tower.UpgradeActive(UpgradeData.UID.IncreaseSlow))
        {
            s += .1f;
        }
        return s;
    }
}
