using System;
using System.Net;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;


public class MultiPlayerManager : MonoBehaviour
{
    #region Public Fields

    public PAUI_Joystick leftJoystick;
    public PAUI_Joystick rightJoystick;
    public GameObject startButton;
    public GameObject stopButton;
    public GameObject augmentedDrone;
    public GameObject realDrone;
    public GameObject ball;
    public GameObject readyButton;
    public UnityEngine.UI.Text scoreText;

    #endregion Public Fields

    #region Private Fields

    private StatusPanelManager _statusPanelManager;
    private Rigidbody _ballRigidbody;
    private Vector3 _initialRealDronePosition;
    private Vector3 _initialBallPosition;
    private float _filteredDroneHeight;
    private GodDiscovery _discovery;
    private GodMultiPlayerConnection _multiPlayerConnection;
    private DroneTelemetry _droneTelemetry;
    private DroneControl _droneControl;
    private GodUpdateDatagram _lastIncomingGodUpdate;
    private UserDialog _userDialog;
    private int _statusFlags;
    private float _ballForceX = 50.0F;
    private volatile bool _gameStarted;

    private readonly byte[] _godUpdateTxBuffer = new byte[GodUpdateDatagram.MaxSize];

    #endregion Private Fields

    #region Properties

    public GameStatusFlags GameStatus => (GameStatusFlags) _statusFlags;

    public GodScore Score { get; private set; }

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void OnDestroy()
    {
        Screen.orientation = ScreenOrientation.AutoRotation;

        // un-register from event handlers:
        try
        {
            VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
        }
        catch
        {
            // suppress exceptions.
        }
        if (_multiPlayerConnection != null)
        {
            _multiPlayerConnection.SetDatagramReceivedCallback(null);
            _multiPlayerConnection.StatusChanged -= MultiPlayerConnection_StatusChanged;
        }
        if (_discovery != null)
            _discovery.SetPartnerDiscoveredCallback(null);
        if (_droneTelemetry != null)
            _droneTelemetry.StatusChanged -= DroneTelemetry_StatusChanged;
        if (_droneControl != null)
            _droneControl.StatusChanged -= TelloClient_StatusChanged;
        if (_userDialog != null)
        {
            _userDialog.OkButtonClick -= UserDialog_OkButtonClick;
            _userDialog.CancelButtonClick -= UserDialog_CancelButtonClick;
        }
    }

    [UsedImplicitly]
    private void Awake()
    {
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        Score = new GodScore();
        // initialize private fields:
        _ballForceX = PlayerPrefs.GetFloat("BallForceX", 5.0F);
        _multiPlayerConnection = FindObjectOfType<GodMultiPlayerConnection>();
        _multiPlayerConnection.SetDatagramReceivedCallback(GodNetworking_DatagramReceived);
        _multiPlayerConnection.StatusChanged += MultiPlayerConnection_StatusChanged;
        _ballRigidbody = ball.GetComponent<Rigidbody>();
        _discovery = FindObjectOfType<GodDiscovery>();
        _discovery.SetPartnerDiscoveredCallback(GodDiscovery_PartnerDiscovered);
        _initialRealDronePosition = realDrone.transform.localPosition;
        _initialBallPosition = ball.transform.localPosition;
        _droneTelemetry = FindObjectOfType<DroneTelemetry>();
        _droneTelemetry.StatusChanged += DroneTelemetry_StatusChanged;
        _droneControl = FindObjectOfType<DroneControl>();
        _droneControl.StatusChanged += DroneControl_StatusChanged;
        _statusPanelManager = FindObjectOfType<StatusPanelManager>();
        ball.GetComponent<BallBehaviour>().CollisionEnter += Ball_CollisionEnter;
        _userDialog = FindObjectOfType<UserDialog>();
        _userDialog.OkButtonClick += UserDialog_OkButtonClick;
        _userDialog.CancelButtonClick += UserDialog_CancelButtonClick;
    }

    [UsedImplicitly]
    private void Start()
    {
        // set screen orientation to horizontal:
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        _gameStarted = false;
        _discovery.StartDiscovery();
        _userDialog.IsVisible = false;
        InvokeRepeating("UpdateGameStatus", 0, 1);
    }

