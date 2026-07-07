using System.Collections;
using UnityEngine;

public class TeleportEnemyEffect : EnemyEffect
{
    public float teleportRadius;
    [SerializeField, Min(1)] private int teleportPositionAttempts = 8;

    private Pathfinding _pathfinding;

    public override void Trigger()
    {
        if (enemy == null || GridManager.instance == null)
        {
            base.Trigger();
            return;
        }

        if (!TryFindValidTeleportPoint(enemy, out Vector3 point))
        {
            base.Trigger();
            return;
        }

        StartCoroutine(TeleportRoutine(enemy, point, 0.1f));
        base.Trigger();
    }

    private bool TryFindValidTeleportPoint(Enemy targetEnemy, out Vector3 validPoint)
    {
        validPoint = targetEnemy.transform.position;

        int attempts = Mathf.Max(1, teleportPositionAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = GridManager.instance.GetRandomPointInsideMazeWithinRadius(
                targetEnemy.transform.position,
                teleportRadius);

            if (!HasPathToGoalFrom(candidate, targetEnemy))
            {
                continue;
            }

            validPoint = candidate;
            return true;
        }

        return false;
    }

    private bool HasPathToGoalFrom(Vector3 worldPosition, Enemy targetEnemy)
    {
        if (targetEnemy == null) return false;

        var movement = targetEnemy.GetMovement();
        var goal = movement != null ? movement.Goal : null;
        if (goal == null)
        {
            return true;
        }

        if (_pathfinding == null)
        {
            _pathfinding = Pathfinding.instance != null ? Pathfinding.instance : FindFirstObjectByType<Pathfinding>();
        }

        if (_pathfinding == null)
        {
            return true;
        }

        float distance = _pathfinding.GetPathDistanceToGoal(worldPosition, goal);
        return !float.IsPositiveInfinity(distance);
    }

    private IEnumerator TeleportRoutine(Enemy enemy, Vector3 targetPosition, float duration)
    {
        if (enemy == null) yield break;
        if (OnHitParticleEffect.instance != null)
        {
            OnHitParticleEffect.instance.OnHitVfx(targetPosition, Color.white);
        }

        yield return new WaitForSeconds(duration);

        if (LaserObjectPool.instance != null)
        {
            LaserObjectPool.instance.ShowLaser(enemy.transform.position, targetPosition, Color.white, duration);
        }

        enemy.transform.position = targetPosition;

        if (OnHitParticleEffect.instance != null)
        {
            OnHitParticleEffect.instance.OnHitVfx(targetPosition, Color.white);
        }
    }
}