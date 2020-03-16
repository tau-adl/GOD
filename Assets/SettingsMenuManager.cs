using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

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

    public void StartLocalMultiplayer(bool actAsServer)
    {
        MultiplayerManager.MultiplayerMode = actAsServer
            ? MultiplayerMode.LocalServer
            : MultiplayerMode.LocalClient;
        SceneManager.LoadScene("MultiplayerScene");
    }

    public void StartMultiplayerAsServer()
    {
        StartLocalMultiplayer(true);
    }

    public void StartMultiplayerAsClient()
    {
        StartLocalMultiplayer(false);
    }
}
