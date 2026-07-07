using System.Collections.Generic;
using UnityEngine;

public class Relic : Interactable
{
    public GameObject activeVisualization;
    public RM.ID id;
    private Juice juice;
    [HideInInspector] public int rarity = -1;

    public override void Awake()
    {
        juice = GetComponent<Juice>();
        base.Awake();
    }

    public void Flash()
    {
        var src = GetComponent<SRC>();
        if (src == null || src.srColorInfos == null) return;

        const string hitEffectMaterialString = "_HitEffectBlend";
        foreach (var srColorInfo in src.srColorInfos)
        {
            if (srColorInfo.sr == null) continue;
            foreach (var sr in srColorInfo.sr)
            {
                if (sr == null || sr.material == null) continue;
                sr.material.SetFloat(hitEffectMaterialString, 1f);
            }
        }
    }

    void Start()
    {
        InvokeRepeating(nameof(CheckVisualizer), 0, .3f);
    }

    public void CheckVisualizer()
    {
        if (RM.i == null) return;
        if (activeVisualization == null) return;

        if (!RM.i.Active(id))
        {
            activeVisualization.SetActive(false);
            return;
        }

        bool indicatorActive = true;

        if (id == RM.ID.loneWolfArtifact)
        {
            indicatorActive = TowerManager.instance != null && TowerManager.instance.GetCurrentPlacedTowers() == 1;
        }
        else if (id == RM.ID.InventoryBuff)
        {
            indicatorActive = TowerManager.instance != null && TowerManager.instance.GetInventoryTowerCount() == 0;
        }

        activeVisualization.SetActive(indicatorActive);
    }

    public void Indicate()
    {
        if (RM.i == null || !RM.i.Active(id)) return;
        if (juice == null) return;
        juice.AddBounce(10);
    }

    public override string GetCursorToolTipText()
    {
        if (RM.i == null) return id.ToString();
        return RM.i.GetName(id);
    }
}
