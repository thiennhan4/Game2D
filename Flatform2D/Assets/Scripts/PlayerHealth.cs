using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("UI")]
    [SerializeField] private StateManager healthBarUI;

    [Header("Hit")]
    [SerializeField] private float invincibleTime = 0.5f;
    [SerializeField] private float stunTime = 0.3f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 5f;

    [Header("Respawn")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Transform respawnPoint;

    [Header("Damage Popup")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1.5f, 0f);

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D playerCollider;

    [Header("Animation Parameters")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string respawnTrigger = "Respawn";
    [SerializeField] private string healTrigger = "Health";
    [SerializeField] private string deadBool = "IsDead";
    [SerializeField] private string runBool = "isRunning";
    [SerializeField] private string jumpBool = "isJumping";
    [SerializeField] private string hurtStateName = "Hurt";
    [SerializeField] private string deathStateName = "Death";

    private PlayerController playerController;
    private Coroutine invincibleCoroutine;
    private Coroutine stunCoroutine;
    private bool isInvincible;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // ──────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────

    private void Reset()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        playerController = GetComponent<PlayerController>();

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    // ──────────────────────────────────────────────
    // Damage
    // ──────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        TakeDamageInternal(damage, Vector2.zero, false);
    }

    public void TakeDamage(float damage, Vector2 hitDirection)
    {
        TakeDamageInternal(damage, hitDirection, true);
    }

    private void TakeDamageInternal(float damage, Vector2 hitDirection, bool applyKnockback)
    {
        if (isDead || isInvincible || damage <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthUI();

        // Hiện damage popup
        SpawnDamagePopup(damage);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Tắt controller TRƯỚC để UpdateAnimationStates() không đè lên animation Hurt
        SetController(false);

        if (applyKnockback) ApplyKnockback(hitDirection);

        PlayHurtAnimation();
        StartStun();
        StartInvincible();
    }

    // ──────────────────────────────────────────────
    // Heal
    // ──────────────────────────────────────────────

    /// <summary>
    /// Hàm xử lý chung tính năng hồi máu từ mọi nguồn (Item, Fountain...)
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f || currentHealth >= maxHealth) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthUI();

        // Gọi animation Health
        PlayHealAnimation();
    }

    public void RestoreFullHealth()
    {
        if (isDead || currentHealth >= maxHealth) return;

        currentHealth = maxHealth;
        UpdateHealthUI();

        PlayHealAnimation();
    }

    // ──────────────────────────────────────────────
    // Die & Respawn
    // ──────────────────────────────────────────────

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0f;
        UpdateHealthUI();

        StopActiveCoroutines();
        SetController(false);
        StopMovement();

        if (animator != null)
        {
            ResetTrigger(hurtTrigger);
            ResetTrigger(respawnTrigger);
            ResetTrigger(deathTrigger);

            SetBool(runBool, false);
            SetBool(deadBool, true);

            SetTrigger(deathTrigger);

            if (!string.IsNullOrEmpty(deathStateName))
                animator.Play(deathStateName, 0, 0f);
        }

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (canRespawn)
            StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return null; // Đợi 1 frame để animator chuyển state

        // Đợi animation Death chạy xong
        yield return WaitForAnimationState(deathStateName);

        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    public void Respawn()
    {
        isDead = false;
        currentHealth = maxHealth;

        if (respawnPoint != null)
            transform.position = respawnPoint.position;

        if (playerCollider != null)
            playerCollider.enabled = true;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        SetController(true);

        if (animator != null)
        {
            SetBool(deadBool, false);
            SetTrigger(respawnTrigger);
        }

        UpdateHealthUI();
        StartInvincible();
    }

    // ──────────────────────────────────────────────
    // Stun & Invincible
    // ──────────────────────────────────────────────

    private void StartStun()
    {
        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        yield return null; // Đợi 1 frame để animator bắt đầu Hurt

        // Đợi animation Hurt chạy xong
        yield return WaitForAnimationState(hurtStateName);

        yield return new WaitForSeconds(stunTime);

        if (!isDead) SetController(true);
        stunCoroutine = null;
    }

    private void StartInvincible()
    {
        if (invincibleCoroutine != null) StopCoroutine(invincibleCoroutine);
        invincibleCoroutine = StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        isInvincible = false;
        invincibleCoroutine = null;
    }

    // ──────────────────────────────────────────────
    // Animation Helpers
    // ──────────────────────────────────────────────

    private void PlayHurtAnimation()
    {
        if (animator == null) return;

        ResetTrigger(deathTrigger);
        ResetTrigger(hurtTrigger);
        SetBool(runBool, false);
        SetBool(jumpBool, false);
        SetTrigger(hurtTrigger);
    }

    private void PlayHealAnimation()
    {
        if (animator == null) return;
        
        SetTrigger(healTrigger);
    }

    /// <summary>
    /// Đợi animation state chạy xong (normalizedTime >= 1).
    /// Timeout 2 giây nếu state không xuất hiện.
    /// </summary>
    private IEnumerator WaitForAnimationState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) yield break;

        // Đợi vào đúng state (timeout 2s)
        float elapsed = 0f;
        while (elapsed < 2f)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName) || info.IsTag(stateName)) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Đợi state chạy hết frame
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            bool inState = info.IsName(stateName) || info.IsTag(stateName);
            if (!inState || info.normalizedTime >= 1f) break;
            yield return null;
        }
    }

    private void SetTrigger(string param)
    {
        if (!string.IsNullOrEmpty(param)) animator.SetTrigger(param);
    }

    private void ResetTrigger(string param)
    {
        if (!string.IsNullOrEmpty(param)) animator.ResetTrigger(param);
    }

    private void SetBool(string param, bool value)
    {
        if (!string.IsNullOrEmpty(param)) animator.SetBool(param, value);
    }

    // ──────────────────────────────────────────────
    // Utility
    // ──────────────────────────────────────────────

    private void SetController(bool enabled)
    {
        if (playerController != null) playerController.enabled = enabled;
    }

    private void ApplyKnockback(Vector2 direction)
    {
        if (rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
    }

    private void StopMovement()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void StopActiveCoroutines()
    {
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }
        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }
        isInvincible = false;
    }

    private void UpdateHealthUI()
    {
        if (healthBarUI != null)
            healthBarUI.UpdateHealthUI(currentHealth, maxHealth);
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