using System.Collections;
using UnityEngine;

// Định nghĩa các trạng thái của Boss
public enum BossState
{
    Idle,
    Intro,
    Chase,
    Attack,
    Cast,
    Stunned,
    Dead
}

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class SkeletonBossController : MonoBehaviour
{
    [Header("Core References")]
    public Rigidbody2D rb;
    public Animator anim;
    public Transform player;
    public SkeletonBossAttack bossAttack;
    public SkeletonBossHealth bossHealth;
   // public BossPhaseManager phaseManager;

    [Header("Boss Stats")]
    public float moveSpeed = 4f;
    public float chaseRange = 15f;
    public bool isActivated = false;

    [Header("State")]
    public BossState currentState = BossState.Idle;

    // Biến nội bộ
    private bool isFacingRight = true;
    private Coroutine stunCoroutine;

    private void Awake()
    {
        // Tự động gán component nếu chưa kéo thả trong Inspector
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
    }

    private void Start()
    {
        // Tự động tìm player nếu chưa có
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Đăng ký event (nếu BossHealth có event OnDeath)
        // if (bossHealth != null) bossHealth.OnDeath += OnBossDeath;
    }

    private void FixedUpdate()
    {
        // Nếu chưa kích hoạt hoặc đã chết thì bỏ qua mọi hành động
        if (!isActivated || currentState == BossState.Dead)
            return;

        UpdateAnimator();

        // Cập nhật hướng xoay mặt liên tục nếu có thể hành động
        if (CanAct() && currentState != BossState.Intro)
        {
            FacePlayer();
        }

        // Uỷ quyền cho BossAttack tự quyết định skill dựa theo State và Phase
        if (CanAct() && bossAttack != null)
        {
            bossAttack.TryChooseSkill();
        }

        // Xử lý di chuyển
        if (CanMove())
        {
            HandleMovement();
        }
        else
        {
            StopMovement();
        }
    }

    // ==========================================
    // STATE & BEHAVIOR MANAGEMENT
    // ==========================================

    public void SetState(BossState newState)
    {
        // Boss đã chết thì không thể bị ghi đè state khác (bất tử state)
        if (currentState == BossState.Dead) return;

        // Bỏ qua nếu đang gán lại đúng state hiện tại
        if (currentState == newState) return;

        currentState = newState;

        if (newState == BossState.Stunned || newState == BossState.Dead)
        {
            if (bossAttack != null) bossAttack.StopAllSkills();
        }

        // Kích hoạt các Trigger tương ứng khi Boss bước vào State mới
        if (anim != null)
        {
            if (newState == BossState.Attack)
            {
                anim.SetTrigger("Attack");
            }
            else if (newState == BossState.Dead)
            {
                anim.SetTrigger("Death");
            }
            else if (newState == BossState.Stunned)
            {
                // Ngăn hàng chờ (queue) đòn đánh khiến animator lỗi
                anim.ResetTrigger("Attack"); 
                anim.SetTrigger("Hurt"); // Map Stunned -> Hurt
            }
            else if (newState == BossState.Idle || newState == BossState.Chase)
            {
                // Xoá trigger Hurt để tránh kẹt animation Hurt khi bị spam sát thương
                anim.ResetTrigger("Hurt");
                anim.ResetTrigger("Attack");
            }
        }
    }

    private void HandleMovement()
    {
        if (player == null) return;

        float distance = GetDistanceToPlayer();

        // Chỉ đi theo khi rơi vào tầm ngắm
        if (distance <= chaseRange)
        {
            SetState(BossState.Chase);

            // Vector di chuyển ngang
            Vector2 targetPos = new Vector2(player.position.x, rb.position.y);
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }
        else
        {
            SetState(BossState.Idle);
            StopMovement();
        }
    }

    public void StopMovement()
    {
        // Đứt phản lực ngang, giữ nguyên trọng lực (y)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void FacePlayer()
    {
        if (player == null) return;

        // Xoay hướng sprite để luôn nhìn vào player
        if ((player.position.x > transform.position.x && !isFacingRight) ||
            (player.position.x < transform.position.x && isFacingRight))
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }

    // ==========================================
    // UTILITIES & CONDITIONS
    // ==========================================

    public float GetDistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    public bool IsPlayerInRange(float range)
    {
        return GetDistanceToPlayer() <= range;
    }

    public bool CanAct()
    {
        // Boss có thể ra đòn nếu đang rảnh rang (Idle) hoặc đang đuổi theo (Chase)
        return currentState == BossState.Idle || currentState == BossState.Chase;
    }

    public bool CanMove()
    {
        // Không cho phép di chuyển nếu đang làm các hành động khác rườm rà
        return currentState == BossState.Idle || currentState == BossState.Chase;
    }

    // ==========================================
    // SPECIAL ACTIONS & EVENTS
    // ==========================================

    public void ActivateBoss()
    {
        isActivated = true;
        SetState(BossState.Idle);
    }

    public void StartIntro()
    {
        SetState(BossState.Intro);
        StopMovement();
    }

    public void ApplyStun(float duration)
    {
        if (currentState == BossState.Dead) return;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
        }
        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        SetState(BossState.Stunned);
        StopMovement();

        yield return new WaitForSeconds(duration);

        // Hết choáng, phục hồi lại Idle nếu chưa chết
        if (currentState != BossState.Dead)
        {
            SetState(BossState.Idle);
        }
    }

    public void OnBossDeath()
    {
        SetState(BossState.Dead);
        StopMovement();
        

         rb.linearVelocity = Vector2.zero;
         rb.isKinematic = true; 
    }


    private void UpdateAnimator()
    {
        if (anim == null) return;

        // Tính toán xem boss có đang di chuyển thực sự không
        bool isMoving = currentState == BossState.Chase && GetDistanceToPlayer() <= chaseRange;

        // isRunning là kiểu Bool (có hình vuông bên cạnh) nên dùng SetBool
        anim.SetBool("isRunning", isMoving);
    }
}
