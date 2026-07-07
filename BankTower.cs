using UnityEngine;

public class BankTower : Tower
{
    public int enemyValueIncrease = 1;
    private int bonusCurrency = 0;

    public override void OnEnemyKilledInRange(Enemy enemy)
    {
        LaserObjectPool.instance.ShowLaser(transform.position, enemy.transform.position, GetColor());
        if (CurrencyManager.instance != null)
        {
            CurrencyManager.instance.SpawnCurrencyForAmount(enemyValueIncrease, enemy.transform.position);
        }

        bonusCurrency += Mathf.Max(0, enemyValueIncrease);
    }

    public int BonusCurrencyGenerated()
    {
        return bonusCurrency;
    }

    protected override void OnChargeApplied(float previousCharge, float currentCharge, bool wasCharged, bool isChargedNow)
    {
        base.OnChargeApplied(previousCharge, currentCharge, wasCharged, isChargedNow);

        if (!UpgradeActive(UpgradeData.UID.SpawnCurrencyOnCharge)) return;
        if (!isChargedNow || wasCharged) return;
        if (CurrencyManager.instance == null) return;

        float chance = Mathf.Clamp01(UpgradeData.SpawnCurrencyOnChargeChancePercent / 100f);
        if (Random.value <= chance)
        {
            CurrencyManager.instance.GetPooledCurrency(transform.position, Quaternion.identity);
        }
    }
}
