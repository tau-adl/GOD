public class TelloVideoSpsPpsCommand
    : TelloCommand
{
    public const int BodySize = 0;

    public TelloVideoSpsPpsCommand()
        : base(TelloPacketType.PacketType60, TelloCommandId.GetVideoSpsPps, true)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType60:
                if (count != BodySize)
                    return TelloErrorCode.PacketTooLong;
                return TelloErrorCode.NoError;
            default:
                return TelloErrorCode.UnknownPacketType;
        }
    }

    protected override byte[] SerializeBody()
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType60:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
