using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class GodNetworking : MonoBehaviour
{
    #region Constants

    public const int GameUdpPort = 5556;
    public const int MaxUdpPayloadSize = 508;

    #endregion Constants

    #region Fields

    private Socket _socket;
    private Thread _communicationThread;
    private DatagramReceivedCallback _datagramReceivedCallback;

    #endregion Fields

    #region Properties

    public bool IsMaster { get; private set; }

    #endregion Properties

    #region Private Methods

    private void OnDestroy()
    {
        try
        {
            Disconnect();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }


    private void CommunicationThreadWork()
    {
        try
        {
            Debug.Log($"GOD communication thread started.");
            var buffer = new byte[MaxUdpPayloadSize];
            var socket = _socket;

            while (true)
            {
                // receive a datagram from the partner:
                var count = socket.Receive(buffer);
                if (count == 0)
                    break; // the socket has been closed.
                // process datagram:
                var keepConnection = OnDatagramReceived((IPEndPoint) socket.RemoteEndPoint, buffer, 0, count);
                if (!keepConnection)
                {
                    socket.Close();
                    break;
                }
            }
        }
        catch (ThreadAbortException)
        {
            // suppress exception.
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
        finally
        {
            _communicationThread = null;
            Debug.Log($"GOD communication thread exited.");
        }
    }

    private bool OnDatagramReceived(IPEndPoint partnerEndPoint, byte[] buffer, int offset, int count)
    {
        var callback = _datagramReceivedCallback;
        return callback == null || callback.Invoke(this, partnerEndPoint, buffer, offset, count);
    }

    #endregion Private Methods

    #region Public Methods

    public void SetDatagramReceivedCallback(DatagramReceivedCallback callback)
    {
        _datagramReceivedCallback = callback;
    }

    public void Disconnect()
    {
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
        if (_communicationThread != null)
        {
            _communicationThread.Join();
            _communicationThread = null;
        }
    }

    private static int CompareTo(IPAddress a, IPAddress b)
    {
        var bytesA = a.GetAddressBytes();
        var bytesB = b.GetAddressBytes();
        if (bytesA.Length != bytesB.Length)
        {
            // address lengths are different.
            return bytesA.Length.CompareTo(bytesB.Length);
        }
        for (int i = 0; i < bytesA.Length; ++i)
        {
            var compare = bytesA[i].CompareTo(bytesB[i]);
            if (compare != 0)
                return compare;
        }
        return 0; // the addresses are identical.
    }

    public void Connect(IPAddress localIPAddress, IPAddress partnerIPAddress)
    {
        // make sure we are not connected:
        Disconnect();
        // create a new UDP client and bind it to the local end-point:
        var socket = new Socket(localIPAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket = socket;
        socket.Bind(new IPEndPoint(localIPAddress, GameUdpPort));
        Debug.Log($"Bound to {socket.LocalEndPoint} (UDP)");
        // connect to the partner:
        socket.Connect(new IPEndPoint(partnerIPAddress, GameUdpPort));
        IsMaster = CompareTo(localIPAddress, partnerIPAddress) <= 0;
        Debug.Log($"Connected to partner: {socket.RemoteEndPoint} (IsMaster = {IsMaster})");
        // setup communication thread:
        var communicationThread = new Thread(CommunicationThreadWork)
        {
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        _communicationThread = communicationThread;
        Interlocked.MemoryBarrier();
        communicationThread.Start();
    }

    public int Send(byte[] payload)
    {
        try
        {
            var socket = _socket;
            return socket != null && socket.Connected
                ? socket.Send(payload)
                : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    #endregion Public Methods
}
