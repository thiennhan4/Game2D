using UnityEngine;


public class PlayerSkillManager : MonoBehaviour
{
    [Header("=== Skill Q - Dark Projectile ===")]
    [SerializeField] private GameObject projectileDarkPrefab;
    [SerializeField] private Transform firePoint;

    [Header("Skill Stats")]
    [SerializeField] private float skillDamage = 30f;
    [SerializeField] private float skillSpeed = 1.5f;
    [SerializeField] private float skillCooldown = 1.5f;
    [SerializeField] private float projectileLifetime = 5f;

    [Header("Animation (Tùy chọn)")]
    [SerializeField] private string skillTriggerName = "Skill_Q";

    [Header("=== Skill W - Buff Attack ===")]
    [SerializeField] private float buffDuration = 5f;
    [SerializeField] private float skillWCooldown = 10f;
    [SerializeField] private float burnDamagePerSecond = 15f;
    [SerializeField] private float burnDuration = 5f;
    [SerializeField] private GameObject burnVfxPrefab;
    [SerializeField] private string skillWTriggerName = "Skill_W";

    [Header("=== Mana System ===")]
    [SerializeField] private float maxMana = 1000f;
    [SerializeField] private float manaCostQ = 10f;
    [SerializeField] private float manaCostW = 15f;
    [SerializeField] private float manaRegenPerSec = 5f; // Hồi mana mỗi giây
    [SerializeField] private StateManager manaBarUI;     // Kéo UI Mana vào đây

    private float currentMana;
    private float nextSkillTime;
    private float nextSkillWTime;
    private float buffEndTime;

    // Cho phép các script khác (vd: PlayerAttack) kiểm tra
    public bool IsBuffActive => Time.time < buffEndTime;
    public float BurnDamagePerSecond => burnDamagePerSecond;
    public float BurnDuration => burnDuration;
    public GameObject BurnVfxPrefab => burnVfxPrefab;

    private Animator anim;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        currentMana = maxMana;
        UpdateManaUI();
    }

    private void Update()
    {
        // Không dùng skill khi đã chết
        if (playerHealth != null && playerHealth.IsDead) return;

        // Hồi mana thụ động theo thời gian
        if (currentMana < maxMana)
        {
            currentMana += manaRegenPerSec * Time.deltaTime;
            currentMana = Mathf.Min(currentMana, maxMana);
            UpdateManaUI();
        }

        // Nhấn Q → dùng Skill Dark Projectile
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TryCastSkillQ();
        }

        // Nhấn W → dùng Skill Buff
        if (Input.GetKeyDown(KeyCode.W))
        {
            TryCastSkillW();
        }
    }

    /// <summary>
    /// Thử cast Skill Q nếu hết cooldown
    /// </summary>
    private void TryCastSkillQ()
    {
        // Kiểm tra cooldown
        if (Time.time < nextSkillTime)
        {
            float remaining = nextSkillTime - Time.time;
            Debug.Log("[Skill Q] Đang hồi chiêu! Còn " + remaining.ToString("F1") + "s");
            return;
        }

        // Kiểm tra Mana
        if (currentMana < manaCostQ)
        {
            Debug.Log("[Skill Q] Không đủ Mana (cần " + manaCostQ + ")!");
            return;
        }

        // Kiểm tra prefab và firePoint
        if (projectileDarkPrefab == null)
        {
            Debug.LogWarning("[Skill Q] projectileDarkPrefab chưa được gán trong Inspector!");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning("[Skill Q] firePoint chưa được gán trong Inspector!");
            return;
        }

        // Set cooldown
        nextSkillTime = Time.time + skillCooldown;

        // Trigger animation (nếu có)
        if (anim != null)
        {
            anim.SetTrigger(skillTriggerName);
        }

        // Trừ Mana
        currentMana -= manaCostQ;
        UpdateManaUI();

        // Spawn projectile
        SpawnDarkProjectile();

        Debug.Log("[Skill Q] Đã bắn Dark Projectile! Cooldown: " + skillCooldown + "s");
    }

    /// <summary>
    /// Tạo ProjectileDark và bắn theo hướng Player đang quay mặt
    /// </summary>
    private void SpawnDarkProjectile()
    {
        // Xác định hướng quay mặt của Player dựa vào localScale.x
        float facingDir = Mathf.Sign(transform.localScale.x);
        Vector2 shootDirection = new Vector2(facingDir, 0f);

        // Tạo projectile tại vị trí firePoint
        GameObject projectileObj = Instantiate(
            projectileDarkPrefab,
            firePoint.position,
            Quaternion.identity
        );

        // Gọi hàm Fire của ProjectileDark
        ProjectileDark projectile = projectileObj.GetComponent<ProjectileDark>();
        if (projectile != null)
        {
            projectile.Fire(shootDirection, skillSpeed, skillDamage, gameObject, projectileLifetime);
        }
        else
        {
            Debug.LogError("[Skill Q] Prefab không có script ProjectileDark!");
            Destroy(projectileObj);
        }
    }

    /// <summary>
    /// Thử cast Skill W nếu hết cooldown
    /// </summary>
    private void TryCastSkillW()
    {
        if (Time.time < nextSkillWTime)
        {
            float remaining = nextSkillWTime - Time.time;
            Debug.Log("[Skill W] Đang hồi chiêu! Còn " + remaining.ToString("F1") + "s");
            return;
        }

        // Kiểm tra Mana
        if (currentMana < manaCostW)
        {
            Debug.Log("[Skill W] Không đủ Mana (cần " + manaCostW + ")!");
            return;
        }

        // Trừ Mana
        currentMana -= manaCostW;
        UpdateManaUI();

        // Kích hoạt buff
        buffEndTime = Time.time + buffDuration;
        nextSkillWTime = Time.time + skillWCooldown;

        if (anim != null && !string.IsNullOrEmpty(skillWTriggerName))
        {
            anim.SetTrigger(skillWTriggerName);
        }

        Debug.Log("[Skill W] Đã dùng Buff Attack! Tồn tại: " + buffDuration + "s. Cooldown: " + skillWCooldown + "s");
    }

    public float GetCooldownPercent()
    {
        if (Time.time >= nextSkillTime) return 0f;
        return (nextSkillTime - Time.time) / skillCooldown;
    }

    /// <summary>
    /// Kiểm tra skill đã sẵn sàng chưa
    /// </summary>
    public bool IsSkillReady()
    {
        return Time.time >= nextSkillTime;
    }

    /// <summary>
    /// Update UI thanh Mana
    /// </summary>
    private void UpdateManaUI()
    {
        if (manaBarUI != null)
        {
            manaBarUI.UpdateManaUI(currentMana, maxMana);
        }
    }
}
