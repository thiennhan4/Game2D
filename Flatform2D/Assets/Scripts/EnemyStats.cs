using System.Collections;
using UnityEngine;

public class EnemyStats : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float armor = 0f;

    [Header("Hit")]
    [SerializeField] private float invincibleTime = 0.3f;

    [Header("Death")]
    [SerializeField] private float deathDelay = 1.5f;

    [Header("UI")]
    [SerializeField] private StateManager healthBarUI;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D enemyCollider;

    [Header("Animation Parameters")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string deadBool = "IsDead";

    private float currentHP;
    private bool isDead;
    private bool isInvincible;

    // Public read-only properties for other scripts (BringerController, PlayerAttack)
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public float Armor => armor;
    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (enemyCollider == null) enemyCollider = GetComponent<Collider2D>();

        currentHP = maxHP;
        UpdateHealthUI();
    }

    /// <summary>
    /// IDamageable implementation. Apply damage, trigger Hurt anim, start iframes.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead || isInvincible || damage <= 0f) return;

        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        UpdateHealthUI();

        if (currentHP <= 0f)
        {
            Die();
            return;
        }

        // Play hurt animation
        if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
            animator.SetTrigger(hurtTrigger);

        StartCoroutine(InvincibleRoutine());
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        UpdateHealthUI();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHP = 0f;
        UpdateHealthUI();

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(dieTrigger))
                animator.SetTrigger(dieTrigger);

            if (!string.IsNullOrEmpty(deadBool))
                animator.SetBool(deadBool, true);
        }

        // Disable collider so no more interactions
        if (enemyCollider != null)
            enemyCollider.enabled = false;

        // Wait for death animation then destroy
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathDelay);
        Destroy(gameObject);
    }

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        isInvincible = false;
    }

    private void UpdateHealthUI()
    {
        if (healthBarUI != null)
            healthBarUI.UpdateHealthUI(currentHP, maxHP);
    }
}