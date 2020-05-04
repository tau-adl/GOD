using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

// Some C# features are not supported in MonoBehaviour scripts:
// ReSharper disable UseNullPropagation

public class DroneControl : MonoBehaviour
{
    private TelloSdkClient _client;

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    /*
    #region Fields

    private TelloSdkControlChannel _controlChannel;

    #endregion Fields

    #region Events

    
    public event EventHandler SerialNumberChanged;

    #endregion Events

    #region Properties

    public string DroneHostName { get; private set; }

    public ConnectionStatus Status => _controlChannel.Status;
    */
    public TelloSdkStickData StickData
    {
        get => _client.StickData;
        set => _client.StickData = value;
    }
    /*
    #endregion Properties
    
    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        DroneHostName = PlayerPrefs.GetString("DroneHostName", TelloSdkClient.DefaultHostName);
        _controlChannel = new TelloSdkControlChannel
        {
            ForceSameSubnet = true
        };
        _controlChannel.StatusChanged += ControlChannel_StatusChanged;
        _controlChannel.SerialNumberChanged += ControlChannel_SerialNumberChanged;
    }

    private void Start()
    {
        Connect();
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        var controlChannel = _controlChannel;
        if (controlChannel != null)
        {
            // try to command the drone to land:
            try
            {
                // ReSharper disable once AssignmentIsFullyDiscarded
                _ = controlChannel.LandAsync();
            }
            catch
            {
                // suppress exceptions.
            }
            // attempt to disconnect the connection gracefully:
            try
            {
                controlChannel.Disconnect();
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
    */

    public async Task<bool> TakeOffAsync()
    {
        return await _client.TakeOffAsync();
        //if (_controlChannel == null)
        //    throw new InvalidOperationException("Not connected");
        //return await _controlChannel.TakeOffAsync();
    }

    public async Task<bool> LandAsync()
    {
        return await _client.LandAsync();
        //if (_controlChannel == null)
        //    throw new InvalidOperationException("Not connected");
        //return await _controlChannel.LandAsync();
    }

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
