using UnityEngine;
public class BatController : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float patrolDistance = 3f;
    public Animator anim;
    public string moveBoolName = "Run";

    private Rigidbody2D rb;
    private Vector2 startPosition;
    private bool movingRight;
    [HideInInspector] public bool facingRight;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
    }

    void Start()
    {
        startPosition = transform.position;

        // Tự detect hướng mặt từ scale.x của sprite
        // scale.x > 0 → sprite nhìn trái (mặc định) → facingRight = false
        // scale.x < 0 → sprite đã flip → facingRight = true
        facingRight = transform.localScale.x < 0f;

        // Bắt đầu di chuyển về hướng đang nhìn (phía trước)
        movingRight = facingRight;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    void FixedUpdate()
    {
        Patrol();
    }

    void Update()
    {
        if (anim != null)
        {
            bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.05f;
            anim.SetBool(moveBoolName, isMoving);
        }
    }

    void Patrol()
    {
        float leftLimit = startPosition.x - patrolDistance;
        float rightLimit = startPosition.x + patrolDistance;

        // Đến biên phải → đổi hướng sang trái + flip
        if (movingRight && transform.position.x >= rightLimit)
        {
            movingRight = false;
            Flip();
        }
        // Đến biên trái → đổi hướng sang phải + flip
        else if (!movingRight && transform.position.x <= leftLimit)
        {
            movingRight = true;
            Flip();
        }

        // Di chuyển theo hướng hiện tại sau khi đã check biên
        float direction = movingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
    }

    void Flip()
    {
        facingRight = !facingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }
}
