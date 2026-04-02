using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
public class AcanerController : MonoBehaviour
{
    private enum State { Patrol, Chase, Returning }

    #region ======= SERIALIZED FIELDS =======

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float loseRange = 9f;

    [Header("Patrol")]
    [SerializeField] private float patrolRange = 5f;
    [SerializeField] private float patrolWaitTime = 2f;
    [Tooltip("Hướng patrol ban đầu: true = phải, false = trái")]
    [SerializeField] private bool startMoveRight = true;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform player;

    #endregion

    #region ======= PRIVATE STATE =======

    private State currentState = State.Patrol;
    private bool isFacingRight = true;
    private bool isWaiting = false;
    private float waitTimer;

    private Vector2 startPosition;
    private float leftBoundX;
    private float rightBoundX;
    private bool movingToRight = true;

    private AcanerHealth acanerHealth;
    private AcanerAttack acanerAttack;

    // Cache Animator hash
    [Header("Animation")]
    [Tooltip("Tên parameter trong Animator dùng để chạy (Ví dụ: isRunning)")]
    [SerializeField] private string runAnimParam = "isRunning"; // <--- Bật sẵn "isRunning"

    private int animRunHash;

    // Ngưỡng "đã tới đích" — giảm nhỏ để snap chính xác, tránh rung
    private const float ARRIVE_THRESHOLD = 0.1f;

    // Chống kẹt tường
    private Vector2 lastPosition;
    private float stuckTimer;
    private const float STUCK_DELAY = 0.2f;

    #endregion

    #region ======= PUBLIC PROPERTIES =======

    /// <summary> Hướng nhìn hiện tại. AcanerAttack dùng để xác định firePoint trái/phải. </summary>
    public bool IsFacingRight => isFacingRight;

    /// <summary> Transform của player (cho AcanerAttack dùng để tính hướng bắn). </summary>
    public Transform Player => player;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Awake()
    {
        // Khởi tạo hash animation thay cho AnimIsRunning
        animRunHash = Animator.StringToHash(runAnimParam);

        // Auto-find components
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        acanerHealth = GetComponent<AcanerHealth>();
        acanerAttack = GetComponent<AcanerAttack>();

        // Tính patrol bounds
        startPosition = transform.position;
        leftBoundX = startPosition.x - patrolRange;
        rightBoundX = startPosition.x + patrolRange;

        // Set hướng patrol ban đầu
        movingToRight = startMoveRight;
        FaceDirection(movingToRight);

        // Auto-find player
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Cảnh báo nếu không tìm thấy player
        if (player == null)
            Debug.LogWarning($"[AcanerController] '{gameObject.name}': Không tìm thấy Player! Hãy gán hoặc đặt tag 'Player'.", this);
    }

    private void Update()
    {
        // Enemy chết → dừng
        if (IsEnemyDead())
        {
            StopMovement();
            return;
        }

        // Đang attack → dừng di chuyển nhưng vẫn face đúng hướng
        if (acanerAttack != null && acanerAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer();

        // === STATE TRANSITIONS (chỉ xử lý ở Update) ===
        switch (currentState)
        {
            case State.Patrol:
                if (distanceToPlayer <= detectionRange)
                    EnterChase();
                break;

            case State.Chase:
                if (distanceToPlayer > loseRange)
                    EnterReturning();
                break;

            case State.Returning:
                if (distanceToPlayer <= detectionRange)
                    EnterChase();
                break;
        }

        // Wait timer cho Patrol (dùng Time.deltaTime nên để ở Update)
        if (currentState == State.Patrol && isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                movingToRight = !movingToRight;
                FaceDirection(movingToRight);
            }
        }
    }

