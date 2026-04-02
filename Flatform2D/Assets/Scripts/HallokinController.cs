using UnityEngine;

/// <summary>
/// AI Controller cho enemy Hallowkin.
/// Chỉ chứa logic điều khiển: phát hiện, di chuyển, tuần tra, quyết định tấn công, state machine.
/// KHÔNG chứa damage hay health — delegate cho HallokinAttack và HallokinHealth.
/// </summary>
public class HallokinController : MonoBehaviour
{
    // ================================================================
    // 1) REFERENCES (Tham chiếu component)
    // ================================================================
    #region References
    [Header("=== References ===")]
    [Tooltip("Transform của Player (tự tìm nếu để trống)")]
    public Transform player;

    [Tooltip("Rigidbody2D — di chuyển vật lý")]
    public Rigidbody2D rb;

    [Tooltip("Animator — điều khiển animation")]
    public Animator anim;

    [Tooltip("HallokinHealth — check dead, stun, knockback...")]
    public HallokinHealth health;

    [Tooltip("HallokinAttack — gọi attack khi đủ điều kiện")]
    public HallokinAttack attack;

    [Tooltip("Collider2D chính của enemy")]
    public Collider2D mainCollider;
    #endregion

    // ================================================================
    // 2) AI DETECTION (Phát hiện mục tiêu)
    // ================================================================
    #region AI Detection
    [Header("=== AI Detection ===")]
    [Tooltip("Tầm phát hiện / aggro range")]
    public float detectRange = 6f;

    [Tooltip("Ra khỏi range này thì thôi đuổi")]
    public float loseRange = 10f;

    [Tooltip("Góc nhìn (FOV) — 360 = nhìn xung quanh")]
    [Range(0f, 360f)]
    public float fieldOfView = 360f;

    [Tooltip("Layer của target (Player)")]
    public LayerMask targetLayer;

    [Tooltip("Bao lâu check target 1 lần (giây) — tối ưu performance")]
    public float checkInterval = 0.2f;

    private float checkTimer;
    private bool targetDetected = false;
    #endregion

    // ================================================================
    // 3) MOVEMENT / CHASE / PATROL
    // ================================================================
    #region Movement / Chase / Patrol
    [Header("=== Movement ===")]
    [Tooltip("Tốc độ di chuyển tuần tra")]
    public float moveSpeed = 2.5f;

    [Tooltip("Tốc độ khi đuổi theo Player")]
    public float chaseSpeed = 4f;

    [Tooltip("Khoảng cách tối thiểu tới target thì dừng lại")]
    public float stopDistance = 1f;

    [Header("--- Patrol ---")]
    [Tooltip("Các điểm tuần tra (nếu để trống → tuần tra quanh startPosition theo patrolDistance)")]
    public Transform[] patrolPoints;

    [Tooltip("Khoảng cách tuần tra từ điểm xuất phát (dùng khi không có patrolPoints)")]
    public float patrolDistance = 5f;

    [Tooltip("Thời gian chờ tại mỗi điểm tuần tra (giây)")]
    public float waitAtPointTime = 1f;

    [Tooltip("Tuần tra lặp lại vòng hay ping-pong")]
    public bool loopPatrol = true;

    // Runtime patrol
    private Vector2 startPosition;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool waitingAtPoint = false;
    private int patrolDirection = 1; // Dùng cho ping-pong
    #endregion

    // ================================================================
    // 4) COMBAT DECISION (Điều kiện để đánh)
    // ================================================================
    #region Combat Decision
    [Header("=== Combat Decision ===")]
    [Tooltip("Tầm đánh — khi target trong tầm này, AI sẽ ra lệnh tấn công (hoặc lấy từ HallokinAttack)")]
    public float attackRange = 1.5f;

    [Tooltip("Cooldown giữa các lần AI quyết định đánh (có thể khác với cooldown animation)")]
    public float attackDecisionCooldown = 2f;

    [Tooltip("Tốc độ xoay mặt về phía target (dùng cho 3D hoặc smooth turn)")]
    public float faceTargetSpeed = 10f;

    [Tooltip("Mất mục tiêu bao lâu (giây) thì quay về patrol")]
    public float disengageDelay = 3f;

    // Runtime combat
    private float attackDecisionTimer = 0f;
    private float disengageTimer = 0f;
    #endregion

    // ================================================================
    // 5) STATE MACHINE (Trạng thái)
    // ================================================================
    #region State Machine
    [Header("=== State Machine ===")]
    public EnemyState currentState = EnemyState.Idle;

