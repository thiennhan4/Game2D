using UnityEngine;
using TMPro;

/// <summary>
/// Hiển thị damage text popup bay lên rồi fade out.
/// Prefab cần có: TextMeshProUGUI hoặc TextMeshPro component,
/// Rigidbody2D (để bay theo InitialVelocity).
/// Sử dụng: DamagePopup.Create(position, damage, prefab)
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 InitialVelocity = new Vector2(2f, 10f);

    [Header("Lifetime")]
    public float lifeTime = 1f;

    [Header("Fade")]
    [SerializeField] private float fadeStartPercent = 0.5f;

    [Header("Scale Punch")]
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchDuration = 0.15f;

    [Header("Random Offset")]
    [SerializeField] private float randomOffsetX = 0.3f;
    [SerializeField] private float randomOffsetY = 0.2f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = new Color(1f, 0.3f, 0.1f, 1f);

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private TMP_Text textMesh;

    private float timer;
    private float fadeStartTime;
    private Color startColor;
    private Vector3 originalScale;
    private bool isInitialized;

    // ──────────────────────────────────────────────
    // Static Factory
    // ──────────────────────────────────────────────

    public static DamagePopup Create(Vector3 position, float damageAmount, GameObject prefab, bool isCritical = false)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[DamagePopup] Prefab is null!");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        DamagePopup popup = instance.GetComponent<DamagePopup>();

        if (popup == null)
        {
            Debug.LogError("[DamagePopup] Prefab does not have DamagePopup component!");
            Destroy(instance);
            return null;
        }

        popup.Setup(damageAmount, isCritical);
        return popup;
    }

    // ──────────────────────────────────────────────
    // Setup
    // ──────────────────────────────────────────────

    public void Setup(float damageAmount, bool isCritical = false)
    {
        FindTextComponent();

        if (textMesh == null)
        {
            Debug.LogError($"[DamagePopup] {name}: TMP_Text not found!");
            return;
        }

        // Hiển thị số damage
        textMesh.text = Mathf.RoundToInt(damageAmount).ToString();

        // Đặt màu
        if (isCritical)
        {
            textMesh.color = criticalColor;
            textMesh.fontSize *= 1.3f;
        }
        else
        {
            textMesh.color = normalColor;
        }

        startColor = textMesh.color;
        isInitialized = true;

        Debug.Log($"[DamagePopup] Setup: damage={damageAmount}, text='{textMesh.text}', color={textMesh.color}");
    }

    // ──────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────

    private void Start()
    {
        // Auto-find references
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        FindTextComponent();

        // ★ FIX: Chuyển layer về Default (0) để Camera luôn render được
        // (Layer "PopupText" có thể không nằm trong Camera Culling Mask)
        SetLayerRecursive(gameObject, 0);

        // Đảm bảo Canvas hiển thị trên cùng
        ConfigureCanvas();

        // Lấy màu mặc định nếu chưa Setup()
        if (!isInitialized && textMesh != null)
        {
            startColor = textMesh.color;
            isInitialized = true;
        }

        // Random offset để popup không chồng nhau
        Vector3 randomOffset = new Vector3(
            Random.Range(-randomOffsetX, randomOffsetX),
            Random.Range(-randomOffsetY, randomOffsetY),
            0f
        );
        transform.position += randomOffset;

        // Random hướng bay
        Vector2 velocity = InitialVelocity;
        if (Random.value > 0.5f) velocity.x = -velocity.x;
        if (rb != null) rb.linearVelocity = velocity;

        // Fade timing
        fadeStartTime = lifeTime * fadeStartPercent;

        // Punch scale
        originalScale = transform.localScale;
        transform.localScale = originalScale * punchScale;

        // Tự hủy sau lifetime
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // ── Punch Scale → về scale gốc ──
        if (timer < punchDuration)
        {
            float t = timer / punchDuration;
            transform.localScale = Vector3.Lerp(originalScale * punchScale, originalScale, t);
        }
        else
        {
            transform.localScale = originalScale;
        }

        // ── Fade Out ──
        if (timer >= fadeStartTime && textMesh != null)
        {
            float fadeElapsed = timer - fadeStartTime;
            float fadeDuration = lifeTime - fadeStartTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);

            Color c = startColor;
            c.a = alpha;
            textMesh.color = c;
        }
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Tìm TMP_Text component (hỗ trợ cả TextMeshPro và TextMeshProUGUI).
    /// </summary>
    private void FindTextComponent()
    {
        if (textMesh != null) return;

        textMesh = GetComponent<TMP_Text>();
        if (textMesh == null)
            textMesh = GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// Đặt layer cho tất cả children (đệ quy).
    /// Đảm bảo Camera render được popup.
    /// </summary>
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Đảm bảo Canvas child hiển thị đúng:
    /// - Override sorting để render trên sprites
    /// - World Space để hiển thị tại vị trí world
    /// - Scale phù hợp khi chuyển từ Screen Space
    /// </summary>
    private void ConfigureCanvas()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        // Override sorting để hiển thị trên sprites
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        // Chuyển sang World Space nếu cần
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;

            // Khi chuyển từ Screen Space, RectTransform có kích thước pixel (1920x1080)
            // → cần scale nhỏ lại để vừa trong world space
            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = new Vector3(0.01f, 0.01f, 1f);
            }
        }
    }
}