    private void FixedUpdate()
    {
        // Enemy chết → dừng
        if (IsEnemyDead())
        {
            StopMovement();
            return;
        }

        // Đang attack → dừng
        if (acanerAttack != null && acanerAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        // === MOVEMENT (ở FixedUpdate để Rigidbody2D chạy mượt) ===
        switch (currentState)
        {
            case State.Patrol:
                Patrol();
                break;

            case State.Chase:
                Chase();
                break;

            case State.Returning:
                Returning();
                break;
        }
    }

    #endregion

    #region ======= STATE ENTER =======

    private void EnterChase()
    {
        currentState = State.Chase;
        isWaiting = false;
    }

    private void EnterReturning()
    {
        currentState = State.Returning;
        isWaiting = false;
    }

    private void EnterPatrol()
    {
        currentState = State.Patrol;
        isWaiting = false;

        // Chọn hướng patrol hợp lý sau khi return (đi tới bound gần hơn)
        float distToLeft = Mathf.Abs(transform.position.x - leftBoundX);
        float distToRight = Mathf.Abs(transform.position.x - rightBoundX);
        movingToRight = distToRight < distToLeft;
        FaceDirection(movingToRight);
    }

    #endregion

    #region ======= MOVEMENT LOGIC =======

    private void Patrol()
    {
        if (isWaiting)
        {
            StopMovement();
            return;
        }

        float targetX = movingToRight ? rightBoundX : leftBoundX;
        bool arrived = MoveToTargetX(targetX, patrolSpeed);

        if (arrived)
        {
            StartWaiting();
        }
    }

    private void Chase()
    {
        if (player == null)
        {
            StopMovement();
            return;
        }

        float deltaX = player.position.x - transform.position.x;

        // Quá sát player → dừng lại tránh rung
        if (Mathf.Abs(deltaX) <= ARRIVE_THRESHOLD)
        {
            StopMovement();
            return;
        }

        float moveDir = Mathf.Sign(deltaX);
        FaceDirection(moveDir > 0f);
        rb.linearVelocity = new Vector2(moveDir * chaseSpeed, rb.linearVelocity.y);
        UpdateAnimation(true);
    }

    private void Returning()
    {
        bool arrived = MoveToTargetX(startPosition.x, patrolSpeed);

        if (arrived)
        {
            EnterPatrol();
        }
    }
    private bool MoveToTargetX(float targetX, float speed)
    {
        float currentX = transform.position.x;
        float delta = targetX - currentX;

        if (Mathf.Abs(delta) <= ARRIVE_THRESHOLD)
        {
            // Snap đúng vị trí đích
            Vector3 pos = transform.position;
            pos.x = targetX;
            transform.position = pos;

            stuckTimer = 0f;
            StopMovement();
            return true;
        }

        // --- Logic Chống Kẹt Tường (hết bị đơ) ---
        if (Mathf.Abs(currentX - lastPosition.x) < 0.01f) // Nếu đang di chuyển mà X không đổi
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_DELAY) // Kẹt quá lâu
            {
                stuckTimer = 0f;
                StopMovement();
                return true; // Ép quay đầu
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPosition = transform.position;
        // ----------------------------------------

        float moveDir = Mathf.Sign(delta);
        FaceDirection(moveDir > 0f);
        rb.linearVelocity = new Vector2(moveDir * speed, rb.linearVelocity.y);
        UpdateAnimation(true);

        return false;
    }

    #endregion

    #region ======= FLIP & HELPERS =======

    private void StartWaiting()
    {
        isWaiting = true;
        waitTimer = patrolWaitTime;
        StopMovement();
    }
    public void FaceDirection(bool faceRight)
    {
        if (isFacingRight == faceRight) return;
        Flip();
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private void StopMovement()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        UpdateAnimation(false);
    }

    private float GetDistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    private void UpdateAnimation(bool isRunning)
    {
        if (anim == null) return;
        anim.SetBool(animRunHash, isRunning);
    }
    private bool IsEnemyDead()
    {
        if (acanerHealth != null) return acanerHealth.IsDead;
        return false;
    }

    #endregion

    #region ======= GIZMOS =======

    private void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? startPosition : (Vector2)transform.position;

        // Patrol bounds (cyan)
        Gizmos.color = Color.cyan;
        Vector3 left = new Vector3(center.x - patrolRange, transform.position.y, 0f);
        Vector3 right = new Vector3(center.x + patrolRange, transform.position.y, 0f);
        Gizmos.DrawLine(left + Vector3.down * 0.5f, left + Vector3.up * 0.5f);
        Gizmos.DrawLine(right + Vector3.down * 0.5f, right + Vector3.up * 0.5f);
        Gizmos.DrawLine(left, right);

        // Start position (white)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(new Vector3(center.x, transform.position.y, 0f), 0.15f);

        // Detection range (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Lose range (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, loseRange);
    }

    #endregion
}