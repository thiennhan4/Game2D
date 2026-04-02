using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 8f;

    [Header("Double Jump")]
    [Tooltip("Số lần nhảy thêm khi đang trên không (1 = double jump, 2 = triple jump)")]
    [SerializeField] private int extraJumpCount = 1;
    [Tooltip("Lực nhảy cho lần nhảy thêm (để 0 = dùng jumpForce)")]
    [SerializeField] private float extraJumpForce = 0f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator anim;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [Tooltip("Kéo Game Object Effect hoặc Prefab vào đây")]
    [SerializeField] private GameObject dashEffect;
    [Tooltip("Phím dùng để Dash (mặc định Left Shift)")]
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

    private bool isDashing;
    private bool canDash = true;

    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private bool isFacingRight = true;
    private float moveInput;
    private bool jumpInput;

    // Double Jump tracking
    private int jumpsRemaining;
    private bool hasJumpedThisFrame;

    // Run sound timing
    private float runStepTimer;
    [SerializeField] private float runStepInterval = 0.35f;

    // Cached reference to check death state
    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        // Block all input when dead
        if (playerHealth != null && playerHealth.IsDead)
        {
            moveInput = 0f;
            jumpInput = false;
            UpdateAnimationStates();
            return;
        }

        if (isDashing) return; // Chặn input di chuyển khi đang Dash

        moveInput = Input.GetAxisRaw("Horizontal");

        // GetKeyDown chỉ bắt được trong Update, lưu lại cho FixedUpdate xử lý
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpInput = true;
        }

        if (Input.GetKeyDown(dashKey) && canDash)
        {
            StartCoroutine(DashRoutine());
        }

        UpdateAnimationStates();
    }

    private void FixedUpdate()
    {
        // No physics movement when dead
        if (playerHealth != null && playerHealth.IsDead) return;
        if (isDashing) return; // Chặn code di chuyển đè lên lực Dash

        HandleMovement();
        HandleJump();
    }

    private void HandleMovement()
    {
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        if (moveInput > 0f && !isFacingRight)
            Flip();
        else if (moveInput < 0f && isFacingRight)
            Flip();

        if (Mathf.Abs(moveInput) > 0.1f && isGrounded)
        {
            runStepTimer -= Time.fixedDeltaTime;
            if (runStepTimer <= 0f)
            {
                if (AudioManager.instance != null)
                    AudioManager.instance.PlaySFX(AudioManager.instance.Run);
                runStepTimer = runStepInterval;
            }
        }
        else
        {
            runStepTimer = 0f;
        }
    }

    private void HandleJump()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        hasJumpedThisFrame = false;

        // Vừa chạm đất → reset số lần nhảy
        if (isGrounded && !wasGroundedLastFrame)
        {
            jumpsRemaining = extraJumpCount;
        }
        // Đang trên đất → luôn giữ đủ jump count
        if (isGrounded)
        {
            jumpsRemaining = extraJumpCount;
        }

        if (jumpInput)
        {
            float actualJumpForce = jumpForce;

            if (isGrounded)
            {
                // Nhảy từ mặt đất (normal jump)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, actualJumpForce);
                hasJumpedThisFrame = true;
                if (AudioManager.instance != null) AudioManager.instance.PlaySFX(AudioManager.instance.jump);
            }
            else if (jumpsRemaining > 0)
            {
                // Nhảy trên không (double jump / extra jump)
                actualJumpForce = (extraJumpForce > 0f) ? extraJumpForce : jumpForce;

                // Reset velocity Y trước khi nhảy → đảm bảo lực nhảy nhất quán
                // dù đang rơi hay đang bay lên
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, actualJumpForce);
                jumpsRemaining--;
                hasJumpedThisFrame = true;
                if (AudioManager.instance != null) AudioManager.instance.PlaySFX(AudioManager.instance.jump);

                // Trigger animation double jump (nếu có)
                if (anim != null)
                {
                    anim.SetTrigger("DoubleJump");
                }
            }
        }

        wasGroundedLastFrame = isGrounded;

        // Consume jump input after physics tick
        jumpInput = false;
    }

    private void UpdateAnimationStates()
    {
        if (anim == null) return;

        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        anim.SetBool("isRunning", isRunning);

        bool isJumping = !isGrounded;
        anim.SetBool("isJumping", isJumping);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        // Tắt trọng lực để bay thẳng
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Tính hướng Dash
        float dashDirection = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);

        // Sinh ra Effect tại vị trí Player (hoặc bật Effect nếu xài chung)
        if (dashEffect != null)
        {
            GameObject effect = Instantiate(dashEffect, transform.position, Quaternion.identity);
            
            // Xoay effect theo hướng nhân vật (nếu Effect có hướng)
            if (!isFacingRight)
            {
                Vector3 effectScale = effect.transform.localScale;
                effectScale.x *= -1f;
                effect.transform.localScale = effectScale;
            }
            
            // Tự động hủy effect sau khi bay xong 1 lúc (thêm 0.3s để particles kịp tan)
            Destroy(effect, dashDuration + 0.3f);
        }

        // Nếu bạn có Animation tên là Dash, hãy mở comment dòng dưới:
        // if (anim != null) anim.SetTrigger("Dash"); 

        // Đợi hết thời gian lướt
        yield return new WaitForSeconds(dashDuration);

        // Trả lại trọng lực và dừng lướt
        rb.gravityScale = originalGravity;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        isDashing = false;

        // Đợi hồi chiêu
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}
