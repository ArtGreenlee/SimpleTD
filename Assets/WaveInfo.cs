using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
public class WaveInfo : MonoBehaviour
{
    [SerializeField]
    public List<WaveEnemyInfo> waveEnemyInfos;
    public GameObject waveEnemyInfoPrefab;
    public void DisplayWaveInfo(WaveManager.WaveData waveData)
    {
        for (int i = 0; i < waveEnemyInfos.Count; i++)
        {
            if (i < waveData.enemyDatas.Count)
            {
                waveEnemyInfos[i].DisplayEnemyData(waveData.enemyDatas[i]);
            }
            else
            {
                waveEnemyInfos[i].gameObject.SetActive(false);
            }
        }
    }
}
