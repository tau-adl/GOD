using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using UnityEngine;
// ReSharper disable UseNullPropagation

// ReSharper disable JoinNullCheckWithUsage

[Flags]
public enum TelloSdkClientFlags
{
    None,
    Control = 1,
    Telemetry = 2,
    Video = 4,
    ControlAndTelemetry = Control | Telemetry,
    ControlAndVideo = Control | Video,
    All = Control | Telemetry | Video
}

public class TelloSdkClient 
    : IDisposable
{
    #region Defaults

    public const int DefaultControlUdpPort = 8889;
    public const int DefaultTelemetryUdpPort = 8890;
    public const string DefaultHostName = "192.168.10.1";
    public const int DefaultStickDataIntervalMS = 20;
    public const int KeepAliveIntervalMS = 5000;
    public const string DefaultRegion = "US";
    public const int DefaultTimeoutMS = 3000;
    public const int DefaultRetries = 0;

    #endregion Defaults

    #region Private Fields

    private readonly UdpClient _commandChannel;
    private readonly UdpClient _telemetryChannel;
    private readonly TelloSdkClientFlags _clientFlags;
    private readonly IPEndPoint _droneEndPoint;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly System.Timers.Timer _stickDataTimer = new System.Timers.Timer();
    private readonly System.Timers.Timer _keepAliveTimer = new System.Timers.Timer();
    private TelloSdkStickData _stickData = new TelloSdkStickData();
    private bool _enableMissionMode;
    private ConnectionStatus _status = ConnectionStatus.Offline;

    #endregion Private Fields

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    #endregion Events

    #region Public Properties

    public TelloSdkStickData StickData
    {
        get => _stickData;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            _stickData = value;
        }
    }
    
    public TelloSdkClientFlags ClientFlags => _clientFlags;

    public TelloSdkTelemetry Telemetry { get; private set; }

    public string HostName { get; private set; }

    public int ControlUdpPort { get; private set; }

    public int TelemetryUdpPort { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool IsStarted { get; private set; }

    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                var e = new ConnectionStatusChangedEventArgs(_status, value);
                _status = value;
                OnStatusChanged(e);
            }
        }
    }

    public int StickDataIntervalMS { get; set; } = DefaultStickDataIntervalMS;

    public bool EnableMissionMode { get => _enableMissionMode;
        set
        {
            if (_enableMissionMode != value)
            {
                if (IsStarted)
                    throw new InvalidOperationException();
                _enableMissionMode = value;
            }
        }
    }

    #endregion Public Properties

    #region Construction & Destruction

    public TelloSdkClient(string hostname = DefaultHostName,
        TelloSdkClientFlags clientFlags = TelloSdkClientFlags.Control,
        int controlUdpPort = DefaultControlUdpPort, int telemetryUdpPort = DefaultTelemetryUdpPort)
    {
        if (hostname == null)
            throw new ArgumentNullException(nameof(hostname));
        if (controlUdpPort <= 0 || controlUdpPort > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(controlUdpPort), controlUdpPort,
                $"Argument '{nameof(controlUdpPort)}' is out of range.");
        if (telemetryUdpPort <= 0 || telemetryUdpPort > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(telemetryUdpPort), telemetryUdpPort,
                $"Argument '{nameof(telemetryUdpPort)}' is out of range.");
        HostName = hostname;
        ControlUdpPort = controlUdpPort;
        TelemetryUdpPort = telemetryUdpPort;
        _clientFlags = clientFlags;
        // resolve Tello hostname and determine the local IP address for communicating with it:
        var hostResolver = new UdpClient(hostname, ControlUdpPort);
        var localIPAddress = ((IPEndPoint)hostResolver.Client.LocalEndPoint).Address;
        var telloIPAddress = ((IPEndPoint)hostResolver.Client.RemoteEndPoint).Address;
        hostResolver.Close();
        // create control and telemetry channels:
        if (_clientFlags.HasFlag(TelloSdkClientFlags.Control))
        {
            _commandChannel = new UdpClient() {ExclusiveAddressUse = false};
            // patch for Unity: multiple listening UDP clients don't work.
            var controlChannelPort =
                _clientFlags.HasFlag(TelloSdkClientFlags.Telemetry)
                    ? TelemetryUdpPort
                    : ControlUdpPort;
            _commandChannel.Client.Bind(new IPEndPoint(IPAddress.Any, controlChannelPort));
            _droneEndPoint = new IPEndPoint(telloIPAddress, ControlUdpPort);
            _telemetryChannel = _commandChannel;
            Status = ConnectionStatus.Connected;
        }
        else
        {
            throw new NotImplementedException();
            // handle telemetry only channel.
        }
        //_telemetryChannel = new UdpClient() { ExclusiveAddressUse = false };
        
        //_telemetryChannel.Client.Bind(new IPEndPoint(localIPAddress, TelemetryUdpPort));
        //_telemetryChannel.Connect(new IPEndPoint(telloIPAddress, telloUdpPort));
    }

    ~TelloSdkClient()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (_stickDataTimer != null)
                _stickDataTimer.Dispose();
            if (_keepAliveTimer != null)
                _keepAliveTimer.Dispose();
            if (_telemetryChannel != null)
                _telemetryChannel.Dispose();
            if (_commandChannel != null)
                _commandChannel.Dispose();
            if (_cts != null)
                _cts.Dispose();
            IsStarted = false;
            IsDisposed = true;
            Status = ConnectionStatus.Offline;
            if (disposing)
                GC.SuppressFinalize(this);
        }
    }

    #endregion Construction & Destruction

    #region SDK 2.0

    private async Task<UdpReceiveResult> SendCommandAsync(string command, int responseTimeoutMS, int retries)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        if (_commandChannel == _telemetryChannel)
        {
            await _commandChannel.SendAsync(bytes, bytes.Length, _droneEndPoint);
            return new UdpReceiveResult();
        }
        var recvTask = _commandChannel.ReceiveAsync();
        do
        {
            // send the command to the drone:
            await _commandChannel.SendAsync(bytes, bytes.Length, _droneEndPoint);
            // wait for a response:
            if (recvTask.Wait(responseTimeoutMS, _cts.Token))
                return recvTask.Result;
            --retries;
        }
        while (retries >= 0);
        return new UdpReceiveResult();
    }

    private void TelemetryChannelLoop()
    {
        Debug.Log($"{GetType().Name}: Telemetry thread started.");
        while (true)
        {
            try
            {
                // receive a telemetry datagram from the drone:
                var recvTask = _telemetryChannel.ReceiveAsync();
                if (!recvTask.Wait(1000, _cts.Token))
                {
                    Status = ConnectionStatus.Timeout;
                    Telemetry = null;
                    // timeout - try to tell the drone to enter SDK mode:
                    Debug.LogWarning($"{GetType().Name}: Timeout while waiting for a telemetry datagram.");
                    _ = SendCommandAsync("command", 0, 0);
                    continue;
                }
                Status = ConnectionStatus.Online;
                // parse telemetry datagram:
                var text = Encoding.ASCII.GetString(recvTask.Result.Buffer);
                if (text.StartsWith("ok", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                    continue; // not a telemetry.
                bool ok = TelloSdkTelemetry.TryParse(text, out var telemetry);                
                if (ok)
                {
                    Telemetry = telemetry;
                }
            }
            catch (AggregateException)
            {
                break; // operation was cancelled.
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }
        Telemetry = null;
        Debug.Log($"{GetType().Name}: Telemetry thread exiting.");
    }

    public async Task<bool> StartAsync()
    {
        var tcs = new TaskCompletionSource<int>();
        using (_cts.Token.Register(s => ((TaskCompletionSource<int>)s).TrySetResult(0), tcs)) {
            // start SDK mode:
            var tasks = new List<Task>(3)
            {
                tcs.Task,
                SendCommandAsync("command", 0, 0)
            };
            if (_clientFlags.HasFlag(TelloSdkClientFlags.Telemetry))
            {
                var missionMode = _enableMissionMode ? "mon" : "moff";
                tasks.Add(SendCommandAsync(missionMode, 0, 0));
            }
            await Task.WhenAny(tasks);
            _cts.Token.ThrowIfCancellationRequested();
            Status = ConnectionStatus.Online;
            IsStarted = true;
        }
        if (_clientFlags.HasFlag(TelloSdkClientFlags.Telemetry))
        {
            var telemetryThread = new Thread(TelemetryChannelLoop)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Lowest
            };
            telemetryThread.Start();
        }
        if (_clientFlags.HasFlag(TelloSdkClientFlags.Control) && StickDataIntervalMS > 0)
        {
            _stickDataTimer.Interval = StickDataIntervalMS;
            _stickDataTimer.Elapsed += StickDataTimer_Elapsed;
            _stickDataTimer.Start();
            _keepAliveTimer.Interval = KeepAliveIntervalMS;
            _keepAliveTimer.Elapsed += KeepAliveTimer_Elapsed;
            _keepAliveTimer.Start();
        }
        return true;
    }

    #endregion Closed API

    #region Event Handlers

    private void OnStatusChanged(ConnectionStatusChangedEventArgs e)
    {
        var handler = StatusChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }

    private void StickDataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        bool restart = true;
        try
        {
            var stickData = _stickData;
            var stickCommand = $"rc {stickData.Roll} {stickData.Pitch} {stickData.Throttle} {stickData.Yaw}";
            var bytes = Encoding.ASCII.GetBytes(stickCommand);
            _ = _commandChannel.SendAsync(bytes, bytes.Length, _droneEndPoint);
        }
        catch (ObjectDisposedException)
        {
            restart = false;
        }
        catch (OperationCanceledException)
        {
            restart = false;
        }
        catch (AggregateException ex)
        {
            restart = ex.InnerExceptions.Count != 1 ||
                      !(ex.InnerExceptions[0] is ObjectDisposedException);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
            if (restart)
                _stickDataTimer.Start();
        }
    }
    private void KeepAliveTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        bool restart = true;
        try
        {
            var bytes = Encoding.ASCII.GetBytes("command");
            var task = _commandChannel.SendAsync(bytes, bytes.Length, _droneEndPoint);
            task.Wait(KeepAliveIntervalMS, _cts.Token);
        }
        catch (ObjectDisposedException)
        {
            restart = false;
        }
        catch (OperationCanceledException)
        {
            restart = false;
        }
        catch (AggregateException ex)
        {
            restart = ex.InnerExceptions.Count != 1 ||
                      !(ex.InnerExceptions[0] is ObjectDisposedException);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
            if (restart)
                _keepAliveTimer.Start();
        }
    }

    #endregion Event Handlers

    #region Public Methods

    public void Close()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // suppress exception.
        }
        catch (AggregateException)
        {
            // suppress exception.
        }
        //if (_telemetryChannel != null)
        //    _telemetryChannel.Close();
        if (_commandChannel != null)
            _commandChannel.Close();
        IsStarted = false;
    }

    private async Task<bool> SendCommandBooleanAsync(string command, string okResponse,
        int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        var result = await SendCommandAsync(command, responseTimeoutMS, retries);
        var response = result.Buffer != null ? Encoding.ASCII.GetString(result.Buffer) : null;
        return response != null &&
               response.StartsWith(okResponse, StringComparison.Ordinal) &&
               (response.Length == okResponse.Length ||
                char.IsPunctuation(response[okResponse.Length]) ||
                char.IsWhiteSpace(response[okResponse.Length]));
    }

    private async Task<bool> SendCommandBooleanAsync(string command, int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        return await SendCommandBooleanAsync(command, "ok", responseTimeoutMS, retries);
    }

    public async Task<bool> TakeOffAsync(int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        return await SendCommandBooleanAsync("takeoff", responseTimeoutMS, retries);
    }

    public async Task<bool> LandAsync(int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        return await SendCommandBooleanAsync("land", responseTimeoutMS, retries);
    }

    /// <summary>
    /// Configure the drone to operate as the wifi station.
    /// i.e. the drone will connect to the specified wifi network.
    /// </summary>
    /// <remarks>
    /// This is the swarm mode of the drone.
    /// In this mode only the SDK API can be used.
    /// Video streaming from the drone (streamon) is not supported in this mode.
    /// In order to reset the drone to the default (access-point) mode, press the power
    /// button for five seconds while the drone is turned-on.
    /// </remarks>
    /// <param name="ssid">Drone wifi network SSID</param>
    /// <param name="password">Drone wifi network password</param>
    /// <returns>True for success; False otherwise.</returns>
    public async Task<bool> EnableWifiStationMode(string ssid, string password, int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        var command = $"ap {ssid} {password}";
        return await SendCommandBooleanAsync(command, responseTimeoutMS, retries);
    }

    /// <summary>
    /// Configure the drone to operate as the wifi access-point.
    /// i.e. the drone will create a wifi network of its own to which the phone should connect.
    /// </summary>
    /// <remarks>
    /// This is the default operation mode of the drone.
    /// In this mode both the closed-API and the SDK can be used.
    /// Video streaming from the drone is also supported in this mode.
    /// </remarks>
    /// <param name="ssid">Drone wifi network SSID</param>
    /// <param name="password">Drone wifi network password</param>
    /// <returns>True for success; False otherwise.</returns>
    public async Task<bool> EnableWifiAccessPointMode(string ssid, string password, int responseTimeoutMS = DefaultTimeoutMS, int retries = DefaultRetries)
    {
        var command = !string.IsNullOrEmpty(ssid) ? $"wifi {ssid} {password}" : "wifi";
        return await SendCommandBooleanAsync(command, responseTimeoutMS, retries);
    }

    public float MoveSpeedFactor
    {
        get => StickData.MoveSpeedFactor;
        set => StickData.MoveSpeedFactor = value;
    }

    public bool MoveLeft
    {
        get => StickData.MoveLeft;
        set => StickData.MoveLeft = value;
    }

    public bool MoveRight
    {
        get => StickData.MoveRight;
        set => StickData.MoveRight = value;
    }


    public bool MoveForward
    {
        get => StickData.MoveForward;
        set => StickData.MoveForward = value;
    }
    public bool MoveBackward
    {
        get => StickData.MoveBackward;
        set => StickData.MoveBackward = value;
    }

    public bool MoveDown
    {
        get => StickData.MoveDown;
        set => StickData.MoveDown = value;
    }

    public bool MoveUp
    {
        get => StickData.MoveUp;
        set => StickData.MoveUp = value;
    }

    public bool TurnLeft
    {
        get => StickData.TurnLeft;
        set => StickData.TurnLeft = value;
    }

    public bool TurnRight
    {
        get => StickData.TurnRight;
        set => StickData.TurnRight = value;
    }

    public bool HoldingPosition
    {
        get => StickData.HoldingPosition;
    }

    public void HoldPosition()
    {
        StickData.HoldPosition();
    }

    #region IDisposable Members

    public void Dispose()
    {
        Dispose(true);
    }

    #endregion IDisposable Members

    #endregion Public Methods
}