    [Tooltip("Timer cho state hiện tại (đa năng)")]
    [HideInInspector] public float stateTimer = 0f;

    [Tooltip("Đang thực hiện hành động (đánh/bị hit) → chặn chuyển state")]
    [HideInInspector] public bool isPerformingAction = false;
    #endregion

    // ================================================================
    // 6) ORIENTATION / GROUND CHECK (Platformer)
    // ================================================================
    #region Orientation / Ground Check
    [Header("=== Orientation / Ground Check ===")]
    public bool facingRight = true;

    [Tooltip("Transform check mặt đất (child object ở chân enemy)")]
    public Transform groundCheck;
    [Tooltip("Bán kính check ground")]
    public float groundCheckRadius = 0.2f;
    [Tooltip("Layer mặt đất")]
    public LayerMask groundLayer;

    [Tooltip("Transform check tường (child object phía trước)")]
    public Transform wallCheck;
    [Tooltip("Khoảng cách raycast check tường")]
    public float wallCheckDistance = 0.5f;

    // Runtime
    private bool isGrounded = false;
    private bool isTouchingWall = false;
    private int facingDirection = 1; // 1 = phải, -1 = trái
    #endregion

    // ================================================================
    // 7) TUNING & DEBUG
    // ================================================================
    #region Tuning & Debug
    [Header("=== Debug ===")]
    [Tooltip("Bật/tắt vẽ Gizmos trong Editor")]
    public bool drawGizmos = true;

    [Tooltip("Màu gizmo tầm phát hiện")]
    public Color gizmoDetectColor = Color.red;
    [Tooltip("Màu gizmo tầm mất target")]
    public Color gizmoLoseColor = new Color(1f, 0.5f, 0f, 0.5f); // Cam
    [Tooltip("Màu gizmo tầm attack")]
    public Color gizmoAttackColor = Color.cyan;
    [Tooltip("Màu gizmo tuần tra")]
    public Color gizmoPatrolColor = Color.yellow;
    #endregion

