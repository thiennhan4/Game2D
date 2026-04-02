using UnityEngine;

public class SkeletonBossHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 1500f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float timeToDestroy = 2f; // Thời gian trễ trước khi xóa khỏi map

    [Header("Animation Parameters")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string deadBool = "IsDead";

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D bossCollider;
    [SerializeField] private SkeletonBossController bossController;
    [SerializeField] private BossPhaseManager phaseManager;
    [SerializeField] private Rigidbody2D rb;

    // Private fields
    private bool isDead;

    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead => isDead;

    // Unity Methods
    private void Awake()
    {
        // Khởi tạo máu nếu chưa được set hoặc = 0
        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;
        }

        // Kiểm tra và lấy các reference cơ bản nếu chưa gán trên Inspector
        if (animator == null) animator = GetComponent<Animator>();
        if (bossCollider == null) bossCollider = GetComponent<Collider2D>();
        if (bossController == null) bossController = GetComponent<SkeletonBossController>();
        if (phaseManager == null) phaseManager = GetComponent<BossPhaseManager>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    // Public Methods
    
    /// <summary>
    /// Xử lý boss nhận sát thương
    /// </summary>
    public void TakeDamage(float damage)
    {
        // Nếu boss đã chết hoặc sát thương không hợp lệ thì bỏ qua
        if (isDead || damage <= 0) return;

        // Trừ máu và đảm bảo máu không nhỏ hơn 0
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // TODO: Cập nhật UI máu ở đây (Đã lược bỏ theo yêu cầu: không cần code script UI)

        if (currentHealth <= 0)
        {
            Die();
            return; // Sửa lỗi chặn không cho thực thi logic Hurt bên dưới nếu boss đã chết
        }

        // Báo cho PhaseManager kiểm tra phase dựa trên % máu
        if (phaseManager != null)
        {
            phaseManager.CheckPhase(HealthPercent);
        }

        // Cập nhật lại logic nhận sát thương và gọi Hurt theo ý bạn
        TriggerHurtAnimation();

        // Xử lý huỷ Hurt khi không còn bị nhận sát thương
        if (hurtResetCoroutine != null)
        {
            StopCoroutine(hurtResetCoroutine);
        }
        hurtResetCoroutine = StartCoroutine(ResetHurtRoutine());
    }

    private Coroutine hurtResetCoroutine;

    private System.Collections.IEnumerator ResetHurtRoutine()
    {
        // Chờ một khoảng thời gian ngắn tương đương độ dài animation Hurt (0.3s)
        yield return new WaitForSeconds(0.3f);

        // Sau 0.3s mà không có sát thương mới đè lên, thì xoá lệnh Hurt để chặn spam và ép quay về hành động trước đó
        if (animator != null && !isDead)
        {
            animator.ResetTrigger(hurtTrigger);
            
            // Cân nhắc ép Boss phát tiếp Idle nếu Animation đang bị kẹt ở sub-state
            // animator.Play("Idle"); 
        }
    }

    /// <summary>
    /// Xử lý hồi máu
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;

        // Cộng máu và đảm bảo không vượt quá giới hạn
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        // TODO: Cập nhật UI máu
    }

    /// <summary>
    /// Hồi đầy máu cho Boss
    /// </summary>
    public void RestoreFullHealth()
    {
        if (isDead) return;
        
        currentHealth = maxHealth;
        
        // TODO: Cập nhật UI máu
    }

    /// <summary>
    /// Hành động khi Boss chết
    /// </summary>
    public void Die()
    {
        if (isDead) return; // Đảm bảo chỉ gọi xử lý chết 1 lần duy nhất

        isDead = true;
        currentHealth = 0;

        // TODO: Cập nhật UI máu lần cuối để hiện 0

        // Tắt collider để tránh tương tác với player
        if (bossCollider != null)
        {
            bossCollider.enabled = false;
        }

        TriggerDeathAnimation();

        // Báo cho BossController biết boss đã chết để xử lý logic state và drop đồ, v.v.
        if (bossController != null)
        {
            bossController.OnBossDeath();
        }

        // (Tùy chọn) Đóng băng hoặc vô hiệu hóa physics khi chết
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
        }

        // Xóa Boss khỏi scene sau phần thời gian định trước (để kịp xem animation chết)
        Destroy(gameObject, timeToDestroy);
    }

    // Private Helper Methods

    private void TriggerHurtAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
        {
            animator.SetTrigger(hurtTrigger);
        }
    }

    private void TriggerDeathAnimation()
    {
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(deathTrigger))
            {
                animator.SetTrigger(deathTrigger);
            }
            if (!string.IsNullOrEmpty(deadBool))
            {
                animator.SetBool(deadBool, true);
            }
        }
    }
}
