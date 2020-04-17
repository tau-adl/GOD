using System;

public class ConnectionStatusChangedEventArgs
    : EventArgs
{
    public ConnectionStatus OldValue { get; }

    public ConnectionStatus NewValue { get; }

    public ConnectionStatusChangedEventArgs(ConnectionStatus oldValue, ConnectionStatus newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }
}