using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
public class GameManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    // Singleton
    // ──────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Scene Transition")]
    [SerializeField] private Image fadeImage; // Kéo thả một Image màu đen che toàn màn hình vào đây
    [SerializeField] private float fadeDuration = 1f;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button spawnButton;
    [SerializeField] private Button quitButton;

    [Header("Game Win UI")]
    [SerializeField] private GameObject gameWinPanel;
    [SerializeField] private Button winRetryButton;
    [SerializeField] private Button winQuitButton;
    [Tooltip("Tên scene màn hình chờ")]
    [SerializeField] private string waitingRoomSceneName = "MenuGame";

    // Lưu vị trí player chết để spawn lại đúng chỗ
    private Vector3 lastDeathPosition;
    private bool isGameOver;

    [Header("Economy System")]
    [Tooltip("Túi vàng của người chơi")]
    [SerializeField] private int currentGold = 0;
    
    [Tooltip("Gắn Component Text hiển thị điểm vàng trên Canvas vào ô này. Trống nó vẫn chạy ngầm không sao cả.")]
    [SerializeField] private TextMeshProUGUI goldText; 

    // Lệnh này cho phép các script khác đọc xem có bao nhiêu vàng mà không được tùy ý ăn gian sửa số vàng
    public int CurrentGold => currentGold;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-tìm player nếu chưa gán
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player != null && playerHealth == null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        // Ẩn panel Game Over và Game Win khi bắt đầu
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
            
        if (gameWinPanel != null)
            gameWinPanel.SetActive(false);

        // Đăng ký sự kiện cho buttons
        if (spawnButton != null)
            spawnButton.onClick.AddListener(OnSpawnButtonClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitButtonClicked);
            
        if (winRetryButton != null)
            winRetryButton.onClick.AddListener(OnWinRetryButtonClicked);
            
        if (winQuitButton != null)
            winQuitButton.onClick.AddListener(OnWinQuitButtonClicked);

        isGameOver = false;

        // Tải dữ liệu từ thiết bị ngay khi vào map (Vàng, vị trí...)
        LoadGame();

        // Cập nhật lên UI sau khi Load
        UpdateGoldUI();

        // Bắt đầu fade in khi mới vào scene
        if (fadeImage != null)
        {
            StartCoroutine(FadeIn());
        }
    }

    private void Update()
    {
        // Nhấn Esc để thoát game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnQuitButtonClicked();
        }

        // Chỉ kiểm tra khi chưa Game Over
        if (isGameOver) return;

        // Kiểm tra player đã chết chưa
        if (playerHealth != null && playerHealth.IsDead)
        {
            TriggerGameOver();
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký events để tránh memory leak
        if (spawnButton != null)
            spawnButton.onClick.RemoveListener(OnSpawnButtonClicked);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitButtonClicked);

        if (winRetryButton != null)
            winRetryButton.onClick.RemoveListener(OnWinRetryButtonClicked);

        if (winQuitButton != null)
            winQuitButton.onClick.RemoveListener(OnWinQuitButtonClicked);
    }

    private void OnApplicationQuit()
    {
        // Tự động lưu game lại khi người chơi tắt App bằng dấu X ở ngoài desktop
        SaveGame();
    }
    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // Lưu vị trí player vừa chết
        if (player != null)
            lastDeathPosition = player.transform.position;

        Debug.Log($"[GameManager] Game Over! Player died at {lastDeathPosition}");

        // Hiện Game Over panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(AudioManager.instance.LoseGame);

        // Pause game (timeScale = 0 → mọi thứ dừng lại)
        Time.timeScale = 0f;
    }

    public void OnSpawnButtonClicked()
    {
        Debug.Log("[GameManager] Spawn button clicked – Respawning player...");

        // Resume game trước khi thao tác (vì timeScale = 0 sẽ block coroutines)
        Time.timeScale = 1f;

        // Gọi Respawn trên PlayerHealth (hồi máu, bật collider, animation, v.v.)
        if (playerHealth != null)
            playerHealth.Respawn();

        // Di chuyển player đến vị trí chết (sau Respawn để override respawnPoint)
        if (player != null)
            player.transform.position = lastDeathPosition;

        // Ẩn Game Over panel
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        isGameOver = false;
    }

    public void OnQuitButtonClicked()
    {
        Debug.Log("[GameManager] Quit button clicked – Exiting game...");

        // Lưu trữ lại trước khi nhấn Quit
        SaveGame();

        // Resume timeScale để tránh lỗi nếu cần cleanup
        Time.timeScale = 1f;

#if UNITY_EDITOR
        // Dừng Play Mode trong Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Thoát ứng dụng khi build
        Application.Quit();
#endif
    }

    public void TriggerGameWin()
    {
        Debug.Log("[GameManager] Game Win!");

        if (gameWinPanel != null)
            gameWinPanel.SetActive(true);

        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(AudioManager.instance.WinGame);

        Time.timeScale = 0f;
    }

    public void OnWinRetryButtonClicked()
    {
        Debug.Log("[GameManager] Win Retry button clicked – Loading waiting room...");
        Time.timeScale = 1f;

        // Load scene màn hình chờ
        if (!string.IsNullOrEmpty(waitingRoomSceneName))
        {
            SceneManager.LoadScene(waitingRoomSceneName);
        }
        else
        {
            // Fallback: Nếu không khai báo tên thì load lại map đầu tiên hoặc map hiện tại
            SceneManager.LoadScene(0); 
        }
    }

    public void OnWinQuitButtonClicked()
    {
        Debug.Log("[GameManager] Win Quit button clicked – Exiting game...");
        
        SaveGame();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        // In log ra Console dễ test hệ thống kinh tế
        Debug.Log($"[GameManager] Người chơi nhặt được {amount} vàng! Tính đến nay có tổng cộng: {currentGold}");

        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(AudioManager.instance.coin);

        UpdateGoldUI();
    }
    
    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            UpdateGoldUI();
            return true; // Trả tiền thành công
        }
        // Thiếu tiền rách túi
        Debug.LogWarning("[GameManager] Số dư không đủ để tiêu xài!");
        return false;
    }

    private void UpdateGoldUI()
    {
        if (goldText != null)
        {
            // Hiển thị dạng chữ số: ví dụ đổi lại "Gold: " + currentGold; tùy gu thẩm mĩ UI của bạn.
            goldText.text = currentGold.ToString(); 
        }
    }
    public void SaveGame()
    {
        // 1. Lưu Vàng
        PlayerPrefs.SetInt("PlayerGold", currentGold);

        // 2. Lưu Vị trí của Player (Để khi bật game lên đứng lại đúng chỗ cũ)
        if (player != null)
        {
            PlayerPrefs.SetFloat("PlayerPosX", player.transform.position.x);
            PlayerPrefs.SetFloat("PlayerPosY", player.transform.position.y);
        }

        // Bắt buộc gọi Save() để ép ổ cứng ghi nhận dữ liệu ngay lập tức
        PlayerPrefs.Save(); 
        
        Debug.Log("[GameManager] Đã LƯU GAME thành công!");
    }
    public void LoadGame()
    {
        // 1. Load Vàng (Nếu chưa có dữ liệu cũ trong máy thì bỏ qua)
        if (PlayerPrefs.HasKey("PlayerGold"))
        {
            currentGold = PlayerPrefs.GetInt("PlayerGold");
        }

        // 2. Load Vị trí. 
        // [!] Mở phần bị comment bên dưới XÓA DẤU /* và */ nếu game bạn là kiểu đi cảnh qua màn liên tục. 
        // Nếu game bạn chia level và mỗi bài bắt đầu ở chỗ cố định (ví dụ spawn base) thì cứ giữ nguyên phần comment này là được.
        /* 
        if (player != null && PlayerPrefs.HasKey("PlayerPosX"))
        {
            float pX = PlayerPrefs.GetFloat("PlayerPosX");
            float pY = PlayerPrefs.GetFloat("PlayerPosY");
            player.transform.position = new Vector3(pX, pY, player.transform.position.z);
        }
        */

        Debug.Log($"[GameManager] TẢI GAME hoàn tất! Đang có: {currentGold} vàng.");
    }

    // ──────────────────────────────────────────────
    // Nền tảng Chuyển Scene (Fade)
    // ──────────────────────────────────────────────

    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    public void LoadNextScene()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            StartCoroutine(FadeOutAndLoadIndex(nextIndex));
        }
        else
        {
            Debug.LogWarning("[GameManager] Đã ở Scene cuối cùng, không thể qua map tiếp.");
        }
    }

    public void LoadPreviousScene()
    {
        int prevIndex = SceneManager.GetActiveScene().buildIndex - 1;
        if (prevIndex >= 0)
        {
            StartCoroutine(FadeOutAndLoadIndex(prevIndex));
        }
        else
        {
            Debug.LogWarning("[GameManager] Đã ở Scene đầu tiên, không thể lùi lại.");
        }
    }

    private IEnumerator FadeIn()
    {
        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;
        color.a = 1f;
        fadeImage.color = color;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            color.a = 1f - Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        fadeImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            float timer = 0f;
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;

            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(timer / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }
        }
        SaveGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeOutAndLoadIndex(int sceneIndex)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            float timer = 0f;
            Color color = fadeImage.color;
            
            // Màn hình sáng lúc đầu, dần tối đi (alpha -> 1)
            color.a = 0f;
            fadeImage.color = color;

            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(timer / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }
        }

        // Lưu game trước khi sang scene khác
        SaveGame();

        // Load scene tiếp theo
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }


}
