using System;

public class TelloConnectionResponse
    : TelloAsciiDatagram
{
    public new const string Text = "conn_ack";

    public ushort VideoUdpPort { get; set; }

    public TelloConnectionResponse()
        : base(Text)
    {
    }

    protected override byte[] SerializeBody()
    {
        return BitConverter.GetBytes(VideoUdpPort);
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        if (count != sizeof(ushort))
            return count > sizeof(ushort)
                ? TelloErrorCode.PacketTooLong
                : TelloErrorCode.PacketTooShort;
        VideoUdpPort = BitConverter.ToUInt16(buffer, offset);
        return TelloErrorCode.NoError;
    }


}