    // ================================================================
    // ENUM STATE
    // ================================================================
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Hit,
        Stun,
        Dead
    }

    // ================================================================
    // UNITY LIFECYCLE
    // ================================================================

    void Awake()
    {
        // Cache components nếu chưa gán trong Inspector
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
        if (health == null) health = GetComponent<HallokinHealth>();
        if (attack == null) attack = GetComponent<HallokinAttack>();
        if (mainCollider == null) mainCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        startPosition = transform.position;

        // Tự tìm Player nếu chưa gán
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Xác định hướng ban đầu dựa trên localScale
        if (transform.localScale.x < 0)
        {
            facingRight = false;
            facingDirection = -1;
        }

        // Đăng ký events từ Health
        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnHit += HandleHit;
            health.OnStunStart += HandleStunStart;
            health.OnStunEnd += HandleStunEnd;
        }

        // Bắt đầu ở trạng thái Patrol
        ChangeState(EnemyState.Patrol);
    }

    void OnDestroy()
    {
        // Hủy đăng ký events
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnHit -= HandleHit;
            health.OnStunStart -= HandleStunStart;
            health.OnStunEnd -= HandleStunEnd;
        }
    }

    void Update()
    {
        // Nếu đã chết → không làm gì
        if (currentState == EnemyState.Dead) return;

        // Nếu Health báo dead (phòng trường hợp)
        if (health != null && health.isDead)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Nếu đang bị khống chế → không chuyển state
        if (health != null && health.IsIncapacitated())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Nếu đang thực hiện action (attack anim) → chờ xong
        if (isPerformingAction) return;

        // Check surroundings
        CheckSurroundings();

        // Detection check (theo interval để tối ưu)
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = checkInterval;
            targetDetected = CheckForTarget();
        }

        // Timers
        if (attackDecisionTimer > 0f) attackDecisionTimer -= Time.deltaTime;
        stateTimer -= Time.deltaTime;

        // State machine
        switch (currentState)
        {
            case EnemyState.Idle:
                State_Idle();
                break;
            case EnemyState.Patrol:
                State_Patrol();
                break;
            case EnemyState.Chase:
                State_Chase();
                break;
            case EnemyState.Attack:
                State_Attack();
                break;
        }

        UpdateAnimations();
    }

    // ================================================================
    // STATE IMPLEMENTATIONS
    // ================================================================

    #region --- State: Idle ---
    private void State_Idle()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (targetDetected)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Chờ hết waitTimer rồi chuyển sang Patrol
        if (stateTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }
    #endregion

    #region --- State: Patrol ---
    private void State_Patrol()
    {
        // Nếu phát hiện target → đuổi
        if (targetDetected)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Nếu đang chờ tại điểm patrol
        if (waitingAtPoint)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waitingAtPoint = false;
                AdvancePatrolIndex();
            }
            return;
        }

        // Có patrol points?
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            PatrolWithPoints();
        }
        else
        {
            PatrolAutoDistance();
        }
    }

    /// <summary>
    /// Tuần tra theo danh sách patrolPoints.
    /// </summary>
    private void PatrolWithPoints()
    {
        if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;

        Transform target = patrolPoints[currentPatrolIndex];
        if (target == null) return;

        float distToPoint = Vector2.Distance(transform.position, target.position);
        if (distToPoint < 0.3f)
        {
            // Đến nơi → chờ
            waitingAtPoint = true;
            waitTimer = waitAtPointTime;
            return;
        }

        // Di chuyển tới điểm tuần tra
        float dir = Mathf.Sign(target.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);

        // Xoay hướng
        if (dir > 0 && !facingRight) Flip();
        else if (dir < 0 && facingRight) Flip();
    }

    /// <summary>
    /// Tuần tra qua lại quanh startPosition theo patrolDistance (không cần patrol points).
    /// </summary>
    private void PatrolAutoDistance()
    {
        rb.linearVelocity = new Vector2(facingDirection * moveSpeed, rb.linearVelocity.y);

        bool needFlip = false;

        // Chạm tường → đổi hướng
        if (isTouchingWall) needFlip = true;

        // Sắp hết đất → đổi hướng
        if (isGrounded && !IsGroundAhead()) needFlip = true;

        // Đi quá xa điểm xuất phát → đổi hướng
        float distFromStart = Mathf.Abs(transform.position.x - startPosition.x);
        if (distFromStart >= patrolDistance) needFlip = true;

        if (needFlip) Flip();
    }

    private void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (loopPatrol)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Ping-pong
            currentPatrolIndex += patrolDirection;
            if (currentPatrolIndex >= patrolPoints.Length || currentPatrolIndex < 0)
            {
                patrolDirection *= -1;
                currentPatrolIndex += patrolDirection * 2;
                currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, patrolPoints.Length - 1);
            }
        }
    }
    #endregion

    #region --- State: Chase ---
    private void State_Chase()
    {
        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // Mất mục tiêu
        if (!targetDetected)
        {
            disengageTimer -= Time.deltaTime;
            if (disengageTimer <= 0f)
            {
                ChangeState(EnemyState.Patrol);
                return;
            }
        }
        else
        {
            disengageTimer = disengageDelay;
        }

        // Đã trong tầm attack → chuyển sang Attack
        if (distToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Quá xa → mất target
        if (distToPlayer > loseRange)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // Di chuyển tới Player
        FaceTarget();

        if (distToPlayer > stopDistance)
        {
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }
    #endregion

    #region --- State: Attack ---
    private void State_Attack()
    {
        if (player == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // Target ra khỏi tầm attack → đuổi tiếp
        if (distToPlayer > attackRange * 1.2f)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Dừng lại khi đánh
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Quay về phía target
        FaceTarget();

        // Thử đánh (nếu hết cooldown)
        if (attackDecisionTimer <= 0f)
        {
            if (attack != null && attack.TryAttack())
            {
                attackDecisionTimer = attackDecisionCooldown;
                // isPerformingAction = true; // Bật nếu muốn chặn chuyển state khi đang đánh
            }
        }
    }
    #endregion

    // ================================================================
    // HEALTH EVENT HANDLERS
    // ================================================================

    #region --- Health Event Handlers ---
    private void HandleDeath()
    {
        ChangeState(EnemyState.Dead);
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleHit()
    {
        // Có thể thêm logic phản ứng khi bị đánh (interrupt attack, v.v.)
        // isPerformingAction = false; // Cho phép interrupt nếu muốn
    }

    private void HandleStunStart()
    {
        ChangeState(EnemyState.Stun);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void HandleStunEnd()
    {
        if (currentState == EnemyState.Stun)
        {
            ChangeState(EnemyState.Patrol);
        }
    }
    #endregion

    // ================================================================
    // HELPER METHODS
    // ================================================================

    #region --- Detection ---
    /// <summary>
    /// Kiểm tra có target (Player) trong tầm phát hiện không.
    /// Chỉ kích hoạt khi Player đang tấn công (giữ logic cũ).
    /// Bỏ điều kiện PlayerAttack.isAttacking nếu muốn enemy luôn phát hiện.
    /// </summary>
    private bool CheckForTarget()
    {
        if (player == null) return false;

        // Chỉ phát hiện khi Player đang tấn công (logic cũ)
        if (!PlayerAttack.isAttacking) return false;

        float dist = Vector2.Distance(transform.position, player.position);

        // Nếu đang đuổi → dùng loseRange, nếu chưa → dùng detectRange
        float range = (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
            ? loseRange
            : detectRange;

        if (dist > range) return false;

        // FOV check (nếu < 360)
        if (fieldOfView < 360f)
        {
            Vector2 dirToTarget = (player.position - transform.position).normalized;
            Vector2 faceDir = facingRight ? Vector2.right : Vector2.left;
            float angle = Vector2.Angle(faceDir, dirToTarget);
            if (angle > fieldOfView * 0.5f) return false;
        }

        return true;
    }
    #endregion

    #region --- Environment Checks ---
    private void CheckSurroundings()
    {
        // Check mặt đất
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // Check tường phía trước
        if (wallCheck != null)
        {
            isTouchingWall = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDirection, wallCheckDistance, groundLayer);
        }
    }

    /// <summary>
    /// Kiểm tra phía trước còn đất không (Raycast bắn xuống).
    /// </summary>
    private bool IsGroundAhead()
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(facingDirection * 0.7f, 0f);
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, 1.5f, groundLayer);
        return hit.collider != null;
    }
    #endregion

    #region --- Flip / Face Target ---
    /// <summary>
    /// Đổi hướng nhìn bằng cách đảo localScale.x
    /// </summary>
    private void Flip()
    {
        facingDirection *= -1;
        facingRight = !facingRight;

        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    /// <summary>
    /// Xoay mặt về phía target (Player).
    /// </summary>
    private void FaceTarget()
    {
        if (player == null) return;
        float dir = player.position.x - transform.position.x;

        if (dir > 0.1f && !facingRight) Flip();
        else if (dir < -0.1f && facingRight) Flip();
    }
    #endregion

    #region --- State Machine Helpers ---
    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Exit logic
        switch (currentState)
        {
            case EnemyState.Chase:
                disengageTimer = 0f;
                break;
        }

        currentState = newState;

        // Enter logic
        switch (newState)
        {
            case EnemyState.Idle:
                stateTimer = waitAtPointTime;
                break;
            case EnemyState.Patrol:
                waitingAtPoint = false;
                break;
            case EnemyState.Chase:
                disengageTimer = disengageDelay;
                break;
            case EnemyState.Attack:
                break;
            case EnemyState.Dead:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }
    #endregion

    #region --- Animation ---
    private void UpdateAnimations()
    {
        if (anim == null) return;

        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        anim.SetBool("isRunning", isMoving);
    }
    #endregion

    // ================================================================
    // GIZMOS (Debug)
    // ================================================================

    #region --- Gizmos ---
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Vẽ tầm phát hiện
        Gizmos.color = gizmoDetectColor;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // Vẽ tầm mất target
        Gizmos.color = gizmoLoseColor;
        Gizmos.DrawWireSphere(transform.position, loseRange);

        // Vẽ tầm tấn công (combat decision)
        Gizmos.color = gizmoAttackColor;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Vẽ phạm vi tuần tra
        Gizmos.color = gizmoPatrolColor;
        Vector2 origin = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Gizmos.DrawLine(
            new Vector3(origin.x - patrolDistance, transform.position.y, 0),
            new Vector3(origin.x + patrolDistance, transform.position.y, 0)
        );

        // Vẽ patrol points
        if (patrolPoints != null)
        {
            Gizmos.color = Color.magenta;
            foreach (var point in patrolPoints)
            {
                if (point != null)
                    Gizmos.DrawSphere(point.position, 0.2f);
            }
        }

        // Vẽ ground check
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Vẽ wall check
        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            int dir = Application.isPlaying ? facingDirection : (transform.localScale.x > 0 ? 1 : -1);
            Gizmos.DrawLine(wallCheck.position, (Vector2)wallCheck.position + Vector2.right * dir * wallCheckDistance);
        }

        // Vẽ FOV (nếu < 360)
        if (fieldOfView < 360f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 faceDir3 = facingRight ? Vector3.right : Vector3.left;
            float halfFOV = fieldOfView * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0, 0, halfFOV) * faceDir3;
            Vector3 rightDir = Quaternion.Euler(0, 0, -halfFOV) * faceDir3;
            Gizmos.DrawLine(transform.position, transform.position + leftDir * detectRange);
            Gizmos.DrawLine(transform.position, transform.position + rightDir * detectRange);
        }
    }
    #endregion
}
