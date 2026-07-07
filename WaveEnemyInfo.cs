using TMPro;
using UnityEngine;

public class WaveEnemyInfo : MonoBehaviour
{
    public Transform enemyPreviewPosition;
    public TextMeshProUGUI text;


    public void DisplayEnemyData(WaveManager.EnemyData enemyData)
    {
        if (enemyPreviewPosition != null)
        {
            for (int i = enemyPreviewPosition.childCount - 1; i >= 0; i--)
            {
                Destroy(enemyPreviewPosition.GetChild(i).gameObject);
            }
        }

        if (EnemyManager.instance == null || EnemyManager.instance.enemyInfoDict == null)
        {
            if (text != null) text.text = enemyData.count + "X";
            return;
        }

        if (!EnemyManager.instance.enemyInfoDict.TryGetValue(enemyData.enemyTag, out var enemyInfo) || enemyInfo.prefab == null)
        {
            if (text != null) text.text = enemyData.count + "X";
            return;
        }

        GameObject enemyObject = Instantiate(enemyInfo.prefab, enemyPreviewPosition.position, Quaternion.identity, enemyPreviewPosition);
        if (enemyObject != null)
        {
            enemyObject.transform.localPosition = Vector3.zero;
            enemyObject.transform.localRotation = Quaternion.identity;
            enemyObject.transform.localScale = Vector3.one;
        }

        DisablePreviewBehaviors(enemyObject);

        Enemy enemy = enemyObject != null ? enemyObject.GetComponent<Enemy>() : null;
        if (enemy != null) enemy.enabled = false;

        string enemyName = enemy != null ? "test" : (enemyObject != null ? enemyObject.name : string.Empty);
        if (text != null) text.text = enemyData.count + " X " + enemyName;
        Destroy(enemyObject);
    }

    private static void DisablePreviewBehaviors(GameObject root)
    {
        if (root == null) return;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var go = r.gameObject;

            var col3D = go.GetComponents<Collider>();
            for (int c = 0; c < col3D.Length; c++)
            {
                if (col3D[c] != null) col3D[c].enabled = false;
            }

            var col2D = go.GetComponents<Collider2D>();
            for (int c = 0; c < col2D.Length; c++)
            {
                if (col2D[c] != null) col2D[c].enabled = false;
            }

            var scripts = go.GetComponents<MonoBehaviour>();
            for (int s = 0; s < scripts.Length; s++)
            {
                // Leave this component running; we only want to disable gameplay behaviors.
                if (scripts[s] != null) scripts[s].enabled = false;
            }
        }
    }
}
