using UnityEngine;

public class XP : MonoBehaviour
{
    private SpriteRenderer sr;
    public float speed = 10f;
    [SerializeField] private float collectDistance = 0.25f;

    private Tower tower;
    [HideInInspector] private Rigidbody2D rb;
    private XPObjectPool pool;
    private float forceDelayTimer;
    public static float forceDelay = 1;

    private float CollectDistanceSqr => collectDistance * collectDistance;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (sr != null)
        {
            sr.color = CM.i.ColorTypeToColor(CM.ColorType.Cyan);
        }
    }

    private void OnEnable()
    {
        if (rb != null)
        {
            forceDelayTimer = Time.fixedTime + forceDelayTimer;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(Random.insideUnitCircle * 1, ForceMode2D.Impulse);
            rb.AddTorque(Random.value * 50);
        }
    }

    public void SetPool(XPObjectPool objectPool)
    {
        pool = objectPool;
    }

    public void SetTargetTower(Tower targetTower)
    {
        tower = targetTower;
    }

    private void Collect()
    {
        if (tower != null)
        {
            tower.AddXP(1);
        }

        if (pool != null)
        {
            pool.Release(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (tower == null) return;

        var hitTower = collision.GetComponent<Tower>();
        if (hitTower == null) return;
        if (hitTower != tower) return;

        Collect();
    }

    private void FixedUpdate()
    {
        if (tower == null)
        {
            if (pool != null) pool.Release(gameObject);
            else gameObject.SetActive(false);
            return;
        }
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 towerPos = tower.transform.position;
        Vector2 toTower = towerPos - pos;

        if (toTower.sqrMagnitude <= CollectDistanceSqr)
        {
            Collect();
            return;
        }

        Vector2 dir = toTower.sqrMagnitude > 0.000001f ? toTower.normalized : Vector2.zero;
        if (rb != null)
        {
            rb.AddForce(dir * speed, ForceMode2D.Force);
        }

    }
}
