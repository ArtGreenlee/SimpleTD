using UnityEngine;

public class EnemyInteractable : Interactable
{
    private Enemy enemy;

    public override void Awake()
    {
        base.Awake();
        enemy = GetComponent<Enemy>();
    }

    public override string GetCursorToolTipText()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        return enemy != null ? enemy.name : string.Empty;
    }

    public string GetEnemyDescription()
    {
        if (enemy.id == Enemy.ID.Cluster)
        {
            return "Small and fast";
        }
        if (enemy.id == Enemy.ID.EliteGrunt)
        {
            return "Armored enemy";
        }
        if (enemy.id == Enemy.ID.Ghost)
        {
            GhostEnemyEffect ghostEnemyEffect = enemy.GetComponent<GhostEnemyEffect>();
            if (ghostEnemyEffect == null)
            {
                return "Phases into ghost form and becomes untargetable by Agents for a short time";
            }

            return "At " + Mathf.RoundToInt(ghostEnemyEffect.ghostHealthPercentage * 100f) +
                   "% health, phases for " + ghostEnemyEffect.ghostDuration.ToString("0.##") +
                   "s, becomes untargetable by " + CM.i.RTC(CM.ColorType.Yellow, "Agents") +
                   ", and re-materializes near the goal";
        }
        if (enemy.id == Enemy.ID.Heal)
        {
            HealEnemyEffect healEnemyEffect = enemy.GetComponent<HealEnemyEffect>();
            if (healEnemyEffect == null)
            {
                return "Periodically heals a nearby enemy";
            }

            return "Periodically heals a nearby enemy for " +
                   CM.i.RTC(CM.ColorType.Green, healEnemyEffect.healAmount.ToString("0.##")) +
                   " health within " + healEnemyEffect.healMaxDistance.ToString("0.##") + " range";
        }
        if (enemy.id == Enemy.ID.Speck)
        {
            return "Small and cannot be targetted by " + CM.i.RTC(CM.ColorType.Yellow, "Agents");
        }
        if (enemy.id == Enemy.ID.Stun)
        {
            StunEnemyEffect stunEnemyEffect = enemy.GetComponent<StunEnemyEffect>();
            if (stunEnemyEffect == null)
            {
                return "Periodically stuns a nearby tower";
            }

            return "Periodically " + CM.i.RTC(CM.ColorType.Purple, "stuns") +
                   " the nearest tower for " + stunEnemyEffect.stunDuration.ToString("0.##") +
                   "s if within " + stunEnemyEffect.minDistance.ToString("0.##") + " range";
        }
        if (enemy.id == Enemy.ID.Tank)
        {
            return "High health, slow";
        }
        if (enemy.id == Enemy.ID.Teleport)
        {
            TeleportEnemyEffect teleportEnemyEffect = enemy.GetComponent<TeleportEnemyEffect>();
            if (teleportEnemyEffect == null)
            {
                return "Periodically teleports to a random nearby position";
            }

            return "Periodically teleports to a random point within " +
                   teleportEnemyEffect.teleportRadius.ToString("0.##") + " range";
        }
        if (enemy.id == Enemy.ID.Scythe) {
            return "Destroys agents";
        }
        return "";
    }
}
