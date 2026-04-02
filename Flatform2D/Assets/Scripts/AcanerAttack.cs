using UnityEngine;

/// <summary>
/// Xử lý tấn công (bắn cung) cho Enemy Acaner.
/// Detect player trong attackRange → trigger animation Attack → 
/// spawn ProjectileArrow qua Animation Event (hoặc Coroutine).
///
/// Attach to: Acaner root GameObject (cùng object với AcanerController, AcanerHealth).
/// </summary>
public class AcanerAttack : MonoBehaviour
{
    #region ======= ENUMS =======
    public enum ShootMode
    {
        AimAtPlayer,
        HorizontalOnly
    }

    #endregion

    #region ======= SERIALIZED FIELDS =======

    [Header("Attack Stats")]
    [Tooltip("Sát thương mỗi mũi tên")]
    [SerializeField] private float attackDamage = 15f;

    [Tooltip("Thời gian cooldown giữa 2 lần attack (giây)")]
    [SerializeField] private float attackCooldown = 2.0f;

    [Header("Detection")]
    [Tooltip("Khoảng cách tối đa để detect player và bắt đầu attack")]
    [SerializeField] private float attackRange = 7f;

    [Header("Projectile")]
    [Tooltip("Prefab mũi tên (cần có script ProjectileArrow)")]
    [SerializeField] private GameObject arrowPrefab;

    [Tooltip("Tốc độ bay của mũi tên")]
    [SerializeField] private float arrowSpeed = 10f;

    [Tooltip("Thời gian sống tối đa của mũi tên trước khi tự hủy")]
    [SerializeField] private float arrowLifetime = 5f;

    [Tooltip("Chế độ bắn: nhắm vào player hoặc bắn ngang")]
    [SerializeField] private ShootMode shootMode = ShootMode.AimAtPlayer;

