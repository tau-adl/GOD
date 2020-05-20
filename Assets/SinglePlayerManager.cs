using System;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;
using Random = System.Random;

// ReSharper disable MergeConditionalExpression
// ReSharper disable UseNullPropagation


public class SinglePlayerManager : MonoBehaviour
{
    #region Public Fields

    public PAUI_Joystick leftJoystick;
    public PAUI_Joystick rightJoystick;
    public GameObject augmentedDrone;
    public GameObject realDrone;
    public GameObject realDronePositionProvider;
    public GameObject droneStickerTarget;
    public GameObject ball;
    public GameObject readyButton;
    public UnityEngine.UI.Text scoreText;
    public GameObject midAirPositioner;
    public GameObject repositionStageButton;
    public GameObject menuButton;
    public GameObject playground;

    #endregion Public Fields

    #region Private Fields

    private TelloSdkClient _droneClient;
    private StatusPanelManager _statusPanelManager;
    private Rigidbody _ballRigidbody;
    private Vector3 _initialRealDronePosition;
    private Vector3 _initialPlaygroundPosition;
    private Vector3 _initialDroneStickerTargetPosition;
    private Vector3 _initialBallPosition;
    private DroneTelemetry _droneTelemetry;
    private DroneControl _droneControl;
    private UserDialog _userDialog;
    private int _statusFlags;
    private float _ballSpeed = GodSettings.Defaults.BallSpeed;
    private volatile bool _gameStarted;
    private bool _midAirStagePositioned;
    private Vector3 _dronePositionScaleFactor;
    private Vector3 _dronePositionBias;
    private byte _joystickSensitivity;
    private Vector3 _droneLimitsMin;
    private Vector3 _droneLimitsMax;
    private Vector3 _ballLimitsMin;
    private Vector3 _ballLimitsMax;
    private Transform _leftWall;
    private Transform _rightWall;
    private Transform _ceiling;
    private Transform _floor;
    private float _stickerPositionRadius = 20;
    private float _ballMovementSmoothingFactor = GodSettings.Defaults.BallMovementSmoothingFactor;
    private bool _droneStickerFound;
    private bool _useGravity;

    #endregion Private Fields

    #region Properties

