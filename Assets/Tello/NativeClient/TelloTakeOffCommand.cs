using System;

public class TelloTakeOffCommand
    : TelloCommand
{
    public TelloTakeOffCommand()
        : base(TelloPacketType.PacketType68, TelloCommandId.TakeOff)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        return TelloErrorCode.NoError;
    }

    protected override byte[] SerializeBody()
    {
        return new byte[0];
    }
}