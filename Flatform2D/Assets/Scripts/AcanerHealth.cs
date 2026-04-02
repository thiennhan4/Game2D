using System.Collections;
using UnityEngine;

public class AcanerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit")]
    [SerializeField] private float invincibleTime = 0.3f;

    [Header("Death")]
    [SerializeField] private float deathAnimDuration = 1.5f;

    [Header("Damage Popup")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1.5f, 0f);

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D enemyCollider;
    [SerializeField] private Rigidbody2D rb;

    [Header("Animation Parameters")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string deadBool = "IsDead";
    [SerializeField] private string runBool = "isRunning";
    [SerializeField] private string deathStateName = "Death";

    private AcanerController acanerController;
    private AcanerAttack acanerAttack;
    private float currentHealth;
    private bool isDead;
    private bool isInvincible;
    private Coroutine invincibleCoroutine;

    // ── Public Properties ──
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // ──────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        enemyCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (enemyCollider == null) enemyCollider = GetComponent<Collider2D>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        acanerController = GetComponent<AcanerController>();
        acanerAttack = GetComponent<AcanerAttack>();

        if (animator == null)
            Debug.LogError($"[AcanerHealth] {name}: Animator not found!");
        if (enemyCollider == null)
            Debug.LogError($"[AcanerHealth] {name}: Collider2D not found!");

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;

        Debug.Log($"[AcanerHealth] {name} initialized: {currentHealth}/{maxHealth} HP");
    }

    // ──────────────────────────────────────────────
    // IDamageable Implementation
    // ──────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        // Đã chết → bỏ qua
        if (isDead) return;

        // Damage không hợp lệ
        if (damage <= 0f) return;

        // Đang invincible → bỏ qua
        if (isInvincible) return;

        float oldHealth = currentHealth;

        // Trừ máu
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[AcanerHealth] {name} took {damage:F0} damage ({oldHealth:F0} → {currentHealth:F0})");

        // Hiện damage popup
        SpawnDamagePopup(damage);

        // Chết
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Bị thương → chơi Hurt animation + bật invincible
        PlayHurtAnimation();
        StartInvincible();
    }

    // ──────────────────────────────────────────────
    // Heal
    // ──────────────────────────────────────────────

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
    }

    // ──────────────────────────────────────────────
    // Die
    // ──────────────────────────────────────────────

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0f;

        Debug.Log($"[AcanerHealth] {name} died!");

        // Dừng invincible
        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }
        isInvincible = false;

        // Tắt controller + attack
        if (acanerController != null) acanerController.enabled = false;
        if (acanerAttack != null) acanerAttack.CancelAttack();

        // Vô hiệu hóa vật lý để xác chết không rơi xuyên bản đồ khi tắt Collider
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false; // Tắt hoàn toàn trọng lực và tác động vật lý
        }

        // Tắt collider để player không hit nữa
        if (enemyCollider != null) enemyCollider.enabled = false;

        // Chơi Death animation
        if (animator != null)
        {
            animator.ResetTrigger(hurtTrigger);
            animator.ResetTrigger(deathTrigger);
            animator.SetBool(runBool, false);
            animator.SetBool(deadBool, true);
            animator.SetTrigger(deathTrigger);

            // Force play Death state ngay lập tức
            if (!string.IsNullOrEmpty(deathStateName))
                animator.Play(deathStateName, 0, 0f);
        }

        // Chờ animation xong rồi destroy
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Chờ 1 frame để animator bắt đầu state Death
        yield return null;

        // Chờ Death animation chạy xong (hoặc fallback timeout)
        if (!string.IsNullOrEmpty(deathStateName) && animator != null)
        {
            float elapsed = 0f;

            // Đợi vào đúng state Death (tối đa 0.5s)
            while (elapsed < 0.5f && animator != null)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(deathStateName)) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Đợi state Death chạy hết (tối đa deathAnimDuration giây)
            elapsed = 0f;
            while (elapsed < deathAnimDuration && animator != null)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(deathStateName) || info.normalizedTime >= 0.95f)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(deathAnimDuration);
        }

        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────
    // Invincible
    // ──────────────────────────────────────────────

    private void StartInvincible()
    {
        if (invincibleCoroutine != null)
            StopCoroutine(invincibleCoroutine);

        invincibleCoroutine = StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        if (!isDead) isInvincible = false;
        invincibleCoroutine = null;
    }

    // ──────────────────────────────────────────────
    // Hurt Animation
    // ──────────────────────────────────────────────

    private void PlayHurtAnimation()
    {
        if (animator == null) return;

        animator.ResetTrigger(deathTrigger);
        animator.ResetTrigger(hurtTrigger);
        animator.SetBool(runBool, false);
        animator.SetTrigger(hurtTrigger);
    }

    // ──────────────────────────────────────────────
    // Damage Popup
    // ──────────────────────────────────────────────

    private void SpawnDamagePopup(float damage, bool isCritical = false)
    {
        if (damagePopupPrefab == null) return;

        Vector3 spawnPos = transform.position + popupOffset;
        DamagePopup.Create(spawnPos, damage, damagePopupPrefab, isCritical);
    }
}