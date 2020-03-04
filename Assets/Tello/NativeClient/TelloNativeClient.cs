using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class TelloNativeClient : IDisposable
{
    #region Defaults

    public const int DefaultControlUdpPort = 8889;
    public const int DefaultVideoUdpPort = 6037;
    public const string DefaultHostName = "192.168.10.1";
    public const double DefaultStickDataIntervalMilliseconds = 20;
    public const double DefaultVideoSpsPpsIntervalMilliseconds = 0;
    public const string DefaultRegion = "US";

    #endregion Defaults

    #region Private Fields

    private readonly UdpClient _client;
    private readonly UdpClient _videoStream;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly System.Timers.Timer _stickDataTimer = new System.Timers.Timer();
    private readonly System.Timers.Timer _videoSpsPpsTimer = new System.Timers.Timer();
    private TelloStickData _stickData = new TelloStickData();
    private int _sequenceNumber;

    public TelloStickData StickData
    {
        get => _stickData;
        set => _stickData = value ?? throw new ArgumentNullException(nameof(value));
    }

    #endregion Private Fields

    #region Public Properties

    public float BatteryPercent
    {
        get; private set;
    }

    public string HostName { get; private set; }

    public int ControlUdpPort { get; private set; }

    public int VideoUdpPort { get; private set; }

    public bool IsDisposed { get; private set; }

    public double StickDataIntervalMilliseconds { get; set; } = DefaultStickDataIntervalMilliseconds;
    public double VideoSpsPpsIntervalMilliseconds { get; set; } = DefaultVideoSpsPpsIntervalMilliseconds;

    #endregion Public Properties

    #region Construction & Destruction

    public TelloNativeClient(string hostname = DefaultHostName, int controlUdpPort = DefaultControlUdpPort, int videoUdpPort = DefaultVideoUdpPort)
    {
        if (hostname == null)
            throw new ArgumentNullException(nameof(hostname));
        if (controlUdpPort <= 0 || controlUdpPort > System.Net.IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(controlUdpPort), controlUdpPort,
                $"Argument '{controlUdpPort}' is out of range.");
        if (videoUdpPort <= 0 || videoUdpPort > System.Net.IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(videoUdpPort), videoUdpPort,
                $"Argument '{videoUdpPort}' is out of range.");

        HostName = hostname;
        ControlUdpPort = controlUdpPort;
        VideoUdpPort = videoUdpPort;
        _client = new UdpClient(hostname, controlUdpPort);
        var localEP = (IPEndPoint)_client.Client.LocalEndPoint;
        if (videoUdpPort != 0)
            _videoStream = new UdpClient(new IPEndPoint(localEP.Address, videoUdpPort));
    }

    ~TelloNativeClient()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (_stickDataTimer != null)
                _stickDataTimer.Dispose();
            if (_client != null)
                _client.Dispose();
            if (_videoStream != null)
                _videoStream.Dispose();
            if (_cts != null)
                _cts.Dispose();
            IsDisposed = true;
            if (disposing)
                GC.SuppressFinalize(this);
        }
    }

    #endregion Construction & Destruction

    #region Closed API

    private Task<int> SendAsync(TelloDatagram datagram)
    {
        var bytes = datagram.Serialize();
        return _client.SendAsync(bytes, bytes.Length);
    }

    private Task<int> SendAsync(TelloCommand command)
    {
        if (!command.ZeroSequenceNumber)
            command.SequenceNumber = unchecked((ushort)Interlocked.Increment(ref _sequenceNumber));
        return SendAsync((TelloDatagram)command);
    }

    private void CotrolChannelLoop()
    {
        while (true)
        {
            _cts.Token.ThrowIfCancellationRequested();
            IPEndPoint remoteEndPoint = null;
            var buffer = _client.Receive(ref remoteEndPoint);
            var incomingDatagram = TelloDatagram.DeserializeNew(buffer, out var errorCode);
            if (errorCode != TelloErrorCode.NoError)
            {
                Debug.Log($"{GetType().Name}: Failed to parse Tello datagram: {errorCode}");
                continue;
            }
            var incomingCommand = incomingDatagram as TelloCommand;
            if (incomingCommand != null)
            {
                // handle incoming commands:
                switch (incomingCommand.CommandId)
                {
                    case TelloCommandId.DateTime:
                        SendAsync(new TelloDateTimeCommand(DateTime.UtcNow) { IsResponse = true });
                        //SendAsync(new TelloGetVersionCommand());
                        //SendAsync(new TelloGetVideoBitRateCommand());
                        //SendAsync(new TelloGetAltitudeLimitCommand());
                        //SendAsync(new TelloGetLowBatteryThresholdCommand());
                        //SendAsync(new TelloGetAttitudeAngleCommand());
                        //SendAsync(new TelloGetRegionCommand());
                        //SendAsync(new TelloExposureValueCommand());
                        break;
                    case TelloCommandId.Status:
                        break;
                }
            }
        }
    }

    public async Task<bool> StartAsync()
    {
        var tcs = new TaskCompletionSource<int>();
        using (_cts.Token.Register(s => ((TaskCompletionSource<int>)s).TrySetResult(0), tcs)) {
            while (true)
            {
                // send connection request:
                var connectionRequest = new TelloConnectionRequest { VideoUdpPort = (ushort)VideoUdpPort };
                var sendTask = SendAsync(connectionRequest);
                await Task.WhenAny(sendTask, tcs.Task);
                _cts.Token.ThrowIfCancellationRequested();
                // await connection ack:
                var recvTask = _client.ReceiveAsync();
                await Task.WhenAny(recvTask, tcs.Task);
                _cts.Token.ThrowIfCancellationRequested();
                var datagram = TelloDatagram.DeserializeNew(recvTask.Result.Buffer, out TelloErrorCode errorCode);
                if (errorCode != TelloErrorCode.NoError) { 
                    Debug.Log($"{GetType().Name}: Failed to parse Tello datagram: {errorCode}");
                    continue;
                }
                var connectionResponse = datagram as TelloConnectionResponse;
                if (connectionResponse == null)
                    continue; // discard datagrams until a connection response is received.
                if (connectionResponse.VideoUdpPort != connectionRequest.VideoUdpPort)
                {
                    Debug.LogWarning($"{GetType().Name}: " +
                        $"The video UDP port returned by the done ({connectionResponse.VideoUdpPort}) " +
                        $"doesn't match the requested one ({connectionRequest.VideoUdpPort}).");
                }
                break;
            }
        }
        _ = Task.Run(CotrolChannelLoop, _cts.Token);
        if (StickDataIntervalMilliseconds > 0)
        {
            _stickDataTimer.Interval = StickDataIntervalMilliseconds;
            _stickDataTimer.Elapsed += StickDataTimer_Elapsed;
            _stickDataTimer.Start();
        }
        if (VideoSpsPpsIntervalMilliseconds > 0)
        {
            _videoSpsPpsTimer.Interval = VideoSpsPpsIntervalMilliseconds;
            _videoSpsPpsTimer.Elapsed += VideoSpsPpsTimer_Elapsed;
            _videoSpsPpsTimer.Start();
        }
        return true;
    }

    #endregion Closed API

    #region Event Handlers

    private void StickDataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var task = SendAsync(new TelloStickCommand(_stickData));
            task.Wait(_cts.Token);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
            _stickDataTimer.Start();
        }
    }

    private void VideoSpsPpsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var task = SendAsync(new TelloVideoSpsPpsCommand());
            task.Wait(_cts.Token);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
            _videoSpsPpsTimer.Start();
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
        if (_videoStream != null)
            _videoStream.Close();
        if (_client != null)
            _client.Close();
    }
    
    public async Task<bool> TakeOffAsync()
    {
        await SendAsync(new TelloTakeOffCommand());
        return true;
    }

    public async Task<bool> LandAsync()
    {
        await SendAsync(new TelloLandCommand());
        return true;
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
