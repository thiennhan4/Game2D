using System.Collections;
using UnityEngine;

public class WinChest : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tên Tag của quái vật. Mặc định là 'enemy' hoặc 'Enemy'.")]
    public string enemyTag = "enemy";

    [Header("Animation")]
    [Tooltip("Tên Trigger trong Animator để chạy animation mở rương")]
    public string openAnimTriggerName = "Open";
    [Tooltip("Thời gian chờ sau khi mở rương để hiện bảng Win (giúp player kịp thấy rương mở xong rồi mới hiện bảng Game Win)")]
    public float delayBeforeWin = 1.5f;

    private bool isPlayerInRange = false;
    private bool hasOpened = false; // Biến kiểm tra để ngăn mở rương nhiều lần
    public Animator chestAnimator; // Component Animator của child (hoặc của rương)

    private void Start()
    {
        // Tự động tìm Animator ở các child object (hoặc chính object này)
        chestAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        // Kiểm tra phím E nếu Player đang đứng gần rương VÀ rương chưa được mở
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E) && !hasOpened)
        {
            TryOpenChest();
        }
    }

    private void TryOpenChest()
    {
        int enemyCount = 0;

        // 1. Kiểm tra bằng Tag chính
        try
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            enemyCount += enemies.Length;
        }
        catch (UnityException)
        {
            // Bỏ qua lỗi nếu Tag chưa tồn tại trong Unit
        }

        // Fallback: Tìm thêm tag "Enemy" hoặc "enemy" nếu người dùng gõ nhầm
        string alternateTag = (enemyTag == "enemy") ? "Enemy" : (enemyTag == "Enemy" ? "enemy" : null);

        if (!string.IsNullOrEmpty(alternateTag))
        {
            try
            {
                GameObject[] altEnemies = GameObject.FindGameObjectsWithTag(alternateTag);
                enemyCount += altEnemies.Length;
            }
            catch (UnityException)
            {
                // Bỏ qua lỗi nếu Tag dự phòng chưa tồn tại
            }
        }

        if (enemyCount > 0)
        {
            Debug.Log("[WinChest] CHƯA THỂ MỞ RƯƠNG! Trên bản đồ vẫn còn " + enemyCount + " quái vật.");
        }
        else
        {
            Debug.Log("[WinChest] Đã diệt sạch Enemy! Đang mở rương và hiển thị Game Win Panel...");
            
            // Đánh dấu là rương đã được mở, không cho ấn E nữa
            hasOpened = true; 
            
            // 2. Chạy Animation mở rương trên child (nếu có)
            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger(openAnimTriggerName);
            }
            else
            {
                Debug.LogWarning("[WinChest] Không tìm thấy component Animator trên Chest hoặc Child của Chest!");
            }

            // Gọi Coroutine để đợi animation chạy xong rồi mới hiện màn hình Game Win
            // Vì Game Win gọi Time.timeScale = 0 sẽ làm đứng animation ngay lập tức
            StartCoroutine(WaitAndTriggerGameWin());
        }
    }

    private IEnumerator WaitAndTriggerGameWin()
    {
        // Chờ 1 khoảng thời gian để animation mở rương chạy
        yield return new WaitForSeconds(delayBeforeWin);

        // Gọi GameManager để hiển thị màn hình Game Win Panel
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameWin();
        }
        else
        {
            Debug.LogWarning("[WinChest] Lỗi: Không tìm thấy Script GameManager.Instance.");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Nhận diện Player đi vào vùng cảm biến của rương, và chỉ báo nếu rương chưa mở
        if (collision.CompareTag("Player") && !hasOpened)
        {
            isPlayerInRange = true;
            Debug.Log("[WinChest] Player đã tới chỗ rương. Nhấn 'E' để mở.");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // Phát hiện Player đi ra xa khỏi rương
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
        }
    }
}
