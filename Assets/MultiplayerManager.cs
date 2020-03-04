using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MultiplayerManager : MonoBehaviour
{
    private TelloSdkClient _telloClient;

    public PAUI_Joystick joystick;
    public GameObject batteryPanel;
    public GameObject droneEnginesToggle;
    public GameObject augmentedDrone;
    public UnityEngine.UI.Text statusText;

    private Button droneEnginesToggleButton;
    private TMP_Text droneEnginesToggleText;
    private Sprite takeOffSprite;
    private Sprite landSprite;
    private uint _frameNumber;

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

    private void Start()
    {
        takeOffSprite = Resources.Load<Sprite>("Buttons/up");
        landSprite = Resources.Load<Sprite>("Buttons/down");

        droneEnginesToggleButton = droneEnginesToggle.GetComponentInChildren<Button>();
        droneEnginesToggleText = droneEnginesToggle.GetComponentInChildren<TMP_Text>();

        var droneIPAddress = PlayerPrefs.GetString("DroneIPAddress", TelloSdkClient.DefaultHostName);
        _telloClient = new TelloSdkClient(droneIPAddress, TelloSdkClientFlags.ControlAndTelemetry);

        var bpm = batteryPanel?.GetComponentInParent<BatteryPanelManager>();
        if (bpm != null)
            bpm.telloClient = _telloClient;
        _telloClient.EnableMissionMode = true;
        _telloClient.StartAsync();
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
    private void Update()
    {
        var client = _telloClient;
        if (client != null)
        {
            var telemetryHistory = client.TelemetryHistory;
            var telemetry = telemetryHistory?[0];
            client.StickData.Throttle = (sbyte)(100 * joystick.outputVector.y);
            if (telemetry != null)
            {
                var newHeight = telemetryHistory[0].Height;
                var oldHeight = telemetryHistory[1] != null ? telemetryHistory[1].Height : 0;
                _filteredDroneHeight = 0.2283F * _filteredDroneHeight + 0.3859F * (newHeight + oldHeight);

                augmentedDrone.transform.localPosition = new Vector3(
                        augmentedDrone.transform.localPosition.x,
                        augmentedDrone.transform.localPosition.y,
                        20 + _filteredDroneHeight);
                statusText.text = $"height: {telemetry.Height} mpz: {telemetry.MissionPadCoordinates.Z} mid: {telemetry.MissionPadId}";
            }
        }
    }
}
