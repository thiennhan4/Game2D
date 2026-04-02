using UnityEngine;

public class Trap : MonoBehaviour
{
    public float damage = 10f;
    public float knockbackForce = 5f;
    public float stunDuration = 1f;
    public float resetTime = 2f;
    public Collider2D trapCollider;

    void Awake()
    {
        trapCollider = GetComponent<Collider2D>();
    }
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerHealth health = collision.GetComponent<PlayerHealth>();
            
            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }
    }
}
