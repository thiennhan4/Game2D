using UnityEngine;


[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class ProjectileDark : MonoBehaviour
{
    [Header("Cài đặt va chạm")]
    [Tooltip("Layer tường, đất... để projectile tự hủy khi chạm vào")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Hiệu ứng (Tùy chọn)")]
    [Tooltip("Prefab VFX nổ khi projectile bị hủy")]
    [SerializeField] private GameObject destroyVFX;

    [Header("Enemy Layer")]
    [Tooltip("Layer của Enemy để nhận biết va chạm")]
    [SerializeField] private LayerMask enemyLayer;

    private float damage;
    private GameObject owner;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Projectile bay thẳng, không bị trọng lực kéo
        rb.gravityScale = 0f;
        // Chống xuyên tường khi bay nhanh
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    /// <summary>
    /// Được gọi từ PlayerSkillManager ngay khi spawn projectile.
    /// </summary>
    /// <param name="direction">Hướng bay (trái/phải)</param>
    /// <param name="speed">Tốc độ bay</param>
    /// <param name="attackDamage">Damage gây ra khi trúng enemy</param>
    /// <param name="shooter">GameObject Player (để tránh tự đâm mình)</param>
    /// <param name="lifetime">Thời gian tối đa trước khi tự hủy (giây)</param>
    public void Fire(Vector2 direction, float speed, float attackDamage, GameObject shooter, float lifetime)
    {
        damage = attackDamage;
        owner = shooter;

        // Bỏ qua va chạm vật lý với người bắn để tránh lỗi đẩy lùi Player hoặc dính vào người Player
        if (owner != null)
        {
            Collider2D myCollider = GetComponent<Collider2D>();
            Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D col in ownerColliders)
            {
                if (myCollider != null && col != null)
                {
                    Physics2D.IgnoreCollision(myCollider, col);
                }
            }
        }

        // Xoay hình ảnh cho khớp hướng bay (chỉ cập nhật trục Z)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Nếu sprite của project mặc định xoay sang trái thì cần cộng/trừ thêm góc nếu cần, 
        // nhưng mặc định Unity Right (1,0) = 0 độ.
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Nạp vận tốc bay
        rb.linearVelocity = direction.normalized * speed;

        // Tự hủy sau lifetime giây nếu không trúng gì
        Destroy(gameObject, lifetime);
    }

    // ============================
    // VA CHẠM TRIGGER (Collider có isTrigger = true)
    // ============================
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject, other);
    }

    // ============================
    // VA CHẠM THƯỜNG (Collider KHÔNG có isTrigger)
    // ============================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject, collision.collider);
    }

    /// <summary>
    /// Logic xử lý chung cho cả Trigger lẫn Collision.
    /// Được gọi từ OnTriggerEnter2D và OnCollisionEnter2D.
    /// </summary>
    private void HandleHit(GameObject hitObject, Collider2D hitCollider)
    {
        // Bỏ qua owner (Player bắn ra → không tự đâm mình)
        if (owner != null && (hitObject == owner || hitObject.transform.root.gameObject == owner))
        {
            return;
        }

        // Bỏ qua các Trigger zone khác (ngoại trừ Enemy)
        if (hitCollider.isTrigger && !IsInEnemyLayer(hitObject))
        {
            return;
        }

        // 1. NẾU TRÚNG ENEMY → gây damage
        if (IsInEnemyLayer(hitObject))
        {
            IDamageable damageable = hitCollider.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = hitCollider.GetComponentInParent<IDamageable>();

            if (damageable != null && !damageable.IsDead)
            {
                // Tính damage sau armor (nếu có EnemyStats)
                float finalDamage = damage;

                EnemyStats enemyStats = hitCollider.GetComponent<EnemyStats>();
                if (enemyStats == null)
                    enemyStats = hitCollider.GetComponentInParent<EnemyStats>();

                if (enemyStats != null)
                    finalDamage = PlayerAttack.CalculateDamageAfterArmor(damage, enemyStats.Armor);

                damageable.TakeDamage(finalDamage);
                Debug.Log("[ProjectileDark] Trúng enemy: " + hitObject.name + " | Damage: " + finalDamage);
            }

            DestroyProjectile();
            return;
        }

        // 2. NẾU TRÚNG TƯỜNG/ĐẤT → tự hủy
        if (((1 << hitObject.layer) & hitLayers) != 0)
        {
            DestroyProjectile();
        }
    }

    /// <summary>
    /// Kiểm tra xem GameObject có thuộc enemyLayer không
    /// </summary>
    private bool IsInEnemyLayer(GameObject obj)
    {
        return ((1 << obj.layer) & enemyLayer) != 0;
    }

    /// <summary>
    /// Hủy projectile và tạo VFX nếu có
    /// </summary>
    private void DestroyProjectile()
    {
        if (destroyVFX != null)
        {
            GameObject vfxInstance = Instantiate(destroyVFX, transform.position, Quaternion.identity);
            Destroy(vfxInstance, 1.5f);
        }

        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject);
    }
}
