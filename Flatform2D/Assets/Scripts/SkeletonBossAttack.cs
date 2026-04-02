using System.Collections;
using UnityEngine;

[System.Serializable]
public class BossSkill
{
    [Header("Skill Settings")]
    public string skillName;
    public bool isEnabled = true;

    [Tooltip("Khoảng cách tối đa với Player để kích hoạt kỹ năng này")]
    public float range;

    [Tooltip("Sát thương của kỹ năng gây ra")]
    public float damage;

    [Tooltip("Thời gian gồng hoặc cảnh báo (Warning) trước khi đòn đánh thực sự xảy ra")]
    public float castTime;

    [Tooltip("Khoảng thời gian cần thiết để dùng lại kỹ năng này")]
    public float cooldown;

    // Lưu lại thời điểm lần cuối dùng chiêu để tính thời gian hồi (cooldown)
    [HideInInspector] public float lastCastTime;

    public bool CanCast()
    {
        return isEnabled && (Time.time >= lastCastTime + cooldown);
    }

    public void RecordCooldown()
    {
        lastCastTime = Time.time;
    }
}

public class SkeletonBossAttack : MonoBehaviour
{
    [Header("Boss Skills Definition")]
    public BossSkill meleeSkill = new BossSkill { skillName = "Melee", range = 2f, damage = 20f, castTime = 0.5f, cooldown = 2f };
    public BossSkill projectileSkill = new BossSkill { skillName = "Projectile", range = 10f, damage = 15f, castTime = 1f, cooldown = 4f };
    public BossSkill aoeSkill = new BossSkill { skillName = "AOE", range = 5f, damage = 30f, castTime = 1.5f, cooldown = 6f };

    [Header("Component References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private SkeletonBossController bossController;
    [SerializeField] private SkeletonBossHealth bossHealth;
    [SerializeField] private BossPhaseManager phaseManager;
    [SerializeField] private Rigidbody2D rb;

    [Header("Combat Output Setup")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float meleeRadius = 1f;

    [Header("Prefabs & Assets")]
    [SerializeField] private GameObject warningPrefab;
    [SerializeField] private GameObject projectilePrefab; // Script gắn trên prefab này: ProjectileImpact
    [SerializeField] private GameObject aoePrefab;        // Script gắn trên prefab này: ProjectileAoe

    [Header("Animation Parameters")]
    [SerializeField] private string isCastingBool = "isCasting";
    [SerializeField] private string isAttackingBool = "isAttacking";
    [SerializeField] private string meleeTrigger = "Attack";
    [SerializeField] private string projectileTrigger = "Projectile";
    [SerializeField] private string aoeTrigger = "AOE";

    // Tracking trạng thái bận
    private bool isUsingSkill = false;
    private Coroutine currentSkillCoroutine;
    
    // Property để cho Controller khác đọc:
    public bool IsBusy => isUsingSkill;

    private void Start()
    {
        // Gán tag cho boss là Enemy theo đúng cấu trúc của bạn
        gameObject.tag = "Enemy";

        // Tự động tìm Player
        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        // Tự động tìm các script nội bộ
        if (animator == null) animator = GetComponent<Animator>();
        if (bossController == null) bossController = GetComponent<SkeletonBossController>();
        if (bossHealth == null) bossHealth = GetComponent<SkeletonBossHealth>();
        if (phaseManager == null) phaseManager = GetComponent<BossPhaseManager>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Thiết lập lastCastTime khởi tạo để mở khoá hồi chiêu, boss có thể đánh ngay lần đầu
        meleeSkill.lastCastTime = -meleeSkill.cooldown;
        projectileSkill.lastCastTime = -projectileSkill.cooldown;
        aoeSkill.lastCastTime = -aoeSkill.cooldown;
    }

    private void Update()
    {
        // Nếu Boss đã chết, bắt buộc dừng lập tức mọi kỹ năng đang cast
        if (bossHealth != null && bossHealth.IsDead && isUsingSkill)
        {
            StopAllSkills();
        }
    }

    /// <summary>
    /// Hàm này được BossController gọi định kỳ khi ở trạng thái nhàn rỗi (Chase / Idle).
    /// </summary>
    public void TryChooseSkill()
    {
        // Đang thi triển 1 kĩ năng khác hoặc Boss đã chết hoặc không có player -> Không làm gì cả
        if (isUsingSkill || (bossHealth != null && bossHealth.IsDead) || player == null)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // --- Ưu tiên tấn công Melee nếu Player đứng sát ---
        if (distanceToPlayer <= meleeSkill.range && meleeSkill.CanCast())
        {
            currentSkillCoroutine = StartCoroutine(ExecuteMeleeAttack());
            return;
        }

        // --- Ưu tiên AOE đòn diện rộng ---
        if (distanceToPlayer <= aoeSkill.range && aoeSkill.CanCast())
        {
            currentSkillCoroutine = StartCoroutine(ExecuteAOE());
            return;
        }

        // --- Ưu tiên đòn ném phóng (Projectile) khi ở khoảng cách xa ---
        if (distanceToPlayer <= projectileSkill.range && projectileSkill.CanCast())
        {
            currentSkillCoroutine = StartCoroutine(ExecuteProjectile());
            return;
        }
    }

    // ===================================
    // KỊCH BẢN KỸ NĂNG (COROUTINES)
    // ===================================

    private IEnumerator ExecuteMeleeAttack()
    {
        isUsingSkill = true;

        // Báo cho BossController đổi state tương ứng trong logic game
        if (bossController != null) bossController.SetState(BossState.Attack);

        SetAnimTrigger(meleeTrigger);
        SetAnimBool(isAttackingBool, true);

        // Sinh Warning Prefab tại mũi vũ khí (nếu có warning cho đòn melee)
        GameObject warning = null;
        if (warningPrefab != null && attackPoint != null)
        {
            warning = Instantiate(warningPrefab, attackPoint.position, Quaternion.identity, attackPoint);
        }

        // [FLOW] WAIT FOR CAST TIME: Quá trình Boss gồng vuốt nhả
        yield return new WaitForSeconds(meleeSkill.castTime);

        if (warning != null) Destroy(warning);

        // Kiểm tra xem Boss có đột tử trong lúc gồng chiêu không
        if (bossHealth != null && bossHealth.IsDead) yield break;

        // [FLOW] EXECUTE DAMAGE
        if (attackPoint != null)
        {
            Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(attackPoint.position, meleeRadius);
            foreach (Collider2D hit in hitPlayers)
            {
                // Chỉ gây sát thương nếu trúng đối tượng mang tag Player
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable == null) damageable = hit.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(meleeSkill.damage);
                    }
                }
            }
        }

        // [FLOW] COOLDOWN
        meleeSkill.RecordCooldown();
        EndSkill();
    }

