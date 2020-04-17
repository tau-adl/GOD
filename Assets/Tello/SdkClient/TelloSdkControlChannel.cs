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
    public const int DefaultCommandTimeoutMS = 500;
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
        _stickDataTimer.Interval = DefaultStickDataIntervalMS;
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

    protected override void ConnectionLoop(Socket socket)
    {
        var txBuffer = new byte[MaxCommandLength];
        var rxBuffer = CreateReceiveBuffer();
        var waitHandles = new[] {_commandQueueCount.AvailableWaitHandle, DisconnectWaitHandle};
        var lastSuccessfulCommand = DateTime.MinValue;
        while (true)
        {
            var signal =
                Status == ConnectionStatus.Online &&
                (DateTime.Now - lastSuccessfulCommand).TotalMilliseconds <= KeepAliveIntervalMS
                    ? WaitHandle.WaitAny(waitHandles, KeepAliveIntervalMS)
                    : WaitHandle.WaitTimeout;
            if (signal == WaitHandle.WaitTimeout)
            {
                _stickDataTimer.Stop();
                if (!PerformInitializationSequence(socket, txBuffer, rxBuffer))
                    return; // failed to initialize.
                if (Status == ConnectionStatus.Online)
                {
                    lastSuccessfulCommand = DateTime.Now;
                    _stickDataTimer.Start();
                }
                continue;
            }
            if (signal != 0)
                return; // disconnecting.
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
        var initializationSequence = new[] {"command", "sn?"};
        // send initialization sequence:
        foreach (var command in initializationSequence)
        {
            var count = Encoding.ASCII.GetBytes(command, 0, command.Length, txBuffer, 0);
            socket.Send(txBuffer, 0, count, SocketFlags.None);
        }

        // wait for responses:
        foreach (var command in initializationSequence)
        {
            var count = socket.Receive(rxBuffer, 0, rxBuffer.Length, SocketFlags.None, out var socketError);
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (socketError)
            {
                case SocketError.Success when count == 0:
                    throw new SocketException((int) SocketError.Interrupted);
                case SocketError.Success:
                {
                    var response = Encoding.ASCII.GetString(rxBuffer, 0, count);
                    if (command.Equals("command", StringComparison.Ordinal))
                    {
                        if (response != "ok")
                            return false;
                    }
                    else
                    {
                        if (DroneSerialNumber != null)
                            return DroneSerialNumber.Equals(response, StringComparison.Ordinal);
                        if (response.Length < MinSerialNumberLength || !response.All(char.IsLetterOrDigit))
                            return false;
                        DroneSerialNumber = response;
                    }
                    continue;
                }
                case SocketError.TimedOut:
                    Status = ConnectionStatus.Timeout;
                    return true;
                default:
                    throw new SocketException((int) socketError);
            }
        }
        Status = ConnectionStatus.Online;
        return true;
    }

    public void Connect(IPAddress droneIPAddress)
    {
        Connect(new IPEndPoint(droneIPAddress, DefaultUdpPort));
    }

    public void Connect(string droneHostName)
    {
        Connect(new DnsEndPoint(droneHostName, DefaultUdpPort));
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
        return goodResult.Equals(response, StringComparison.Ordinal);
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
