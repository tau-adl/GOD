using System;
using JetBrains.Annotations;
using UnityEngine;
// ReSharper disable MergeConditionalExpression

// Some C# features are not supported in MonoBehaviour scripts:
// ReSharper disable UseNullPropagation

public sealed class DroneTelemetry : MonoBehaviour
{
    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    #endregion Events

    /*

    #region Constants

    public const int TelemetryHistorySize = 1;

    #endregion Constants

    #region Fields

    private TelloSdkTelemetryChannel _telemetryChannel;

    #endregion Fields

    #region Properties

    */

    public TelloSdkTelemetry Telemetry => _client != null ? _client.Telemetry : null;

    public ConnectionStatus Status => _client != null ? _client.Status : ConnectionStatus.Offline;

    /*
    public string DroneHostName { get; private set; }

    public ConnectionStatus Status => _telemetryChannel.Status;

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        DroneHostName = PlayerPrefs.GetString("DroneHostName", TelloSdkClient.DefaultHostName);
        TelemetryHistory = new TelloSdkTelemetry[1 + TelemetryHistorySize];
        _telemetryChannel = new TelloSdkTelemetryChannel()
        {
            ForceSameSubnet = true
        };
        _telemetryChannel.TelemetryChanged += TelemetryChannelTelemetryChanged;
        _telemetryChannel.StatusChanged += TelemetryChannel_StatusChanged;
    }

    private void Start()
    {
        Connect();
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_telemetryChannel != null)
        {
            try
            {
                _telemetryChannel.Disconnect();
            }
            catch
            {
                // suppress exceptions.
            }
        }
    }

    [UsedImplicitly]
    private void OnApplicationQuit()
    {
        OnDestroy();
    }

    #endregion MonoBehaviour

    #region Methods

    private void TelemetryChannel_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        Debug.Log($"Drone {DroneHostName} Telemetry Status: {e.NewValue}");
        var handler = StatusChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }

    private void TelemetryChannelTelemetryChanged(object sender, System.EventArgs e)
    {
        var history = new TelloSdkTelemetry[1 + TelemetryHistorySize];
        for (var i = 1; i < TelemetryHistorySize; ++i)
            history[i] = TelemetryHistory[i - 1];
        history[0] = ((TelloSdkTelemetryChannel) sender).Telemetry;
        TelemetryHistory = history;
    }

    public void Connect(string droneHostName = null)
    {
        if (droneHostName != null)
            DroneHostName = droneHostName;
        _telemetryChannel.Connect(DroneHostName);
    }

    #endregion Methods
    */
    private TelloSdkClient _client;


    public void Connect(TelloSdkClient client)
    {
        _client = client;
        if (_client != null)
            _client.StatusChanged += ClientOnStatusChanged;
    }

    private void OnDestroy()
    {
        if (_client != null)
            _client.StatusChanged -= ClientOnStatusChanged;
    }

    private void ClientOnStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        var handler = StatusChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }
}