    [UsedImplicitly]
    private void Update()
    {
        try
        {
            // update stick data:
            _droneControl.StickData.Throttle = (sbyte)(100 * leftJoystick.outputVector.y);
            _droneControl.StickData.Roll = (sbyte)(100 * rightJoystick.outputVector.y);
            _droneControl.StickData.Pitch = (sbyte)(-100 * rightJoystick.outputVector.x);
            // update real drone position:
            UpdateRealDronePosition();
            // send update message:
            SendGodUpdate();
            // process update message:
            ProcessLastGodUpdate();

            /*
            var droneStatusChanged = _droneStatusOk != droneStatusOk;
            var partnerStatusChanged = Interlocked.Exchange(ref _partnerStatusChanged, 0) != 0;
            if (partnerStatusChanged || droneStatusChanged
            {
                if (partnerStatusOk && droneStatusOk)
                {
                    _statusText.text = $"Connected to {_remoteIPAddress}";
                    lock (startButton)
                    {
                        startButton.SetActive(!_gameStarted);
                    }
                }
                else
                {
                    _statusText.text = partnerStatusOk
                        ? "Waiting for the drone to connect..."
                        : "Looking for partners...";
                    lock (startButton)
                    {
                        startButton.SetActive(false);
                        _ballRigidbody.isKinematic = true;
                        _gameStarted = false;
                    }
                }

                _droneStatusOk = droneStatusOk;
            }*/
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void UpdateGameStatus()
    {
        var myFlags = (GameStatusFlags) _statusFlags;
        var partnerFlags = _lastIncomingGodUpdate?.GameStatus ?? GameStatusFlags.None;
        if (!_gameStarted)
        {
            if (myFlags.HasFlag(GameStatusFlags.UserReady))
            {
                if (myFlags.HasFlag(GameStatusFlags.AllClear) && myFlags.HasFlag(GameStatusFlags.AllClear))
                {
                    _userDialog.IsVisible = false;
                    StartGame();
                }
                else
                {
                    _userDialog.HeaderText = "Game is Starting";
                    _userDialog.ShowOkButton = false;
                    _userDialog.ShowCancelButton = true;
                    var builder = new StringBuilder();
                    if (!myFlags.HasFlag(GameStatusFlags.VuforiaReady))
                        builder.Append("- Mission pad was not detected...\n" +
                                       "  Please make sure it can be seen by the camera.");
                    if (!myFlags.HasFlag(GameStatusFlags.DroneReady))
                        builder.Append("- Waiting for the drone to become ready...");
                    if (!myFlags.HasFlag(GameStatusFlags.PartnerConnected))
                        builder.Append("- Waiting for a partner to connect...");
                    else if (!myFlags.HasFlag(GameStatusFlags.PartnerReady) || !partnerFlags.HasFlag(GameStatusFlags.AllClear))
                        builder.Append("- Waiting for partner the become ready...");
                    _userDialog.BodyText = builder.ToString();
                    _userDialog.IsVisible = true;
                }
            }
            else
            {
                _userDialog.IsVisible = false;
                readyButton.SetActive(true);
            }
        }
        else if (myFlags.HasFlag(GameStatusFlags.DroneAirborne) && partnerFlags.HasFlag(GameStatusFlags.DroneAirborne))
        {
            if (_ballRigidbody.isKinematic)
            {
                _ballRigidbody.transform.localPosition = _initialBallPosition;
                // allow ball motion:
                _ballRigidbody.isKinematic = false;
                _ballRigidbody.useGravity = true;
                _ballRigidbody.AddForce(new Vector3(_ballForceX, 10F, 0));
            }
        }
    }

    #endregion MonoBehaviour

    private void UserDialog_CancelButtonClick(object sender, EventArgs e)
    {
        UnsetStatusFlag(GameStatusFlags.UserReady);
        _ = _droneControl.LandAsync();
    }

    private void UserDialog_OkButtonClick(object sender, EventArgs e)
    {
    }

    public void ShowMenu()
    {
        SceneManager.LoadScene("SettingsMenu");
    }

    /// <summary>
    /// Safely set a flag on <see cref="GameStatus"/>.
    /// </summary>
    /// <param name="flag">the flag to set.</param>
    /// <returns>The new value of <see cref="GameStatus"/>.</returns>
    private GameStatusFlags SetStatusFlag(GameStatusFlags flag)
    {
        _statusPanelManager.SetStatusFlag(flag);
        return flag.SetOnField(ref _statusFlags);
    }

    /// <summary>
    /// Safely unset a flag on <see cref="GameStatus"/>.
    /// </summary>
    /// <param name="flag">the flag to unset.</param>
    /// <returns>The new value of <see cref="GameStatus"/>.</returns>
    private GameStatusFlags UnsetStatusFlag(GameStatusFlags flag)
    {
        _statusPanelManager.UnsetStatusFlag(flag);
        return flag.UnsetOnField(ref _statusFlags);
    }

    private GameStatusFlags ChangeStatusFlag(GameStatusFlags flag, bool flagValue)
    {   
        return flagValue ? SetStatusFlag(flag) : UnsetStatusFlag(flag);
    }

    /// <summary>
    /// Safely get the value of <see cref="GameStatus"/> and reset the <seealso cref="GameStatusFlags.ValueChanged"/> flag.
    /// </summary>
    /// <returns>The original value of <see cref="GameStatus"/>, before the <seealso cref="GameStatusFlags.ValueChanged"/> flag was reset.</returns>
    private GameStatusFlags GetStatusFlagsAndResetValueChanged()
    {
        return GameStatusFlagsExtensions.GetAndResetChanges(ref _statusFlags);
    }


    public void SetUserReady()
    {
        SetStatusFlag(GameStatusFlags.UserReady);
    }

    [UsedImplicitly]
    private void SendTakeOffCommandBeforeGameStart()
    {
        if (_gameStarted)
        {
            try
            {
                _ = _droneControl.TakeOffAsync();
            }
            catch
            {
                // suppress exceptions
            }
        }
    }

    [UsedImplicitly]
    private void HideUserDialogBeforeGameStart()
    {
        if (_gameStarted)
            _userDialog.IsVisible = false;
    }

    private void StartGame()
    {
        if (!_gameStarted)
        {
            try
            {
                _gameStarted = true;
                _userDialog.HeaderText = "Game is Starting";
                _userDialog.BodyText = "GET READY!\n" +
                                       "The game will start in 3 seconds...\n"
                                       + "WARNING: The drone will take-off automatically!";
                _userDialog.ShowOkButton = false;
                _userDialog.ShowCancelButton = false;
                _userDialog.IsVisible = true;
                Interlocked.MemoryBarrier();
                Invoke("HideUserDialogBeforeGameStart", 3);
                Invoke("SendTakeOffCommandBeforeGameStart", 3);
                _ = _droneControl.TakeOffAsync();
            }
            catch (Exception ex)
            {
                _gameStarted = false;
                Debug.LogError(ex);
            }
        }
        
        /*
        // ReSharper disable once InconsistentlySynchronizedField
        if (!_gameStarted)
        {
            lock (startButton)
            {
                if (!_gameStarted)
                {
                    _ = _telloClient.TakeOffAsync();
                    // stop any ball motion:
                    _ballRigidbody.isKinematic = true;
                    _ballRigidbody.transform.localPosition = _initialBallPosition;
                    // allow ball motion:
                    _ballRigidbody.isKinematic = false;
                    _ballRigidbody.useGravity = true;
                    _ballRigidbody.AddForce(new Vector3(ballForceX, 10F, 0));
                    _gameStarted = true;
                    startButton.SetActive(false);
                }
            }
        }
        */
    }

    [UsedImplicitly]
    public void StopGame()
    {
        UnsetStatusFlag(GameStatusFlags.UserReady);
        _ballRigidbody.isKinematic = true;
        try
        {
            _ = _droneControl.LandAsync();
        }
        catch
        {
            // suppress exception.
        }
        readyButton.SetActive(true);
    }

    [UsedImplicitly]
    public void OnTargetMissionPadFound()
    {
        SetStatusFlag(GameStatusFlags.VuforiaReady);
    }
    [UsedImplicitly]
    public void OnTargetMissionPadLost()
    {
        UnsetStatusFlag(GameStatusFlags.VuforiaReady);
    }

    private void OnVuforiaStarted()
    {
        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
        // set camera settings:
        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
        CameraDevice.Instance.SetField("exposure-time", "auto");
        CameraDevice.Instance.SetField("iso", "auto");
    }

    private bool GodDiscovery_PartnerDiscovered(GodDiscovery sender, IPEndPoint remoteEndPoint, string additionalFields)
    {
        var localIPAddress = NetUtils.GetLocalIPAddress(remoteEndPoint.Address);
        if (localIPAddress == null)
            return false;
        _multiPlayerConnection.Connect(localIPAddress, remoteEndPoint.Address);
        return true;
    }

    private bool GodNetworking_DatagramReceived(object sender, IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        if (GodDatagram.TryDeserialize(buffer, offset, count, out var datagram))
        {
            switch (datagram.Type)
            {
                case GodDatagramType.Update:
                    _lastIncomingGodUpdate = (GodUpdateDatagram) datagram;
                    return true;
            }
        }
        return false; // discard the connection.
    }

    private void MultiPlayerConnection_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        ChangeStatusFlag(GameStatusFlags.PartnerReady, e.NewValue == ConnectionStatus.Online);
    }

    private void TelloClient_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        ChangeStatusFlag(GameStatusFlags.DroneReady, e.NewValue == ConnectionStatus.Online);
    }

