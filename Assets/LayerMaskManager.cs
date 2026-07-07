using UnityEngine;

public class LayerMaskManager : MonoBehaviour
{
    public static LayerMaskManager instance { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Avoid nuking the shared managers GameObject.
            Destroy(this);
            return;
        }

        instance = this;
    }

    [Header("Masks")]
    public LayerMask enemyLayerMask;
    public LayerMask laserLayerMask;
    public LayerMask PICLayermask;
    public LayerMask wallLayerMask;
    public LayerMask fieldLayerMask;
    public LayerMask currencyLayerMask;
    public LayerMask agentLayerMask;
}
