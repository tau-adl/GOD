using System;
using System.IO;

public class TelloDateTimeCommand
    : TelloCommand
{
    public const int RequestBodySize = 0;
    public const int ResponseBodySize = 15;

    public DateTime? DateTime { get; set; }

    public bool IsResponse
    {
        get => PacketType == TelloPacketType.PacketType88;
        set
        {
            PacketType = value
                ? TelloPacketType.PacketType50
                : TelloPacketType.PacketType88;
        }
    }
    
    public TelloDateTimeCommand(DateTime? dateTime = null)
        : base(TelloPacketType.PacketType50, TelloCommandId.DateTime)
    {
        DateTime = dateTime;
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType50: // response
                if (count != ResponseBodySize)
                    return count < ResponseBodySize
                        ? TelloErrorCode.PacketTooShort
                        : TelloErrorCode.PacketTooLong;
                // ? = buffer[offset];
                var year = BitConverter.ToUInt16(buffer, ++offset);
                var month = BitConverter.ToUInt16(buffer, offset += 2);
                var day = BitConverter.ToUInt16(buffer, offset += 2);
                var hour = BitConverter.ToUInt16(buffer, offset += 2);
                var minute = BitConverter.ToUInt16(buffer, offset += 2);
                var second = BitConverter.ToUInt16(buffer, offset += 2);
                var msec = BitConverter.ToUInt16(buffer, offset += 2);
                DateTime = new DateTime(year, month, day, hour, minute, second, msec, DateTimeKind.Utc);
                return TelloErrorCode.NoError;
            case TelloPacketType.PacketType88: // request
                if (count != RequestBodySize)
                    return TelloErrorCode.PacketTooLong;
                DateTime = null;
                return TelloErrorCode.NoError;
            default:
                return TelloErrorCode.UnknownPacketType;
        }
    }

    protected override byte[] SerializeBody()
    {
        switch (PacketType)
        {
            case TelloPacketType.PacketType50: // response
                var dateTime = DateTime ?? System.DateTime.Now;
                using (var stream = new MemoryStream(ResponseBodySize))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)0);
                    writer.Write(unchecked((ushort)dateTime.Year));
                    writer.Write(unchecked((ushort)dateTime.Month));
                    writer.Write(unchecked((ushort)dateTime.Day));
                    writer.Write(unchecked((ushort)dateTime.Hour));
                    writer.Write(unchecked((ushort)dateTime.Minute));
                    writer.Write(unchecked((ushort)dateTime.Second));
                    writer.Write(unchecked((ushort)dateTime.Millisecond));
                    return stream.ToArray();
                }
            case TelloPacketType.PacketType88:
                return new byte[0];
            default:
                throw new TelloException($"{GetType().Name}: {nameof(PacketType)} {PacketType} is not supported for serialization.");
        }
    }
}
