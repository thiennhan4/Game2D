using UnityEngine;

/// <summary>
/// AI Controller for Bringer enemy: Patrol / Chase / Attack / Dead states.
/// Attach to: Bringer enemy root GameObject.
/// Required components: Rigidbody2D, EnemyStats, Animator, SpriteRenderer.
/// Required child objects:
///   - GroundCheckPoint: empty child at the bottom-front edge (for ground detection)
/// Inspector setup:
///   - groundLayer: set to "Ground"
///   - anim, spriteRenderer: auto-fetched if not assigned
///   - Player must have tag "Player" for auto-detection
/// Animator Parameters: Run (Bool).
/// Works with: EnemyStats (health/death), BringerAttack (damage dealing).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BringerController : MonoBehaviour
{
    private enum EnemyState { Patrol, Chase, Attack, Dead }

    [Header("Patrol")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float moveRange = 3f;
    [SerializeField] private bool startMoveRight = false;

    [Header("Chase")]
    [SerializeField] private float chaseRange = 6f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation Parameters")]
    [SerializeField] private string runParam = "Run";

    private Rigidbody2D rb;
    private EnemyStats enemyStats;
    private Transform playerTarget;
    private Vector2 startPos;
    private bool movingRight;
    private float flipCooldown;
    private EnemyState currentState = EnemyState.Patrol;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyStats = GetComponent<EnemyStats>();

        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        startPos = transform.position;
        movingRight = startMoveRight;
        UpdateFacingDirection();

        // Auto-find player by tag
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    private void FixedUpdate()
    {
        if (flipCooldown > 0f)
            flipCooldown -= Time.fixedDeltaTime;

        // Dead — stop everything
        if (enemyStats != null && enemyStats.IsDead)
        {
            currentState = EnemyState.Dead;
            StopMove();
            UpdateRunAnimation(false);
            return;
        }

        // Hurt animation playing — pause movement
        if (IsPlayingAnimation("Hurt"))
        {
            StopMove();
            UpdateRunAnimation(false);
            return;
        }

        // Update state based on player distance
        UpdateState();

        // Execute current state behavior
        switch (currentState)
        {
            case EnemyState.Patrol:
                HandlePatrol();
                break;
            case EnemyState.Chase:
                HandleChase();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }
    }

    // =============== STATE TRANSITIONS ===============

    private void UpdateState()
    {
        if (currentState == EnemyState.Dead) return;

        // No player found — just patrol
        if (playerTarget == null)
        {
            currentState = EnemyState.Patrol;
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        if (distToPlayer <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (distToPlayer <= chaseRange)
        {
            currentState = EnemyState.Chase;
        }
        else
        {
            currentState = EnemyState.Patrol;
        }
    }

    // =============== PATROL ===============

    private void HandlePatrol()
    {
        if (flipCooldown <= 0f)
        {
            if (ShouldFlipByRange() || ShouldFlipByNoGround() || ShouldFlipByWall())
            {
                Flip();
            }
        }

        float dir = movingRight ? 1f : -1f;
        Vector2 velocity = rb.linearVelocity;
        velocity.x = dir * moveSpeed;
        rb.linearVelocity = velocity;

        UpdateRunAnimation(true);
    }

    // =============== CHASE ===============

    private void HandleChase()
    {
        if (playerTarget == null) return;

        // Face toward player
        float dirToPlayer = playerTarget.position.x - transform.position.x;
        bool shouldFaceRight = dirToPlayer > 0f;

        if (shouldFaceRight != movingRight && flipCooldown <= 0f)
            Flip();

        // Safety: don't run off edges or into walls while chasing
        if (ShouldFlipByNoGround() || ShouldFlipByWall())
        {
            StopMove();
            UpdateRunAnimation(false);
            return;
        }

        float dir = movingRight ? 1f : -1f;
        Vector2 velocity = rb.linearVelocity;
        velocity.x = dir * chaseSpeed;
        rb.linearVelocity = velocity;

        UpdateRunAnimation(true);
    }

    // =============== ATTACK ===============

    private void HandleAttack()
    {
        StopMove();
        UpdateRunAnimation(false);

        // Face toward player even in attack state
        if (playerTarget != null && flipCooldown <= 0f)
        {
            float dirToPlayer = playerTarget.position.x - transform.position.x;
            bool shouldFaceRight = dirToPlayer > 0f;
            if (shouldFaceRight != movingRight)
                Flip();
        }

        // Actual attack logic is handled by BringerAttack component
    }

    // =============== HELPERS ===============

    private bool ShouldFlipByRange()
    {
        if (movingRight && transform.position.x >= startPos.x + moveRange)
            return true;

        if (!movingRight && transform.position.x <= startPos.x - moveRange)
            return true;

        return false;
    }

    private bool ShouldFlipByNoGround()
    {
        if (groundCheckPoint == null) return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            groundCheckPoint.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        foreach (var hit in hits)
        {
            // Ignore self and trigger colliders
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return false; // Ground found
            }
        }

        return true; // No ground
    }

    private bool ShouldFlipByWall()
    {
        float dir = movingRight ? 1f : -1f;
        Vector2 checkPos = groundCheckPoint != null
            ? (Vector2)groundCheckPoint.position
            : (Vector2)transform.position;
        checkPos.y += 0.5f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            checkPos,
            Vector2.right * dir,
            0.5f,
            groundLayer
        );

        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true; // Wall found
            }
        }

        return false;
    }

    private bool IsPlayingAnimation(string stateName)
    {
        if (anim == null) return false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName);
    }

    private void Flip()
    {
        movingRight = !movingRight;
        flipCooldown = 0.2f;
        UpdateFacingDirection();
    }

    private void UpdateFacingDirection()
    {
        // Default sprite faces LEFT: scale.x > 0 = left, scale.x < 0 = right
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (movingRight ? -1f : 1f);
        transform.localScale = scale;
    }

    private void UpdateRunAnimation(bool isRunning)
    {
        if (anim == null) return;
        anim.SetBool(runParam, isRunning);
    }

    private void StopMove()
    {
        if (rb != null)
        {
            Vector2 velocity = rb.linearVelocity;
            velocity.x = 0f;
            rb.linearVelocity = velocity;
        }
    }

    private void OnDisable()
    {
        StopMove();
    }

    // =============== GIZMOS ===============

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? (Vector3)startPos : transform.position;

        // Patrol range (yellow)
        Gizmos.color = Color.yellow;
        Vector3 left = new Vector3(center.x - moveRange, center.y + 0.5f, center.z);
        Vector3 right = new Vector3(center.x + moveRange, center.y + 0.5f, center.z);
        Gizmos.DrawLine(left, right);
        Gizmos.DrawSphere(left, 0.08f);
        Gizmos.DrawSphere(right, 0.08f);

        // Chase range (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, chaseRange);

        // Attack range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRange);

        // Ground check ray (green)
        if (groundCheckPoint != null)
        {
            Vector3 previewWorld = groundCheckPoint.position;

            if (!Application.isPlaying)
            {
                Vector3 previewLocal = groundCheckPoint.localPosition;
                previewLocal.x = Mathf.Abs(previewLocal.x) * (startMoveRight ? -1f : 1f);
                previewWorld = transform.TransformPoint(previewLocal);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawLine(previewWorld, previewWorld + Vector3.down * groundCheckDistance);
            Gizmos.DrawSphere(previewWorld + Vector3.down * groundCheckDistance, 0.06f);

            // Wall check ray (red)
            Gizmos.color = Color.red;
            float previewDir = Application.isPlaying
                ? (movingRight ? 1f : -1f)
                : (startMoveRight ? 1f : -1f);
            Vector3 wallCheckPos = previewWorld + Vector3.up * 0.5f;
            Gizmos.DrawLine(wallCheckPos, wallCheckPos + Vector3.right * previewDir * 0.5f);
        }
    }
}