using UnityEngine;

[RequireComponent(typeof(Juice))]
public class Rod : MonoBehaviour
{
    private Lens lens;
    private Juice juice;

    void Awake()
    {
        lens = GetComponentInParent<Lens>();
        juice = GetComponent<Juice>();
    }

    public void Bounce(Vector2 fromPosition)
    {
        if (juice == null)
        {
            juice = GetComponent<Juice>();
            if (juice == null) return;
        }

        Vector2 dir = ((Vector2)transform.position - fromPosition);
        if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

        juice.AddForce(dir.normalized, 0.75f);
        juice.AddBounce(4f);
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        lens.OnRodTriggerEnter(collision);
    }
}