    private IEnumerator ExecuteProjectile()
    {
        isUsingSkill = true;

        // Báo BossController trạng thái Cast
        if (bossController != null) bossController.SetState(BossState.Cast);

        SetAnimTrigger(projectileTrigger);
        SetAnimBool(isCastingBool, true);

        // [FLOW] WAIT FOR CAST TIME
        yield return new WaitForSeconds(projectileSkill.castTime);

        if (bossHealth != null && bossHealth.IsDead) yield break;

        // [FLOW] EXECUTE: Sinh Projectile
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            
            // Quản lý hướng bay của viên đạn dựa theo vị trí vị trí gốc của player 
            Vector2 projDir = player != null ? (player.position - firePoint.position).normalized : Vector2.right * Mathf.Sign(transform.localScale.x);

            // Gán thông số sát thương và hướng bay cho Projectile đó:
            var projImpactScript = proj.GetComponent<ProjectileImpact>();
            if (projImpactScript != null) 
            {
                projImpactScript.Setup(projectileSkill.damage, projDir); 
            }
        }

        // [FLOW] COOLDOWN
        projectileSkill.RecordCooldown();
        EndSkill();
    }

    private IEnumerator ExecuteAOE()
    {
        isUsingSkill = true;

        if (bossController != null) bossController.SetState(BossState.Cast);

        SetAnimTrigger(aoeTrigger);
        SetAnimBool(isCastingBool, true);

        // Lấy chính xác toạ độ sàn mà player đang đứng ngay lúc Boss giơ tay báo chiêu
        Vector3 targetPos = player != null ? player.position : transform.position;

        // Sinh ra Warning (thường là cái vòng báo hiệu nằm ở trên mặt đất)
        GameObject warning = null;
        if (warningPrefab != null)
        {
            warning = Instantiate(warningPrefab, targetPos, Quaternion.identity);
        }

        // [FLOW] WAIT FOR CAST TIME: Warning hiện, Player có castTime giây để lướt ra ngoài vòng
        yield return new WaitForSeconds(aoeSkill.castTime);

        if (warning != null) Destroy(warning);

        if (bossHealth != null && bossHealth.IsDead) yield break;

        // [FLOW] EXECUTE: Cột điện trồi lên, Vụ Nổ... (Spawn AOE Prefab)
        if (aoePrefab != null)
        {
            GameObject aoe = Instantiate(aoePrefab, targetPos, Quaternion.identity);
            
            // Gán mức độ sát thương cho khối nổ AOE (đã nhân từ phase)
            var aoeScript = aoe.GetComponent<ProjectileAoe>();
            if (aoeScript != null) { aoeScript.Setup(aoeSkill.damage); }
        }

        // [FLOW] COOLDOWN
        aoeSkill.RecordCooldown();
        EndSkill();
    }

    // ===================================
    // HELPER METHODS
    // ===================================

    private void EndSkill()
    {
        isUsingSkill = false;
        
        SetAnimBool(isAttackingBool, false);
        SetAnimBool(isCastingBool, false);

        // Báo lại BossController rằng "chiêu này xong rồi, hãy quay lại Idle/Chase"
        if (bossController != null && bossController.currentState != BossState.Dead) 
        {
            bossController.SetState(BossState.Idle);
        }
    }

    /// <summary>
    /// Ép dừng lập tức tất cả các logic tấn công đang chạy
    /// </summary>
    public void StopAllSkills()
    {
        if (currentSkillCoroutine != null)
        {
            StopCoroutine(currentSkillCoroutine);
            currentSkillCoroutine = null;
        }

        EndSkill();
    }

    private void SetAnimTrigger(string paramName)
    {
        // Bỏ trống theo yêu cầu: "về animator skill của skeleton enemy là tôi chỉ dùng effect thôi"
        // Điều này sẽ ngắt việc gửi trigger sang Animator và hết bị văng lỗi.
    }

    private void SetAnimBool(string paramName, bool value)
    {
        // Bỏ trống theo yêu cầu: "về animator skill của skeleton enemy là tôi chỉ dùng effect thôi"
        // Điều này sẽ ngắt việc điều khiển bool sang Animator và hết bị văng lỗi.
    }

    // Tiện ích hiển thị phạm vi của Skills + AttackPoint khi dùng Scene View
    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, meleeRadius);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, meleeSkill.range);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, projectileSkill.range);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, aoeSkill.range);
    }
}
