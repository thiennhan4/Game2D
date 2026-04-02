using UnityEngine;
public class BatHealth : MonoBehaviour
{
    
    public float hitPoints = 0f;
    public float maxHitPoints = 5f;
    public HealthBar healthBar;
    public Animator aim;

    void Start()
    {
        hitPoints = maxHitPoints;
        healthBar.SetHealth(hitPoints, maxHitPoints);
    }
    public void TakeDamage(float damage)
    {
        hitPoints -= damage;
        healthBar.SetHealth(hitPoints, maxHitPoints);
        if (hitPoints <= 0)
        {
            aim.SetTrigger("Die");
             Destroy(gameObject, 0.5f);
        }
    }
}
