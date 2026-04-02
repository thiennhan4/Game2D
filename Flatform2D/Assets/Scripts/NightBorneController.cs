using UnityEngine;

/// <summary>
/// NightBorneController – Di chuyển patrol đơn giản.
/// Logic:
///   1. Patrol: đi patrolStepDistance unit → dừng (chờ patrolWaitTime) → quay đầu → đi tiếp.
///   2. Nếu gặp tường/mép đất phía trước → quay đầu ngay.
///   3. Enemy khác → đi xuyên qua (ray không detect Enemy layer).
///   4. Player trong attackRange → NightBorneAttack tự xử lý (script này chỉ lo di chuyển).
///
/// Attach to: NightBorne root GameObject.
/// Required: Rigidbody2D, Animator, NightBorneHealth, NightBorneAttack.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NightBorneController : MonoBehaviour
{
    #region ======= SERIALIZED FIELDS =======

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;

    [Header("Patrol")]
    [Tooltip("Mỗi lần đi bao nhiêu unit rồi dừng quay đầu")]
    [SerializeField] private float patrolStepDistance = 5f;
    [Tooltip("Thời gian chờ trước khi đi tiếp (giây)")]
    [SerializeField] private float patrolWaitTime = 2f;
    [Tooltip("Hướng patrol ban đầu: true = phải, false = trái")]
    [SerializeField] private bool startMoveRight = true;

    [Header("Obstacle Detection")]
    [Tooltip("Layer dùng để phát hiện tường/vật cản (KHÔNG bao gồm Enemy layer)")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("Khoảng cách ray phát hiện tường phía trước")]
    [SerializeField] private float wallCheckDistance = 0.5f;
    [Tooltip("Offset từ chân lên để bắn ray ngang (tránh raycast vào ground)")]
    [SerializeField] private float wallCheckYOffset = 0.5f;
    [Tooltip("Bật kiểm tra mép đất — quay lại nếu phía trước không có ground")]
    [SerializeField] private bool checkGroundEdge = true;
    [Tooltip("Khoảng cách ray xuống để kiểm tra mép đất")]
    [SerializeField] private float groundCheckDistance = 1.5f;
    [Tooltip("Offset ngang từ chân để bắn ray xuống kiểm tra ground")]
    [SerializeField] private float groundCheckXOffset = 0.6f;
    [SerializeField] private LayerMask groundLayer;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform player;

    [Header("Animation")]
    [Tooltip("Tên parameter trong Animator dùng để chạy")]
    [SerializeField] private string runAnimParam = "isRunning";

    #endregion

    #region ======= PRIVATE STATE =======

    private bool isFacingRight = true;
    private bool isWaiting = false;
    private float waitTimer;

    // Trạng thái di chuyển — dùng để điều khiển animation trong Update
    private bool isPatrolling = false;

    // Patrol step tracking
    private float stepWalked;

    private NightBorneHealth nightBorneHealth;
    private NightBorneAttack nightBorneAttack;

    private int animRunHash;

    // Cache giá trị animation trước đó để tránh gọi SetBool mỗi frame
    private bool lastAnimRunning = false;

    #endregion

    #region ======= PUBLIC PROPERTIES =======

    public bool IsFacingRight => isFacingRight;
    public Transform Player => player;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Awake()
    {
        animRunHash = Animator.StringToHash(runAnimParam);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        nightBorneHealth = GetComponent<NightBorneHealth>();
        nightBorneAttack = GetComponent<NightBorneAttack>();

        isFacingRight = startMoveRight;
        FaceDirection(isFacingRight);
        stepWalked = 0f;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player == null)
        {
            Debug.LogWarning($"[NightBorneController] '{gameObject.name}': Không tìm thấy Player!", this);
        }
    }

    private void Update()
    {
        // Enemy chết → dừng mọi thứ
        if (IsEnemyDead())
        {
            isPatrolling = false;
            SetAnimationRunning(false);
            return;
        }

        // Đang attack → không patrol
        if (nightBorneAttack != null && nightBorneAttack.IsAttacking)
        {
            isPatrolling = false;
            SetAnimationRunning(false);
            return;
        }

        // Xử lý wait timer
        if (isWaiting)
        {
            isPatrolling = false;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                // Quay đầu sau khi chờ xong
                isFacingRight = !isFacingRight;
                FaceDirection(isFacingRight);
                stepWalked = 0f;
            }

            SetAnimationRunning(false);
            return;
        }

        // Đang patrol → animation chạy
        isPatrolling = true;
        SetAnimationRunning(true);
    }

    private void FixedUpdate()
    {
        // Không di chuyển nếu không ở trạng thái patrol
        if (!isPatrolling)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Di chuyển patrol (chỉ physics, không animation)
        Patrol();
    }

    #endregion

    #region ======= MOVEMENT LOGIC =======

    private void Patrol()
    {
        float moveDir = isFacingRight ? 1f : -1f;

        // Kiểm tra tường hoặc mép đất phía trước
        if (IsWallAhead(moveDir) || (checkGroundEdge && !IsGroundAhead(moveDir)))
        {
            // Gặp vật cản → flip quay đầu, reset step, tiếp tục đi luôn
            isFacingRight = !isFacingRight;
            FaceDirection(isFacingRight);
            stepWalked = 0f;
            moveDir = -moveDir; // Đổi hướng di chuyển ngay
        }

        // Di chuyển (chỉ set velocity — animation do Update quản lý)
        rb.linearVelocity = new Vector2(moveDir * patrolSpeed, rb.linearVelocity.y);

        // Tính quãng đường đã đi
        stepWalked += patrolSpeed * Time.fixedDeltaTime;

        // Đã đi đủ stepDistance → dừng lại chờ
        if (stepWalked >= patrolStepDistance)
        {
            StartWaiting();
        }
    }

    #endregion

    #region ======= HELPERS =======

    private void StartWaiting()
    {
        isWaiting = true;
        isPatrolling = false;
        waitTimer = patrolWaitTime;
        stepWalked = 0f;
        // Dừng velocity ngay lập tức
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }


    public void FaceDirection(bool faceRight)
    {
        // Xác định hướng hiện tại dựa trên localScale (không dùng isFacingRight để tránh xung đột)
        bool currentlyFacingRight = transform.localScale.x > 0f;
        if (currentlyFacingRight != faceRight)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (faceRight ? 1f : -1f);
            transform.localScale = scale;
        }
    }


    private void SetAnimationRunning(bool running)
    {
        if (anim == null) return;
        if (lastAnimRunning == running) return; // Giá trị không đổi → bỏ qua
        lastAnimRunning = running;
        anim.SetBool(animRunHash, running);
    }

    private bool IsEnemyDead()
    {
        if (nightBorneHealth != null) return nightBorneHealth.IsDead;
        return false;
    }

    #endregion

    #region ======= WALL & GROUND DETECTION =======


    private bool IsWallAhead(float moveDir)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * wallCheckYOffset;
        Vector2 direction = moveDir > 0f ? Vector2.right : Vector2.left;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance, obstacleLayer);

