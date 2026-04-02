using UnityEngine;
using UnityEngine.SceneManagement;
public class Menu : MonoBehaviour
{
  public void PlayGame()
  {
    SceneManager.LoadScene("Phase1");
  }
  public void QuitGame()
  {
    Application.Quit();
  }
}
