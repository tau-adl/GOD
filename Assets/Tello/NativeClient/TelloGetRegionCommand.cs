using System;

public class TelloGetRegionCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 4;

    private string _region;

    public string Region
    {
        get => _region;
        set
        {
            if (!string.IsNullOrEmpty(value) && value.Length != 2 && char.IsLetter(value[0]) && char.IsLetter(value[1]))
                throw new ArgumentException($"Property '{nameof(Region)}' must be a two-letter string.", nameof(value));
            _region = value;
        }
    }

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

    public TelloGetRegionCommand(string region = null)
        : base(TelloPacketType.PacketType48, TelloCommandId.GetRegion)
    {
        Region = region;
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
                // ? = buffer[offset];
                Region = new string(new[] { (char)buffer[offset + 1], (char)buffer[offset + 2] });
                // ? = buffer[offset + 3];
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType48: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                Region = null;
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
                var region = Region ?? TelloNativeClient.DefaultRegion;
                var body = new byte[ResponseBodySize];
                body[1] = (byte)region[1];
                body[2] = (byte)region[2];
                return body;
            case TelloPacketType.PacketType48:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
