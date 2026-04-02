using UnityEngine;

/// <summary>
/// Quản lý máu, giáp, nhận sát thương, trạng thái dead/stun, và VFX Death Fake
/// cho enemy Hallowkin. Script này KHÔNG chứa logic AI hay damage output.
/// </summary>
public class HallokinHealth : MonoBehaviour
{
    #region Health Stats
    [Header("--- Health Stats ---")]
    [Tooltip("Máu tối đa")]
    public float maxHealth = 100f;
    [Tooltip("Máu hiện tại (runtime)")]
    [HideInInspector] public float currentHealth;
    [Tooltip("Giáp — giảm sát thương nhận vào")]
    public float armor = 10f;
    #endregion

    #region State Flags (trạng thái do Health quản lý)
    [Header("--- State Flags ---")]
    [Tooltip("Đã chết chưa")]
    public bool isDead = false;
    [Tooltip("Đang bị stun")]
    public bool isStunned = false;
    [Tooltip("Đang bị knockback")]
    public bool isKnockback = false;

    [Tooltip("Thời gian stun (giây)")]
    public float stunDuration = 0.5f;
    [Tooltip("Thời gian knockback (giây)")]
    public float knockbackDuration = 0.2f;

    // Timers nội bộ
    private float stunTimer;
    private float knockbackTimer;
    #endregion

    #region VFX (Fake Death)
    [Header("--- Death VFX (Fake Death) ---")]
    [Tooltip("Kéo prefab VFX vào đây (particle, explosion, smoke...). Spawn khi enemy chết.")]
    public GameObject deathVFXPrefab;
    [Tooltip("Offset vị trí spawn VFX so với vị trí enemy")]
    public Vector2 deathVFXOffset = Vector2.zero;
    [Tooltip("Thời gian (giây) trước khi hủy VFX object. Đặt đủ lâu để VFX chạy xong.")]
    public float deathVFXLifetime = 2f;
    #endregion

    #region References (Cache)
    private Animator anim;
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    #endregion
    #region Damage Popup
    public GameObject damagePopupPrefab;
    public Transform popupPoint;

    #endregion
    // ======== Events (để Controller / Attack lắng nghe) ========
    public System.Action OnDeath;
    public System.Action OnHit;
    public System.Action OnStunStart;
    public System.Action OnStunEnd;

    void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        // --- Stun timer ---
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                OnStunEnd?.Invoke();
            }
        }

        // --- Knockback timer ---
        if (isKnockback)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockback = false;
            }
        }
    }

    #region --- Nhận sát thương (Take Damage) ---
    /// <summary>
    /// Gọi hàm này khi enemy bị đánh.
    /// Sát thương thực tế = damage - armor (tối thiểu 1).
    /// </summary>
    public float TakeDamage(float damage)
    {
        if (isDead) return 0f;

        float actualDamage = Mathf.Max(damage - armor, 1f);
        currentHealth -= actualDamage;

        Debug.Log($"[HallokinHealth] Nhận {actualDamage} damage. HP còn: {currentHealth}/{maxHealth}");

        // Trigger animation bị đánh
        if (anim != null)
            anim.SetTrigger("Hit");

        OnHit?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }

        return actualDamage;
    }

    /// <summary>
    /// Overload: nhận damage + knockback hướng + lực knockback.
    /// </summary>
    public float TakeDamage(float damage, Vector2 hitDirection, float knockbackForce)
    {
        if (isDead) return 0f;

        float actualDamage = TakeDamage(damage);

        // Áp dụng knockback
        if (rb != null && knockbackForce > 0f)
        {
            isKnockback = true;
            knockbackTimer = knockbackDuration;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        return actualDamage;
    }
    #endregion


    public void ShowDamagePopup(float damage)
    {
       if (damagePopupPrefab != null)
        {
            Vector3 spawnPos = popupPoint != null ? popupPoint.position : transform.position + Vector3.up;
            GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

            DamagePopup dmgText = popup.GetComponent<DamagePopup>();
            if (dmgText != null)
            {
                dmgText.Setup(damage);
            }
        }
    }

    #region --- Stun ---
    /// <summary>
    /// Gọi hàm này để stun enemy trong một khoảng thời gian.
    /// </summary>
    public void ApplyStun(float duration = -1f)
    {
        if (isDead) return;
        isStunned = true;
        stunTimer = duration > 0f ? duration : stunDuration;
        OnStunStart?.Invoke();
    }
    #endregion

    #region --- Fake Death (VFX) ---
    /// <summary>
    /// Không dùng animation chết. Thay vào đó:
    /// 1. Dừng enemy hoàn toàn
    /// 2. Spawn VFX prefab (particle, smoke, explosion...)
    /// 3. Ẩn sprite enemy
    /// 4. Hủy enemy sau khi VFX chạy xong
    /// </summary>
    private void Die()
    {
        isDead = true;

        // Dừng di chuyển
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        // Tắt collider để không tương tác nữa
        if (col != null)
            col.enabled = false;

        // Spawn Death VFX
        if (deathVFXPrefab != null)
        {
            Vector2 spawnPos = (Vector2)transform.position + deathVFXOffset;
            GameObject vfx = Instantiate(deathVFXPrefab, spawnPos, Quaternion.identity);
            Destroy(vfx, deathVFXLifetime);
        }

        // Ẩn sprite ngay lập tức (fake death)
        if (sr != null)
            sr.enabled = false;

        // Tắt animator để không chạy animation nữa
        if (anim != null)
            anim.enabled = false;

        // Gửi event
        OnDeath?.Invoke();

        // Hủy enemy object sau khi VFX chạy xong
        Destroy(gameObject, deathVFXLifetime + 0.1f);

        Debug.Log("[HallokinHealth] Enemy đã chết (Fake Death).");
    }
    #endregion

    #region --- Tiện ích ---
    /// <summary>
    /// Hồi máu (dùng cho heal/buff nếu cần)
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    /// <summary>
    /// Tỉ lệ máu hiện tại (0~1), dùng cho health bar.
    /// </summary>
    public float GetHealthPercent()
    {
        return maxHealth > 0f ? currentHealth / maxHealth : 0f;
    }

    /// <summary>
    /// Enemy đang bị khống chế? (stun/knockback/dead)
    /// Controller dùng để block chuyển state.
    /// </summary>
    public bool IsIncapacitated()
    {
        return isDead || isStunned || isKnockback;
    }
    #endregion
}
