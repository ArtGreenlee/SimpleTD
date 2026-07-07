using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveDataSO", menuName = "Scriptable Objects/WaveDataSO")]
public class WaveDataSO : ScriptableObject
{
	[Serializable]
	public struct WaveData
	{
		public List<WaveManager.EnemyData> enemyDatas;
		public int totalWaveValue;
	}

	[Tooltip("Ordered list of waves.")]
	public List<WaveData> waves = new List<WaveData>();
}
