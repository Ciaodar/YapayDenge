using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Oyun sahnelerinizin build settings'te ekli olduğundan emin olun.")]
    public string gameSceneName = "GameScene";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenOptions()
    {
        // Options menüsü için bir Canvas/Panel aktifleştirilebilir
        Debug.Log("Options menüsü açılıyor...");
    }

    public void QuitGame()
    {
        Debug.Log("Oyundan çıkılıyor...");
        Application.Quit();
    }
}
