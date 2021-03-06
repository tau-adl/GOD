﻿public enum ConnectionStatus
{
    UnknownError = -7,
    NetworkError = -6,
    CantResolveHostName = -5,
    ConnectionRefused = -4,
    AddressAlreadyInUse = -3,
    NoRoute = -2,
    Timeout = -1,

    Offline = 0,
    Disconnected = 1,
    Disconnecting = 2,
    Connecting = 3,
    Connected = 4,
    Online = 5
}