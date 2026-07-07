using System.Collections.Generic;
using UnityEngine;

public class Lens : MonoBehaviour
{
    private LensTower tower;

    private readonly HashSet<Enemy> enemiesInRangeSet = new HashSet<Enemy>();
    private readonly List<Enemy> enemiesInRange = new List<Enemy>();
    private readonly HashSet<Agent> agentsInRangeSet = new HashSet<Agent>();
    private readonly List<Agent> agentsInRange = new List<Agent>();
    private readonly HashSet<Tower> placedTowersInRangeSet = new HashSet<Tower>();
    private readonly List<Tower> placedTowersInRange = new List<Tower>();

    public IReadOnlyList<Enemy> EnemiesInRange => enemiesInRange;
    public IReadOnlyList<Agent> AgentsInRange => agentsInRange;
    public IReadOnlyList<Tower> PlacedTowersInRange => placedTowersInRange;

    private void Awake()
    {
        tower = GetComponentInParent<LensTower>();
    }

    private void Update()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<LensTower>();
            if (tower == null) return;
        }

        transform.position = tower.GetTargetPosition();
        PrunePlacedTowersInRange();
    }

    private void OnDisable()
    {
        enemiesInRangeSet.Clear();
        enemiesInRange.Clear();
        agentsInRangeSet.Clear();
        agentsInRange.Clear();
        placedTowersInRangeSet.Clear();
        placedTowersInRange.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null)
        {
            enemy = other.GetComponent<Enemy>();
        }

        if (enemy != null && enemiesInRangeSet.Add(enemy))
        {
            enemiesInRange.Add(enemy);
        }

        Agent agent = other.GetComponentInParent<Agent>();
        if (agent == null)
        {
            agent = other.GetComponent<Agent>();
        }

        if (agent != null && agentsInRangeSet.Add(agent))
        {
            agentsInRange.Add(agent);
        }

        Tower placedTower = GetPlacedTowerFromCollider(other);
        if (placedTower != null && placedTowersInRangeSet.Add(placedTower))
        {
            placedTowersInRange.Add(placedTower);
        }
    }

    public void OnRodTriggerEnter(Collider2D col)
    {
        if (tower == null)
        {
            tower = GetComponentInParent<LensTower>();
            if (tower == null) return;
        }

        Projectile projectile = col.GetComponentInParent<Projectile>();
        if (projectile == null)
        {
            projectile = col.GetComponent<Projectile>();
        }

        if (projectile == null) return;
        tower.OnProjectileTriggerEnterRod(projectile);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null)
        {
            enemy = other.GetComponent<Enemy>();
        }

        if (enemy != null && enemiesInRangeSet.Remove(enemy))
        {
            enemiesInRange.Remove(enemy);
        }

        Agent agent = other.GetComponentInParent<Agent>();
        if (agent == null)
        {
            agent = other.GetComponent<Agent>();
        }

        if (agent != null && agentsInRangeSet.Remove(agent))
        {
            agentsInRange.Remove(agent);
        }

        Tower towerFromCollider = GetTowerFromCollider(other);
        if (towerFromCollider != null && placedTowersInRangeSet.Remove(towerFromCollider))
        {
            placedTowersInRange.Remove(towerFromCollider);
        }
    }

    public bool HasEnemyInRange(Enemy enemy)
    {
        return enemy != null && enemiesInRangeSet.Contains(enemy);
    }

    public bool HasAgentInRange(Agent agent)
    {
        return agent != null && agentsInRangeSet.Contains(agent);
    }

    public bool HasPlacedTowerInRange(Tower placedTower)
    {
        return placedTower != null && placedTowersInRangeSet.Contains(placedTower);
    }

    private Tower GetPlacedTowerFromCollider(Collider2D other)
    {
        Tower t = GetTowerFromCollider(other);
        if (t == null) return null;
        if (t == tower) return null;
        if (t.CurrentState != Tower.State.Placed) return null;
        return t;
    }

    private static Tower GetTowerFromCollider(Collider2D other)
    {
        if (other == null) return null;

        Tower t = other.GetComponentInParent<Tower>();
        if (t == null)
        {
            t = other.GetComponent<Tower>();
        }

        return t;
    }

    private void PrunePlacedTowersInRange()
    {
        for (int i = placedTowersInRange.Count - 1; i >= 0; i--)
        {
            Tower t = placedTowersInRange[i];
            if (t == null || t == tower || t.CurrentState != Tower.State.Placed)
            {
                placedTowersInRange.RemoveAt(i);
                if (t != null)
                {
                    placedTowersInRangeSet.Remove(t);
                }
            }
        }
    }
}
