using UnityEngine;
using UnityEngine.UI;

public class UIBackgroundAnimation : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Gắn Component Image hiển thị nền vào đây")]
    public Image targetImage;

    [Header("Animation Settings")]
    [Tooltip("Kéo thả toàn bộ 140 bức ảnh background của bạn vào mảng này")]
    public Sprite[] frames;

    [Tooltip("Tốc độ khung hình trên giây (càng cao chạy càng mượt/nhanh)")]
    public float fps = 30f;

    private int currentFrame;
    private float timer;
    private void Start()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || targetImage == null) return;

        timer += Time.deltaTime;
        float frameInterval = 1f / fps;

        if (timer >= frameInterval)
        {
            timer -= frameInterval;
            currentFrame = (currentFrame + 1) % frames.Length;
            targetImage.sprite = frames[currentFrame];
        }
    }
}
