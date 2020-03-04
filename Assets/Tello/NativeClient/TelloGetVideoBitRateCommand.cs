public class TelloGetVideoBitRateCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 1;

    public byte VideoBitRate { get; set; }

    public bool IsResponse
    {
        get => PacketType == TelloPacketType.PacketType90;
        set
        {
            PacketType = value
                ? TelloPacketType.PacketType90
                : TelloPacketType.PacketType48;
        }
    }

    public TelloGetVideoBitRateCommand()
        : base(TelloPacketType.PacketType48, TelloCommandId.GetVideoBitRate)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType90: // response
                if (count != ResponseBodySize)
                    return count < ResponseBodySize
                        ? TelloErrorCode.PacketTooShort
                        : TelloErrorCode.PacketTooLong;
                VideoBitRate = buffer[offset];
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType48: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                VideoBitRate = 0;
                return TelloErrorCode.NoError;
            default:
                return TelloErrorCode.UnknownPacketType;
        }
    }

    protected override byte[] SerializeBody()
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType90: // response
                return new byte[] { VideoBitRate };
            case TelloPacketType.PacketType48:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
