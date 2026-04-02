using UnityEngine;
using UnityEngine.SceneManagement;
public class NextLevel : MonoBehaviour
{
    public string namePhase ;
    
    public void LoadNextPhase()
    {
        SceneManager.LoadScene(namePhase);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            LoadNextPhase();
        }
    }
}
