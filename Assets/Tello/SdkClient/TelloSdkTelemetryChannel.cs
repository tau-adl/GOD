using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
// ReSharper disable UseNullPropagation

class TelloSdkTelemetryChannel
    : NetworkConnectionBase
{
    #region Consants

    public const int DefaultLocalUdpPort = 8890;
    public const int DefaultRemoteUdpPort = 8889;
    public const int DefaultReceiveTimeoutMS = 500;
    private const string ChannelName = "Drone Telemetry Channel";

    #endregion Consants

    #region Fields

    private TelloSdkTelemetry _telemetry;

    #endregion Fields

    #region Events

    public event EventHandler TelemetryChanged;

    #endregion Events

    #region Properties

    public TelloSdkTelemetry Telemetry => _telemetry;

    #endregion Properties

    #region NetworkConnectionBase

    protected override Socket CreateSocket(AddressFamily addressFamily)
    {
        return new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
    }

    protected override void OnStatusChanged(ConnectionStatusChangedEventArgs e)
    {
        if (e.NewValue != ConnectionStatus.Online && _telemetry != null)
        {
            _telemetry = null;
            var handler = TelemetryChanged;
            if (handler != null)
                handler.Invoke(this, EventArgs.Empty);
        }
        base.OnStatusChanged(e);
    }

    protected override bool OnPacketReceived(IPEndPoint remoteEndPoint, byte[] buffer, int offset, int count)
    {
        var text = Encoding.ASCII.GetString(buffer, offset, count);
        if (!TelloSdkTelemetry.TryParse(text, out _telemetry))
        {
            Debug.LogError("Failed to parse Tello SDK telemetry datagram.");
            return false;
        }
        var handler = TelemetryChanged;
        if (handler != null)
            handler.Invoke(this, EventArgs.Empty);
        return true; // we don't call base method.
    }

    #endregion NetworkConnectionBase

    #region Construction & Destruction

    public TelloSdkTelemetryChannel()
        : base(ChannelName)
    {
        ReceiveTimeoutMS = DefaultReceiveTimeoutMS;
    }

    public TelloSdkTelemetryChannel(IPAddress droneIPAddress)
    : base(ChannelName, 
        new IPEndPoint(IPAddress.Any, DefaultLocalUdpPort), 
        new IPEndPoint(droneIPAddress, DefaultLocalUdpPort))
    {
    }

    #endregion Construction & Destruction

    #region Methods

    public void Connect(IPAddress droneIPAddress)
    {
        Connect(
            new IPEndPoint(IPAddress.Any, DefaultLocalUdpPort), 
            new IPEndPoint(droneIPAddress, DefaultRemoteUdpPort));
    }

    public void Connect(string droneHostName)
    {
        Connect(
            new IPEndPoint(IPAddress.Any, DefaultLocalUdpPort),
            new DnsEndPoint(droneHostName, DefaultRemoteUdpPort));
    }

    #endregion Methods
}
