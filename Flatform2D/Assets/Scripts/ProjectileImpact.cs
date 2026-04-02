using UnityEngine;

public class ProjectileImpact : MonoBehaviour
{
    [Header("Projectile Settings")]
    [Tooltip("Tốc độ bay của viên đạn")]
    public float speed = 10f;
    
    [Tooltip("Thời gian tự hủy nếu không chạm mục tiêu")]
    public float lifetime = 5f;

    [Header("Effects")]
    [Tooltip("Prefab hiệu ứng khi viên đạn nổ/chạm mục tiêu")]
    public GameObject hitEffectPrefab;

    [Header("Target Settings")]
    [Tooltip("Layer của Player để viên đạn biết đâu là mục tiêu cần gây sát thương")]
    public LayerMask playerLayer;

    private float damage = 55f; 
    private Vector2 moveDirection = Vector2.right;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Tự động xóa viên đạn nếu bay quá lâu không trúng ai
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Hàm này được Boss gọi ngay lúc Instantiate viên đạn để truyền Damage (đã nhân hệ số Phase) và Hướng bay.
    /// </summary>
    public void Setup(float newDamage, Vector2 direction)
    {
        damage = newDamage;
        moveDirection = direction.normalized;

        // Cập nhật lại góc xoay hình ảnh viên đạn sao cho đầu đạn chỉ về hướng bay
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // Nếu Prefab có Rigidbody2D thì vận tốc sẽ được gán bằng Rigidbody
        if (rb != null)
        {
            rb.linearVelocity = moveDirection * speed;
        }
    }

    private void Update()
    {
        // Nếu không xài Rigidbody2D thì nó sẽ bay dựa trên Transform Update
        if (rb == null)
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime, Space.Self);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Bỏ qua các trigger không cần thiết của đối tượng (chỉ gây sát thương vào collider thật)
        if (collision.isTrigger) 
            return; 

        // Bỏ qua nếu chạm phải chính Boss (Enemy) hoặc các đạn/effect khác (impact, aoe) 
        if (collision.CompareTag("Enemy") || collision.CompareTag("impact") || collision.CompareTag("aoe")) 
            return;
            
        // Kiểm tra nếu là Player thì mới bắt đầu xử lý sát thương
        if (collision.CompareTag("Player"))
        {
            // Lấy script và gây sát thương tự nhận diện với IDamageable
            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable == null) damageable = collision.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        // Tạo hiệu ứng kết thúc (nổ, tia lửa...) ngay tại điểm chạm tường hoặc player
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // Cứ đụng tường hoặc Player thì tiêu hủy viên đạn
        Destroy(gameObject);
    }
}
