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

    private void Update()
    {
        var client = _telloClient;
        if (client != null)
        {
            var telemetry = client.Telemetry;
            client.StickData.Throttle = (sbyte)(100 * joystick.outputVector.y);
            /*
            if (unchecked(++_frameNumber) % 30 == 0)
            {
                switch (_telloClient.MotorsStatus)
                {
                    case TelloMotorStatus.Off:
                        droneEnginesToggleButton.image.sprite = takeOffSprite;
                        droneEnginesToggleText.text = "take off";
                        break;
                    case TelloMotorStatus.On:
                        droneEnginesToggleButton.image.sprite = landSprite;
                        droneEnginesToggleText.text = "land";
                        break;
                }
            }*/
            if (telemetry != null)
            {
                augmentedDrone.transform.localPosition = new Vector3(
                        augmentedDrone.transform.localPosition.x,
                        augmentedDrone.transform.localPosition.y,
                        10 + telemetry.Height);
                statusText.text = $"height: {telemetry.Height} mpz: {telemetry.MissionPadCoordinates.Z} mid: {telemetry.MissionPadId}";
            }
        }
    }
}
