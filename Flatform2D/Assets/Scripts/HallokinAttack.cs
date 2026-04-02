using UnityEngine;

/// <summary>
/// Quản lý việc tấn công (damage output) và sát thương chạm (contact damage)
/// của enemy Hallowkin. Script này KHÔNG chứa logic AI hay health.
/// Controller sẽ gọi TryAttack() khi đủ điều kiện.
/// </summary>
public class HallokinAttack : MonoBehaviour
{
    #region Attack Stats
    [Header("--- Attack Stats ---")]
    [Tooltip("Sát thương cơ bản mỗi đòn đánh")]
    public float attackDamage = 15f;
    [Tooltip("Tầm đánh (dùng OverlapCircle tại attackPoint)")]
    public float attackRange = 1.2f;
    [Tooltip("Cooldown giữa các đòn đánh (giây)")]
    public float attackCooldown = 1.5f;
    #endregion

    #region Attack Point
    [Header("--- Attack Point ---")]
    [Tooltip("Điểm xuất phát hitbox đánh (child transform)")]
    public Transform attackPoint;
    [Tooltip("Layer của Player — dùng để detect hit")]
    public LayerMask targetLayer;
    #endregion

    #region Contact Damage (Sát thương chạm)
    [Header("--- Contact Damage ---")]
    [Tooltip("Layer của Player. Khi Player chạm enemy → bị trừ máu.")]
    public LayerMask playerContactLayer;
    [Tooltip("Sát thương gây ra khi Player chạm enemy")]
    public float contactDamage = 10f;
    [Tooltip("Thời gian hồi (giây) giữa mỗi lần gây sát thương chạm.")]
    public float contactDamageCooldown = 1f;
    #endregion

    #region Runtime State (ẩn trong Inspector)
    [HideInInspector] public bool canAttack = true;
    private float attackTimer;
    private float contactDamageTimer;
    #endregion

    #region References (Cache)
    private Animator anim;
    private HallokinHealth health;
    #endregion

    void Awake()
    {
        anim = GetComponent<Animator>();
        health = GetComponent<HallokinHealth>();
    }

    void Update()
    {
        // --- Attack cooldown timer ---
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                canAttack = true;
        }

        // --- Contact damage cooldown ---
        if (contactDamageTimer > 0f)
            contactDamageTimer -= Time.deltaTime;
    }

    #region --- Thực hiện Attack ---
    /// <summary>
    /// Controller gọi hàm này khi AI quyết định đánh.
    /// Trả về true nếu attack được thực thi.
    /// </summary>
    public bool TryAttack()
    {
        // Không đánh nếu đang cooldown, đã chết, hoặc bị khống chế
        if (!canAttack) return false;
        if (health != null && health.IsIncapacitated()) return false;

        PerformAttack();
        return true;
    }

    /// <summary>
    /// Thực hiện đòn đánh: trigger anim + gây damage.
    /// </summary>
    private void PerformAttack()
    {
        canAttack = false;
        attackTimer = attackCooldown;

        // Trigger animation attack
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        // Gây damage — có thể gọi trực tiếp hoặc qua Animation Event
        // Nếu muốn gọi qua Animation Event, đặt event gọi AnimEvent_DoHit()
        // Ở đây ta gọi trực tiếp luôn (nếu không dùng anim event thì bỏ comment)
        // DealDamage();
    }

    /// <summary>
    /// Gọi bởi Animation Event ở frame chém thực sự trong animation Attack.
    /// OverlapCircle tại attackPoint để tìm target trong tầm, rồi gây damage.
    /// </summary>
    public void AnimEvent_DoHit()
    {
        DealDamage();
    }

    /// <summary>
    /// Animation Event: kết thúc animation attack.
    /// </summary>
    public void AnimEvent_EndAttack()
    {
        // Có thể dùng để báo Controller rằng action đã xong
    }

    private void DealDamage()
    {
        if (attackPoint == null) return;

        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(
            attackPoint.position, attackRange, targetLayer
        );

        foreach (Collider2D targetCol in hitTargets)
        {
            PlayerHealth playerHP = targetCol.GetComponent<PlayerHealth>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(attackDamage);
                Debug.Log($"[HallokinAttack] Gây {attackDamage} damage cho Player.");
            }
        }
    }
    #endregion

    #region --- Contact Damage (Sát thương khi chạm) ---
    /// <summary>
    /// Khi Player chạm vào enemy (collision) → kiểm tra layer.
    /// Nếu đúng layer Player → gây sát thương.
    /// Dùng OnCollisionStay2D để liên tục gây damage khi đứng trên/chạm enemy.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        // Không gây damage nếu đã chết
        if (health != null && health.isDead) return;
        if (contactDamageTimer > 0f) return; // Đang cooldown

        // Kiểm tra object chạm có nằm trong playerContactLayer không
        if (((1 << collision.gameObject.layer) & playerContactLayer) != 0)
        {
            PlayerHealth playerHP = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(contactDamage);
                contactDamageTimer = contactDamageCooldown; // Reset cooldown
                Debug.Log($"[HallokinAttack] Contact damage: {contactDamage}");
            }
        }
    }
    #endregion

    #region --- Gizmos ---
    private void OnDrawGizmosSelected()
    {
        // Vẽ tầm tấn công
        if (attackPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
    #endregion
}
