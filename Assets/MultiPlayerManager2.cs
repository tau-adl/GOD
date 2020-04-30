using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;
// ReSharper disable MergeConditionalExpression


public class MultiPlayerManager2 : MonoBehaviour
{
    #region Public Fields

    public PAUI_Joystick leftJoystick;
    public PAUI_Joystick rightJoystick;
    public GameObject augmentedDrone;
    public GameObject realDrone;
    public GameObject realDronePositionProvider;
    public GameObject ball;
    public GameObject readyButton;
    public UnityEngine.UI.Text scoreText;
    public GameObject droneStickerTarget;
    public GameObject midAirPositioner;
    public GameObject repositionStageButton;
    public GameObject menuButton;
    public GameObject playground;

    #endregion Public Fields

    #region Private Fields

    private StatusPanelManager _statusPanelManager;
    private Rigidbody _ballRigidbody;
    private Vector3 _initialRealDronePosition;
    private Vector3 _initialPlaygroundPosition;
    private Vector3 _initialAugmentedDronePosition;
    private Vector3 _initialRealDronePositionProviderPosition;
    private Vector3 _initialBallPosition;
    private GodDiscovery _discovery;
    private GodMultiPlayerConnection _multiPlayerConnection;
    private DroneTelemetry _droneTelemetry;
    private DroneControl _droneControl;
    private GodUpdateDatagram _lastIncomingGodUpdate;
    private UserDialog _userDialog;
    private int _statusFlags;
    private float _ballForceX = 50.0F;
    private volatile bool _gameStarted;
    private bool _demoMode;
    private bool _midAirStagePositioned;
    private GameObject _targetScaleAdapter;
    private Vector3 _dronePositionScaleFactor;
    private Vector3 _dronePositionBias;
    private byte _joystickSensitivity;
    private Vector3 _droneLimitsMin;
    private Vector3 _droneLimitsMax;

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
            _droneControl.StatusChanged -= DroneControl_StatusChanged;
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
        _multiPlayerConnection.SetDatagramReceivedCallback(MultiPlayerConnection_DatagramReceived);
        _multiPlayerConnection.StatusChanged += MultiPlayerConnection_StatusChanged;
        _ballRigidbody = ball.GetComponent<Rigidbody>();
        _discovery = FindObjectOfType<GodDiscovery>();
        _discovery.SetPartnerDiscoveredCallback(GodDiscovery_PartnerDiscovered);
        _initialRealDronePosition = realDrone.transform.localPosition;
        _initialPlaygroundPosition = playground.transform.localPosition;
        _initialAugmentedDronePosition = augmentedDrone.transform.localPosition;
        _initialRealDronePositionProviderPosition = realDronePositionProvider.transform.position;
        _initialBallPosition = ball.transform.localPosition;
        _targetScaleAdapter = droneStickerTarget.transform.GetChild(0).gameObject;
        _droneTelemetry = FindObjectOfType<DroneTelemetry>();
        _droneTelemetry.StatusChanged += DroneTelemetry_StatusChanged;
        _droneControl = FindObjectOfType<DroneControl>();
        _droneControl.StatusChanged += DroneControl_StatusChanged;
        //_droneCommunication = FindObjectOfType<DroneCommunication>();
        //_droneCommunication.StatusChanged += DroneTelemetry_StatusChanged;
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
        _userDialog.IsVisible = false;
        // get game settings:
        _demoMode = GodSettings.GetDemoMode();
        _dronePositionScaleFactor = GodSettings.GetDronePositionScaleFactor();
        _dronePositionBias = GodSettings.GetDronePositionBias();
        _joystickSensitivity = GodSettings.GetJoystickSensitivity();
        // compute static fields:
        ComputeDroneLimits();
        // enable networking:
        Debug.Log("Starting discovery");
        _discovery.StartDiscovery();
        // initialize GUI mode:
        OnRepositionStageButtonClicked();
        InvokeRepeating("UpdateGameStatus", 0, 1);
    }

    [UsedImplicitly]
    private void Update()
    {
        try
        {
            // update stick data:
            _droneControl.StickData.Yaw = (sbyte)(-_joystickSensitivity * leftJoystick.outputVector.x);
            _droneControl.StickData.Roll = (sbyte)(_joystickSensitivity * leftJoystick.outputVector.y);
            _droneControl.StickData.Pitch = (sbyte)(-_joystickSensitivity * rightJoystick.outputVector.x);
            _droneControl.StickData.Throttle = (sbyte) (_joystickSensitivity * rightJoystick.outputVector.y);
            var stickerPosition = _dronePositionBias + GetNormalizedStickerTargetPosition();
            // update real-drone position:
            var z = stickerPosition.z;
            // limit z value:
            if (Math.Abs(z) > 20)
                z = Math.Sign(z) * 20;
            stickerPosition = new Vector3(stickerPosition.x, stickerPosition.y, 0);
            realDrone.transform.localPosition = EnforceDroneLimits(_initialRealDronePosition + stickerPosition);
            playground.transform.localPosition = new Vector3(
                playground.transform.localPosition.x,
                playground.transform.localPosition.y,
                _initialPlaygroundPosition.z + z);
            // send update message:
            SendGodUpdate();
            // process update message:
            ProcessLastGodUpdate();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    [UsedImplicitly]
    private void UpdateGameStatus()
    {
        var lastIncomingGodUpdate = _lastIncomingGodUpdate;
        var myFlags = (GameStatusFlags) _statusFlags;
        var partnerFlags = lastIncomingGodUpdate != null ? lastIncomingGodUpdate.GameStatus : GameStatusFlags.None;
        var partnerReady = partnerFlags.HasFlag(GameStatusFlags.AllClear);
        if (partnerReady ^ myFlags.HasFlag(GameStatusFlags.PartnerReady))
            myFlags = ChangeStatusFlag(GameStatusFlags.PartnerReady, partnerReady);

        var droneTelemetry = _droneTelemetry.Telemetry;
        var droneAirborne = droneTelemetry != null && droneTelemetry.Height > 0;
        if (myFlags.HasFlag(GameStatusFlags.DroneAirborne) ^ droneAirborne)
            ChangeStatusFlag(GameStatusFlags.DroneAirborne, droneAirborne);

        if (!_gameStarted)
        {
            if (myFlags.HasFlag(GameStatusFlags.UserReady))
            {
                if (myFlags.HasFlag(GameStatusFlags.AllClear) && (_demoMode || partnerReady))
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
                        builder.AppendLine("- Drone sticker was not detected...\n" +
                                           "  Please make sure it can be seen by the camera.");
                    if (!myFlags.HasFlag(GameStatusFlags.DroneReady))
                        builder.AppendLine("- Waiting for the drone to become ready...");
                    if (!myFlags.HasFlag(GameStatusFlags.PartnerConnected))
                        builder.AppendLine("- Waiting for the other player to connect...");
                    else if (!myFlags.HasFlag(GameStatusFlags.PartnerReady))
                        builder.AppendLine("- Waiting for the other player to become ready...");
                    _userDialog.BodyText = builder.ToString();
                    _userDialog.IsVisible = true;
                }
            }
            else
            {
                _userDialog.IsVisible = false;
                readyButton.SetActive(_midAirStagePositioned);
            }
        }
        else
        {
            if (_gameStarted && !partnerFlags.HasFlag(GameStatusFlags.UserReady) && !_demoMode)
                StopGame();
            else if (myFlags.HasFlag(GameStatusFlags.DroneAirborne) &&
                     (_demoMode || partnerFlags.HasFlag(GameStatusFlags.DroneAirborne)))
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
    
    [UsedImplicitly]
    public void ShowMenu()
    {
        SceneManager.LoadScene("MainMenu");
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

    [UsedImplicitly]
    public void SetUserReady()
    {
        SetStatusFlag(GameStatusFlags.UserReady);
        readyButton.SetActive(false);
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
                repositionStageButton.SetActive(false);
                menuButton.SetActive(false);
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
            }
            catch (Exception ex)
            {
                _gameStarted = false;
                repositionStageButton.SetActive(true);
                menuButton.SetActive(true);
                Debug.LogError(ex);
            }
        }
    }

    [UsedImplicitly]
    public void StopGame()
    {
        UnsetStatusFlag(GameStatusFlags.UserReady);
        _gameStarted = false;
        _ballRigidbody.isKinematic = true;
        try
        {
            _ = _droneControl.LandAsync();
        }
        catch
        {
            // suppress exception.
        }

        readyButton.SetActive(_midAirStagePositioned);
        repositionStageButton.SetActive(true);
        menuButton.SetActive(true);
    }

    public void OnStopButtonClicked()
    {
        StopGame();
    }

    public void OnReadyButtonClicked()
    {
        SetUserReady();
    }

    public void OnRepositionStageButtonClicked()
    {
        _statusPanelManager.SetText("Point your camera somewhere and click\n" +
                                    "on the screen to position the game field");
        midAirPositioner.SetActive(true);
        repositionStageButton.SetActive(false);
        readyButton.SetActive(false);
        _midAirStagePositioned = false;
    }

    [UsedImplicitly]
    public void OnDroneStickerFound()
    {
        SetStatusFlag(GameStatusFlags.VuforiaReady);
    }

    [UsedImplicitly]
    public void OnDroneStickerLost()
    {
        UnsetStatusFlag(GameStatusFlags.VuforiaReady);
    }

    [UsedImplicitly]
    public void OnMidAirStagePlaced()
    {
        midAirPositioner.SetActive(false);
        _midAirStagePositioned = true;
        readyButton.SetActive(true);
        repositionStageButton.SetActive(true);
        _statusPanelManager.SetText(string.Empty);
    }

    private void OnVuforiaStarted()
    {
        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
        // set camera settings:
        Debug.Log("Setting focus mode to auto");
        CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
        Debug.Log("Setting exposure-time to auto");
        CameraDevice.Instance.SetField("exposure-time", "auto");
        Debug.Log("Setting iso to auto");
        CameraDevice.Instance.SetField("iso", "auto");
    }

    private bool GodDiscovery_PartnerDiscovered(GodDiscovery sender, IPEndPoint remoteEndPoint, string additionalFields)
    {
        var localIPAddress = NetUtils.GetLocalIPAddress(remoteEndPoint.Address);
        if (localIPAddress == null)
            return false;
        SetStatusFlag(GameStatusFlags.PartnerDiscovered);
        _multiPlayerConnection.Connect(localIPAddress, remoteEndPoint.Address);
        return true;
    }

    private bool MultiPlayerConnection_DatagramReceived(object sender, IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
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
        ChangeStatusFlag(GameStatusFlags.PartnerConnected, e.NewValue == ConnectionStatus.Online);
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
        if (_multiPlayerConnection.IsMaster || _demoMode)
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

    private void ComputeDroneLimits()
    {
        var droneSize = realDrone.transform.localScale;
        Transform leftWall = default;
        Transform rightWall = default;
        Transform topPlane = default;
        Transform bottomPlane = default;
        foreach (Transform child in playground.transform)
        {
            switch (child.name)
            {
                case "LeftWall":
                    leftWall = child;
                    break;
                case "RightWall":
                    rightWall = child;
                    break;
                case "TopPlane":
                    topPlane = child;
                    break;
                case "BottomPlane":
                    bottomPlane = child;
                    break;
            }
        }

        if (new[] {leftWall, rightWall, topPlane, bottomPlane}.Any(plane => plane == null))
        {
            Debug.LogError("Could not compute drone limits - missing planes.");
            return;
        }

        var minX = leftWall.localPosition.x + droneSize.x / 2;
        var maxX = rightWall.localPosition.x - droneSize.x / 2;
        var minY = bottomPlane.localPosition.y + droneSize.y / 2;
        var maxY = topPlane.localPosition.y - droneSize.y / 2;
        _droneLimitsMin = new Vector3(minX, minY, float.NegativeInfinity);
        _droneLimitsMax = new Vector3(maxX, maxY, float.PositiveInfinity);
    }

    private Vector3 EnforceDroneLimits(Vector3 dronePosition)
    {
        var x = dronePosition.x;
        var y = dronePosition.y;
        var z = dronePosition.z;
        if (dronePosition.x < _droneLimitsMin.x)
            x = _droneLimitsMin.x;
        else if (dronePosition.x > _droneLimitsMax.x)
            x = _droneLimitsMax.x;
        if (dronePosition.y < _droneLimitsMin.y)
            y = _droneLimitsMin.y;
        else if (dronePosition.y > _droneLimitsMax.y)
            y = _droneLimitsMax.y;
        if (dronePosition.z < _droneLimitsMin.z)
            z = _droneLimitsMin.z;
        else if (dronePosition.z > _droneLimitsMax.z)
            z = _droneLimitsMax.z;
        return new Vector3(x, y, z);
    }

    private void ProcessLastGodUpdate()
    {
        if (_demoMode)
        {
            var stickerPosition = _dronePositionBias + GetNormalizedStickerTargetPosition();
            stickerPosition = new Vector3(stickerPosition.x, stickerPosition.y, 0);
            augmentedDrone.transform.localPosition = EnforceDroneLimits(_initialAugmentedDronePosition + stickerPosition);
            return;
        }

        var godUpdate = _lastIncomingGodUpdate;
        if (godUpdate == null)
            return;

        // update drone position:
        if (godUpdate.DronePosition.HasValue)
            augmentedDrone.transform.localPosition = godUpdate.DronePosition.Value;

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

    private Vector3 GetNormalizedStickerTargetPosition()
    {
        var x = (realDronePositionProvider.transform.position.x - _initialRealDronePositionProviderPosition.x);
        var y = (realDronePositionProvider.transform.position.y - _initialRealDronePositionProviderPosition.y);
        var z = (realDronePositionProvider.transform.position.z - _initialRealDronePositionProviderPosition.z);
        return new Vector3(x * _dronePositionScaleFactor.x, y * _dronePositionScaleFactor.y, z * _dronePositionScaleFactor.z);
    }

    private void SendGodUpdate()
    {
        try
        {
            var dronePosition = _initialRealDronePosition + GetNormalizedStickerTargetPosition();
            var datagram = new GodUpdateDatagram
            {
                BallPosition = ball.transform.localPosition,
                BallVelocity = _ballRigidbody.velocity,
                DronePosition = dronePosition,
                Score = Score,
                GameStatus = GameStatus
            };
            var count = datagram.Serialize(_godUpdateTxBuffer, 0);
            if (_multiPlayerConnection != null)
                _multiPlayerConnection.Send(_godUpdateTxBuffer, 0, count);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }
}
