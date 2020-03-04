using System;

public class TelloUnknownCommand
    : TelloCommand
{
    public TelloUnknownCommand(TelloPacketType packetType, TelloCommandId commandId)
        : base(packetType, commandId)
    {
    }

    public byte[] Body { get; private set; }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        Body = new byte[count];
        Buffer.BlockCopy(buffer, offset, Body, 0, count);
        return TelloErrorCode.NoError;
    }

    protected override byte[] SerializeBody()
    {
        return Body;
    }
}