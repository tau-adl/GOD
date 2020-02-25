using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsMenuManager : MonoBehaviour
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
        SceneManager.LoadScene("MultiplayerScene");
    }
}
