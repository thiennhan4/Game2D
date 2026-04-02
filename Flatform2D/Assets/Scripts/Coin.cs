using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Nếu có GameManager trong scene thì gọi hàm cộng vàng
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddGold(value);
            }

            // Hủy đồng vàng trên bản đồ
            Destroy(gameObject);
        }
    }
}
