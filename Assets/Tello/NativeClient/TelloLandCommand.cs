using System;

public class TelloLandCommand
    : TelloCommand
{
    public TelloLandCommand()
        : base(TelloPacketType.PacketType68, TelloCommandId.Land)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        return TelloErrorCode.NoError;
    }

    protected override byte[] SerializeBody()
    {
        return new byte[1];
    }
}