    private void DroneTelemetry_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        ChangeStatusFlag(GameStatusFlags.DroneReady, e.NewValue == ConnectionStatus.Online);
    }

    private void DroneControl_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        
    }

    private void Ball_CollisionEnter(MonoBehaviour source, Collision collision)
    {
        var go = collision.collider.gameObject;
        if (_multiPlayerConnection.IsMaster)
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

    

    private void ProcessLastGodUpdate()
    {
        var godUpdate = _lastIncomingGodUpdate;
        if (godUpdate == null)
            return;

        // update drone position:
        if (godUpdate.DronePosition.HasValue)
            augmentedDrone.transform.localPosition = new Vector3(
                augmentedDrone.transform.localPosition.x,
                godUpdate.DronePosition.Value.y,
                augmentedDrone.transform.localPosition.z);
        
        if (!_multiPlayerConnection.IsMaster)
        {
            // update score according to master:
            if (godUpdate.Score != null && !Score.ScoreEquals(godUpdate.Score))
            {
                Score = godUpdate.Score;
                scoreText.text = $"{Score.MyScore}/{Score.TheirScore}";

                // update ball position and speed according to master:
                if (godUpdate.BallPosition.HasValue && godUpdate.BallVelocity.HasValue)
                {
                    _ballRigidbody.isKinematic = true;
                    ball.transform.localPosition = godUpdate.BallPosition.Value;
                    _ballRigidbody.isKinematic = false;
                    _ballRigidbody.velocity = godUpdate.BallVelocity.Value;
                }
            }
        }
    }

    private void SendGodUpdate()
    {
        try
        {
            var datagram = new GodUpdateDatagram
            {
                BallPosition = ball.transform.localPosition,
                BallVelocity = _ballRigidbody.velocity,
                DronePosition = realDrone.transform.localPosition,
                Score = Score,
                GameStatus = GameStatus
            };
            var count = datagram.Serialize(_godUpdateTxBuffer, 0);
            _multiPlayerConnection.Send(_godUpdateTxBuffer, 0, count);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void UpdateRealDronePosition()
    {
        var telemetryHistory = _droneTelemetry.TelemetryHistory;
        var telemetry = telemetryHistory?[0];
        if (telemetry != null)
        {
            var newHeight = telemetryHistory[0].Height;
            var oldHeight = telemetryHistory[1]?.Height ?? 0;
            _filteredDroneHeight = 0.2283F * _filteredDroneHeight + 0.3859F * (newHeight + oldHeight);
            realDrone.transform.localPosition = new Vector3(
                realDrone.transform.localPosition.x,
                _initialRealDronePosition.y + realDrone.transform.localScale.y * (_filteredDroneHeight / 10),
                realDrone.transform.localPosition.z);
        }
    }
}
