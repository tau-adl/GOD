using System;
using JetBrains.Annotations;
using UnityEngine;

// Some C# features are not supported in MonoBehaviour scripts:
// ReSharper disable UseNullPropagation

public sealed class DroneTelemetry : MonoBehaviour
{
    #region Constants

    public const int TelemetryHistorySize = 1;

    #endregion Constants

    #region Fields

    private TelloSdkTelemetryChannel _telemetryChannel;

    #endregion Fields

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    #endregion Events

    #region Properties

    public TelloSdkTelemetry[] TelemetryHistory { get; private set; }
    public TelloSdkTelemetry Telemetry => TelemetryHistory[0];
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
}
