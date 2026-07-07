using UnityEngine;
using UID = UpgradeData.UID;

public class AgentTower : Tower
{
    public float agentRestingRadius = 2;
    public GameObject agentPrefab;
    [SerializeField] private int maxAgents;
    [Header("Agent Spawn")]
    [SerializeField] private Agent.CombatMode spawnedAgentCombatMode = Agent.CombatMode.Default;
    public CM.ColorType agentColor = CM.ColorType.Yellow;

    public int GetMaxNumAgents()
    {
        int bonusAgents = (RM.i != null && RM.i.Active(RM.ID.agentMaxIncrease)) ? 1 : 0;
        int yellowTagBonusAgents = TagManager.instance != null ? TagManager.instance.GetYellowTagMaxAgentsPerTowerBonus() : 0;
        return Mathf.Max(0, maxAgents + bonusAgents + yellowTagBonusAgents);
    }

    public override void Update()
    {
        base.Update();

        if (CurrentState != Tower.State.Placed) return;
        if (AgentHelper.instance == null) return;

        Vector3 restingPosition = towerTool != null
            ? towerTool.transform.position
            : GetTargetPosition();

        if (PIC.forceAllTowersTargetHighlightedEnemy && PIC.instance != null)
        {
            Enemy highlightedEnemy = PIC.instance.GetHighlightedEnemy();
            RangeManager rm = GetRangeManager();
            if (highlightedEnemy != null && rm != null && rm.IsEnemyValidTarget(highlightedEnemy))
            {
                restingPosition = highlightedEnemy.transform.position;
            }
        }

        restingPosition.z = 0f;
        AgentHelper.instance.SetTowerRestingPosition(this, restingPosition);
    }

    public override void Attack()
    {
        bool isMaxCharge = IsAtMaxCharge();
        float agentBuffBonus = (isMaxCharge && UpgradeActive(UID.AgentChargeBuff)) ? UpgradeData.AgentChargeBuff_Bonus : 0f;
        float spawnScale = 1f + agentBuffBonus;

        base.Attack();
        if (AgentHelper.instance == null) return;
        if (id == Tower.ID.Necromancer) return; // Necromancer spawns agents on enemy death, not on attack)

        Vector3 restingPosition = towerTool != null
            ? towerTool.transform.position
            : GetTargetPosition();
        restingPosition.z = 0f;

        Agent spawned = AgentHelper.instance.SpawnAgent(
            tower: this,
            colorType: agentColor,
            restingRadius: agentRestingRadius,
            restingPosition: restingPosition,
            spawnScale: spawnScale,
            maxAgents: GetMaxNumAgents(),
            combatMode: spawnedAgentCombatMode,
            agentPrefabOverride: agentPrefab);

        if (spawned != null && agentBuffBonus > 0f)
        {
            spawned.SetDamageMultiplier(1f + agentBuffBonus);

            Health agentHealth = spawned.GetComponentInChildren<Health>();
            if (agentHealth != null)
            {
                agentHealth.SetMaxHealth(agentHealth.GetMaxHealth() * (1f + agentBuffBonus));
            }

            spawned.RefreshYellowTagHealthBonus();
        }
    }

    public override void OnEnemyKilledInRange(Enemy enemy)
    {
        base.OnEnemyKilledInRange(enemy);

        if (id != Tower.ID.Necromancer) return;
        if (enemy == null) return;
        if (AgentHelper.instance == null) return;

        Vector3 spawnPosition = enemy.transform.position;
        spawnPosition.z = 0f;

        Agent spawned = AgentHelper.instance.SpawnAgent(
            tower: this,
            colorType: agentColor,
            restingRadius: agentRestingRadius,
            restingPosition: spawnPosition,
            spawnScale: 1f,
            maxAgents: GetMaxNumAgents(),
            combatMode: spawnedAgentCombatMode,
            agentPrefabOverride: agentPrefab,
            spawnAtRestingPosition: true);

        if (spawned != null && LaserObjectPool.instance != null)
        {
            Color laserColor = CM.i != null ? CM.i.ColorTypeToColor(agentColor) : Color.white;
            LaserObjectPool.instance.ShowLaser(transform.position, spawned.transform.position, laserColor, 0.05f, 0.15f, 1f);
        }
    }

    public override void ActivateUpgrade(UID uid)
    {
        base.ActivateUpgrade(uid);

        if (uid != UID.AgentLaserConversion) return;

        spawnedAgentCombatMode = Agent.CombatMode.Laser;
        agentColor = CM.ColorType.Purple;
        SetDamageTypeFlags(DamageTypeFlags.Purple);

        if (!tags.Contains(Tag.Purple))
        {
            tags.Add(Tag.Purple);
            if (TagManager.instance != null) TagManager.instance.NotifyTowersChanged();
        }

        if (AgentHelper.instance != null)
        {
            AgentHelper.instance.UpdateTowerAgents(this, agentColor, spawnedAgentCombatMode);
        }
    }

    public bool ExplodeOnDeathActive()
    {
        return UpgradeActive(UID.AgentExplodeOnDeath);
    }

    public override string GetUpgradeDescription(UID uid)
    {
        return base.GetUpgradeDescription(uid);
    }
}
