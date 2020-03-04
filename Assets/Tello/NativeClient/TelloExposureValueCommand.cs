public sealed class TelloExposureValueCommand
    : TelloCommand
{
    public const int RequestBodySize = 1;
    public const int ResponseBodySize = 1;

    public byte ExposureValue { get; set; }

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

    public TelloExposureValueCommand()
        : base(TelloPacketType.PacketType48, TelloCommandId.ExposureValue)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType48: // request
                if (count != ResponseBodySize)
                    return count < ResponseBodySize
                        ? TelloErrorCode.PacketTooShort
                        : TelloErrorCode.PacketTooLong;
                ExposureValue = buffer[offset];
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType90: // response
                if (count != ResponseBodySize)
                    return count < ResponseBodySize
                        ? TelloErrorCode.PacketTooShort
                        : TelloErrorCode.PacketTooLong;
                ExposureValue = buffer[offset];
                return TelloErrorCode.NoError;
            default:
                return TelloErrorCode.UnknownPacketType;
        }
    }

    protected override byte[] SerializeBody()
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType48: // request
                return new byte[] { ExposureValue };
            case TelloPacketType.PacketType90: // response
                return new byte[] { ExposureValue };
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
