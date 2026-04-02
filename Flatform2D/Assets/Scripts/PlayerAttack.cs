using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Stats")]
    [SerializeField] private float baseDamage = 30f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Attack Point")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Animation")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private bool cancelAttackWhenReleaseMouse = true;

    [Header("Damage Popup")]
    [SerializeField] private GameObject damagePopupPrefab;

    [Header("Alert Settings")]
    [SerializeField] private float alertDuration = 0.2f;

    public static bool isAttackAlert;
    public static bool isAttacking => isAttackAlert;

    private float nextAttackTime;
    private float attackAlertTimer;
    private Animator anim;
    private bool isHoldingAttack;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        UpdateAttackAlert();

        // Giữ chuột trái
        if (Input.GetMouseButtonDown(0))
        {
            isHoldingAttack = true;
        }

        // Thả chuột trái
        if (Input.GetMouseButtonUp(0))
        {
            isHoldingAttack = false;

            if (cancelAttackWhenReleaseMouse)
            {
                CancelAttack();
            }
        }

        // Khi đang giữ chuột thì liên tục thử attack theo cooldown
        if (isHoldingAttack)
        {
            TryAttackByHold();
        }

        // Chuột phải vẫn có thể hủy attack nếu muốn
        if (Input.GetMouseButtonDown(1))
        {
            isHoldingAttack = false;
            CancelAttack();
        }
    }

    private void UpdateAttackAlert()
    {
        if (attackAlertTimer > 0f)
        {
            attackAlertTimer -= Time.deltaTime;
            isAttackAlert = true;
        }
        else
        {
            isAttackAlert = false;
        }
    }

    private void TryAttackByHold()
    {
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;
        attackAlertTimer = alertDuration;

        TriggerAttackAnimation();
        DealDamageIfHitEnemy();
    }

    private void TriggerAttackAnimation()
    {
        if (anim == null) return;

        anim.ResetTrigger(attackTriggerName);
        anim.SetTrigger(attackTriggerName);
    }

    private void DealDamageIfHitEnemy()
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("attackPoint is NULL");
            return;
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        if (hitEnemies == null || hitEnemies.Length == 0)
        {
            Debug.Log("Khong trung enemy -> khong gay damage");
            return;
        }

        foreach (Collider2D enemyCol in hitEnemies)
        {
            if (enemyCol == null) continue;

            IDamageable damageable = enemyCol.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = enemyCol.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                Debug.LogWarning(enemyCol.name + " has no IDamageable");
                continue;
            }

            if (damageable.IsDead)
            {
                Debug.Log(enemyCol.name + " is already dead");
                continue;
            }

            float finalDamage = baseDamage;

            EnemyStats enemyStats = enemyCol.GetComponent<EnemyStats>();
            if (enemyStats == null)
                enemyStats = enemyCol.GetComponentInParent<EnemyStats>();

            if (enemyStats != null)
                finalDamage = CalculateDamageAfterArmor(baseDamage, enemyStats.Armor);

            damageable.TakeDamage(finalDamage);

            // Kiểm tra xem Player có đang dùng Buff Skill W không
            PlayerSkillManager skillManager = GetComponent<PlayerSkillManager>();
            if (skillManager != null && skillManager.IsBuffActive)
            {
                // Thêm hoặc làm mới hiệu ứng đốt máu cho Enemy
                BurnEffect burn = enemyCol.gameObject.GetComponent<BurnEffect>();
                if (burn == null)
                {
                    burn = enemyCol.gameObject.AddComponent<BurnEffect>();
                }
                
                // Khởi tạo/Cập nhật thông số từ Buff
                burn.Initialize(skillManager.BurnDamagePerSecond, skillManager.BurnDuration, skillManager.BurnVfxPrefab);
            }

         // Hiện damage popup

            Debug.Log("Trung enemy: " + enemyCol.name + " | Damage: " + finalDamage);
        }
    }

    public void CancelAttack()
    {
        if (anim == null) return;

        anim.ResetTrigger(attackTriggerName);
        anim.Play(idleStateName, 0, 0f);
        isAttackAlert = false;
    }

    public static float CalculateDamageAfterArmor(float rawDamage, float armor)
    {
        if (rawDamage <= 0f) return 0f;

        if (armor >= 0f)
            return rawDamage * (100f / (100f + armor));
        else
            return rawDamage * (2f - (100f / (100f - armor)));
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}