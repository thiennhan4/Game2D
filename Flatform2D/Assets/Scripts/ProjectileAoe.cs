using UnityEngine;

public class ProjectileAoe : MonoBehaviour
{
    [Header("Vụ nổ Settings")]
    [Tooltip("Bán kính vụ nổ AOE (to hay nhỏ tuỳ hình ảnh)")]
    [SerializeField] private float explosionRadius = 1.5f;

    [Tooltip("Sát thương tĩnh truyền thống (sẽ bị ghi đè nếu Boss chuyển Phase và gọi Setup)")]
    [SerializeField] private float damage = 30f;
    
    [Tooltip("Layer của Player hoặc các hệ nhận sát thương")]
    [SerializeField] private LayerMask damageableLayer; 

    [Tooltip("Thời gian tồn tại của vụ nổ trước khi xoá Prefab (Thường set bằng độ dài animation)")]
    [SerializeField] private float destroyDelay = 1f;

    [Header("Hiệu ứng (Tuỳ chọn)")]
    [SerializeField] private GameObject hitEffectPrefab; 

    private bool hasExploded = false;


    public void Setup(float dynamicDamage)
    {
        damage = dynamicDamage;
    }

    private void Start()
    {
        // Khi Prefab sinh ra trên map => nổ luôn 
        Explode();

        // Dọn dẹp nó
        Destroy(gameObject, destroyDelay);
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Thả lưới quét 1 vòng tròn quanh tâm Vụ Nổ xem có dính ai ở layer quy định không
        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        
        foreach (Collider2D hit in hitTargets)
        {
            // Chỉ gây sát thương nếu trúng mục tiêu mang tag "Player"
            if (hit.CompareTag("Player"))
            {
                // Trừ máu với Interface chuẩn IDamageable
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                    
                    // Máu me chớp sáng nếu có
                    if (hitEffectPrefab != null)
                    {
                        Instantiate(hitEffectPrefab, hit.transform.position, Quaternion.identity);
                    }
                }
            }
        }
    }

    // Tiện ích hiển thị phạm vi vụ nổ ngay trong Scene để dev dễ căng chỉnh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f); // Đỏ hơi trong suốt
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
