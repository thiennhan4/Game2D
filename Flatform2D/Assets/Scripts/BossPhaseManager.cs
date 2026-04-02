using UnityEngine;

public class BossPhaseManager : MonoBehaviour
{
    [Header("Phase Management")]
    [Tooltip("Tổng số phase của Boss")]
    [SerializeField] private int totalPhases = 3;
    
    [Tooltip("Phase hiện tại của Boss")]
    [SerializeField] private int currentPhase = 1;

    [Header("Phase Thresholds")]
    [Tooltip("Ngưỡng bắt đầu Phase 2 (ví dụ 0.7 = dưới 70% máu)")]
    [SerializeField] [Range(0f, 1f)] private float phase2Threshold = 0.7f;
    
    [Tooltip("Ngưỡng bắt đầu Phase 3 (ví dụ 0.3 = dưới 30% máu)")]
    [SerializeField] [Range(0f, 1f)] private float phase3Threshold = 0.3f;

    [Header("Phase 1 Settings (> 70% Máu)")]
    [SerializeField] private float p1SpeedMulti = 1f;
    [SerializeField] private float p1DamageMulti = 1f;
    [SerializeField] private float p1CooldownMulti = 1f;
    [SerializeField] private bool p1EnableProjectile = true;
    [SerializeField] private bool p1EnableAOE = false; // Mặc định tắt đòn bùng nổ diện rộng ở Phase đầu

    [Header("Phase 2 Settings (30% - 70% Máu)")]
    [SerializeField] private float p2SpeedMulti = 1.2f;
    [SerializeField] private float p2DamageMulti = 1.2f;
    [SerializeField] private float p2CooldownMulti = 0.8f;
    [SerializeField] private bool p2EnableProjectile = true;
    [SerializeField] private bool p2EnableAOE = true; // Bật kỹ năng AOE khi vào Phase 2

    [Header("Phase 3 Settings (<= 30% Máu)")]
    [SerializeField] private float p3SpeedMulti = 1.5f;
    [SerializeField] private float p3DamageMulti = 1.5f;
    [SerializeField] private float p3CooldownMulti = 0.5f; // Hồi chiêu nhanh gấp đôi
    [SerializeField] private bool p3EnableProjectile = true;
    [SerializeField] private bool p3EnableAOE = true; // Phase cuối thả combo dồn dập

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SkeletonBossController bossController;
    [SerializeField] private SkeletonBossAttack bossAttack; 
    
    // Lưu lại thông số kỹ năng thiết lập trên Inspector của BossAttack để khi nhân hệ số không bị sai lệch lũy tiến
    private float originalMeleeDamage;
    private float originalMeleeCooldown;
    private float originalProjectileDamage;
    private float originalProjectileCooldown;
    private float originalAoeDamage;
    private float originalAoeCooldown;

    // --- CÁC PROPERTIES ĐƯỢC YÊU CẦU ---
    public int CurrentPhase => currentPhase;
    public int TotalPhases => totalPhases;
    public bool IsLastPhase => currentPhase == totalPhases;

    private void Start()
    {
        // Tự động gán nếu chưa set trên Inspector
        if (animator == null) animator = GetComponent<Animator>();
        if (bossController == null) bossController = GetComponent<SkeletonBossController>();
        if (bossAttack == null) bossAttack = GetComponent<SkeletonBossAttack>();

        // Lấy và lưu lại gốc các chỉ số kỹ năng từ SkeletonBossAttack
        if (bossAttack != null)
        {
            originalMeleeDamage = bossAttack.meleeSkill.damage;
            originalMeleeCooldown = bossAttack.meleeSkill.cooldown;
            
            originalProjectileDamage = bossAttack.projectileSkill.damage;
            originalProjectileCooldown = bossAttack.projectileSkill.cooldown;
            
            originalAoeDamage = bossAttack.aoeSkill.damage;
            originalAoeCooldown = bossAttack.aoeSkill.cooldown;
        }

        // Khởi động áp dụng thuộc tính Phase 1 lúc mới bắt đầu
        ForceSetPhase(1);
    }


