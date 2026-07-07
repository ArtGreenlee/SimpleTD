using System.Collections;
using UnityEngine;
public class test : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public float randomOffset;
    public float randomLength;
    private void Awake()
    {
        randomLength = Random.Range(2f, 6f);
        randomOffset = Random.Range(0f, 100f);
    }
    private void Update()
    {
        transform.position = new Vector3(Mathf.Sin(Time.time + randomOffset), Mathf.Cos(Time.time + randomOffset), 0) * randomLength;
    }
    public void OnParticleCollision(GameObject other)
    {
        Debug.Log("Particle collision detected with " + other.name);
        OnHit();
    }

    public void OnHit()
    {
        StartCoroutine(flashRoutine());
    }

    private IEnumerator flashRoutine()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(.1f);
        spriteRenderer.color = Color.black;
    }
}
    