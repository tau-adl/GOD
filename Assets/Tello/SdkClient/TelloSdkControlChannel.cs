using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

class TelloSdkControlChannel
    : NetworkConnectionBase
{
    #region Constants

    public const int DefaultUdpPort = 8889;
    public const int DefaultStickDataIntervalMS = 20;
    public const int DefaultCommandTimeoutMS = 1000;
    public const int DefaultKeepAliveIntervalMS = 3000;
    public const int MaxCommandLength = 128;
    private const int MinSerialNumberLength = 10;

    #endregion Constants

    #region Events

    public event EventHandler SerialNumberChanged;

    #endregion Events

    #region Fields

    private readonly System.Timers.Timer _stickDataTimer = new System.Timers.Timer();
    private readonly Queue<TaskCompletionSource<string>> _commandQueue = new Queue<TaskCompletionSource<string>>();
    private readonly SemaphoreSlim _commandQueueCount = new SemaphoreSlim(0);
    private TelloSdkStickData _stickData = new TelloSdkStickData();
    private string _droneSerialNumber;

    #endregion Fields

    #region Properties

    public int StickDataIntervalMS
    {
        get => (int) _stickDataTimer.Interval;
        set => _stickDataTimer.Interval = value;
    }

    public int KeepAliveIntervalMS { get; set; } = DefaultKeepAliveIntervalMS;

    public TelloSdkStickData StickData
    {
        get => _stickData;
        set => _stickData = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string DroneSerialNumber
    {
        get => _droneSerialNumber;
        private set
        {
            if (!string.Equals(_droneSerialNumber, value, StringComparison.Ordinal))
            {
                _droneSerialNumber = value;
                SerialNumberChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    #endregion Properties

    #region Construction & Destruction

    public TelloSdkControlChannel()
        : base("Drone Control Channel")
    {
        ReceiveTimeoutMS = DefaultCommandTimeoutMS;
        StickDataIntervalMS = DefaultStickDataIntervalMS;
        _stickDataTimer.Elapsed += StickDataTimer_Elapsed;
        _stickDataTimer.AutoReset = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // stop the stick data-timer:
            _stickDataTimer?.Dispose();
            _commandQueueCount?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion Construction & Destruction

    #region NetworkConnectionBase

    protected override bool OnPacketReceived(IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        return true;
    }

    protected override Socket CreateSocket(AddressFamily addressFamily)
    {
        return new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
    }

    protected override void OnStatusChanged(ConnectionStatusChangedEventArgs e)
    {
        _stickDataTimer.Enabled = e.NewValue >= ConnectionStatus.Connected;
    }

    protected override void ConnectionLoop(Socket socket, CancellationToken cancel)
    {
        var txBuffer = new byte[MaxCommandLength];
        var rxBuffer = CreateReceiveBuffer();
        var lastSuccessfulCommand = DateTime.MinValue;
        while (true)
        {
            Debug.Log($"{Name}: BEGIN connection-loop");
            var performInitializationSequence =
                Status != ConnectionStatus.Online ||
                (DateTime.Now - lastSuccessfulCommand).TotalMilliseconds > KeepAliveIntervalMS;
            bool? waitingCommands = null;
            if (!performInitializationSequence)
                waitingCommands = _commandQueueCount.Wait(KeepAliveIntervalMS, cancel);
            if (performInitializationSequence || waitingCommands == false)
            {
                PerformInitializationSequence(socket, txBuffer, rxBuffer);
                if (Status == ConnectionStatus.Online)
                    lastSuccessfulCommand = DateTime.Now;
                if (!waitingCommands.HasValue)
                    waitingCommands = _commandQueueCount.Wait(KeepAliveIntervalMS, cancel);
            }
            if (waitingCommands == false)
                continue; // currently there are no commands in queue.
            // there's a new outgoing command pending in the queue.
            TaskCompletionSource<string> commandTask;
            // get the command-task from the queue:
            lock (_commandQueue)
            {
                if (_commandQueue.Count == 0)
                    continue; // queue is empty.
                commandTask = _commandQueue.Dequeue();
            }

            // send the command to the drone:
            var command = (string) commandTask.Task.AsyncState;
            var count = Encoding.ASCII.GetBytes(command, 0, command.Length, txBuffer, 0);
            socket.Send(txBuffer, 0, count, SocketFlags.None);
            // wait for a response from the drone:
            count = socket.Receive(rxBuffer, 0, rxBuffer.Length, SocketFlags.None, out var socketError);
            switch (socketError)
            {
                case SocketError.Success:
                    if (count == 0)
                        return; // the socket has been closed.
                    // process datagram:
                    var response = Encoding.ASCII.GetString(rxBuffer, 0, count);
                    try
                    {
                        commandTask.TrySetResult(response);
                    }
                    catch (ObjectDisposedException)
                    {
                        // suppress exception.
                    }
                    lastSuccessfulCommand = DateTime.Now;
                    continue;
                case SocketError.TimedOut:
                    continue;
                default:
                    throw new SocketException((int) socketError);
            }
        }
    }

    #endregion NetworkConnectionBase

    #region Methods

    private void StickDataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        var restart = true;
        try
        {
            var stickData = _stickData;
            var stickCommand = $"rc {stickData.Roll} {stickData.Pitch} {stickData.Throttle} {stickData.Yaw}";
            var bytes = Encoding.ASCII.GetBytes(stickCommand);
            Send(bytes);
        }
        catch (ObjectDisposedException)
        {
            restart = false;
        }
        catch (OperationCanceledException)
        {
            restart = false;
        }
        catch (SocketException)
        {
            // suppress exception.
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

    private bool PerformInitializationSequence(Socket socket, byte[] txBuffer, byte[] rxBuffer)
    {
        Debug.Log($"{Name}: Starting initialization sequence.");
        // send initialization sequence:
        var count = Encoding.ASCII.GetBytes("command", 0, "command".Length, txBuffer, 0);
        count = socket.Send(txBuffer, 0, count, SocketFlags.None);
        if (count == 0) return false; // failed to send the command.
        Debug.Log($"{Name}: Waiting for drone response (timeout={socket.ReceiveTimeout}ms)...");
        count = socket.Receive(rxBuffer, 0, rxBuffer.Length, SocketFlags.None, out var socketError);
        if (socketError == SocketError.Success && count > 0)
        {
            var response = Encoding.ASCII.GetString(rxBuffer, 0, count);
            if (!"ok".Equals(response, StringComparison.Ordinal))
                return false;
            count = Encoding.ASCII.GetBytes("sn?", 0, "sn?".Length, txBuffer, 0);
            count = socket.Send(txBuffer, 0, count, SocketFlags.None);
            if (count == 0) return false; // failed to send the command.
            Debug.Log($"{Name}: Waiting for drone response...");
            count = socket.Receive(rxBuffer, 0, rxBuffer.Length, SocketFlags.None, out socketError);
            if (socketError == SocketError.Success && count > 0)
            {
                if (DroneSerialNumber != null && !DroneSerialNumber.Equals(response, StringComparison.Ordinal))
                {
                    // serial number mismatch, or lost sync.
                    DroneSerialNumber = null;
                    return false;
                }

                if (response.Length < MinSerialNumberLength || !response.All(char.IsLetterOrDigit))
                    return false; // not a valid serial number - lost sync.
                DroneSerialNumber = response;
            }
        }
        switch (socketError)
        {
            case SocketError.Success when count == 0:
                throw new SocketException((int)SocketError.Interrupted);
            case SocketError.Success:
                Status = ConnectionStatus.Online;
                return true;
            case SocketError.TimedOut:
                Debug.Log($"{Name}: Timeout");
                Status = ConnectionStatus.Timeout;
                return true;
            default:
                throw new SocketException((int)socketError);
        }
    }

    public void Connect(IPAddress droneIPAddress)
    {
        Connect(new IPEndPoint(IPAddress.Any, DefaultUdpPort),
            new IPEndPoint(droneIPAddress, DefaultUdpPort));
    }

    public void Connect(string droneHostName)
    {
        Connect(new IPEndPoint(IPAddress.Any, DefaultUdpPort),
            new DnsEndPoint(droneHostName, DefaultUdpPort));
    }

    private async Task<string> SendAsync(string command)
    {
        // create a command-task and put it in the command queue:
        var commandTask = new TaskCompletionSource<string>(command);
        lock (_commandQueue)
        {
            _commandQueue.Enqueue(commandTask);
            // notify the command dispatcher about the new command:
            _commandQueueCount.Release();
        }

        await commandTask.Task;
        return commandTask.Task.Result;
    }

    private async Task<bool> SendBooleanCommandAsync(string command, string goodResult = "ok")
    {
        var response = await SendAsync(command);
        return response != null &&
               response.StartsWith(goodResult, StringComparison.Ordinal) &&
               (response.Length == goodResult.Length ||
                char.IsPunctuation(response[goodResult.Length]) ||
                char.IsWhiteSpace(response[goodResult.Length]));
    }

    public async Task<bool> TakeOffAsync()
    {
        return await SendBooleanCommandAsync("takeoff");
    }

    public async Task<bool> LandAsync()
    {
        return await SendBooleanCommandAsync("land");
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
    public async Task<bool> EnableWifiStationMode(string ssid, string password)
    {
        var command = $"ap {ssid} {password}";
        return await SendBooleanCommandAsync(command);
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
    public async Task<bool> EnableWifiAccessPointMode(string ssid, string password)
    {
        var command = !string.IsNullOrEmpty(ssid) ? $"wifi {ssid} {password}" : "wifi";
        return await SendBooleanCommandAsync(command);
    }

    #endregion Methods
}
