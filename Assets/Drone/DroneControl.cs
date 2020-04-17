using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

// Some C# features are not supported in MonoBehaviour scripts:
// ReSharper disable UseNullPropagation

public class DroneControl : MonoBehaviour
{
    #region Fields

    private TelloSdkControlChannel _controlChannel;

    #endregion Fields

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
    public event EventHandler SerialNumberChanged;

    #endregion Events

    #region Properties

    public string DroneHostName { get; private set; }

    public ConnectionStatus Status => _controlChannel.Status;

    public TelloSdkStickData StickData
    {
        get => _controlChannel.StickData;
        set => _controlChannel.StickData = value;
    }

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        DroneHostName = PlayerPrefs.GetString("DroneHostName", TelloSdkClient.DefaultHostName);
    }

    [UsedImplicitly]
    private void Start()
    {
        _controlChannel = new TelloSdkControlChannel();
        _controlChannel.StatusChanged += ControlChannel_StatusChanged;
        _controlChannel.SerialNumberChanged += ControlChannel_SerialNumberChanged;
        _controlChannel.ForceSameSubnet = true;
        _controlChannel.Connect(DroneHostName);
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_controlChannel != null)
        {
            // try to command the drone to land:
            try
            {
                // ReSharper disable once AssignmentIsFullyDiscarded
                _ = _controlChannel.LandAsync();
            }
            catch
            {
                // suppress exceptions.
            }
            // attempt to disconnect the connection gracefully:
            try
            {
                _controlChannel?.Disconnect();
            }
            catch
            {
                // suppress exceptions.
            }
        }
    }

    #endregion MonoBehaviour

    #region Methods

    private void ControlChannel_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        Debug.Log($"Drone {DroneHostName} Control Status: {e.NewValue}");
        var handler = StatusChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }

    private void ControlChannel_SerialNumberChanged(object sender, EventArgs e)
    {
        var sn = _controlChannel.DroneSerialNumber;
        if (string.IsNullOrEmpty(sn))
            Debug.Log($"Drone {DroneHostName} serial-number is: {sn}");
        var handler = SerialNumberChanged;
        if (handler != null)
            handler.Invoke(this, e);
    }

    public async Task<bool> TakeOffAsync()
    {
        if (_controlChannel == null)
            throw new InvalidOperationException("Not connected");
        return await _controlChannel.TakeOffAsync();
    }

    public async Task<bool> LandAsync()
    {
        if (_controlChannel == null)
            throw new InvalidOperationException("Not connected");
        return await _controlChannel.LandAsync();
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
        if (_controlChannel == null)
            throw new InvalidOperationException("Not started");
        return await _controlChannel.EnableWifiStationMode(ssid, password);
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
        if (_controlChannel == null)
            throw new InvalidOperationException("Not started");
        return await _controlChannel.EnableWifiAccessPointMode(ssid, password);
    }

    #endregion Methods
}
