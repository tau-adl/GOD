using System;
using System.Net;

public sealed class TelloConnectionRequest
    : TelloAsciiDatagram
{
    public new const string Text = "conn_req";
    public const int BodySize = 2;
    public const int Size = 11; // Text.Length + 1 + BodySize;

    private ushort _videoUdpPort;

    /// <summary>
    /// Gets or sets the requested UDP port for video streaming.
    /// </summary>
    public int VideoUdpPort
    {
        get { return _videoUdpPort; }
        set
        {
            if (value < IPEndPoint.MinPort || value > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"Argument '{nameof(value)}' must be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}.");
            _videoUdpPort = unchecked((ushort) value);
        }
    }

    public TelloConnectionRequest(int videoUdpPort = TelloNativeClient.DefaultVideoUdpPort)
        : base(Text)
    {
        VideoUdpPort = videoUdpPort;
    }

    protected override byte[] SerializeBody()
    {
        return BitConverter.GetBytes(_videoUdpPort);
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        if (count != BodySize)
            return count < BodySize ? TelloErrorCode.PacketTooShort : TelloErrorCode.PacketTooLong;
        _videoUdpPort = BitConverter.ToUInt16(buffer, offset);
        return TelloErrorCode.NoError;
    }
}