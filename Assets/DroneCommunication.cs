using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

// ReSharper disable UseNullPropagation
// ReSharper disable JoinNullCheckWithUsage
// ReSharper disable MergeConditionalExpression

public class DroneCommunication : MonoBehaviour
{
    #region Constants

    public const int DefaultUdpPort = 8889;
    public const int DefaultStickDataIntervalMS = 20;
    public const int DefaultCommandTimeoutMS = 1000;
    public const int DefaultKeepAliveIntervalMS = 3000;

    #endregion Constants

    #region Events

    public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

    #endregion Events

    #region Fields

    private TelloSdkClient _client;
    private Thread _watchdog;
    private ConnectionStatus _status;
    private CancellationTokenSource _cts;

    #endregion Fields

    #region Properties
    public bool ForceSameSubnet { get; set; }

    public TelloSdkClient Client => _client;
    public string DroneHostName { get; private set; }

    public int KeepAliveIntervalMS { get; set; } = DefaultKeepAliveIntervalMS;

    public int ReconnectIntervalMS { get; set; }

    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                var oldValue = _status;
                _status = value;
                var handler = StatusChanged;
                if (handler != null)
                    handler.Invoke(this, new ConnectionStatusChangedEventArgs(oldValue, value));
            }
        }
    }

    public IPEndPoint LocalEndPoint { get; private set; }

    public TelloSdkTelemetry Telemetry
    {
        get
        {
            var client = _client;
            return client != null ? client.Telemetry : null;
        }
    }

    #endregion Properties

    public DroneCommunication()
    {
        ForceSameSubnet = true;
        ReconnectIntervalMS = DefaultKeepAliveIntervalMS;
    }

    protected static bool Sleep(int intervalMS, CancellationToken cancel)
    {
        return !cancel.WaitHandle.WaitOne(intervalMS);
    }

    private void WatchdogWork(object state)
    {
        var cts = (CancellationTokenSource) state;
        var cancel = cts.Token;
        while (true)
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                var droneHostName = PlayerPrefs.GetString("DroneHostName");
                _client = new TelloSdkClient(droneHostName, TelloSdkClientFlags.ControlAndTelemetry);
                _client.StatusChanged += Client_StatusChanged;
                _client.StartAsync().Wait(cancel);
                return; // success.
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not connect to drone: " + ex.Message);
                if (!Sleep(ReconnectIntervalMS, cancel))
                {
                    return; // abort - we are disconnecting.
                }
            }
        }
    }

    private void Client_StatusChanged(object sender, ConnectionStatusChangedEventArgs e)
    {
        Status = e.NewValue;
    }

    // Start is called before the first frame update
    void Start()
    {
        _cts = new CancellationTokenSource();
        _watchdog = new Thread(WatchdogWork) {IsBackground = true, Priority = ThreadPriority.BelowNormal};
        _watchdog.Start(_cts);
    }

    void OnDestroy()
    {
        var cancelled = false;
        if (_cts != null)
        {
            try
            {
                _cts.Cancel();
                cancelled = true;
            }
            catch
            {
                // suppress exceptions.
            }
            _cts.Dispose();
            _cts = null;
        }
        if (_watchdog != null)
        {
            if (!cancelled)
            {
                try
                {
                    _watchdog.Abort();
                }
                catch
                {
                    // suppress exceptions.
                }
            }
            _watchdog = null;
        }
    }
}
