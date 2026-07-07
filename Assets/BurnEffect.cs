using UnityEngine;

public class BurnOnHitEffect : Effect
{
    public enum StackMode
    {
        Constant,
        RandomRange,
    }

    [SerializeField] private StackMode stackMode = StackMode.Constant;
    [Min(1)] public int constantStacks = 1;
    [Min(1)] public int randomMinStacks = 1;
    [Min(1)] public int randomMaxStacks = 1;

    // Backward-compatible alias used by existing tower setup logic.
    public int burnStacks
    {
        get => constantStacks;
        set => constantStacks = Mathf.Max(1, value);
    }

    private int ResolveStacks()
    {
        if (stackMode == StackMode.RandomRange)
        {
            int min = Mathf.Min(randomMinStacks, randomMaxStacks);
            int max = Mathf.Max(randomMinStacks, randomMaxStacks);
            return Random.Range(min, max + 1);
        }

        return Mathf.Max(1, constantStacks);
    }

    public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
    {
        if (!ShouldApplyEffect())
        {
            base.ApplyEffect(enemy, projectile);
            return;
        }

        enemy.health.ApplyBurn(ResolveStacks(), tower);
        base.ApplyEffect(enemy, projectile);
    }
}
