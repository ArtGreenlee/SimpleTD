using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SaveData", menuName = "Scriptable Objects/SaveData")]
public class SaveData : ScriptableObject
{
	[Serializable]
	public struct RelicLockState
	{
		public RM.ID id;
		public bool unlocked;
	}

	[Serializable]
	public struct TowerSeenState
	{
		public Tower.ID id;
		public bool purchased;
	}

	[Header("Relics")]
	[Tooltip("Serialized unlock state for each relic id.")]
	public List<RelicLockState> relicLockStates = new List<RelicLockState>();
	[Tooltip("Serialized purchased-from-shop state for each tower id.")]
	public List<TowerSeenState> towerSeenStates = new List<TowerSeenState>();

	public float damageDealtToEnemies;
	public float blueDamageDealt;
	public float redDamageDealt;
	public float greenDamageDealt;
	public float lightningDamageDealt;
	public float fireDamageDealt;
   public float poisonDamageDealt;
	public float critDamageDealt;
	public float critProcCount;
	public int currencyCollected;
	public int maxCurrencyCollected;
	public int maxPlacedTowers;
	public int purchasedTowerCount;
	public int enemiesDefeated;
	public int unlockedRelicCount;
	public List<Tower.ID> seenTowerIDs; //only for placed towers
	public int maxRedTagTowersPlaced;
	public int maxBlueTagTowersPlaced;
	public int maxGreenTagTowersPlaced;
	public int maxPurpleTagTowersPlaced;
	public int maxYellowTagTowersPlaced;

	[Header("Crits by Damage Type")]
	public int redCritsLanded;
	public int blueCritsLanded;
	public int greenCritsLanded;
	public int yellowCritsLanded;
	public int purpleCritsLanded;
	public int orangeCritsLanded;
	public int whiteCritsLanded;
	public int cyanCritsLanded;

	[Header("Unlock Progress")]
	public float maxSingleHitDamage;
	public float exposedDamageDealt;
	public int maxEnemiesHitByLaser;
	public int maxLaserBounces;
	public int maxTowersPlacedAtOnce;
	public int maxAgentsOnField;
	public int totalRerollCount;

    public void SetRelicLockStates(Dictionary<RM.ID, bool> source)
	{
		if (relicLockStates == null) relicLockStates = new List<RelicLockState>();
		relicLockStates.Clear();

		if (source == null) return;
		foreach (var pair in source)
		{
			relicLockStates.Add(new RelicLockState
			{
				id = pair.Key,
				unlocked = pair.Value
			});
		}
	}

	public void FillRelicLockStates(Dictionary<RM.ID, bool> destination)
	{
		if (destination == null) return;

		var ids = (RM.ID[])Enum.GetValues(typeof(RM.ID));
		for (int i = 0; i < ids.Length; i++)
		{
			destination[ids[i]] = false;
		}

		if (relicLockStates == null) return;
		for (int i = 0; i < relicLockStates.Count; i++)
		{
			var entry = relicLockStates[i];
			destination[entry.id] = entry.unlocked;
		}
	}

	public void SetTowerSeenStates(Dictionary<Tower.ID, bool> source)
	{
		if (towerSeenStates == null) towerSeenStates = new List<TowerSeenState>();
		towerSeenStates.Clear();

		if (source == null) return;
		foreach (var pair in source)
		{
			towerSeenStates.Add(new TowerSeenState
			{
				id = pair.Key,
				purchased = pair.Value
			});
		}
	}

	public void FillTowerSeenStates(Dictionary<Tower.ID, bool> destination, bool defaultPurchased)
	{
		if (destination == null) return;

		var ids = (Tower.ID[])Enum.GetValues(typeof(Tower.ID));
		for (int i = 0; i < ids.Length; i++)
		{
			destination[ids[i]] = defaultPurchased;
		}

		if (towerSeenStates == null) return;
		for (int i = 0; i < towerSeenStates.Count; i++)
		{
			var entry = towerSeenStates[i];
			destination[entry.id] = entry.purchased;
		}
	}
}
