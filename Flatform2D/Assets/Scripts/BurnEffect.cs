using UnityEngine;


public class BurnEffect : MonoBehaviour
{
    private float damagePerSecond;
    private float duration;
    private IDamageable damageable;

    private float lifeTimer;
    private float tickTimer;
    private GameObject currentVfx;

    /// <summary>
    /// Khởi tạo hoặc làm mới hiệu ứng đốt máu.
    /// </summary>
    /// <param name="dps">Lượng sát thương mỗi giây</param>
    /// <param name="dur">Thời gian tồn tại của hiệu ứng</param>
    /// <param name="vfxPrefab">Prefab hiệu ứng hình ảnh (tùy chọn)</param>
    public void Initialize(float dps, float dur, GameObject vfxPrefab)
    {
        damagePerSecond = dps;
        duration = dur;
        lifeTimer = 0f; // Làm mới lại thời gian đốt máu
        tickTimer = 0f;
        
        if (damageable == null)
        {
            damageable = GetComponent<IDamageable>();
            if (damageable == null)
                damageable = GetComponentInParent<IDamageable>();
        }

        // Tạo hiệu ứng hình ảnh nếu có và chưa tạo
        if (vfxPrefab != null && currentVfx == null)
        {
            // Tạo VFX làm con của Enemy để nó di chuyển theo Enemy
            currentVfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity, transform);
        }
    }

    private void Update()
    {
        // Nếu kẻ địch đã chết hoặc không có IDamageable, tự hủy script
        if (damageable == null || damageable.IsDead)
        {
            Destroy(this);
            return;
        }

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= duration)
        {
            // Hết thời gian đốt máu
            Destroy(this);
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer >= 1f)
        {
            tickTimer -= 1f;

            float finalDamage = damagePerSecond;

            EnemyStats enemyStats = GetComponent<EnemyStats>();
            if (enemyStats == null)
                enemyStats = GetComponentInParent<EnemyStats>();

            if (enemyStats != null)
            {
                finalDamage = PlayerAttack.CalculateDamageAfterArmor(damagePerSecond, enemyStats.Armor);
            }

            damageable.TakeDamage(finalDamage);
            Debug.Log("[BurnEffect] Đốt máu: " + gameObject.name + " | Damage: " + finalDamage);
        }
    }

    private void OnDestroy()
    {
        // Dọn dẹp VFX khi hết đốt máu hoặc kẻ địch chết
        if (currentVfx != null)
        {
            Destroy(currentVfx);
        }
    }
}
