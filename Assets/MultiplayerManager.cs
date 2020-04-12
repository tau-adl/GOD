using System;
using System.Net;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

public class MultiplayerManager : MonoBehaviour
{
    private TelloSdkClient _telloClient;

    public int ConnectionTimeoutMS = 2000;
    public PAUI_Joystick leftJoystick;
    public PAUI_Joystick rightJoystick;
    public GameObject batteryPanel;
    public GameObject startButton;
    public GameObject stopButton;
    public GameObject augmentedDrone;
    public GameObject realDrone;
    public GameObject ball;
    public GameObject statusTextObject;
    public UnityEngine.UI.Text scoreText;
    public float ballForceX = 50.0F;

    private Rigidbody _ballRigidbody;
    private Vector3 initialRealDronePosition;
    private Vector3 _initialBallPosition;
    private float _filteredDroneHeight = 0;
    private GodDiscovery _discovery;
    private GodNetworking _networking;
    private string _lastIncomingGodUpdate;
    private DateTime _lastIncomingGodUpdateTime;
    private bool _partnerStatusOk = false;
    private bool _partnerStatusChanged = true;
    private IPAddress _localIPAddress;
    private IPAddress _remoteIPAddress;
    private TMP_Text statusText;

    public GodScore Score { get; private set; }

    public void ShowMenu()
    {
        SceneManager.LoadScene("SettingsMenu");
    }

    public void StartGame()
    {
        _ = _telloClient.TakeOffAsync();
        // stop any ball motion:
        _ballRigidbody.isKinematic = true;
        _ballRigidbody.transform.localPosition = _initialBallPosition;
        // allow ball motion:
        _ballRigidbody.isKinematic = false;
        _ballRigidbody.useGravity = true;
        _ballRigidbody.AddForce(new Vector3(ballForceX, 10F, 0));
    }

    public void StopGame()
    {
        _ = _telloClient.LandAsync();
    }

    private void OnVuforiaStarted()
    {
        // set camera settings:
        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
        CameraDevice.Instance.SetField("exposure-time", "auto");
        CameraDevice.Instance.SetField("iso", "auto");
    }

    private void OnDestroy()
    {
        _telloClient?.Close();
        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
    }

    private bool GodDiscovery_PartnerDiscovered(GodDiscovery sender, IPEndPoint remoteEndPoint, string additionalFields)
    {
        var localIPAddress = NetUtils.GetLocalIPAddress(remoteEndPoint.Address);
        if (localIPAddress == null)
            return false;
        _networking.Connect(localIPAddress, remoteEndPoint.Address);
        _localIPAddress = localIPAddress;
        _remoteIPAddress = remoteEndPoint.Address;
        _partnerStatusChanged = true;
        return true;
    }

    private bool GodNetworking_DatagramReceived(object sender, IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        var godMessage = Encoding.ASCII.GetString(buffer, offset, count);
        var headerLength = godMessage.IndexOf(' ');
        if (headerLength < 0)
            return false; // this is not a valid GOD message - drop the connection.
        var header = godMessage.Substring(0, headerLength);
        switch (header)
        {
            case GodMessageType.Update:
                _lastIncomingGodUpdate = godMessage;
                _lastIncomingGodUpdateTime = DateTime.Now;
                return true;
            default:
                // unsupported GOD message.
                return true; // discard message and keep the connection.
        }
    }


    private void Start()
    {
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        statusText = statusTextObject.GetComponent<TMP_Text>();
        Score = new GodScore();

        _ballRigidbody = ball.GetComponent<Rigidbody>();
        _discovery = FindObjectOfType<GodDiscovery>();
        _networking = FindObjectOfType<GodNetworking>();

        _discovery.SetPartnerDiscoveredCallback(GodDiscovery_PartnerDiscovered);
        _networking.SetDatagramReceivedCallback(GodNetworking_DatagramReceived);

        initialRealDronePosition = realDrone.transform.localPosition;
        _initialBallPosition = ball.transform.localPosition;

        ball.GetComponent<BallBehaviour>().CollisionEnter += Ball_CollisionEnter;

        ballForceX = PlayerPrefs.GetFloat("BallForceX", 5.0F);
        Physics.gravity = new Vector3(0, -PlayerPrefs.GetFloat("Gravity", 9.81F), 0);
        var droneHostName = PlayerPrefs.GetString("DroneHostName", TelloSdkClient.DefaultHostName);
       _telloClient = new TelloSdkClient(droneHostName, TelloSdkClientFlags.ControlAndTelemetry);

        var bpm = batteryPanel.GetComponentInParent<BatteryPanelManager>();
        bpm.telloClient = _telloClient;
        _ = _telloClient.StartAsync();
        _discovery.StartDiscovery();
        //{
        //    EditorUtility.DisplayDialog(
        //        "Error",
        //        "Could not commnicate with the drone.\n" +
        //        "Please check drone settings."
        //        , "ok");
        //}
    }

    private void Ball_CollisionEnter(MonoBehaviour source, Collision collision)
    {
        var go = collision.collider.gameObject;
        if (_networking.IsMaster)
        {
            switch (go.name)
            {
                case "LeftWall":
                    ++Score.MyScore;
                    scoreText.text = $"{Score.MyScore}/{Score.TheirScore}";
                    break;
                case "RightWall":
                    ++Score.TheirScore;
                    scoreText.text = $"{Score.MyScore}/{Score.TheirScore}";
                    break;
            }
        }
    }

