using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserExplosionOnHitEffect : ChainEffect
{
	[Min(1)] public int radialLaserCount = 8;
	[Min(0f)] public float laserWidth = 0.12f;
	[Min(0f)] public float segmentMaxDistance = 12f;
	[Min(0f)] public float baseDamageRatio = 0.5f;
	[Min(0f)] public float explosionDelaySeconds = 0f;
	[Min(0.01f)] public float timerIndicatorSize = 1.25f;

	public int GetCurrentRadialLaserCount()
	{
		return Mathf.Max(1, radialLaserCount);
	}

	public override void ApplyEffect(Enemy enemy, Projectile projectile = null)
	{
		if (tower == null || enemy == null || LaserHelper.instance == null)
		{
			base.ApplyEffect(enemy, projectile);
			return;
		}

		if (!ShouldApplyEffect())
		{
			base.ApplyEffect(enemy, projectile);
			return;
		}

		int count = Mathf.Max(1, radialLaserCount);
		Vector3 origin = enemy.transform.position;

		float delay = Mathf.Max(0f, explosionDelaySeconds);
		if (delay > 0f)
		{
			if (AOEObjectPool.instance != null)
			{
				AOEObjectPool.instance.PlayTimerIndicator(origin, timerIndicatorSize, tower.GetColor(), delay);
			}

			StartCoroutine(DelayedExplosion(origin, count, delay));
		}
		else
		{
			FireRadialLasers(origin, count);
		}

		base.ApplyEffect(enemy, projectile);
	}

	private IEnumerator DelayedExplosion(Vector3 origin, int count, float delay)
	{
		yield return new WaitForSeconds(delay);

		if (this == null || tower == null || LaserHelper.instance == null)
		{
			yield break;
		}

		FireRadialLasers(origin, count);
	}

	private void FireRadialLasers(Vector3 origin, int count)
	{
		float randomAngleOffset = Random.Range(0f, 360f);
		List<Effect> nonChainEffects = GetNonChainEffectsFromTower();

		for (int i = 0; i < count; i++)
		{
			float angle = randomAngleOffset + (360f * i) / count;
			Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;

			var data = new Tower.CustomDamageData
			{
				baseDamageRatio = Mathf.Max(0f, baseDamageRatio)
			};

			LaserHelper.instance.LaserAttackHelper(
				tower,
				origin,
				direction,
				laserWidth,
				tower.GetColor(data),
				bounceCountOverride: tower.GetBaseBounceCount(),
				applyEffects: nonChainEffects != null && nonChainEffects.Count > 0,
				effectOverrideList: nonChainEffects,
				segmentMaxDistance: Mathf.Max(0f, segmentMaxDistance));
		}
	}
}