    public void CheckPhase(float healthPercent)
    {
        // Cần check phase 3 trước vì máu ở phase 3 bao giờ cũng nhỏ hơn phase 2
        // Thêm điều kiện currentPhase < 3 để tránh gọi lại EnterPhase(3) liên tục
        if (currentPhase < 3 && healthPercent <= phase3Threshold)
        {
            EnterPhase(3);
        }
        else if (currentPhase < 2 && healthPercent <= phase2Threshold)
        {
            EnterPhase(2);
        }
    }

    public void EnterPhase(int newPhase)
    {
        // Chống spam gọi chuyển nhầm cùng 1 phase
        if (currentPhase == newPhase) return;

        currentPhase = newPhase;
        
        // Có thể bổ sung gọi Animator set trigger ở đây để boss đứng gồng hú đổi Phase
        if (animator != null)
        {
            // animator.SetInteger("Phase", currentPhase);
            // animator.SetTrigger("PhaseChange"); 
        }

        ApplyPhaseEffects();
        Debug.Log($"[BossPhaseManager] Boss đã tức giận chuyển sang Phase {currentPhase}!");
    }


    public void ForceSetPhase(int phase)
    {
        currentPhase = Mathf.Clamp(phase, 1, totalPhases);
        ApplyPhaseEffects();
    }


    private void ApplyPhaseEffects()
    {
        if (bossAttack == null) return;

        float dmgMulti = GetDamageMultiplier();
        float cdMulti = GetCooldownMultiplier();

        // 1) Toggle Bật / Tắt kỹ năng tuỳ thuộc phase
        bossAttack.projectileSkill.isEnabled = IsSkillProjectileEnabled();
        bossAttack.aoeSkill.isEnabled = IsSkillAOEEnabled();

        // 2) Scale Sát thương (Damage)
        bossAttack.meleeSkill.damage = originalMeleeDamage * dmgMulti;
        bossAttack.projectileSkill.damage = originalProjectileDamage * dmgMulti;
        bossAttack.aoeSkill.damage = originalAoeDamage * dmgMulti;

        // 3) Scale Thời gian Hồi chiêu (Cooldown) -> Càng nhân nhỏ đánh càng nhanh
        bossAttack.meleeSkill.cooldown = originalMeleeCooldown * cdMulti;
        bossAttack.projectileSkill.cooldown = originalProjectileCooldown * cdMulti;
        bossAttack.aoeSkill.cooldown = originalAoeCooldown * cdMulti;
        
        // Lưu ý: Speed (Tốc độ chạy) sẽ được BossController trực tiếp gọi GetSpeedMultiplier()
        // trong các đoạn Update() của nó thay vì set ở đây, vì logic di chuyển thuộc về Controller.
    }

    // ===================================
    // PUBLIC GETTERS (Dành cho việc query từ Controller)
    // ===================================

    public float GetSpeedMultiplier()
    {
        switch (currentPhase)
        {
            case 3: return p3SpeedMulti;
            case 2: return p2SpeedMulti;
            default: return p1SpeedMulti;
        }
    }

    public float GetDamageMultiplier()
    {
        switch (currentPhase)
        {
            case 3: return p3DamageMulti;
            case 2: return p2DamageMulti;
            default: return p1DamageMulti;
        }
    }

    public float GetCooldownMultiplier()
    {
        switch (currentPhase)
        {
            case 3: return p3CooldownMulti;
            case 2: return p2CooldownMulti;
            default: return p1CooldownMulti;
        }
    }

    public bool IsSkillProjectileEnabled()
    {
        switch (currentPhase)
        {
            case 3: return p3EnableProjectile;
            case 2: return p2EnableProjectile;
            default: return p1EnableProjectile; // Giai đoạn 1 vẫn được bắn Projectile
        }
    }

    public bool IsSkillAOEEnabled()
    {
        switch (currentPhase)
        {
            case 3: return p3EnableAOE;
            case 2: return p2EnableAOE;
            default: return p1EnableAOE; // Giai đoạn 1 thường tắt AOE
        }
    }
}
