using System;
using System.Text;

public class TelloGetVersionCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 32;
    public const int VersionMaxLength = 31;

    private string _version;

    public string Version
    {
        get => _version;
        set
        {
            if (value != null && value.Length > VersionMaxLength)
                throw new ArgumentException($"{nameof(Version)} string length cannot exceed {VersionMaxLength}.", nameof(value));
            _version = value;
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

    public TelloGetVersionCommand(string version = null)
        : base(TelloPacketType.PacketType48, TelloCommandId.GetVersionString)
    {
        Version = version;
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
                int length;
                for (length = 0; length < VersionMaxLength; ++length)
                    if (buffer[length] == 0)
                        break;
                Version = Encoding.ASCII.GetString(buffer, offset + 1, length);
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType48: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                Version = null;
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
                var version = Version ?? string.Empty;
                var body = new byte[ResponseBodySize];
                for (var i = 0; i < version.Length; ++i)
                    body[i] = (byte)version[i];
                return body;
            case TelloPacketType.PacketType48:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
