using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DroneSettingsManager : MonoBehaviour
{
    private const int DroneWifiModeAccessPoint = 0;
    private const int DroneWifiModeStation = 1;

    public GameObject droneIPAddressGameObject;
    public GameObject wifiSsidGameObject;
    public GameObject wifiPasswordGameObject;
    public GameObject wifiModeGameObject;

    private UnityEngine.UI.InputField droneIPAddressText;
    private UnityEngine.UI.InputField wifiSsidText;
    private TMP_InputField wifiPasswordText;
    private TMP_Dropdown wifiModeDropdown;

    // Start is called before the first frame update
    void Start()
    {
        droneIPAddressText = droneIPAddressGameObject.GetComponentInChildren<UnityEngine.UI.InputField>();
        wifiSsidText = wifiSsidGameObject.GetComponentInChildren<UnityEngine.UI.InputField>();
        wifiPasswordText = wifiPasswordGameObject.GetComponentInChildren<TMP_InputField>();
        wifiModeDropdown = wifiModeGameObject.GetComponentInChildren<TMP_Dropdown>();
        droneIPAddressText.text = PlayerPrefs.GetString("DroneIPAddress", TelloSdkClient.DefaultHostName);
        wifiSsidText.text = PlayerPrefs.GetString("WifiSSID", "");
        wifiPasswordText.text = PlayerPrefs.GetString("WifiPassword", "");
        wifiModeDropdown.value = PlayerPrefs.GetInt("DroneWifiMode", 0);
    }

    public async void SetDroneWifiSettings()
    {
        try
        {
            PlayerPrefs.SetString("DroneIPAddress", droneIPAddressText.text);
            var changed =
                wifiSsidText.text != PlayerPrefs.GetString("WifiSSID", "") ||
                wifiPasswordText.text != PlayerPrefs.GetString("WifiPassword", "") ||
                wifiModeDropdown.value != PlayerPrefs.GetInt("DroneWifiMode", 0);

            if (!changed)
            {
                ExitDroneSettings();
                return;
            }

            PlayerPrefs.SetString("WifiSSID", wifiSsidText.text);
            PlayerPrefs.SetString("WifiPassword", wifiPasswordText.text);
            PlayerPrefs.GetInt("DroneWifiMode", wifiModeDropdown.value);

            using (var client = new TelloSdkClient(droneIPAddressText.text, TelloSdkClientFlags.Control))
            {
                await client.StartAsync();
                switch (wifiModeDropdown.value)
                {
                    case DroneWifiModeAccessPoint:
                        if (!await client.EnableWifiAccessPointMode(wifiSsidText.text, wifiPasswordText.text))
                        {
                            return; // disable drone-related errors.
                            //EditorUtility.DisplayDialog(
                            //    "Operation Failed", "Failed to set drone mode to WiFi Access-Point.",
                            //    "ok");
                        }
                        break;
                    case DroneWifiModeStation:
                        if (!await client.EnableWifiStationMode(wifiSsidText.text, wifiPasswordText.text))
                        {
                            return; // disable drone-related errors.
                            //EditorUtility.DisplayDialog(
                            //    "Operation Failed", "Failed to set drone mode to WiFi-Station.",
                            //    "ok");
                        }
                        break;
                    default:
                        //EditorUtility.DisplayDialog(
                        //        "Operation Failed", "Unsupported drone WiFi mode.",
                        //        "ok");
                        break;

                }
                client.Close();
                ExitDroneSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
    }

    public void ExitDroneSettings()
    {
        SceneManager.LoadScene("SettingsMenu");
    }
}
