using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class ProjectileArrow : MonoBehaviour
{
    [Header("Cài đặt va chạm")]
    [Tooltip("Layer tường, đất, hòm... để mũi tên tự hủy khi cắm vào")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Hiệu ứng (Tùy chọn)")]
    [Tooltip("Prefab tia lửa/gỗ văng ra khi đập vô tường")]
    [SerializeField] private GameObject destroyVFX;

    private float damage;
    private GameObject owner;
    private Rigidbody2D rb;
    private bool isDeflected = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Chỉnh cho mũi tên bay thẳng không bị rớt xuống như đá
        rb.gravityScale = 0f;
        // Chỉnh loại va chạm để chống xuyên tường khi bay nhanh
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
    }

    /// <summary>
    /// Được gọi từ AcanerAttack.cs ngay lúc hàm SpawnArrow() chạy.
    /// Dùng Rigidbody2D để điều khiển bay giúp va chạm cực chuẩn.
    /// </summary>
    public void Fire(Vector2 direction, float speed, float attackDamage, GameObject shooter, float lifetime)
    {
        damage = attackDamage;
        owner = shooter;

        // Xoay hình ảnh mũi tên cho khớp với Vector bay tới
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Nạp lực bay (Dùng Velocity của vật lý giúp không bao giờ bị lỗi xuyên vật thể)
        rb.linearVelocity = direction.normalized * speed;

        // Xóa mũi tên nếu nó bay mút chỉ ngoài map không trúng ai
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // TRÁNH LỖI TỰ ĐÂM MÌNH: Nếu là cục Owner bắn ra thì bỏ qua
        if (owner != null && (other.gameObject == owner || other.transform.root.gameObject == owner))
        {
            return;
        }

        // Bỏ qua các Box check vùng nhìn tự do khác (Trigger màng chướng ngại vật)
        if (other.isTrigger && !other.CompareTag("Player"))
        {
            return;
        }

        // 1. NẾU TRÚNG PLAYER
        if (other.CompareTag("Player"))
        {
            // Thử lấy script máu theo interface IDamageable (Tối ưu nhất)
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage);
                DestroyArrow();
                return;
            }

            // Hoặc lấy thẳng PlayerHealth nếu game thiết kế kiểu cũ
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                // Vector knockback = đúng cái chiều mà mũi tên đang bay
                Vector2 knockbackDir = rb.linearVelocity.normalized;
                playerHealth.TakeDamage(damage, knockbackDir);
                DestroyArrow();
                return;
            }
        }

        // 2. NẾU TRÚNG MẶT ĐẤT, BỨC TƯỜNG (Kiểm tra Layer có khớp hitLayers không)
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            DestroyArrow();
        }
    }

    /// <summary>
    /// Xóa mũi tên và tạo hiệu ứng vỡ nát nếu có
    /// </summary>
    private void DestroyArrow()
    {
        // Tạo hạt nổ tung (nếu có set ngoài inspector)
        if (destroyVFX != null)
        {
            GameObject vfxInstance = Instantiate(destroyVFX, transform.position, Quaternion.identity);
            
            // Xóa rác: Hủy khối hiệu ứng hình ảnh sau 1.5 giây để tránh làm full bộ nhớ
            Destroy(vfxInstance, 1.5f);
        }

        // Dừng khựng lại để tránh lỗi chạy lố 1 frame rồi chết
        rb.linearVelocity = Vector2.zero; 
        
        Destroy(gameObject);
    }
}