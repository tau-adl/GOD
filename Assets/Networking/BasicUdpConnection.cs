using System.Net;
using System.Net.Sockets;

public sealed class BasicUdpConnection
    : NetworkConnectionBase
{
    #region Fields

    private DatagramReceivedCallback _datagramReceivedCallback;

    #endregion Fields

    #region NetworkConnectionBase

    protected override Socket CreateSocket(AddressFamily addressFamily)
    {
        return new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
    }

    protected override bool OnPacketReceived(IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        var callback = _datagramReceivedCallback;
        if (callback != null)
            return callback.Invoke(this, remoteEndPoint, buffer, offset, count);
        return true;
    }

    #endregion NetworkConnectionBase

    #region Construction & Destruction

    public BasicUdpConnection(string name, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint) 
        : base(name, localEndPoint, remoteEndPoint)
    {
    }

    public BasicUdpConnection(string name)
        : base(name)
    {
    }

    #endregion Construction & Destruction

    #region Methods

    public void SetDatagramReceivedCallback(DatagramReceivedCallback callback)
    {
        _datagramReceivedCallback = callback;
    }

    public new int Send(byte[] payload, int offset, int size)
    {
        return base.Send(payload, offset, size);
    }

    public new int Send(byte[] payload)
    {
        return base.Send(payload);
    }

    #endregion Methods
}