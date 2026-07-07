using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AgentHelper : MonoBehaviour
{
	public static AgentHelper instance;

	[Header("Defaults")]
	[SerializeField] private GameObject defaultAgentPrefab;

	private class TowerAgentState
	{
		public readonly List<Agent> agents = new List<Agent>();
		public readonly Dictionary<Enemy, int> engagedEnemyCounts = new Dictionary<Enemy, int>();
		public Vector3 restingPosition;
		public float restingSize = 1f;
		public int maxAgents = 1;
	}

	private readonly Dictionary<Tower, TowerAgentState> _stateByTower = new Dictionary<Tower, TowerAgentState>();

	private void Awake()
	{
		instance = this;
	}

	private void Update()
	{
		if (_stateByTower.Count == 0) return;

		var towersToRemove = new List<Tower>();

		foreach (var kvp in _stateByTower)
		{
			Tower tower = kvp.Key;
			TowerAgentState state = kvp.Value;

			if (tower == null || tower.CurrentState != Tower.State.Placed)
			{
				DestroyStateAgents(state);
				towersToRemove.Add(tower);
				continue;
			}

			state.agents.RemoveAll(a => a == null);
			for (int i = 0; i < state.agents.Count; i++)
			{
				if (state.agents[i] == null) continue;
				state.agents[i].RefreshYellowTagHealthBonus();
			}

			CleanupEngagedEnemyCounts(state);
		}

		for (int i = 0; i < towersToRemove.Count; i++)
		{
			_stateByTower.Remove(towersToRemove[i]);
		}
	}

	public Agent SpawnAgent(Tower tower, CM.ColorType colorType, float restingRadius, Vector3 restingPosition)
	{
		return SpawnAgent(
			tower,
			colorType,
			restingRadius,
			restingPosition,
			spawnScale: 1f,
			maxAgents: 1,
			combatMode: Agent.CombatMode.Default,
			agentPrefabOverride: null,
			spawnAtRestingPosition: false);
	}

	public Agent SpawnAgent(
		Tower tower,
		CM.ColorType colorType,
		float restingRadius,
		Vector3 restingPosition,
		float spawnScale,
		int maxAgents,
		Agent.CombatMode combatMode,
		GameObject agentPrefabOverride,
		bool spawnAtRestingPosition = false)
	{
		if (tower == null) return null;
		if (tower.CurrentState != Tower.State.Placed) return null;

		GameObject prefab = agentPrefabOverride != null ? agentPrefabOverride : defaultAgentPrefab;
		if (prefab == null)
		{
			Debug.LogWarning("AgentHelper has no agent prefab configured.", this);
			return null;
		}

		TowerAgentState state = GetOrCreateState(tower);
		state.restingPosition = restingPosition;
		state.restingPosition.z = 0f;
		state.restingSize = Mathf.Max(0f, restingRadius);
		state.maxAgents = Mathf.Max(0, maxAgents);

		state.agents.RemoveAll(a => a == null);
		if (state.agents.Count >= state.maxAgents)
		{
			return null;
		}

		Vector3 spawnPosition = spawnAtRestingPosition ? state.restingPosition : tower.transform.position;
		spawnPosition.z = 0f;

		GameObject go = Instantiate(prefab, spawnPosition, Quaternion.identity);
		SRC src = go.GetComponentInChildren<SRC>();
		if (src != null)
		{
			src.ApplyColorToAll(colorType);
		}

		Agent agent = go.GetComponent<Agent>();
		if (agent == null)
		{
			agent = go.GetComponentInChildren<Agent>();
		}

		if (agent == null)
		{
			Debug.LogWarning("AgentHelper spawned prefab without an Agent component.", tower);
			Destroy(go);
			return null;
		}

		if (!(tower is AgentTower))
		{
			state.restingPosition = FindNearestFreeCellWorld(go.transform.position);
			state.restingPosition.z = 0f;
		}

		if (!Mathf.Approximately(spawnScale, 1f) && spawnScale > 0f)
		{
			go.transform.localScale = Vector3.one * spawnScale;
		}

		agent.SetOwnerTower(tower);
		agent.SetCombatMode(combatMode);
		state.agents.Add(agent);
		return agent;
	}

	public Vector3 GetDesiredAgentPosition(Tower tower, Agent agent)
	{
		if (tower == null)
		{
			Vector3 fallback = agent != null ? agent.transform.position : transform.position;
			fallback.z = 0f;
			return fallback;
		}

		TowerAgentState state = GetOrCreateState(tower);
		state.agents.RemoveAll(a => a == null);

		if (tower is AgentTower)
		{
			Vector3 center = state.restingPosition;
			center.z = 0f;

			if (agent == null)
			{
				return center;
			}

			int index = state.agents.IndexOf(agent);
			if (index < 0)
			{
				state.agents.Add(agent);
				index = state.agents.Count - 1;
			}

			int count = Mathf.Max(1, state.agents.Count);
			float angleStep = 360f / count;
			float angleRad = Mathf.Deg2Rad * (index * angleStep);
			Vector3 offset = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * state.restingSize;
			Vector3 desired = center + offset;
			desired.z = 0f;
			return desired;
		}

		Vector3 fixedPosition = state.restingPosition;
		fixedPosition.z = 0f;
		return fixedPosition;
	}

	public bool IsEnemyEngagedByAgent(Tower tower, Enemy enemy)
	{
		if (tower == null || enemy == null) return false;
		if (!enemy.gameObject.activeInHierarchy)
		{
			GetOrCreateState(tower).engagedEnemyCounts.Remove(enemy);
			return false;
		}

		TowerAgentState state = GetOrCreateState(tower);
		return state.engagedEnemyCounts.TryGetValue(enemy, out int count) && count > 0;
	}

	public void ClaimEnemy(Tower tower, Enemy enemy)
	{
		if (tower == null || enemy == null) return;

		TowerAgentState state = GetOrCreateState(tower);
		if (!state.engagedEnemyCounts.TryGetValue(enemy, out int count)) count = 0;
		state.engagedEnemyCounts[enemy] = count + 1;
	}

	public void ReleaseEnemy(Tower tower, Enemy enemy)
	{
		if (tower == null || enemy == null) return;

		TowerAgentState state = GetOrCreateState(tower);
		if (!state.engagedEnemyCounts.TryGetValue(enemy, out int count)) return;

		count--;
		if (count <= 0)
		{
			state.engagedEnemyCounts.Remove(enemy);
		}
		else
		{
			state.engagedEnemyCounts[enemy] = count;
		}
	}

	public bool DealAgentEngageDamage(Tower tower, Enemy enemy, float damageMultiplier = 1f)
	{
		if (tower == null || enemy == null || enemy.health == null) return false;

		var data = new Tower.CustomDamageData
		{
			enemyHit = enemy,
			baseDamageRatio = Mathf.Max(0f, damageMultiplier),
		};

		float damage = tower.GetDamage(data, rollCrit: true);
		if (damage <= 0f) return false;

		enemy.health.TakeDamage(damage, tower, tower.GetDamageType(data), data);
		tower.ApplyEffects(enemy, null);
		return true;
	}

	public bool ExplodeOnDeathActive(Tower tower)
	{
		if (tower is AgentTower agentTower)
		{
			return agentTower.ExplodeOnDeathActive();
		}

		return false;
	}

	public void UpdateTowerAgents(Tower tower, CM.ColorType colorType, Agent.CombatMode combatMode)
	{
		if (tower == null) return;

		TowerAgentState state = GetOrCreateState(tower);
		state.agents.RemoveAll(a => a == null);

		for (int i = 0; i < state.agents.Count; i++)
		{
			Agent agent = state.agents[i];
			if (agent == null) continue;
			agent.SetCombatMode(combatMode);
			agent.SetColor(colorType);
		}
	}

	public void SetTowerRestingPosition(Tower tower, Vector3 restingPosition)
	{
		if (tower == null) return;

		TowerAgentState state = GetOrCreateState(tower);
		restingPosition.z = 0f;
		state.restingPosition = restingPosition;
	}

	public Vector3 FindNearestFreeCellWorld(Vector3 origin)
	{
		GridManager grid = GridManager.instance;
		if (grid == null)
		{
			origin.z = 0f;
			return origin;
		}

		Vector3 bestPos = origin;
		bestPos.z = 0f;
		float bestDistSqr = float.PositiveInfinity;
		bool found = false;

		for (int y = 0; y < grid.CellsY; y++)
		{
			for (int x = 0; x < grid.CellsX; x++)
			{
				if (!grid.TryGetCell(x, y, out var cell)) continue;
				if (cell.IsBlocked) continue;

				Vector2Int idx = new Vector2Int(x, y);
				if (!grid.IsInMazeBounds(idx)) continue;
				if (grid.TryGetTowerAtCell(idx, out var occupyingTower) && occupyingTower != null) continue;

				float distSqr = (cell.WorldCenter - origin).sqrMagnitude;
				if (!found || distSqr < bestDistSqr)
				{
					found = true;
					bestDistSqr = distSqr;
					bestPos = cell.WorldCenter;
				}
			}
		}

		bestPos.z = 0f;
		return bestPos;
	}

	private TowerAgentState GetOrCreateState(Tower tower)
	{
		if (!_stateByTower.TryGetValue(tower, out TowerAgentState state) || state == null)
		{
			state = new TowerAgentState();
			_stateByTower[tower] = state;
		}

		return state;
	}

	private static void CleanupEngagedEnemyCounts(TowerAgentState state)
	{
		var toRemove = new List<Enemy>();
		foreach (var kvp in state.engagedEnemyCounts)
		{
			Enemy enemy = kvp.Key;
			int count = kvp.Value;
			if (enemy == null || !enemy.gameObject.activeInHierarchy || count <= 0)
			{
				toRemove.Add(enemy);
			}
		}

		for (int i = 0; i < toRemove.Count; i++)
		{
			state.engagedEnemyCounts.Remove(toRemove[i]);
		}
	}

	private static void DestroyStateAgents(TowerAgentState state)
	{
		if (state == null) return;

		for (int i = state.agents.Count - 1; i >= 0; i--)
		{
			Agent agent = state.agents[i];
			if (agent == null) continue;
			agent.DespawnWithSwell();
		}

		state.agents.Clear();
		state.engagedEnemyCounts.Clear();
	}
}
