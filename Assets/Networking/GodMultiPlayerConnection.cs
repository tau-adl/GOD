using System;
using System.Net;
using JetBrains.Annotations;
using UnityEngine;
// ReSharper disable UseNullPropagation

public sealed class GodMultiPlayerConnection : MonoBehaviour
{
    #region Constants

    public const string ConnectionName = "multi-player";
    public const int DefaultGameUdpPort = 5556;

    #endregion Constants

    #region Fields

    private BasicUdpConnection _connection;
    private DatagramReceivedCallback _datagramReceivedCallback;

    #endregion Fields

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged; 

    #endregion Events

    #region Properties

    public ConnectionStatus Status => _connection.Status;

    /// <summary>
    /// Gets a value indicating if this host is chosen to be the master.
    /// </summary>
    public bool IsMaster { get; private set; }

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        _connection = new BasicUdpConnection(ConnectionName);
        _connection.StatusChanged += ConnectionStatusChanged;
        _connection.SetDatagramReceivedCallback(_datagramReceivedCallback);
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_connection != null)
        {
            try
            {
                _connection.Disconnect();
            }
            catch
            {
                // suppress exceptions.
            }
            _connection.StatusChanged -= ConnectionStatusChanged;
        }
    }

    #endregion MonoBehaviour

    #region Methods

    private void ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        var connection = (NetworkConnectionBase) sender;
        switch (e.NewValue)
        {
            case ConnectionStatus.Connected:
                IsMaster = NetUtils.CompareTo(
                    connection.LocalEndPoint.Address,
                    connection.RemoteIPEndPoint.Address) <= 0;
                var masterEndPoint = IsMaster ? connection.LocalEndPoint : connection.RemoteEndPoint;
                Debug.Log($"{ConnectionName}: {masterEndPoint} will act as master.");
                break;
        }
        StatusChanged?.Invoke(this, e);
    }

    public void Connect(IPAddress localIPAddress, IPAddress remoteIPAddress)
    {
        var localEndPoint = new IPEndPoint(localIPAddress, DefaultGameUdpPort);
        var remoteEndPoint = new IPEndPoint(remoteIPAddress, DefaultGameUdpPort);
        _connection.Connect(localEndPoint, remoteEndPoint);
    }

    public void Disconnect()
    {
        _connection.Disconnect();
    }

    public void SetDatagramReceivedCallback(DatagramReceivedCallback callback)
    {
        _datagramReceivedCallback = callback;
        if (_connection != null)
            _connection.SetDatagramReceivedCallback(callback);
    }

    public int Send(byte[] payload, int offset, int size)
    {
        return _connection.Send(payload, offset, size);
    }

    public int Send(byte[] payload)
    {
        return _connection.Send(payload);
    }

    #endregion Methods
}
