using UnityEngine;
public class FlyObeliskHealth : MonoBehaviour
{
    [Header("Fountain Healing Logic (MOBA style)")]
    [Tooltip("Bán kính vùng hiệu tác dụng hồi máu (Mặc định: 5 ô)")]
    [SerializeField] private float healRadius = 5f;

    [Tooltip("Lượng máu hồi mỗi lần (tick)")]
    [SerializeField] private float healAmount = 10f;

    [Tooltip("Tính hồi máu theo % máu tối đa (VD check vào điền 10 = hồi 10% máu tối đa mỗi tick)")]
    [SerializeField] private bool healByPercentage = false;

    [Tooltip("Khoảng thời gian (giây) giữa các lần tick hồi máu")]
    [SerializeField] private float healInterval = 1f;

    [Tooltip("Layer của đối tượng được phép hồi máu (Vd: Player)")]
    [SerializeField] private LayerMask healLayer;

    private float nextHealTime;

    private void Start()
    {
        // Cho phép tick đầu tiên diễn ra ngay lập tức (tuỳ chọn)
        nextHealTime = Time.time;
    }

    private void Update()
    {
        // Liên tục kiểm tra thời gian để thực hiện tick hồi máu
        if (Time.time >= nextHealTime)
        {
            PerformHealingTick();
            // Đặt lại thời gian cho tick tiếp theo
            nextHealTime = Time.time + healInterval;
        }
    }
    private void PerformHealingTick()
    {
        // Lấy tất cả collider nằm trong bán kính healRadius
        Collider2D[] collidersInRange = Physics2D.OverlapCircleAll(transform.position, healRadius);

        foreach (Collider2D col in collidersInRange)
        {
            if (col == null) continue;

            // KIỂM TRA NGHIÊM NGẶT: Chỉ hồi máu duy nhất cho người chơi
            if (!col.CompareTag("Player")) continue;

            // Truy xuất component PlayerHealth
            PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = col.GetComponentInParent<PlayerHealth>();
            }

            // Nếu đúng là Player, chưa chết, chưa đầy máu
            if (playerHealth != null && !playerHealth.IsDead && playerHealth.CurrentHealth < playerHealth.MaxHealth)
            {
                float actualHealAmount = healAmount;

                // Tuỳ chọn: Hồi theo %
                if (healByPercentage)
                {
                    actualHealAmount = (healAmount / 100f) * playerHealth.MaxHealth;
                }

                playerHealth.Heal(actualHealAmount);
                Debug.Log($"[FlyObelisk - Fountain] Đã hồi {actualHealAmount:F1} máu độc quyền cho {col.name}");
            }
        }
    }

    #region ======= GIZMOS =======
    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng tròn thể hiện bán kính hồi máu trong Scene View để dễ canh chỉnh
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.5f); // Xanh lá cây nhạt, thể hiện sự sống/hồi phục
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
    #endregion
}
