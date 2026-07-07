using UnityEngine;
using UID = UpgradeData.UID;

public class UpgradeItem : MonoBehaviour
{
    public UID uid;

    public void OnClick()
    {
        var src = GetComponent<SRC>();
        if (src != null)
        {
            src.Indicate();
        }
    }

    public void Indicate(Color color)
    {
        var pool = AOEObjectPool.instance;
        if (pool == null) return;

        const float diameter = 0.4f;
        pool.Indicate(transform.position, diameter, color);
    }
}
