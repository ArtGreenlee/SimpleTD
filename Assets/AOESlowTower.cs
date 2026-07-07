using System.Collections.Generic;
using UnityEngine;

public class AOESlowTower : AOETower
{
    //[Header("Support (Upgrade1)")]
    //[Tooltip("If enabled (upgrade level >=1), this tower will charge other towers within its range.")]
    //private bool chargeAuraEnabled;

    //private float _nextChargeTime;
    private RangeManager _rm;

    public override void Awake()
    {
        base.Awake();
        _rm = GetComponentInChildren<RangeManager>();
    }

    public override void Attack()
    {
        base.Attack();
        //if (!chargeAuraEnabled) return;
        //if (upgradeLevel < 1) return;
        //if (Time.time < _nextChargeTime) return;

        //if (_rm == null) _rm = GetComponentInChildren<RangeManager>();
        //if (_rm == null) return;

        //List<Tower> towers = _rm.GetAllActiveTowersInRange();
        ////Debug.Log("towers in range: " + towers.Count);
        //for (int i = 0; i < towers.Count; i++)
        //{
        //    var t = towers[i];
        //    if (t == null) continue;
        //    if (t == this) continue;

        //    t.Charge();
        //    // Visual feedback using the shared indicator helper.
        //    t.Indicate(GetColor());
        //}
        
    }

    //public override void Update()
    //{
    //    base.Update();

    //    if (!chargeAuraEnabled) return;
    //    if (upgradeLevel < 1) return;
    //    if (Time.time < _nextChargeTime) return;

    //    if (_rm == null) _rm = GetComponentInChildren<RangeManager>();
    //    if (_rm == null) return;

    //    List<Tower> towers = _rm.GetAllActiveTowersInRange();
    //    if (towers == null || towers.Count == 0)
    //    {
    //        _nextChargeTime = Time.time + chargeInterval;
    //        return;
    //    }

    //    for (int i = 0; i < towers.Count; i++)
    //    {
    //        var t = towers[i];
    //        if (t == null) continue;
    //        if (!canChargeSelf && t == this) continue;
    //        t.Charge();
    //    }

    //    _nextChargeTime = Time.time + chargeInterval;
    //}
}
