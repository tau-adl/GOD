public sealed class TelloGetAttitudeAngleCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 5;

    public byte Angle1 { get; set; }
    public byte Angle2 { get; set; }
    public byte Angle3 { get; set; }
    public byte Angle4 { get; set; }

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

    public TelloGetAttitudeAngleCommand()
        : base(TelloPacketType.PacketType48, TelloCommandId.GetAttitudeAngle)
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
                Angle1 = buffer[offset + 1];
                Angle1 = buffer[offset + 2];
                Angle1 = buffer[offset + 3];
                Angle1 = buffer[offset + 4];
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType48: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                Angle1 = 0;
                Angle2 = 0;
                Angle3 = 0;
                Angle4 = 0;
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
                return new byte[] { 0, Angle1, Angle2, Angle3, Angle4 };
            case TelloPacketType.PacketType48:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
