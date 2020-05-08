using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System.Net.NetworkInformation;
using JetBrains.Annotations;
// ReSharper disable ConvertIfStatementToNullCoalescingExpression

public class GodDiscovery : MonoBehaviour
{
    #region Types

    private sealed class UdpReceiverState
    {
        public Socket Socket { get; }
        public GodDiscovery Discovery { get; }
        public EndPoint remoteEndPoint;
        public byte[] Buffer { get; }

        public UdpReceiverState(GodDiscovery discovery, Socket socket)
        {
            Socket = socket;
            Discovery = discovery;
            Buffer = new byte[MaxUdpPayloadSize];
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }
    }

    private sealed class UdpSenderState
    {
        public Socket Socket;
        public GodDiscovery Discovery;

        public UdpSenderState(GodDiscovery discovery, Socket socket)
        {
            Socket = socket;
            Discovery = discovery;
        }
    }

    #endregion Types

    #region Constants

    public const string DiscoveryPrefix = "GOD HELLO";
    /// <summary>
    /// The default game room name.
    /// </summary>
    public const string DefaultRoomName = "";
    /// <summary>
    /// The maximum allowed UDP payload size.
    /// </summary>
    public const int MaxUdpPayloadSize = NetworkConnectionBase.MaxSafeUdpPayloadSize;
    /// <summary>
    /// The default discovery UDP port.
    /// </summary>
    public const ushort DefaultDiscoveryPort = 5555;
    /// <summary>
    /// The default discovery interval in milliseconds
    /// </summary>
    public const int DefaultDiscoveryIntervalMS = 1000;
    
    #endregion Constants

    #region Fields

    private static readonly byte[] DiscoveryPrefixBytes = Encoding.UTF8.GetBytes(DiscoveryPrefix);
    /// <summary>
    /// The discovery interval in milliseconds.
    /// </summary>
    private int _discoveryIntervalMS = DefaultDiscoveryIntervalMS;
    /// <summary>
    /// A socket for discovering IPv4 partners.
    /// </summary>
    private Socket _discoveryIPv4;
    /// <summary>
    /// The discovery thread.
    /// </summary>
    private Thread _discoveryThread;
    /// <summary>
    /// Provides a mechanism for cancelling the discovery thread.
    /// </summary>
    private CancellationTokenSource _cts;

    /// <summary>
    /// The broadcast message that is transmitted by this end-point, excluding additional fields.
    /// </summary>
    private byte[] _broadcastMessagePrefix;
    /// <summary>
    /// The broadcast message that is transmitted by this end-point, including additional fields.
    /// </summary>
    private byte[] _broadcastMessage;
    /// <summary>
    /// The additional fields from the partner's original discovery packet.
    /// </summary>
    private string _partnerAdditionalFields;

    /// <summary>
    /// increased on each new discovery session (see <see cref="StartDiscovery"/>).
    /// </summary>
    private int _instanceNumber = (new System.Random()).Next();

