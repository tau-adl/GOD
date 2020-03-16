using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerMode MultiplayerMode { get; set; } = MultiplayerMode.LocalServer;

    private TelloSdkClient _telloClient;

    public NetworkDiscovery networkDiscovery;
    public PAUI_Joystick leftJoystick;
    public PAUI_Joystick rightJoystick;
    public GameObject batteryPanel;
    public GameObject droneEnginesToggle;
    public GameObject augmentedDrone;
    public GameObject realDrone;
    public GameObject ball;
    public UnityEngine.UI.Text statusText;

    private Button droneEnginesToggleButton;
    private TMP_Text droneEnginesToggleText;
    private Sprite takeOffSprite;
    private Sprite landSprite;
    private uint _frameNumber;

    private Vector3 initialRealDronePosition;
    private Vector3 initialAugmentedDronePosition;
    
    public void ShowMenu()
    {
        SceneManager.LoadScene("SettingsMenu");
    }

    public async void ToggleDroneEngines()
    {
        await _telloClient.TakeOffAsync();
        /*
        switch (_telloClient.MotorsStatus)
        {
            case TelloMotorStatus.Off:
                await _telloClient.TakeOffAsync();
                break;
            case TelloMotorStatus.On:
                await _telloClient.LandAsync();
                break;
        }*/
    }

    private static IPAddress GetLocalIPAddress(string droneHostName)
    {
        var droneIPAddress = System.Net.Dns.GetHostAddresses(droneHostName).FirstOrDefault();
        if (droneIPAddress == null)
            throw new ApplicationException($"Drone hostname '{droneHostName}' cannot be resolved.");
        var droneAddrBytes = droneIPAddress.GetAddressBytes();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            // make sure the interface is up:
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            // make sure that the interface is a Wifi of an Ethernet NIC:
            switch (nic.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Wireless80211:
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.GigabitEthernet:
                    break;
                default:
                    continue;
            }

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != droneIPAddress.AddressFamily)
                    continue;
                var maskBytes = addr.IPv4Mask.GetAddressBytes();
                var addrBytes = addr.Address.GetAddressBytes();
                var found = true;
                for (int i = 0; i < addrBytes.Length; ++i)
                {
                    if ((addrBytes[i] & maskBytes[i]) != (droneAddrBytes[i] & maskBytes[i]))
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return addr.Address;
            }
        }
        return null;
    }

    private void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        takeOffSprite = Resources.Load<Sprite>("Buttons/up");
        landSprite = Resources.Load<Sprite>("Buttons/down");

        droneEnginesToggleButton = droneEnginesToggle.GetComponentInChildren<Button>();
        droneEnginesToggleText = droneEnginesToggle.GetComponentInChildren<TMP_Text>();

        initialRealDronePosition = realDrone.transform.localPosition;
        initialAugmentedDronePosition = augmentedDrone.transform.localPosition;

        var droneIPAddress = PlayerPrefs.GetString("DroneIPAddress", TelloSdkClient.DefaultHostName);
        var localIPAddress = GetLocalIPAddress(droneIPAddress);
        var networkManager = FindObjectOfType<NetworkManager>();

        if (localIPAddress == null)
        {
            statusText.text = "Error: the drone must be on the same subnet.";
            return;
        }
        networkManager.networkAddress = localIPAddress.ToString();
        networkDiscovery.useNetworkManager = true;
        //networkDiscovery.broadcastData =            Application.productName + Application.version + '@' + SystemInfo.deviceName;
        networkDiscovery.Initialize();
        switch (MultiplayerMode)
        {
            case MultiplayerMode.LocalClient:
                networkDiscovery.StartAsClient();
                break;
            case MultiplayerMode.LocalServer:
                networkDiscovery.StartAsServer();
                break;
            default:
                throw new NotSupportedException($"MultiplayerMode \'{MultiplayerMode}\' is not supported.");
        }

        _telloClient = new TelloSdkClient(droneIPAddress, TelloSdkClientFlags.ControlAndTelemetry);

        var bpm = batteryPanel?.GetComponentInParent<BatteryPanelManager>();
        if (bpm != null)
            bpm.telloClient = _telloClient;
        _telloClient.EnableMissionMode = true;
        _ = _telloClient.StartAsync();
        //{
        //    EditorUtility.DisplayDialog(
        //        "Error",
        //        "Could not commnicate with the drone.\n" +
        //        "Please check drone settings."
        //        , "ok");
        //}
    }

    private void OnDestroy()
    {
        if (_telloClient != null)
            _telloClient.Close();
    }

    private float _filteredDroneHeight = 0;

    private int ballSpeed;

    private void Update()
    {
        var client = _telloClient;

        if (client != null)
        {
            var telemetryHistory = client.TelemetryHistory;
            var telemetry = telemetryHistory?[0];
            client.StickData.Throttle = (sbyte)(100 * leftJoystick.outputVector.y);
            client.StickData.Roll = (sbyte)(100 * rightJoystick.outputVector.y);
            client.StickData.Pitch = (sbyte)(-100 * rightJoystick.outputVector.x);
            if (telemetry != null)
            {
                var newHeight = telemetryHistory[0].Height;
                var oldHeight = telemetryHistory[1] != null ? telemetryHistory[1].Height : 0;
                _filteredDroneHeight = 0.2283F * _filteredDroneHeight + 0.3859F * (newHeight + oldHeight);

                //realDrone.transform.localPosition = new Vector3(
                //            augmentedDrone.transform.localPosition.x,
                //            augmentedDrone.transform.localPosition.y,
                //            _filteredDroneHeight);
                realDrone.transform.localPosition = new Vector3(
                            realDrone.transform.localPosition.x,
                            initialRealDronePosition.y + realDrone.transform.localScale.y * (_filteredDroneHeight / 10),
                            realDrone.transform.localPosition.z);
                statusText.text = $"height: {telemetry.Height} mpz: {telemetry.MissionPadCoordinates.Z} mid: {telemetry.MissionPadId}";
            }
        }
    }
}
