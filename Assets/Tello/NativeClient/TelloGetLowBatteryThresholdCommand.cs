using System;

public class TelloGetLowBatteryThresholdCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 3;

    public ushort LowBatteryThreshold { get; set; }

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

    public TelloGetLowBatteryThresholdCommand()
        : base(TelloPacketType.PacketType48, TelloCommandId.GetLowBatteryThreshold)
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
                LowBatteryThreshold = BitConverter.ToUInt16(buffer, offset + 1);
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType48: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                LowBatteryThreshold = 0;
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
                var bytes = new byte[3];
                bytes[1] = unchecked((byte)(LowBatteryThreshold & 0xFF));
                bytes[2] = unchecked((byte)(LowBatteryThreshold >> 8));
                return bytes;
            case TelloPacketType.PacketType48:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
