using System;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DroneSettingsManager : MonoBehaviour
{
    private static class SettingKeys
    {
        public const string DroneHostName = "DroneHostName";
        public const string WifiSsid = "WifiSSID";
        public const string WifiPassword = "WifiPassword";
        public const string DroneWifiMode = "DroneWifiMode";
    }

    private static class WifiMode
    {
        public const string AccessPoint = "Access-Point";
        public const string Station = "Station";
    }

    public GameObject droneIPAddressGameObject;
    public GameObject wifiSsidGameObject;
    public GameObject wifiPasswordGameObject;
    public GameObject wifiModeGameObject;

    private UnityEngine.UI.InputField droneIPAddressText;
    private UnityEngine.UI.InputField wifiSsidText;
    private TMP_InputField wifiPasswordText;
    private TMP_Dropdown wifiModeDropdown;

    private void SetWifiMode([NotNull] string wifiMode)
    {
        for (var i = 0; i < wifiModeDropdown.options.Count; ++i)
        {
            var option = wifiModeDropdown.options[i];
            if (wifiMode.Equals(option.text, StringComparison.Ordinal))
            {
                wifiModeDropdown.value = i;
                break;
            }
        }
    }

    private string GetWifiMode()
    {
        var index = wifiModeDropdown.value;
        return wifiModeDropdown.options[index].text;
    }

    [UsedImplicitly]
    private void Awake()
    {
        droneIPAddressText = droneIPAddressGameObject.GetComponentInChildren<UnityEngine.UI.InputField>();
        wifiSsidText = wifiSsidGameObject.GetComponentInChildren<UnityEngine.UI.InputField>();
        wifiPasswordText = wifiPasswordGameObject.GetComponentInChildren<TMP_InputField>();
        wifiModeDropdown = wifiModeGameObject.GetComponentInChildren<TMP_Dropdown>();
    }

    [UsedImplicitly]
    private void Start()
    {
        droneIPAddressText.text = PlayerPrefs.GetString(SettingKeys.DroneHostName, TelloSdkClient.DefaultHostName);
        wifiSsidText.text = PlayerPrefs.GetString(SettingKeys.WifiSsid, "");
        wifiPasswordText.text = PlayerPrefs.GetString(SettingKeys.WifiPassword, "");
        var wifiMode = PlayerPrefs.GetString(SettingKeys.DroneWifiMode, WifiMode.AccessPoint);
        SetWifiMode(wifiMode);
    }

    [UsedImplicitly]
    public async void SetDroneWifiSettings()
    {
        try
        {
            PlayerPrefs.SetString(SettingKeys.DroneHostName, droneIPAddressText.text);
            var currentWifiSsid = PlayerPrefs.GetString(SettingKeys.WifiSsid, ""); 
            var desiredWifiSsid = wifiSsidText.text;
            var currentWifiPassword = PlayerPrefs.GetString(SettingKeys.WifiPassword, "");
            var desiredWifiPassword = wifiPasswordText.text; 
            var currentWifiMode = PlayerPrefs.GetString(SettingKeys.DroneWifiMode, WifiMode.AccessPoint);
            var desiredWifiMode = GetWifiMode();

            var changed =
                !currentWifiSsid.Equals(desiredWifiSsid, StringComparison.OrdinalIgnoreCase) ||
                !currentWifiPassword.Equals(desiredWifiPassword, StringComparison.Ordinal) ||
                !currentWifiMode.Equals(desiredWifiMode, StringComparison.Ordinal);

            if (!changed)
                return;
            PlayerPrefs.SetString(SettingKeys.WifiSsid, desiredWifiSsid);
            PlayerPrefs.SetString(SettingKeys.WifiPassword, desiredWifiPassword);
            PlayerPrefs.SetString(SettingKeys.DroneWifiMode, desiredWifiMode);

            using (var client = new TelloSdkClient(droneIPAddressText.text, TelloSdkClientFlags.Control))
            {
                await client.StartAsync();
                switch (desiredWifiMode)
                {
                    case WifiMode.AccessPoint:
                        _ = client.EnableWifiAccessPointMode(wifiSsidText.text, wifiPasswordText.text);
                        break;
                    case WifiMode.Station:
                        _ = client.EnableWifiStationMode(wifiSsidText.text, wifiPasswordText.text);
                        break;

                }
                client.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
    }

    [UsedImplicitly]
    public void ShowMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }


    [UsedImplicitly]
    public void OnOkButtonClicked()
    {
        SetDroneWifiSettings();
        ShowMainMenu();
    }

    [UsedImplicitly]
    public void OnCancelButtonClicked()
    {
        ShowMainMenu();
    }
}