    private static string ToString(Vector3 v)
    {
        return $"{v.x}:{v.y}:{v.z}";
    }

    private static bool TryParse(string text, out Vector3 value)
    {
        if (text != null)
        {
            var segments = text.Split(':');
            if (segments.Length == 3 &&
                float.TryParse(segments[0], out float x) &&
                float.TryParse(segments[1], out float y) &&
                float.TryParse(segments[2], out float z))
            {
                value = new Vector3(x, y, z);
                return true;
            }
        }
        value = default;
        return false;
    }

    private void ProcessLastGodUpdate()
    {
        var godUpdate = _lastIncomingGodUpdate;
        if (godUpdate == null)
            return;

        var pairs = godUpdate.Split(' ');
        Vector3? ballPosition = null;
        Vector3? ballSpeed = null;
        Vector3? dronePosition = null;
        GodScore score = null;
        foreach (var pair in pairs)
        {
            var keyLength = pair.IndexOf(':');
            if (keyLength <= 0)
                continue; // invalid key - skip pair.
            var key = pair.Substring(0, keyLength);
            var valueText = pair.Substring(keyLength + 1);
            switch (key)
            {
                case "ball-p":
                    {
                        if (TryParse(valueText, out Vector3 value))
                            ballPosition = value;
                        break;
                    }
                case "ball-v":
                    {
                        if (TryParse(valueText, out Vector3 value))
                            ballSpeed = value;
                        break;
                    }
                case "drone-p":
                    {
                        if (TryParse(valueText, out Vector3 value))
                            dronePosition = value;
                        break;
                    }
                case "score":
                    {
                        if (GodScore.TryParse(valueText, out GodScore value))
                            score = value;
                        break;
                    }
                default:
                    continue; // skip unknown key.
            }
        }
        // update drone position:
        if (dronePosition.HasValue)
            augmentedDrone.transform.localPosition = new Vector3(
                augmentedDrone.transform.localPosition.x,
                dronePosition.Value.y,
                augmentedDrone.transform.localPosition.z);
        
        if (!_networking.IsMaster)
        {
            // update score according to master:
            if (score != null && !Score.ScoreEquals(score))
            {
                Score = score;
                scoreText.text = $"{Score.MyScore}/{Score.TheirScore}";

                // update ball position and speed according to master:
                if (ballPosition.HasValue && ballSpeed.HasValue)
                {
                    _ballRigidbody.isKinematic = true;
                    _ballRigidbody.velocity = ballSpeed.Value;
                    ball.transform.localPosition = ballPosition.Value;
                    _ballRigidbody.isKinematic = false;
                }
            }
        }
    }

    private void SendGodUpdate()
    {
        try
        {
            var godUpdate = $"{GodMessageType.Update}" +
                $" ball-p:{ToString(ball.transform.localPosition)}" +
                $" ball-v:{ToString(_ballRigidbody.velocity)} " +
                $" drone-p:{ToString(realDrone.transform.localPosition)} score:{Score.MyScore}:{Score.TheirScore}";
            _networking.Send(Encoding.ASCII.GetBytes(godUpdate));
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void UpdateRealDronePosition()
    {
        var client = _telloClient;
        if (client != null)
        {
            var telemetryHistory = client.TelemetryHistory;
            var telemetry = telemetryHistory?[0];
            if (telemetry != null)
            {
                var newHeight = telemetryHistory[0].Height;
                var oldHeight = telemetryHistory[1] != null ? telemetryHistory[1].Height : 0;
                _filteredDroneHeight = 0.2283F * _filteredDroneHeight + 0.3859F * (newHeight + oldHeight);
                realDrone.transform.localPosition = new Vector3(
                            realDrone.transform.localPosition.x,
                            initialRealDronePosition.y + realDrone.transform.localScale.y * (_filteredDroneHeight / 10),
                            realDrone.transform.localPosition.z);
            }
        }
    }

    private void Update()
    {
        var client = _telloClient;

        if (client != null)
        {
            // update stick data:
            client.StickData.Throttle = (sbyte) (100 * leftJoystick.outputVector.y);
            client.StickData.Roll = (sbyte) (100 * rightJoystick.outputVector.y);
            client.StickData.Pitch = (sbyte) (-100 * rightJoystick.outputVector.x);
            // update real drone position:
            UpdateRealDronePosition();
            // send update message:
            SendGodUpdate();
            // process update message:
            ProcessLastGodUpdate();
        }

        if (!_partnerStatusChanged)
        {
            var connectionStatusOk = (DateTime.Now - _lastIncomingGodUpdateTime).TotalSeconds < ConnectionTimeoutMS;
            if (connectionStatusOk != _partnerStatusOk)
            {
                _partnerStatusOk = connectionStatusOk;
                _partnerStatusChanged = true;
            }
        }

        if (_partnerStatusChanged)
        {
            statusText.text = _partnerStatusOk
                ? $"Connected to {_remoteIPAddress}"
                : "Looking for partners...";
            _partnerStatusChanged = false;
            startButton.SetActive(_partnerStatusOk && _telloClient.Telemetry != null);
        }
    }
}