#if UNITY_EDITOR
        Debug.DrawRay(origin, direction * wallCheckDistance,
            hit.collider != null ? Color.red : Color.green);
#endif

        return hit.collider != null;
    }


    private bool IsGroundAhead(float moveDir)
    {
        float xOffset = moveDir > 0f ? groundCheckXOffset : -groundCheckXOffset;
        Vector2 origin = (Vector2)transform.position + new Vector2(xOffset, 0.1f);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);

#if UNITY_EDITOR
        Debug.DrawRay(origin, Vector2.down * groundCheckDistance,
            hit.collider != null ? Color.cyan : Color.red);
#endif

        return hit.collider != null;
    }

    #endregion

    #region ======= GIZMOS =======

    private void OnDrawGizmosSelected()
    {
        // Wall check ray
        float dir = isFacingRight ? 1f : -1f;
        Vector3 wallOrigin = transform.position + Vector3.up * wallCheckYOffset;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + new Vector3(dir * wallCheckDistance, 0f, 0f));

        // Ground edge check ray
        if (checkGroundEdge)
        {
            float xOff = dir * groundCheckXOffset;
            Vector3 groundOrigin = transform.position + new Vector3(xOff, 0.1f, 0f);
            Gizmos.color = new Color(1f, 0.5f, 0f); // orange
            Gizmos.DrawLine(groundOrigin, groundOrigin + Vector3.down * groundCheckDistance);
        }
    }

    #endregion
}