using UnityEngine;

public class BossAttack : MonoBehaviour
{
    #region ======= SERIALIZED FIELDS =======

    [Header("═══ Skill 1 — Đòn thường ═══")]
    [Tooltip("Sát thương skill 1")]
    [SerializeField] private float skill1Damage = 25f;

    [Tooltip("Cooldown riêng của skill 1 (giây)")]
    [SerializeField] private float skill1Cooldown = 2f;

    [Tooltip("Thời gian chờ trước khi gây damage skill 1 (khớp animation vung vũ khí)")]
    [SerializeField] private float skill1DamageDelay = 0.4f;

    [Tooltip("Thời gian chờ sau khi gây damage skill 1 (khớp animation hạ vũ khí)")]
    [SerializeField] private float skill1EndDelay = 0.4f;

    [Header("═══ Skill 2 — Đòn mạnh ═══")]
    [Tooltip("Sát thương skill 2")]
    [SerializeField] private float skill2Damage = 40f;

    [Tooltip("Cooldown riêng của skill 2 (giây)")]
    [SerializeField] private float skill2Cooldown = 6f;

    [Tooltip("Thời gian chờ trước khi gây damage skill 2 (khớp animation charge)")]
    [SerializeField] private float skill2DamageDelay = 0.5f;

    [Tooltip("Thời gian chờ sau khi gây damage skill 2 (khớp animation recovery)")]
    [SerializeField] private float skill2EndDelay = 0.5f;

    [Header("═══ General ═══")]
    [Tooltip("Cooldown chung giữa 2 đòn bất kỳ (chống spam)")]
    [SerializeField] private float globalCooldown = 1.5f;

    [Tooltip("Khoảng cách tối đa để detect player và bắt đầu attack")]
    [SerializeField] private float attackRange = 2.5f;

    [Header("═══ Melee Hit Area ═══")]
    [Tooltip("Vị trí trung tâm vùng đánh (dùng chung cho cả 2 skill)")]
    [SerializeField] private Transform attackPoint;

    [Tooltip("Bán kính vùng đánh skill 1")]
    [SerializeField] private float skill1Radius = 1.2f;

    [Tooltip("Bán kính vùng đánh skill 2 (to hơn vì đòn mạnh)")]
    [SerializeField] private float skill2Radius = 1.5f;

    [Tooltip("Layer của Player để detect")]
    [SerializeField] private LayerMask playerLayer;

    [Header("═══ Component References ═══")]
    [SerializeField] private Animator animator;
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private BossController bossController;

    #endregion

    #region ======= PRIVATE STATE =======

    // Cooldown timers — dùng thời điểm (Time.time) để so sánh
    private float nextGlobalTime;   // Thời điểm sớm nhất được đánh đòn tiếp theo
    private float nextSkill1Time;   // Thời điểm skill 1 hết CD
    private float nextSkill2Time;   // Thời điểm skill 2 hết CD

    // Đang trong animation attack → lock không cho attack lại
    private bool isAttacking;

    // Đang dùng skill nào (1 hoặc 2) — để tránh conflict
    private int currentSkill;

    // Reference lưu trữ coroutine tấn công
    private Coroutine attackCoroutine;

    // Cache Animator hash
    private static readonly int AnimAttack1 = Animator.StringToHash("Attack1");
    private static readonly int AnimAttack2 = Animator.StringToHash("Attack2");

    #endregion

    #region ======= PUBLIC PROPERTIES =======

    /// <summary> Đang attack hay không (BossController dùng để dừng di chuyển). </summary>
    public bool IsAttacking => isAttacking;

    #endregion

    #region ======= UNITY CALLBACKS =======

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        bossHealth = GetComponent<BossHealth>();
        bossController = GetComponent<BossController>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();
        if (bossController == null) bossController = GetComponent<BossController>();

        if (attackPoint == null)
            Debug.LogError($"[BossAttack] '{gameObject.name}': AttackPoint chưa gán! " +
                "Tạo empty child GameObject, đặt tên 'AttackPoint', kéo vào đây.", this);

        if (bossHealth == null)
            Debug.LogWarning($"[BossAttack] '{gameObject.name}': Không tìm thấy BossHealth!", this);

        if (animator == null)
            Debug.LogWarning($"[BossAttack] '{gameObject.name}': Không tìm thấy Animator!", this);

