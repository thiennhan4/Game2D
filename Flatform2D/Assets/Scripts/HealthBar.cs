using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class HealthBar : MonoBehaviour
{
    public Image fillBar; 
    public TextMeshProUGUI healthText;

    public void UpdateHealth(int currentValue, int maxValue)
    {
        fillBar.fillAmount = (float)currentValue / (float)maxValue;
        healthText.text = currentValue.ToString() + " / " + maxValue.ToString();
    }

    public void UpdateBar(int value , int maxValue , string text)
    {
        healthText.text = text;  
        fillBar.fillAmount = (float)value / (float)maxValue;
    }
}
