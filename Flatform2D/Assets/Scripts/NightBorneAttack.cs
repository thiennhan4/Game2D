using UnityEngine;

public class NightBorneAttack : MonoBehaviour
{
    #region ======= SERIALIZED FIELDS =======

    [Header("Attack Stats")]
    [Tooltip("Sát thương mỗi đòn đánh cận chiến")]
    [SerializeField] private float attackDamage = 20f;

    [Tooltip("Thời gian cooldown giữa 2 lần attack (giây)")]
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Detection")]
    [Tooltip("Khoảng cách tối đa để detect player và bắt đầu attack")]
    [SerializeField] private float attackRange = 2f;

    [Header("Melee Hit Area")]
    [Tooltip("Empty child Transform → vị trí trung tâm vùng đánh")]
    [SerializeField] private Transform attackPoint;

    [Tooltip("Bán kính vùng đánh cận chiến (OverlapCircle)")]
    [SerializeField] private float meleeRadius = 1f;

    [Tooltip("Layer của Player để detect")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NightBorneHealth nightBorneHealth;
    [SerializeField] private NightBorneController nightBorneController;

    #endregion

    #region ======= PRIVATE STATE =======

    // Cooldown timer
    private float nextAttackTime;

    // Đang trong animation attack → lock không cho attack lại
    private bool isAttacking;

    // Reference lưu trữ coroutine tấn công
    private Coroutine attackCoroutine;

    // Cache Animator hash
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    #endregion

    #region ======= PUBLIC PROPERTIES =======

    /// <summary> Đang attack hay không (NightBorneController dùng để dừng di chuyển). </summary>
    public bool IsAttacking => isAttacking;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Reset()
    {
        // Auto-populate khi thêm component trong Editor
        animator = GetComponentInChildren<Animator>();
        nightBorneHealth = GetComponent<NightBorneHealth>();
        nightBorneController = GetComponent<NightBorneController>();
    }

    private void Awake()
    {
        // Tự tìm nếu chưa gán
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (nightBorneHealth == null) nightBorneHealth = GetComponent<NightBorneHealth>();
        if (nightBorneController == null) nightBorneController = GetComponent<NightBorneController>();

        // Cảnh báo thiếu reference
        if (attackPoint == null)
            Debug.LogError($"[NightBorneAttack] '{gameObject.name}': AttackPoint chưa gán! " +
                "Tạo empty child GameObject, đặt tên 'AttackPoint', kéo vào đây.", this);

        if (nightBorneHealth == null)
            Debug.LogWarning($"[NightBorneAttack] '{gameObject.name}': Không tìm thấy NightBorneHealth! " +
                "Attack sẽ không check isDead.", this);

        if (animator == null)
            Debug.LogWarning($"[NightBorneAttack] '{gameObject.name}': Không tìm thấy Animator!", this);

        if (nightBorneController == null)
            Debug.LogWarning($"[NightBorneAttack] '{gameObject.name}': Không tìm thấy NightBorneController!", this);
    }

    private void Update()
    {
        // Không attack nếu enemy đã chết
        if (nightBorneHealth != null && nightBorneHealth.IsDead)
        {
            if (isAttacking) CancelAttack();
            return;
        }

        // Đang trong animation attack → chờ Coroutine kết thúc
        if (isAttacking) return;

        // Chưa đủ cooldown
        if (Time.time < nextAttackTime) return;

        // Kiểm tra player có trong tầm attack không
        if (IsPlayerInAttackRange())
        {
            StartAttack();
        }
    }

    #endregion

    #region ======= DETECTION =======

    private bool IsPlayerInAttackRange()
    {
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null) return false;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance > attackRange) return false;

        // Tránh lỗi logic quay đầu (Aim) rườm rà
        // Kiểm tra xem player có nằm ở đúng hướng nhìn hay không
        if (nightBorneController != null)
        {
            bool playerOnRight = playerTransform.position.x > transform.position.x;
            if (playerOnRight != nightBorneController.IsFacingRight)
            {
                // Player ở sau lưng -> không đánh, để Controller tự quay hướng bằng hành vi Chase
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lấy Transform player từ NightBorneController.
    /// </summary>
    private Transform GetPlayerTransform()
    {
        if (nightBorneController != null && nightBorneController.Player != null)
            return nightBorneController.Player;

        // Fallback: tìm bằng tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    #endregion

    #region ======= ATTACK LOGIC =======

    private void StartAttack()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        // Trigger animation Attack
        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack);
            animator.SetTrigger(AnimAttack);
        }

        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        // Chờ animation vung vũ khí (tăng/giảm giá trị này cho khớp với frame gây damage trong animation)
        yield return new WaitForSeconds(0.4f);

        // NẾU enemy chết lúc đang vung vũ khí → KHÔNG gây damage
        if (nightBorneHealth != null && nightBorneHealth.IsDead)
        {
            CancelAttack();
            yield break;
        }

        // Gây damage cận chiến
        DealMeleeDamage();

        // Chờ thêm để animation hoàn thành phần hạ vũ khí
        yield return new WaitForSeconds(0.4f);

        if (isAttacking)
        {
            EndAttack();
        }
    }

    /// <summary>
    /// Hủy tấn công ngay lập tức khi enemy chết.
    /// </summary>
    public void CancelAttack()
    {
        if (!isAttacking) return;
        isAttacking = false;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack);
        }
    }

    private void EndAttack()
    {
        isAttacking = false;
        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack);
        }
    }

    private void DealMeleeDamage()
    {
        if (attackPoint == null) return;

        // Tìm tất cả collider Player trong vùng đánh
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(
            attackPoint.position,
            meleeRadius,
            playerLayer
        );

        if (hitPlayers == null || hitPlayers.Length == 0)
        {
            Debug.Log($"[NightBorneAttack] '{gameObject.name}': Đánh trượt - không trúng Player.");
            return;
        }

        foreach (Collider2D playerCol in hitPlayers)
        {
            if (playerCol == null) continue;

            // Tìm IDamageable trên Player
            IDamageable damageable = playerCol.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = playerCol.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                Debug.LogWarning($"[NightBorneAttack] '{playerCol.name}' không có IDamageable!");
                continue;
            }

            if (damageable.IsDead)
            {
                Debug.Log($"[NightBorneAttack] '{playerCol.name}' đã chết, bỏ qua.");
                continue;
            }

            damageable.TakeDamage(attackDamage);
            Debug.Log($"[NightBorneAttack] '{gameObject.name}' đánh trúng '{playerCol.name}' | Damage: {attackDamage}");
        }
    }

    /// <summary>
    /// Giữ lại hàm này để tránh báo lỗi nếu Animation Event lỡ được set trong Unity.
    /// </summary>
    public void AnimEvent_DealDamage()
    {
        // Do nothing (handled by Coroutine)
    }

    /// <summary>
    /// Giữ lại hàm này để tránh báo lỗi nếu Animation Event lỡ được set trong Unity.
    /// </summary>
    public void AnimEvent_EndAttack()
    {
        // Do nothing (handled by Coroutine)
    }

    #endregion

    #region ======= GIZMOS =======

    /// <summary>
    /// Vẽ vùng attack range và melee hit area trong Scene View:
    /// - Cam: attackRange (vùng detect player để bắt đầu đánh)
    /// - Đỏ: meleeRadius tại attackPoint (vùng gây damage thực tế)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Attack detection range (cam)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Melee hit area (đỏ)
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, meleeRadius);
        }
    }

    #endregion
}
