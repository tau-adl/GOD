using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [UsedImplicitly]
    public void ShowDroneSettings()
    {
        SceneManager.LoadScene("DroneSettings");
    }

    [UsedImplicitly]
    public void ShowGameSettings()
    {
        SceneManager.LoadScene("GameSettings");
    }

    [UsedImplicitly]
    public void StartSinglePlayer()
    {
        MultiPlayerManager2.SinglePlayer = true;
        SceneManager.LoadScene("MultiplayerScene2");
    }

    [UsedImplicitly]
    public void StartMultiPlayer()
    {
        MultiPlayerManager2.SinglePlayer = false;
        SceneManager.LoadScene("MultiplayerScene2");
    }
}