    [Header("References")]
    [Tooltip("Empty child Transform → vị trí spawn mũi tên (tự flip theo localScale)")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Layer của Player để detect")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AcanerHealth acanerHealth;
    [SerializeField] private AcanerController acanerController;
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

    /// <summary> Đang attack hay không (AcanerController dùng để dừng di chuyển). </summary>
    public bool IsAttacking => isAttacking;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Reset()
    {
        // Auto-populate khi thêm component trong Editor
        animator = GetComponentInChildren<Animator>();
        acanerHealth = GetComponent<AcanerHealth>();
        acanerController = GetComponent<AcanerController>();
    }

    private void Awake()
    {
        // Tự tìm nếu chưa gán
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (acanerHealth == null) acanerHealth = GetComponent<AcanerHealth>();
        if (acanerController == null) acanerController = GetComponent<AcanerController>();

        // Cảnh báo thiếu reference
        if (firePoint == null)
            Debug.LogError($"[AcanerAttack] '{gameObject.name}': FirePoint chưa gán! " +
                "Tạo empty child  GameObject, đặt tên 'FirePoint', kéo vào đây.", this);

        if (arrowPrefab == null)
            Debug.LogError($"[AcanerAttack] '{gameObject.name}': ArrowPrefab chưa gán! " +
                "Tạo prefab mũi tên có script ProjectileArrow, kéo vào đây.", this);

        if (acanerHealth == null)
            Debug.LogWarning($"[AcanerAttack] '{gameObject.name}': Không tìm thấy AcanerHealth! " +
                "Attack sẽ không check isDead.", this);

        if (animator == null)
            Debug.LogWarning($"[AcanerAttack] '{gameObject.name}': Không tìm thấy Animator!", this);

        if (acanerController == null)
            Debug.LogWarning($"[AcanerAttack] '{gameObject.name}': Không tìm thấy AcanerController! " +
                "Không thể xác định hướng bắn.", this);
    }

    private void Update()
    {
        // Không attack nếu enemy đã chết
        if (acanerHealth != null && acanerHealth.IsDead)
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
            // Face về phía player trước khi bắn
            FaceTowardsPlayer();
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
        return distance <= attackRange;
    }

    private Transform GetPlayerTransform()
    {
        if (acanerController != null && acanerController.Player != null)
            return acanerController.Player;

        // Fallback: tìm bằng tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    /// <summary>
    /// Quay mặt về phía player trước khi bắn.
    /// </summary>
    private void FaceTowardsPlayer()
    {
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null || acanerController == null) return;

        bool playerOnRight = playerTransform.position.x > transform.position.x;
        acanerController.FaceDirection(playerOnRight);
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
        // Thời gian chờ giương cung. Hãy tăng/giảm số 0.5f này cho khớp với frame mũi tên rời cung trong animation!
        // Ví dụ: nếu animation dài, có thể tăng lên 0.7f
        yield return new WaitForSeconds(0.5f);

        // NẾU enemy chết lúc đang giương cung -> KHÔNG bắn
        if (acanerHealth != null && acanerHealth.IsDead)
        {
            CancelAttack();
            yield break;
        }

        // Đã attack thì bắt buộc phải hoàn thành (không cancel giữa chừng để tránh lỗi giật animation)
        SpawnArrow();

        // Chờ thêm 0.5s để animation hoàn thành phần hạ cung
        yield return new WaitForSeconds(0.5f);

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

    /// <summary>
    /// Bắn cung thật sự
    /// </summary>
    private void SpawnArrow()
    {
        if (firePoint == null || arrowPrefab == null) return;

        // Tính hướng bắn
        Vector2 shootDirection = CalculateShootDirection();

        // Lấy gốc xoay cơ bản từ firePoint
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, firePoint.rotation);

        ProjectileArrow arrowScript = arrowObj.GetComponent<ProjectileArrow>();
        if (arrowScript != null)
        {
            // Truyền tham số để mũi tên tự bay theo Rigidbody2D
            arrowScript.Fire(shootDirection, arrowSpeed, attackDamage, gameObject, arrowLifetime);
        }
        else
        {
            Debug.LogError($"[AcanerAttack] Thiếu script ProjectileArrow trên Prefab '{arrowPrefab.name}'!", this);
            Destroy(arrowObj);
        }
    }

    /// <summary>
    /// Giữ lại hàm này để tránh báo lỗi nếu Animation Event lỡ được set trong Unity.
    /// Bây giờ logic đã được xử lý an toàn bằng Coroutine.
    /// </summary>
    public void AnimEvent_SpawnArrow()
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

    /// <summary>
    /// Tính hướng bắn dựa trên shootMode:
    /// - AimAtPlayer: vector từ firePoint tới player (có hướng xiên)
    /// - HorizontalOnly: vector ngang trái/phải theo hướng nhìn enemy
    /// </summary>
    private Vector2 CalculateShootDirection()
    {
        switch (shootMode)
        {
            case ShootMode.AimAtPlayer:
                Transform playerTransform = GetPlayerTransform();
                if (playerTransform != null)
                {
                    // Hướng từ firePoint tới player → mũi tên bay chéo/xiên
                    Vector2 dir = (playerTransform.position - firePoint.position);
                    return dir.normalized;
                }
                // Fallback: bắn ngang nếu không tìm thấy player
                return GetHorizontalDirection();

            case ShootMode.HorizontalOnly:
                return GetHorizontalDirection();

            default:
                return GetHorizontalDirection();
        }
    }

    /// <summary>
    /// Trả về vector hướng ngang (1, 0) hoặc (-1, 0) theo hướng nhìn enemy.
    /// </summary>
    private Vector2 GetHorizontalDirection()
    {
        if (acanerController != null)
            return acanerController.IsFacingRight ? Vector2.right : Vector2.left;

        // Fallback: dùng localScale.x để xác định hướng
        return transform.localScale.x > 0f ? Vector2.right : Vector2.left;
    }

    #endregion

    #region ======= GIZMOS =======

    /// <summary>
    /// Vẽ vùng attack range trong Scene View:
    /// - Cam: attackRange (vùng detect player để bắt đầu bắn)
    /// - Xanh lá nhỏ: firePoint position
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Attack detection range (cam)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // FirePoint marker (xanh lá)
        if (firePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(firePoint.position, 0.1f);

            // Vẽ đường hướng bắn (nếu đang play)
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;
                Vector2 dir = CalculateShootDirection();
                Gizmos.DrawRay(firePoint.position, dir * 2f);
            }
            else
            {
                // Preview hướng bắn ngang
                Gizmos.color = Color.red;
                float dirX = transform.localScale.x > 0f ? 1f : -1f;
                Gizmos.DrawRay(firePoint.position, new Vector3(dirX * 2f, 0f, 0f));
            }
        }
    }

    #endregion
}
