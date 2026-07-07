using System.Collections.Generic;
using UnityEngine;

public class Field : MonoBehaviour
{
    private FieldTower fieldTower;
    [HideInInspector] public HashSet<Enemy> intersectedEnemies = new HashSet<Enemy>();

    public void SetFieldTower(FieldTower fieldTower)
    {
        this.fieldTower = fieldTower;
    }

    public FieldTower GetFieldTower()
    {
        return fieldTower;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (fieldTower == null)
        {
            Debug.LogError("field tower not set for field");
            return;
        }
        Projectile p = collision.GetComponent<Projectile>();
        if (p != null)
        {
            fieldTower.ApplyFieldToProjectile(p);
            return;
        }
        Enemy e = collision.GetComponent<Enemy>();
        if (e != null && !intersectedEnemies.Contains(e))
        {
            intersectedEnemies.Add(e);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Enemy e = collision.GetComponent<Enemy>();
        if (e != null && intersectedEnemies.Contains(e))
        {
            intersectedEnemies.Remove(e);
        }
    }
}