        if (bossController == null)
            Debug.LogWarning($"[BossAttack] '{gameObject.name}': Không tìm thấy BossController!", this);
    }

    private void Update()
    {
        // ── Boss đã chết → hủy attack nếu đang đánh ──
        if (bossHealth != null && bossHealth.IsDead)
        {
            if (isAttacking) CancelAttack();
            return;
        }

        // ── Đang attack → chờ coroutine kết thúc, KHÔNG cho đánh thêm ──
        if (isAttacking) return;

        // ── Chưa hết global cooldown → chờ ──
        if (Time.time < nextGlobalTime) return;

        // ── Player không trong tầm → không đánh ──
        if (!IsPlayerInAttackRange()) return;

        // ══════════════════════════════════════════
        //  CHỌN SKILL — logic đơn giản, không trùng lặp
        // ══════════════════════════════════════════
        //  Ưu tiên: Skill 2 (nếu hết CD) → Skill 1 (nếu hết CD)
        //  Chỉ 1 skill được chọn mỗi lần, không bao giờ chạy 2 skill cùng lúc.

        if (Time.time >= nextSkill2Time)
        {
            ExecuteSkill(2);
        }
        else if (Time.time >= nextSkill1Time)
        {
            ExecuteSkill(1);
        }
    }

    #endregion

    #region ======= DETECTION =======

    private bool IsPlayerInAttackRange()
    {
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null) return false;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance > attackRange) return false;

        // Kiểm tra player có nằm ở đúng hướng nhìn hay không
        if (bossController != null)
        {
            bool playerOnRight = playerTransform.position.x > transform.position.x;
            if (playerOnRight != bossController.IsFacingRight)
            {
                // Player ở sau lưng → không đánh
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lấy Transform player từ BossController.
    /// </summary>
    private Transform GetPlayerTransform()
    {
        if (bossController != null && bossController.Player != null)
            return bossController.Player;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    #endregion

    #region ======= ATTACK EXECUTION =======

    private void ExecuteSkill(int skillNumber)
    {
        // ── Lock trạng thái ──
        isAttacking = true;
        currentSkill = skillNumber;

        // ── Set global cooldown → chống spam giữa 2 đòn ──
        nextGlobalTime = Time.time + globalCooldown;

        // ── Set cooldown riêng cho skill vừa dùng ──
        if (skillNumber == 1)
            nextSkill1Time = Time.time + skill1Cooldown;
        else
            nextSkill2Time = Time.time + skill2Cooldown;

        // ── XÓA TẤT CẢ trigger cũ trước khi set trigger mới ──
        // Đây là cách tránh trùng lặp: chỉ 1 trigger active tại 1 thời điểm
        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack1);
            animator.ResetTrigger(AnimAttack2);

            if (skillNumber == 1)
                animator.SetTrigger(AnimAttack1);
            else
                animator.SetTrigger(AnimAttack2);
        }

        // ── Chạy coroutine xử lý timing + damage ──
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackRoutine(skillNumber));
    }

    private System.Collections.IEnumerator AttackRoutine(int skillNumber)
    {
        // ═══ Lấy thông số theo skill ═══
        float damageDelay = (skillNumber == 1) ? skill1DamageDelay : skill2DamageDelay;
        float endDelay    = (skillNumber == 1) ? skill1EndDelay    : skill2EndDelay;
        float damage      = (skillNumber == 1) ? skill1Damage      : skill2Damage;
        float radius      = (skillNumber == 1) ? skill1Radius      : skill2Radius;
        string label      = (skillNumber == 1) ? "Skill1"          : "Skill2";

        // ═══ 1. CHỜ ANIMATION VUNG VŨ KHÍ ═══
        yield return new WaitForSeconds(damageDelay);

        // Kiểm tra boss còn sống không
        if (bossHealth != null && bossHealth.IsDead)
        {
            CancelAttack();
            yield break;
        }

        // ═══ 2. GÂY DAMAGE ═══
        DealMeleeDamage(damage, radius, label);

        // ═══ 3. CHỜ ANIMATION HẠ VŨ KHÍ ═══
        yield return new WaitForSeconds(endDelay);

        // ═══ 4. KẾT THÚC ═══
        if (isAttacking)
        {
            EndAttack();
        }
    }

    #endregion

    #region ======= CANCEL & END =======

    /// <summary>
    /// Hủy tấn công ngay lập tức (khi boss chết / bị disable).
    /// </summary>
    public void CancelAttack()
    {
        if (!isAttacking) return;
        isAttacking = false;
        currentSkill = 0;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // Xóa cả 2 trigger để animator không chạy animation attack dư
        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack1);
            animator.ResetTrigger(AnimAttack2);
        }
    }

    private void EndAttack()
    {
        isAttacking = false;
        currentSkill = 0;
        attackCoroutine = null;

        // Xóa trigger dư thừa
        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack1);
            animator.ResetTrigger(AnimAttack2);
        }
    }

    #endregion

    #region ======= DEAL DAMAGE =======

    private void DealMeleeDamage(float damage, float radius, string label)
    {
        // ── Xác định vị trí hitbox ──
        Vector2 hitPos;

        if (attackPoint != null)
        {
            hitPos = attackPoint.position;
        }
        else
        {
            // Fallback: tính vị trí theo hướng nhìn
            float facingDir = (bossController != null && bossController.IsFacingRight) ? 1f : -1f;
            hitPos = (Vector2)transform.position + new Vector2(facingDir * 1.5f, 0f);
            Debug.LogWarning($"[BossAttack] AttackPoint chưa gán! Dùng fallback position: {hitPos}");
        }

        // ── Tìm player bằng OverlapCircle ──
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(hitPos, radius, playerLayer);

        // ── Debug chi tiết (xem trong Console để kiểm tra) ──
        Debug.Log($"[BossAttack] {label} | HitPos: {hitPos} | Radius: {radius} | " +
                  $"PlayerLayer: {playerLayer.value} | HitCount: {hitPlayers.Length}");

        // ── Nếu OverlapCircle tìm thấy player ──
        if (hitPlayers != null && hitPlayers.Length > 0)
        {
            foreach (Collider2D playerCol in hitPlayers)
            {
                DealDamageToCollider(playerCol, damage, label);
            }
            return;
        }


        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null) return;

        float distToPlayer = Vector2.Distance(hitPos, playerTransform.position);

        if (distToPlayer <= radius + 0.5f) // Thêm 0.5f buffer
        {
            Debug.LogWarning($"[BossAttack] OverlapCircle không detect được, nhưng player đang trong tầm ({distToPlayer:F2}). " +
                $"KIỂM TRA: Player Layer có đúng không? PlayerLayer mask = {playerLayer.value}");

            // Tìm IDamageable trực tiếp trên player
            IDamageable damageable = playerTransform.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = playerTransform.GetComponentInChildren<IDamageable>();
            if (damageable == null)
                damageable = playerTransform.GetComponentInParent<IDamageable>();

            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage);
                Debug.Log($"[BossAttack] {label} trúng '{playerTransform.name}' (fallback) | Damage: {damage}");
            }
        }
        else
        {
            Debug.Log($"[BossAttack] {label} đánh trượt | DistToPlayer: {distToPlayer:F2} > Radius: {radius}");
        }
    }

    /// <summary>
    /// Gây damage cho 1 collider cụ thể qua IDamageable.
    /// </summary>
    private void DealDamageToCollider(Collider2D col, float damage, string label)
    {
        if (col == null) return;

        IDamageable damageable = col.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = col.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogWarning($"[BossAttack] '{col.name}' không có IDamageable!");
            return;
        }

        if (damageable.IsDead)
        {
            Debug.Log($"[BossAttack] '{col.name}' đã chết, bỏ qua.");
            return;
        }

        damageable.TakeDamage(damage);
        Debug.Log($"[BossAttack] {label} trúng '{col.name}' | Damage: {damage}");
    }

    /// <summary>
    /// Giữ lại hàm này để tránh báo lỗi nếu Animation Event lỡ được set trong Unity.
    /// </summary>
    public void AnimEvent_DealDamage()
    {
        // Do nothing (handled by Coroutine)
    }

    /// <summary>
    /// Giữ lại hàm này để tránh báo lỗi nếu Animation Event lỡ được set trong Unity.
    /// </summary>
    public void AnimEvent_EndAttack()
    {
        // Do nothing (handled by Coroutine)
    }

    #endregion

    #region ======= GIZMOS =======

    private void OnDrawGizmosSelected()
    {
        // Attack detection range (cam)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Skill 1 hit area (đỏ)
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, skill1Radius);
        }

        // Skill 2 hit area (tím)
        if (attackPoint != null)
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.8f);
            Gizmos.DrawWireSphere(attackPoint.position, skill2Radius);
        }
    }

    #endregion
}
