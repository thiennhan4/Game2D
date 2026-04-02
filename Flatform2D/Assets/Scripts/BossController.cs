using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossController : MonoBehaviour
{
    private enum State { Patrol, Chase, Returning, Dead }

    #region ======= SERIALIZED FIELDS =======

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;

    [Header("Detection")]
    [Tooltip("Khoảng cách phát hiện player")]
    [SerializeField] private float detectionRange = 7f;
    [Tooltip("Khoảng cách mất dấu player")]
    [SerializeField] private float loseRange = 12f;

    [Header("Patrol")]
    [SerializeField] private float patrolRange = 4f;
    [SerializeField] private float patrolWaitTime = 2f;
    [Tooltip("Hướng patrol ban đầu: true = phải, false = trái")]
    [SerializeField] private bool startMoveRight = true;

    [Header("Patrol Step")]
    [Tooltip("Mỗi lần đi bao nhiêu unit rồi nghỉ")]
    [SerializeField] private float patrolStepDistance = 5f;
    [Tooltip("Bật nếu muốn Boss đi từng chặng rồi nghỉ")]
    [SerializeField] private bool useStepPatrol = true;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform player;

    [Header("Animation")]
    [Tooltip("Tên parameter trong Animator dùng để chạy")]
    [SerializeField] private string runAnimParam = "isRunning";

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

    // Dùng cho patrol từng chặng
    private float currentPatrolTargetX;

    private BossHealth bossHealth;
    private BossAttack bossAttack;

    private int animRunHash;

    private const float ARRIVE_THRESHOLD = 0.1f;

    // Chống kẹt tường
    private Vector2 lastPosition;
    private float stuckTimer;
    private const float STUCK_DELAY = 0.2f;

    #endregion

    #region ======= PUBLIC PROPERTIES =======

    public bool IsFacingRight => isFacingRight;
    public Transform Player => player;
    public bool IsDead => currentState == State.Dead;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Awake()
    {
        animRunHash = Animator.StringToHash(runAnimParam);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        bossHealth = GetComponent<BossHealth>();
        bossAttack = GetComponent<BossAttack>();

        startPosition = transform.position;
        leftBoundX = startPosition.x - patrolRange;
        rightBoundX = startPosition.x + patrolRange;

        movingToRight = startMoveRight;
        FaceDirection(movingToRight);

        if (useStepPatrol)
            SetupNextPatrolStepTarget();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player == null)
        {
            Debug.LogWarning($"[BossController] '{gameObject.name}': Không tìm thấy Player! Hãy gán hoặc đặt tag 'Player'.", this);
        }
    }

    private void Update()
    {
        if (IsEnemyDead())
        {
            StopMovement();
            return;
        }

        // Đang attack → dừng di chuyển
        if (bossAttack != null && bossAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer();

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

        // Xử lý đợi khi patrol
        if (currentState == State.Patrol && isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;

                if (useStepPatrol)
                {
                    SetupNextPatrolStepTarget();
                }
                else
                {
                    movingToRight = !movingToRight;
                    FaceDirection(movingToRight);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsEnemyDead())
        {
            StopMovement();
            return;
        }

        if (bossAttack != null && bossAttack.IsAttacking)
        {
            StopMovement();
            return;
        }

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

        float distToLeft = Mathf.Abs(transform.position.x - leftBoundX);
        float distToRight = Mathf.Abs(transform.position.x - rightBoundX);
        movingToRight = distToRight < distToLeft;
        FaceDirection(movingToRight);

        if (useStepPatrol)
            SetupNextPatrolStepTarget();
    }

    public void EnterDeadState()
    {
        currentState = State.Dead;
        StopMovement();
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

        float targetX;

        if (useStepPatrol)
        {
            targetX = currentPatrolTargetX;
        }
        else
        {
            targetX = movingToRight ? rightBoundX : leftBoundX;
        }

        bool arrived = MoveToTargetX(targetX, patrolSpeed);

        if (arrived)
        {
            if (useStepPatrol)
            {
                bool reachedRightEdge = Mathf.Abs(transform.position.x - rightBoundX) <= ARRIVE_THRESHOLD;
                bool reachedLeftEdge = Mathf.Abs(transform.position.x - leftBoundX) <= ARRIVE_THRESHOLD;

                if (reachedRightEdge || reachedLeftEdge)
                {
                    movingToRight = !movingToRight;
                    FaceDirection(movingToRight);
                }

                StartWaiting();
            }
            else
            {
                StartWaiting();
            }
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
            Vector3 pos = transform.position;
            pos.x = targetX;
            transform.position = pos;

            stuckTimer = 0f;
            StopMovement();
            return true;
        }

        if (Mathf.Abs(currentX - lastPosition.x) < 0.01f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_DELAY)
            {
                stuckTimer = 0f;
                StopMovement();
                return true;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;

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

    private void SetupNextPatrolStepTarget()
    {
        float currentX = transform.position.x;

        if (movingToRight)
            currentPatrolTargetX = Mathf.Min(currentX + patrolStepDistance, rightBoundX);
        else
            currentPatrolTargetX = Mathf.Max(currentX - patrolStepDistance, leftBoundX);
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
        if (bossHealth != null) return bossHealth.IsDead;
        return false;
    }

    #endregion

    #region ======= GIZMOS =======

    private void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? startPosition : (Vector2)transform.position;

        // Patrol range
        Gizmos.color = Color.cyan;
        Vector3 left = new Vector3(center.x - patrolRange, transform.position.y, 0f);
        Vector3 right = new Vector3(center.x + patrolRange, transform.position.y, 0f);
        Gizmos.DrawLine(left + Vector3.down * 0.5f, left + Vector3.up * 0.5f);
        Gizmos.DrawLine(right + Vector3.down * 0.5f, right + Vector3.up * 0.5f);
        Gizmos.DrawLine(left, right);

        // Start position
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(new Vector3(center.x, transform.position.y, 0f), 0.15f);

        // Detection range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Lose range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, loseRange);

        if (Application.isPlaying && useStepPatrol)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(new Vector3(currentPatrolTargetX, transform.position.y, 0f), 0.12f);
        }
    }

    #endregion
}
