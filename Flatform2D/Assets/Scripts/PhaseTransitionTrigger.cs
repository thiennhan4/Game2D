using UnityEngine;
using UnityEngine.SceneManagement;

public class PhaseTransitionTrigger : MonoBehaviour
{
    public enum TransitionType
    {
        SpecificScene,
        NextScene,
        PreviousScene
    }

    [Header("Cấu hình chuyển Scene")]
    [Tooltip("Chọn cách chuyển: Theo tên cụ thể, Scene kế tiếp, hay Scene trước đó")]
    [SerializeField] private TransitionType transitionType = TransitionType.NextScene;

    [Tooltip("Tên của Scene muốn chuyển tới (chỉ dùng khi chọn SpecificScene)")]
    [SerializeField] private string targetSceneName;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered) return;

        // Kiểm tra xem có trúng Player không
        if (collision.CompareTag("Player"))
        {
            isTriggered = true;
            Debug.Log($"[PhaseTransition] Player chạm cổng. Chuẩn bị chuyển cảnh...");
            
            if (GameManager.Instance != null)
            {
                switch (transitionType)
                {
                    case TransitionType.NextScene:
                        GameManager.Instance.LoadNextScene();
                        break;
                    case TransitionType.PreviousScene:
                        GameManager.Instance.LoadPreviousScene();
                        break;
                    case TransitionType.SpecificScene:
                        GameManager.Instance.LoadSceneWithFade(targetSceneName);
                        break;
                }
            }
            else
            {
                // Nếu chưa có GameManager thì load thẳng
                switch (transitionType)
                {
                    case TransitionType.NextScene:
                        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
                        if (nextIndex < SceneManager.sceneCountInBuildSettings)
                            SceneManager.LoadScene(nextIndex);
                        break;
                    case TransitionType.PreviousScene:
                        int prevIndex = SceneManager.GetActiveScene().buildIndex - 1;
                        if (prevIndex >= 0)
                            SceneManager.LoadScene(prevIndex);
                        break;
                    case TransitionType.SpecificScene:
                        SceneManager.LoadScene(targetSceneName);
                        break;
                }
            }
        }
    }
}
