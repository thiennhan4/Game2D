using System.Collections;
using UnityEngine;

public class NightBorneHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 120f;

    [Header("Hit")]
    [SerializeField] private float invincibleTime = 0.3f;

    [Header("Death")]
    [SerializeField] private float deathAnimDuration = 1.5f;

    [Header("Death Explosion Damage")]
    [Tooltip("Bật/tắt gây damage khi chết (vụ nổ tím trong animation Death)")]
    [SerializeField] private bool enableDeathExplosion = true;
    [Tooltip("Damage gây lên Player khi vụ nổ xảy ra")]
    [SerializeField] private float explosionDamage = 30f;
    [Tooltip("Bán kính vụ nổ (OverlapCircle)")]
    [SerializeField] private float explosionRadius = 2f;
    [Tooltip("Thời điểm vụ nổ xảy ra trong animation Death (0.0 = đầu, 1.0 = cuối).\nDựa trên sprite sheet: vụ nổ bắt đầu khoảng frame 5/13 ≈ 0.4")]
    [SerializeField] [Range(0f, 1f)] private float explosionNormalizedTime = 0.4f;
    [Tooltip("Layer của Player để detect vụ nổ")]
    [SerializeField] private LayerMask playerLayer;

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

    private NightBorneController nightBorneController;
    private NightBorneAttack nightBorneAttack;
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

        nightBorneController = GetComponent<NightBorneController>();
        nightBorneAttack = GetComponent<NightBorneAttack>();

        if (animator == null)
            Debug.LogError($"[NightBorneHealth] {name}: Animator not found!");
        if (enemyCollider == null)
            Debug.LogError($"[NightBorneHealth] {name}: Collider2D not found!");

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;

        Debug.Log($"[NightBorneHealth] {name} initialized: {currentHealth}/{maxHealth} HP");
    }

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

        Debug.Log($"[NightBorneHealth] {name} took {damage:F0} damage ({oldHealth:F0} → {currentHealth:F0})");

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

        Debug.Log($"[NightBorneHealth] {name} died!");

        // Dừng invincible
        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }
        isInvincible = false;

        // Tắt controller + attack
        if (nightBorneController != null) nightBorneController.enabled = false;
        if (nightBorneAttack != null) 
        {
            nightBorneAttack.CancelAttack();
            nightBorneAttack.enabled = false;
        }

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

        bool explosionTriggered = false;

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

                // ── Death Explosion: gây damage khi animation đến frame vụ nổ ──
                if (enableDeathExplosion && !explosionTriggered 
                    && info.IsName(deathStateName) 
                    && info.normalizedTime >= explosionNormalizedTime)
                {
                    explosionTriggered = true;
                    DealExplosionDamage();
                }

                if (!info.IsName(deathStateName) || info.normalizedTime >= 0.95f)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Fallback khi không có animator — dùng timer
            if (enableDeathExplosion)
            {
                yield return new WaitForSeconds(deathAnimDuration * explosionNormalizedTime);
                DealExplosionDamage();
                yield return new WaitForSeconds(deathAnimDuration * (1f - explosionNormalizedTime));
            }
            else
            {
                yield return new WaitForSeconds(deathAnimDuration);
            }
        }

        Destroy(gameObject);
    }

    private void DealExplosionDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            playerLayer
        );

        if (hits == null || hits.Length == 0)
        {
            Debug.Log($"[NightBorneHealth] '{name}' Death Explosion: không trúng ai.");
            return;
        }

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;

            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = col.GetComponentInParent<IDamageable>();

            if (damageable == null || damageable.IsDead) continue;

            damageable.TakeDamage(explosionDamage);
            Debug.Log($"[NightBorneHealth] '{name}' Death Explosion hit '{col.name}' for {explosionDamage} damage!");
        }
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

    // ──────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (enableDeathExplosion)
        {
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.4f); // Tím nhạt
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
