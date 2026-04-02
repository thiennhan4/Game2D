using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a health bar fill UI (Image fillAmount).
/// Attach to: a UI GameObject that contains the health bar fill Image.
/// Inspector setup: drag the fill Image into fillHealth field.
/// Used by: PlayerHealth, EnemyStats via UpdateHealthUI(current, max).
/// </summary>
public class StateManager : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private Image fillHealth;
    [SerializeField] private Image fillMana;
    [SerializeField] private bool updateOnStart = true;
    [SerializeField] private float defaultCurrentHealth = 100f;
    [SerializeField] private float defaultMaxHealth = 100f;
    [SerializeField] private float defaultCurrentMana = 100f;
    [SerializeField] private float defaultMaxMana = 100f;
    private void Start()
    {
        if (updateOnStart)
        {
            if (fillHealth != null)
                UpdateHealthUI(defaultCurrentHealth, defaultMaxHealth);
                
            if (fillMana != null)
                UpdateManaUI(defaultCurrentMana, defaultMaxMana);
        }
    }

    /// <summary>
    /// Update the fill amount of the health bar.
    /// Called externally by PlayerHealth or EnemyStats.
    /// </summary>
    public void UpdateHealthUI(float currentHp, float maxHp)
    {
        if (fillHealth == null) return;

        if (maxHp <= 0f)
        {
            fillHealth.fillAmount = 0f;
            return;
        }

        fillHealth.fillAmount = Mathf.Clamp01(currentHp / maxHp);
    }

    /// <summary>
    /// Update the fill amount of the mana bar.
    /// Gọi bởi skill manager để trừ mp.
    /// </summary>
    public void UpdateManaUI(float currentMp, float maxMp)
    {
        // Sử dụng Image fillMana hoàn toàn mới bạn vừa tạo
        if (fillMana == null) return;

        if (maxMp <= 0f)
        {
            fillMana.fillAmount = 0f;
            return;
        }

        fillMana.fillAmount = Mathf.Clamp01(currentMp / maxMp);
    }
}