    public GameStatusFlags GameStatus => (GameStatusFlags)_statusFlags;

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
        if (_droneClient != null)
            _droneClient.Dispose();
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
        _ballRigidbody = ball.GetComponent<Rigidbody>();
        _initialRealDronePosition = realDrone.transform.localPosition;
        _initialPlaygroundPosition = playground.transform.localPosition;
        _initialBallPosition = ball.transform.localPosition;
        _initialDroneStickerTargetPosition = droneStickerTarget.transform.localPosition;
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
        _userDialog.IsVisible = false;
        // get game settings:
        _dronePositionScaleFactor = GodSettings.GetDronePositionScaleFactor();
        _dronePositionBias = GodSettings.GetDronePositionBias();
        _joystickSensitivity = GodSettings.GetJoystickSensitivity();
        _ballSpeed = GodSettings.GetBallSpeed();
        _ballMovementSmoothingFactor = GodSettings.GetBallMovementSmoothingFactor();
        // compute static fields:
        FindPlaygroundWalls();
        ComputeDroneLimits();
        ComputeBallLimits();
        // initialize GUI mode:
        var gravity = GodSettings.GetGravity();
        _useGravity = Math.Abs(gravity) > 0.1;
        if (_useGravity)
            Physics.gravity = new Vector3(0, gravity, 0);
        OnRepositionStageButtonClicked();
        try
        {
            var droneHostName = GodSettings.GetDroneHostName();
            _droneClient = new TelloSdkClient(droneHostName, TelloSdkClientFlags.ControlAndTelemetry);
            _droneTelemetry.Connect(_droneClient);
            _droneControl.Connect(_droneClient);
            _ = _droneClient.StartAsync();
        }
        catch (Exception ex)
        {
            _userDialog.HeaderText = "Can't Connect To Drone";
            _userDialog.BodyText =
                "Connection with drone could not be established.\n" +
                "Make sure you are connected to the same Wifi network " +
                "and that drone settings are correct.\n" +
                "Details: " + ex.Message;
            _userDialog.ShowCancelButton = false;
            _userDialog.ShowOkButton = true;
            _userDialog.IsVisible = true;
            return;
        }
        InvokeRepeating("UpdateGameStatus", 0, 1);
    }

    [UsedImplicitly]
    private void FixedUpdate()
    {
        // process update message:
        ProcessLastGodUpdate();
        // make sure the ball is still inside the playground:
        ball.transform.localPosition = EnforceBallLimits(ball.transform.localPosition);
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
            _droneControl.StickData.Throttle = (sbyte)(_joystickSensitivity * rightJoystick.outputVector.y);
            // update real-drone position:
            if (_droneStickerFound)
            {
                var stickerPosition = _dronePositionBias + GetNormalizedStickerTargetPosition();
                var dronePositionDelta = new Vector3(0, stickerPosition.y, 0);
                var playgroundPositionDelta = new Vector3(stickerPosition.x, 0, stickerPosition.z);
                realDrone.transform.localPosition = EnforceDroneLimits(_initialRealDronePosition + dronePositionDelta);
                playground.transform.localPosition = _initialPlaygroundPosition + playgroundPositionDelta;
                //playground.transform.rotation = realDronePositionProvider.transform.rotation;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    [UsedImplicitly]
    private void LateUpdate()
    {
        // maintain constant ball speed:
        if (_gameStarted)
        {
            _ballRigidbody = ball.GetComponent<Rigidbody>();
            var currentVelocity = _ballRigidbody.velocity;
            var targetVelocity = currentVelocity.normalized * _ballSpeed;
            if (Math.Abs(targetVelocity.x) < 1)
                targetVelocity.x = Math.Sign(targetVelocity.x);
            if (Math.Abs(targetVelocity.y) < 1)
                targetVelocity.y = Math.Sign(targetVelocity.y);
            //targetVelocity.z = 0;
            //targetVelocity = targetVelocity.normalized * _ballSpeed;
            _ballRigidbody.velocity = Vector3.Lerp(
                currentVelocity, targetVelocity, Time.deltaTime * _ballMovementSmoothingFactor);
        }
    }

    [UsedImplicitly]
    private void UpdateGameStatus()
    {
        var myFlags = (GameStatusFlags)_statusFlags;
        var droneTelemetry = _droneTelemetry.Telemetry;
        var droneAirborne = droneTelemetry != null && droneTelemetry.Height > 0;
        if (myFlags.HasFlag(GameStatusFlags.DroneAirborne) ^ droneAirborne)
            ChangeStatusFlag(GameStatusFlags.DroneAirborne, droneAirborne);

        if (!_gameStarted)
        {
            if (myFlags.HasFlag(GameStatusFlags.UserReady))
            {
                if (myFlags.HasFlag(GameStatusFlags.AllClear))
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
        else if (myFlags.HasFlag(GameStatusFlags.DroneAirborne) && _ballRigidbody.isKinematic)
        {
            _ballRigidbody.transform.localPosition = _initialBallPosition;
            // allow ball motion:
            _ballRigidbody.isKinematic = false;
            _ballRigidbody.useGravity = _useGravity;
            if (!_useGravity)
            {
                var random = new Random();
                var xDirection = random.Next(0, 1) == 0 ? -1 : 1;
                var yDirection = random.Next(0, 1) == 0 ? -1 : 1;
                var force = new Vector3(xDirection, yDirection, 0).normalized * _ballSpeed;
                _ballRigidbody.AddForce(force);
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
        ShowMenu();
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

    [UsedImplicitly]
    public void OnStopButtonClicked()
    {
        StopGame();
    }

    [UsedImplicitly]
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
        _droneStickerFound = true;
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

    private void FindPlaygroundWalls()
    {
        foreach (Transform child in playground.transform)
        {
            switch (child.name)
            {
                case "LeftWall":
                    _leftWall = child;
                    break;
                case "RightWall":
                    _rightWall = child;
                    break;
                case "Ceiling":
                    _ceiling = child;
                    break;
                case "Floor":
                    _floor = child;
                    break;
            }
        }
        if (new[] { _leftWall, _rightWall, _ceiling, _floor }.Any(plane => plane == null))
            Debug.LogError("Could not find all playground walls.");
    }

    private void ComputeDroneLimits()
    {
        var realDroneBox = realDrone.transform.GetChild(0);
        var droneSize = realDroneBox.localScale;
        var minX = _leftWall.localPosition.x + droneSize.x / 2;
        var maxX = _rightWall.localPosition.x - droneSize.x / 2;
        var minY = _floor.localPosition.y + droneSize.y / 2;
        var maxY = _ceiling.localPosition.y - droneSize.y / 2;
        _droneLimitsMin = new Vector3(minX, minY, float.NegativeInfinity);
        _droneLimitsMax = new Vector3(maxX, maxY, float.PositiveInfinity);
    }

    private void ComputeBallLimits()
    {
        var ballSize = ball.transform.localScale;
        var minX = _leftWall.localPosition.x + ballSize.x / 2;
        var maxX = _rightWall.localPosition.x - ballSize.x / 2;
        var minY = _floor.localPosition.y + ballSize.y / 2;
        var maxY = _ceiling.localPosition.y - ballSize.y / 2;
        _ballLimitsMin = new Vector3(minX, minY, 0);
        _ballLimitsMax = new Vector3(maxX, maxY, 0);
    }

    private static Vector3 EnforceLimits(Vector3 position, Vector3 min, Vector3 max)
    {
        var x = position.x;
        var y = position.y;
        var z = position.z;
        if (position.x < min.x)
            x = min.x;
        else if (position.x > max.x)
            x = max.x;
        if (position.y < min.y)
            y = min.y;
        else if (position.y > max.y)
            y = max.y;
        if (position.z < min.z)
            z = min.z;
        else if (position.z > max.z)
            z = max.z;
        return new Vector3(x, y, z);
    }

    private Vector3 EnforceBallLimits(Vector3 ballPosition)
    {
        return EnforceLimits(ballPosition, _ballLimitsMin, _ballLimitsMax);
    }

    private Vector3 EnforceDroneLimits(Vector3 dronePosition)
    {
        return EnforceLimits(dronePosition, _droneLimitsMin, _droneLimitsMax);
    }

    private void ProcessLastGodUpdate()
    {
        augmentedDrone.transform.localPosition = EnforceDroneLimits(realDrone.transform.localPosition);
    }

    private Vector3 GetNormalizedStickerTargetPosition()
    {
        var x = (realDronePositionProvider.transform.position.x - _initialDroneStickerTargetPosition.x);
        var y = (realDronePositionProvider.transform.position.y - _initialDroneStickerTargetPosition.y);
        var z = (realDronePositionProvider.transform.position.z - _initialDroneStickerTargetPosition.z);
        // limit sticker position coordinates:
        if (Math.Abs(x) > _stickerPositionRadius)
            x = Math.Sign(x) * _stickerPositionRadius;
        if (Math.Abs(y) > _stickerPositionRadius)
            y = Math.Sign(y) * _stickerPositionRadius;
        if (Math.Abs(z) > _stickerPositionRadius)
            z = Math.Sign(z) * _stickerPositionRadius;
        return new Vector3(x * _dronePositionScaleFactor.x, y * _dronePositionScaleFactor.y, z * _dronePositionScaleFactor.z);
    }
}
