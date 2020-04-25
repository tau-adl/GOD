using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void ShowDroneSettings()
    {
        SceneManager.LoadScene("DroneSettings");
    }

    public void ShowGameSettings()
    {
        SceneManager.LoadScene("GameSettings");
    }

    public void StartMultiplayer()
    {
        SceneManager.LoadScene("MultiplayerScene2");
    }
}
