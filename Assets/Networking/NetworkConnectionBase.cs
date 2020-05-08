using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
// ReSharper disable UseNullPropagation
// ReSharper disable JoinNullCheckWithUsage

public abstract class NetworkConnectionBase
    : IDisposable
{
    #region Constants

    public const int MaxSafeUdpPayloadSize = 508;
    public const int ReconnectIntervalMS = 3000;

    #endregion Constants

    #region Fields

    private Socket _socket;
    private Thread _communicationThread;
    private int _receiveTimeoutMS;
    private ConnectionStatus _status;
    private IPEndPoint _localEndPoint;
    private CancellationTokenSource _cts;

    #endregion Fields

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    #endregion Events

    #region Properties

    public string Name { get; }
    public IPEndPoint LocalEndPoint { get; private set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public IPEndPoint RemoteIPEndPoint { get; private set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the local IP address
    /// must be in the same subnet as the remote IP address.
    /// </summary>
    public bool ForceSameSubnet { get; set; }

    public bool IsDisposed { get; private set; }

    public ConnectionStatus Status
    {
        get => _status;
        protected set
        {
            if (_status != value)
            {
                var e = new ConnectionStatusChangedEventArgs(_status, value);
                _status = value;
                OnStatusChanged(e);
            }
        }
    }

    /// <summary>
    /// Gets or sets the amount of time a receive operation may take before
    /// property <see cref="Status"/> is changed to <seealso cref="ConnectionStatus.Timeout"/>.
    /// </summary>
    public int ReceiveTimeoutMS
    {
        get => _receiveTimeoutMS;
        set
        {
            try
            {
                var socket = _socket;
                if (socket != null)
                    socket.ReceiveTimeout = value;
            }
            catch
            {
                // suppress exceptions.
            }
            _receiveTimeoutMS = value;
        }
    }

    #endregion Properties

    #region Construction & Destruction

    protected NetworkConnectionBase(string name, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (localEndPoint == null)
            throw new ArgumentNullException(nameof(localEndPoint));
        if (remoteEndPoint == null)
            throw new ArgumentNullException(nameof(remoteEndPoint));
        Name = name;
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
    }

    protected NetworkConnectionBase(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        Name = name;
    }

    ~NetworkConnectionBase()
    {
        Dispose(false);
    }

    #endregion Construction & Destruction

    #region Non-Public Methods

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (Status > ConnectionStatus.Disconnected || Status < ConnectionStatus.Offline)
                Status = ConnectionStatus.Disconnecting;
            var cts = _cts;
            if (cts != null)
            {
                _cts = null;
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // suppress exception.
                }
                catch (AggregateException)
                {
                    // suppress exception.
                }
                finally
                {
                    cts.Dispose();
                }
            }

            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }

            if (_communicationThread != null)
            {
                try
                {
                    _communicationThread.Abort();
                }
                catch (ThreadStateException)
                {
                    // suppress exception.
                }
                _communicationThread = null;
            }

            try
            {
                Status = ConnectionStatus.Offline;
            }
            catch
            {
                // suppress any exception thrown by the StatusChanged event-handler.
            }

            GC.SuppressFinalize(this);
            IsDisposed = true;
        }
    }

    protected static bool Sleep(int intervalMS, CancellationToken cancel)
    {
        return !cancel.WaitHandle.WaitOne(intervalMS);
    }

    protected abstract Socket CreateSocket(AddressFamily addressFamily);

    protected virtual void Bind(Socket socket, IPEndPoint localEndPoint)
    {
        socket.Bind(localEndPoint);
        Debug.Log($"{Name}: Bound to {socket.LocalEndPoint} ({socket.ProtocolType.ToString().ToUpper()})");
    }

    protected virtual void Connect(Socket socket, IPEndPoint remoteEndPoint, CancellationToken cancel)
    {
        // a Unity bug does not allow the usage of socket.Connect(EndPoint).
        // therefore, we use socket.Connect(string, int) instead.
        socket.Connect(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
        Debug.Log($"{Name}: Connected to {socket.RemoteEndPoint} ({socket.ProtocolType.ToString().ToUpper()})");
    }

    protected virtual byte[] CreateReceiveBuffer()
    {
        return new byte[MaxSafeUdpPayloadSize];
    }

    protected virtual void ConnectionLoop(Socket socket, CancellationToken cancel)
    {
        var buffer = CreateReceiveBuffer();
        var receiveArgs = new SocketAsyncEventArgs();
        var receiveComplete = new ManualResetEventSlim(false);
        receiveArgs.SetBuffer(buffer, 0, buffer.Length);
        receiveArgs.Completed += (s, e) => receiveComplete.Set();
        while (true)
        {
            // receive a datagram from the partner:
            receiveComplete.Reset();
            receiveArgs.RemoteEndPoint = RemoteIPEndPoint;
            if (socket.ReceiveFromAsync(receiveArgs))
            {
                while (!receiveComplete.Wait(ReceiveTimeoutMS, cancel))
                {
                    Status = ConnectionStatus.Timeout;
                }
            }
            switch (receiveArgs.SocketError)
            {
                case SocketError.Success:
                    if (receiveArgs.Count == 0)
                        return; // the socket has been closed.
                    // process datagram:
                    var keepConnection =
                        OnPacketReceived((IPEndPoint)receiveArgs.RemoteEndPoint,
                            receiveArgs.Buffer, receiveArgs.Offset, receiveArgs.Count);
                    if (keepConnection)
                    {
                        Status = ConnectionStatus.Online;
                        continue;
                    }
                    else
                    {
                        socket.Close();
                        return;
                    }
                case SocketError.WouldBlock: // returned instead of TimedOut on Android.
                case SocketError.TimedOut:
                    Status = ConnectionStatus.Timeout;
                    continue;
                default:
                    throw new SocketException((int)receiveArgs.SocketError);
            }
        }
    }

    private static ConnectionStatus ToConnectionStatus(SocketError socketError)
    {
        switch (socketError)
        {
            case SocketError.AddressAlreadyInUse:
                return ConnectionStatus.AddressAlreadyInUse;
            case SocketError.ConnectionRefused:
                return ConnectionStatus.ConnectionRefused;
            case SocketError.OperationAborted:
            case SocketError.ConnectionAborted:
            case SocketError.Interrupted:
                return ConnectionStatus.Disconnected;
            default:
                return ConnectionStatus.NetworkError;
        }
    }

    private void CommunicationThreadWork(object state)
    {
        string threadExitReason = null;
        Debug.Log($"{Name}: communication thread started.");
        var cancel = ((CancellationTokenSource) state).Token;
        while (true)
        {
            Socket socket = null;
            try
            {
                var skipSubnetCheck = false;
                LocalEndPoint = _localEndPoint;
                RemoteIPEndPoint = RemoteEndPoint as IPEndPoint;
                if (RemoteIPEndPoint == null)
                {
                    // check for a DNS end-point:
                    if (RemoteEndPoint is DnsEndPoint dnsEndPoint)
                    {
                        var dnsResult = Dns.GetHostAddresses(dnsEndPoint.Host);
                        if (dnsResult.Length == 0)
                        {
                            // failed to resolve remote host-name.
                            Debug.LogError($"{Name}: Could not resolve host-name \"{dnsEndPoint.Host}\".");
                            Status = ConnectionStatus.CantResolveHostName;
                            // sleep for ReconnectIntervalMS:
                            if (!Sleep(ReconnectIntervalMS, cancel))
                            {
                                threadExitReason = "cancelled";
                                return; // abort - we are disconnecting.
                            }
                            continue;
                        }

                        // check if any of the addresses can be reached locally:
                        foreach (var remoteIPAddress in dnsResult)
                        {
                            var info = NetUtils.GetLocalIPAddressInformation(_localEndPoint.Address,
                                remoteIPAddress);
                            if (info != null && info.Item1.OperationalStatus == OperationalStatus.Up)
                            {
                                LocalEndPoint = new IPEndPoint(info.Item2.Address, _localEndPoint.Port);
                                RemoteIPEndPoint = new IPEndPoint(remoteIPAddress, dnsEndPoint.Port);
                                skipSubnetCheck = true;
                                break;
                            }
                        }

                        if (RemoteIPEndPoint == null)
                            RemoteIPEndPoint = new IPEndPoint(dnsResult[0], dnsEndPoint.Port);
                    }
                    else
                        throw new NotSupportedException(
                            $"The type {RemoteEndPoint.GetType().Name} of the remote end-point {RemoteEndPoint} is not supported.");
                }

                // validate local and remote end-points:
                if (ForceSameSubnet && !skipSubnetCheck)
                {
                    var localIPAddressInfo =
                        NetUtils.GetLocalIPAddressInformation(_localEndPoint.Address, RemoteIPEndPoint.Address);
                    if (localIPAddressInfo == null ||
                        localIPAddressInfo.Item1.OperationalStatus != OperationalStatus.Up)
                    {
                        Status = ConnectionStatus.NoRoute;
                        // failed to find an appropriate NIC for communicating with
                        // the remote end-point.
                        Debug.LogError($"{Name}: The remote IP address {RemoteIPEndPoint.Address} is not on " +
                                       $"a subnet of any of the NICs that are currently up." +
                                       "Make sure Wifi is turned-on and that you are connected to " +
                                       "the correct network.");
                        // sleep for ReconnectIntervalMS:
                        if (!Sleep(ReconnectIntervalMS, cancel))
                        {
                            threadExitReason = "cancelled";
                            return; // abort - we are disconnecting.
                        }

                        continue; // try again.
                    }

                    LocalEndPoint = new IPEndPoint(localIPAddressInfo.Item2.Address, _localEndPoint.Port);
                }

                Interlocked.MemoryBarrier();
                // create a new UDP client and bind it to the local end-point:
                socket = CreateSocket(RemoteIPEndPoint.AddressFamily);
                socket.ReceiveTimeout = ReceiveTimeoutMS;
                Bind(socket, LocalEndPoint);
                // assign the actual local end-point:
                LocalEndPoint = (IPEndPoint) socket.LocalEndPoint;
                // expose the socket to the external world:
                _socket = socket;
                Interlocked.MemoryBarrier();
                // connect to the partner:
                //RemoteIPEndPointConnect(socket, RemoteIPEndPoint, cancel);
                cancel.ThrowIfCancellationRequested();
                Status = ConnectionStatus.Connected;
                // begin communication:
                Interlocked.MemoryBarrier();
                ConnectionLoop(socket, cancel);
            }
            catch (OperationCanceledException)
            {
                threadExitReason = "cancelled";
                break; // we are disconnecting.
            }
            catch (SocketException ex)
            {
                if (Status >= ConnectionStatus.Connecting)
                {
                    Debug.LogWarning($"{Name}: {ex.Message}");
                    Status = ToConnectionStatus(ex.SocketErrorCode);
                }
            }
            catch (ThreadAbortException)
            {
                // thread aborted - suppress exception.
                Status = ConnectionStatus.Offline;
                threadExitReason = $"thread-abort";
                break;
            }
            catch (Exception ex)
            {
                if (Status >= ConnectionStatus.Connecting)
                {
                    // unknown error - write log:
                    Debug.LogError(ex.ToString());
                    Status = ConnectionStatus.UnknownError;
                }
            }
            finally
            {
                _socket = null;
                if (socket != null)
                    socket.Close();
            }
            // check if we are disconnecting:
            if (Status == ConnectionStatus.Disconnecting)
                break;
            // sleep before trying to recover:
            try
            {
                if (!Sleep(ReconnectIntervalMS, cancel))
                    break; // abort - we are disconnecting.
            }
            catch (OperationCanceledException)
            {
                break; // we are disconnecting.
            }
            catch
            {
                // if we got here, then we are disposing.
                break;
            }
        }
        if (Status > ConnectionStatus.Offline)
            Status = ConnectionStatus.Offline;
        _communicationThread = null;
        Debug.Log($"{Name}: communication thread exited.\n" +
                  $"reason: {threadExitReason}");
    }

    protected virtual void OnStatusChanged(ConnectionStatusChangedEventArgs e)
    {
        var handler = StatusChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }


    protected abstract bool OnPacketReceived(IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count);

    #endregion Non-Public Methods

    #region Public Methods

    public void Dispose()
    {
        Dispose(true);
    }

    public void Disconnect()
    {
        if (Status > ConnectionStatus.Disconnected || Status < ConnectionStatus.Offline)
            Status = ConnectionStatus.Disconnecting;
        if (_socket != null)
        {
            try
            {
                _socket.Close();
            }
            catch (InvalidOperationException)
            {
                // suppress exception.
            }
            _socket = null;
        }

        var cts = _cts;
        if (cts != null)
        {
            _cts = null;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // suppress exception.
            }
            catch (AggregateException ex)
            {
                Debug.LogWarning(ex);
            }
            finally
            {
                cts.Dispose();
            }
        }

        if (_communicationThread != null)
        {
            if (!_communicationThread.Join(50))
                _communicationThread.Abort();
            _communicationThread = null;
        }
        Status = ConnectionStatus.Offline;
    }

    public void Connect()
    {
        if (RemoteEndPoint == null)
            throw new InvalidOperationException($"Property {nameof(RemoteEndPoint)} must be assigned before using this method.");
        if (_localEndPoint != null)
            Connect(_localEndPoint, RemoteEndPoint);
        else
            Connect(RemoteEndPoint);
    }

    public void Connect(EndPoint remoteEndPoint)
    {
        var localEndPoint = new IPEndPoint(IPAddress.Any, 0);
        Connect(localEndPoint, remoteEndPoint);
    }

    public void Connect(IPEndPoint localEndPoint, EndPoint remoteEndPoint)
    {
        if (localEndPoint == null)
            throw new ArgumentNullException(nameof(localEndPoint));
        if (remoteEndPoint == null)
            throw new ArgumentNullException(nameof(remoteEndPoint));
        if (!(remoteEndPoint is IPEndPoint) && !(remoteEndPoint is DnsEndPoint))
            throw new ArgumentException(
                $"Only {typeof(IPEndPoint).Name} and {typeof(DnsEndPoint).Name} are currently supported.", nameof(remoteEndPoint));
        _localEndPoint = LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        // make sure we are not connected:
        Disconnect();
        try
        {
            Status = ConnectionStatus.Connecting;
            _cts = new CancellationTokenSource();
            // setup communication thread:
            var communicationThread = new Thread(CommunicationThreadWork)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
            _communicationThread = communicationThread;
            Interlocked.MemoryBarrier();
            communicationThread.Start(_cts);
        }
        catch
        {
            Status = ConnectionStatus.Offline;
            throw;
        }
    }

    protected int Send(byte[] payload, int offset, int size)
    {
        try
        {
            var socket = _socket;
            if (socket != null)
            {
                var count = socket.SendTo(payload, offset, size, SocketFlags.None, RemoteIPEndPoint);
                return count;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    protected int Send(byte[] payload)
    {
        return Send(payload, 0, payload.Length);
    }

    #endregion Public Methods
}