    /// <summary>
    /// A callback that is called when a partner is discovered.
    /// </summary>
    private PartnerDiscoveredCallback _partnerDiscoveredCallback;
    /// <summary>
    /// A callback that is called when connected and a non discovery datagram is received from the partner.
    /// </summary>
    private DatagramReceivedCallback _datagramReceivedCallback;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets game room name.
    /// </summary>
    public string RoomName { get; set; } = DefaultRoomName;
    /// <summary>
    /// Gets or sets the discovery UDP port.
    /// </summary>
    public ushort DiscoveryPort { get; set; } = DefaultDiscoveryPort;
    /// <summary>
    /// Gets or sets the discovery interval in milliseconds.
    /// </summary>
    public int DiscoveryIntervalMS
    {
        get { return _discoveryIntervalMS; }
        set
        {
            if (value < 10)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"The value of '{nameof(DiscoveryIntervalMS)} must be at least 10 milliseconds.");
            _discoveryIntervalMS = value;
        }
    }
    /// <summary>
    /// Gets whether a partner has been found.
    /// </summary>
    public bool IsConnected { get { return PartnerEndPoint != null; } }
    /// <summary>
    /// Gets the end-point of the remote partner, if discovered.
    /// </summary>
    public IPEndPoint PartnerEndPoint { get; private set; }

    /// <summary>
    /// An array of all local IP addresses.
    /// </summary>
    private IPAddress[] _localIPAddresses = new IPAddress[0];

    #endregion Properties

    #region Private Methods

    [UsedImplicitly]
    private void OnDestroy()
    {
        try
        {
            StopDiscovery();
        }
        catch
        {
            // suppress exceptions
        }
    }
    
    [UsedImplicitly]
    private void OnApplicationQuit()
    {
        OnDestroy();
    }

    private static void IPv4Discovery_EndSendTo(IAsyncResult asyncResult)
    {
        var state = (UdpSenderState)asyncResult.AsyncState;
        state.Discovery.IPv4Discovery_EndSendTo(state.Socket, asyncResult);
    }

    private static void IPv4Discovery_EndReceiveFrom(IAsyncResult asyncResult)
    {
        var state = (UdpReceiverState)asyncResult.AsyncState;
        state.Discovery.IPv4Discovery_EndReceiveFrom(state, asyncResult);
    }

    private void IPv4Discovery_EndSendTo(Socket socket, IAsyncResult asyncResult)
    {
        try
        {
            // end the send operation:
            socket.EndSendTo(asyncResult);
        }
        catch (ObjectDisposedException)
        {
            // suppress exception.
        }
        catch (SocketException sex)
        {
            if (sex.SocketErrorCode != SocketError.OperationAborted)
                Debug.LogError(sex.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
    }

    private static bool StartsWith(byte[] buffer, int offset, int count, byte[] prefix)
    {
        if (prefix == null || buffer.Length - offset < count || count < prefix.Length)
            return false;
        for (int i = 0; i < prefix.Length; ++i)
        {
            if (buffer[offset + i] != prefix[i])
                return false;
        }
        return true;
    }

    private string GetDiscoveryAdditionalFields(byte[] buffer, int offset, int count)
    {
        var prefix = _broadcastMessagePrefix;
        if (buffer.Length - offset < count || count < prefix.Length)
            return null;
        for (int i = 0; i < count; ++i)
        {
            if (buffer[offset + i] != prefix[i])
                return null;
            if (prefix[i] == '\n')
                return Encoding.UTF8.GetString(buffer, offset + i + 1, count - i - 1);
        }
        return null;
    }

    private void IPv4Discovery_EndReceiveFrom(UdpReceiverState state, IAsyncResult asyncResult)
    {
        try
        {
            var socket = state.Socket;
            // end the current receive operation:
            var count = socket.EndReceiveFrom(asyncResult, ref state.remoteEndPoint);
            var remoteEndPoint = (IPEndPoint)state.remoteEndPoint;
            var buffer = state.Buffer;
            var partner = PartnerEndPoint;
            var keepConnection = true;
            // check for success:
            if (count > 0)
            {
                // make sure the received datagram didn't come from this end-point:
                if (!_localIPAddresses.Contains(remoteEndPoint.Address))
                {
                    // check if this is a discovery datagram:
                    if (StartsWith(buffer, 0, count, DiscoveryPrefixBytes))
                    {
                        // validate discovery packet:
                        var additionalFields = GetDiscoveryAdditionalFields(buffer, 0, count);
                        if (additionalFields != null)
                        {
                            // this is a valid discovery datagram.
                            if (remoteEndPoint.Equals(partner))
                                // check for additional-fields consistency:
                                keepConnection = additionalFields == _partnerAdditionalFields;
                            else if (partner == null)
                            {
                                // call the partner discovered callback:
                                if (OnPartnerDiscovered(remoteEndPoint, additionalFields)) {
                                    // partner has been confirmed.
                                    _partnerAdditionalFields = additionalFields;
                                    PartnerEndPoint = remoteEndPoint;
                                }
                            }
                            // else, already connected - ignore datagram.
                        }
                        // else, ignore datagram.
                    }
                    else if (remoteEndPoint.Equals(partner))
                    {
                        // process datagram:
                        keepConnection = OnDatagramReceived(partner, buffer, 0, count);
                    }
                    // else, ignore datagram.
                    // check if connection should be kept:
                    if (!keepConnection)
                    {
                        Reset();
                    }
                }
                // initiate another receive operation:
                state.remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                socket.BeginReceiveFrom(buffer, 0, buffer.Length,
                    SocketFlags.None, ref state.remoteEndPoint, IPv4Discovery_EndReceiveFrom, state);
            }
        }
        catch (InvalidOperationException)
        {
            // suppress exception.
        }
        catch (SocketException sex)
        {
            if (sex.SocketErrorCode != SocketError.OperationAborted &&
                sex.SocketErrorCode != SocketError.Interrupted)
                Debug.LogError(sex.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
    }

    private bool OnPartnerDiscovered(IPEndPoint remoteEndPoint, string additionalFields)
    {
        Debug.Log($"Partner discovered: {remoteEndPoint}");
        var callback = _partnerDiscoveredCallback;
        return callback == null || callback.Invoke(this, remoteEndPoint, additionalFields);
    }

    private bool OnDatagramReceived(IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        var callback = _datagramReceivedCallback;
        return callback == null || callback.Invoke(this, remoteEndPoint, buffer, offset, count);
    }

    private void DoDiscovery()
    {
        try
        {
            Debug.Log($"{GetType().Name} discovery thread started.");
            // initiate a receive operation for incoming discovery datagrams:
            var cancellationToken = _cts.Token;
            var recvState = new UdpReceiverState(this, _discoveryIPv4);
            _discoveryIPv4.BeginReceiveFrom(recvState.Buffer, 0, recvState.Buffer.Length,
                SocketFlags.None, ref recvState.remoteEndPoint, IPv4Discovery_EndReceiveFrom, recvState);
            var sendState = new UdpSenderState(this, _discoveryIPv4);
            while (true)
            {
                // send a discovery datagram:
                var partner = PartnerEndPoint;
                if (partner != null)
                {
                    // send heart-beat to partner:
                    _discoveryIPv4.BeginSendTo(_broadcastMessage, 0, _broadcastMessage.Length,
                        SocketFlags.None, partner, IPv4Discovery_EndSendTo, sendState);
                }
                else
                {
                    // send broadcast on all available Wifi and Ethernet NICs:
                    var unicastAddresses = (
                        from nic in NetUtils.GetNetworkInterfaces()
                        where nic.OperationalStatus == OperationalStatus.Up
                        from address in nic.GetIPProperties().UnicastAddresses
                        select address).ToArray();
                    // update the local IP address array (pointer update - thread-safe):
                    _localIPAddresses = unicastAddresses.Select(address => address.Address).ToArray();
                    Interlocked.MemoryBarrier();
                    // enumerate network interfaces:
                    foreach (var addr in unicastAddresses)
                    {
                        switch (addr.Address.AddressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                var broadcastAddress = NetUtils.GetBroadcastAddress(addr.Address, addr.IPv4Mask);
                                var broadcastEndPoint = new IPEndPoint(broadcastAddress, DiscoveryPort);
                                // send broadcast:
                                //Debug.Log($"Sending discovery packet to {broadcastEndPoint}");
                                _discoveryIPv4.BeginSendTo(_broadcastMessage, 0, _broadcastMessage.Length,
                                    SocketFlags.None, broadcastEndPoint, IPv4Discovery_EndSendTo, sendState);
                                break;
                            case AddressFamily.InterNetworkV6:
                                // support IPv6 in the future.
                                break;
                        }
                    }
                }
                // sleep:
                if (cancellationToken.WaitHandle.WaitOne(DiscoveryIntervalMS))
                    return; // a cancellation signal received.
            }
        }
        finally
        {
            _discoveryThread = null;
            Debug.Log($"{GetType().Name} discovery thread exited.");
        }
    }

    #endregion Private Methods

    #region Public Methods

    public void SetPartnerDiscoveredCallback(PartnerDiscoveredCallback callback)
    {
        _partnerDiscoveredCallback = callback;
    }

    public void SetDatagramReceivedCallback(DatagramReceivedCallback callback)
    {
        _datagramReceivedCallback = callback;
    }

    public void StopDiscovery()
    {
        Debug.Log($"{GetType().Name}.{nameof(StopDiscovery)}() called.");
        // signal the discovery thread to stop:
        if (_cts != null)
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // suppress exception.
            }
        }
        if (_discoveryIPv4 != null)
        {
            try
            {
                _discoveryIPv4.Close();
            }
            catch (InvalidOperationException)
            {
                // suppress exception.
            }
        }
        if (_discoveryThread != null)
            _discoveryThread.Join();
    }

    public void StartDiscovery()
    {
        // make sure we are not already running:
        if (_discoveryThread != null)
            return; // already started.
        Debug.Log($"{GetType().Name}.{nameof(StartDiscovery)}() called.");
        // initialize the broadcast message:
        var roomName = RoomName;
        if (roomName == null)
            roomName = DefaultRoomName;
        _broadcastMessagePrefix = Encoding.UTF8.GetBytes($"{DiscoveryPrefix} {Application.companyName}.{Application.productName} {Application.version} {roomName}\n");
        var additionalFields = Encoding.UTF8.GetBytes($"instance: {Interlocked.Increment(ref _instanceNumber)}");
        _broadcastMessage = new byte[_broadcastMessagePrefix.Length + additionalFields.Length];
        Buffer.BlockCopy(_broadcastMessagePrefix, 0, _broadcastMessage, 0, _broadcastMessagePrefix.Length);
        Buffer.BlockCopy(additionalFields, 0, _broadcastMessage, _broadcastMessagePrefix.Length, additionalFields.Length);
        if (_broadcastMessage.Length > MaxUdpPayloadSize)
            throw new ApplicationException("Network discovery message is too long!");

        _cts = new CancellationTokenSource();
        // create and bind discovery sockets:
        _discoveryIPv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            DontFragment = true,
            MulticastLoopback = false,
            EnableBroadcast = true
        };
        _discoveryIPv4.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        Debug.Log($"Discovery socket bound to {_discoveryIPv4.LocalEndPoint}.");
        // create the discovery thread:
        _discoveryThread = new Thread(DoDiscovery)
        {
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.Lowest
        };
        // make sure the thread is started only after
        // all field values committed to memory:
        Interlocked.MemoryBarrier();
        // start the discovery thread:
        _discoveryThread.Start();
    }

    public void Reset()
    {
        _partnerAdditionalFields = null;
        PartnerEndPoint = null;
    }

    #endregion Public Methods
}
