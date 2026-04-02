using UnityEngine;

/// <summary>
/// Handles Bringer enemy attack: cooldown, trigger animation, deal damage via AnimEvent.
/// Attach to: Bringer enemy root GameObject (same object as BringerController + EnemyStats).
/// Required child objects:
///   - AttackPoint: empty child Transform in front of the enemy for hit detection
/// Inspector setup:
///   - playerLayer: set to "Player" layer
///   - attackPoint: empty child at melee strike position
///   - anim: auto-fetched if not assigned
/// Animator Parameters: Attack (Trigger).
/// Animation Events: AnimEvent_DoHit (hit frame), AnimEvent_EndAttack (last frame).
/// </summary>
public class BringerAttack : MonoBehaviour
{
    [Header("Attack Stats")]
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private LayerMask playerLayer;

    [Header("References")]
    [SerializeField] private Animator anim;

    private float nextAttackTime;
    private EnemyStats enemyStats;
    private Transform playerTarget;

    private void Awake()
    {
        enemyStats = GetComponent<EnemyStats>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    private void Update()
    {
        // Don't attack when dead
        if (enemyStats != null && enemyStats.IsDead) return;
        if (playerTarget == null) return;

        float dist = Vector2.Distance(transform.position, playerTarget.position);

        // Auto-attack when player is in range and cooldown is ready
        if (dist <= attackRange && Time.time >= nextAttackTime)
        {
            StartAttack();
        }
    }

    private void StartAttack()
    {
        nextAttackTime = Time.time + attackCooldown;

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Attack");
        }
    }

    /// <summary>
    /// Animation Event: call at the HIT frame of Attack animation.
    /// Detects player in attack radius and applies damage + knockback.
    /// </summary>
    public void AnimEvent_DoHit()
    {
        if (attackPoint == null) return;
        if (enemyStats != null && enemyStats.IsDead) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position, attackRadius, playerLayer
        );

        foreach (Collider2D col in hits)
        {
            PlayerHealth playerHP = col.GetComponent<PlayerHealth>();
            if (playerHP != null && !playerHP.IsDead)
            {
                // Knockback direction: enemy → player
                Vector2 knockDir = (col.transform.position - transform.position).normalized;
                playerHP.TakeDamage(attackDamage, knockDir);
            }
        }
    }

    /// <summary>
    /// Animation Event: call at the LAST frame of Attack animation.
    /// Resets the attack trigger to prevent animation queue issues.
    /// </summary>
    public void AnimEvent_EndAttack()
    {
        if (anim != null)
        {
            anim.ResetTrigger("Attack");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Attack hit radius (red)
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }

        // Attack detection range (magenta)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
