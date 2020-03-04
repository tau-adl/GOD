using System;
using System.IO;

public abstract class TelloCommand
    : TelloDatagram
{
    public const int Magic = 0xCC;
    public const int HeaderSize = 11;
    public TelloPacketType PacketType { get; set; }
    public TelloCommandId CommandId { get; private set; }
    public ushort SequenceNumber { get; set; }

    public bool ZeroSequenceNumber { get; private set; }

    protected TelloCommand(TelloPacketType packetType, TelloCommandId commandId, bool zeroSequenceNumber = false)
    {
        PacketType = packetType;
        CommandId = commandId;
        ZeroSequenceNumber = zeroSequenceNumber;
    }

    protected abstract byte[] SerializeBody();

    protected abstract TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count);

    protected override byte[] SerializeCore()
    {
        var body = SerializeBody();
        var packetSize = HeaderSize + (body != null ? body.Length : 0);
        using (var output = new MemoryStream())
        using (var writer = new BinaryWriter(output))
        {
            writer.Write((byte)Magic);
            writer.Write((ushort)(packetSize << 3));
            var crc8 = ComputeCrc8(output.GetBuffer(), 0, 3);
            writer.Write(crc8);
            writer.Write((byte)PacketType);
            writer.Write((ushort)CommandId);
            writer.Write(SequenceNumber);
            if (body != null)
                writer.Write(body);
            var crc16 = ComputeCrc16(output.GetBuffer(), 0, packetSize - sizeof(ushort));
            writer.Write(crc16);
            return output.ToArray();
        }
    }

    public static TelloCommand Create(TelloPacketType packetType, TelloCommandId commandId)
    {
        switch (commandId)
        {
            case TelloCommandId.DateTime:
                return new TelloDateTimeCommand { PacketType = packetType };
            case TelloCommandId.GetVersionString:
                return new TelloGetVersionCommand { PacketType = packetType };
            case TelloCommandId.GetVideoSpsPps:
                return new TelloVideoSpsPpsCommand { PacketType = packetType };
            case TelloCommandId.GetRegion:
                return new TelloGetRegionCommand { PacketType = packetType };
            case TelloCommandId.Stick:
                return new TelloStickCommand { PacketType = packetType };
            case TelloCommandId.GetVideoBitRate:
                return new TelloGetVideoBitRateCommand { PacketType = packetType };
            case TelloCommandId.GetLowBatteryThreshold:
                return new TelloGetLowBatteryThresholdCommand { PacketType = packetType };
            case TelloCommandId.ExposureValue:
                return new TelloExposureValueCommand { PacketType = packetType };
            case TelloCommandId.GetAttitudeAngle:
                return new TelloGetAttitudeAngleCommand { PacketType = packetType };
            case TelloCommandId.GetAltitudeLimit:
                return new TelloGetAltitudeLimitCommand { PacketType = packetType };
            default:
                return new TelloUnknownCommand(packetType, commandId);
        }
    }

    public static new TelloCommand DeserializeNew(byte[] buffer, int offset, int count, out TelloErrorCode errorCode)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (buffer.Length < offset + count)
            throw new ArgumentException(
                $"The length of '{nameof(buffer)}' can't be " +
                $"less than the sum of '{nameof(offset)}' and '{nameof(count)}'.");
        if (count < HeaderSize) {
            errorCode = TelloErrorCode.PacketTooShort;
            return null;
        }

        using (var reader = new BinaryReader(new MemoryStream(buffer, offset, count)))
        {
            var magic = reader.ReadByte();
            if (magic != Magic)
            {
                errorCode = TelloErrorCode.UnsupportedMagic;
                return null;
            }
            var size = reader.ReadUInt16() >> 3;
            var givenCrc8 = reader.ReadByte();
            var computedCrc8 = ComputeCrc8(buffer, offset, 3);
            if (givenCrc8 != computedCrc8)
            {
                errorCode = TelloErrorCode.BadCrc8;
                return null;
            }
            var packetType = (TelloPacketType) reader.ReadByte();
            var commandId = (TelloCommandId)reader.ReadUInt16();
            var command = Create(packetType, commandId);
            command.SequenceNumber = reader.ReadUInt16();
            var data = reader.ReadBytes(count - HeaderSize);
            var givenCrc16 = reader.ReadUInt16();
            var computedCrc16 = ComputeCrc16(buffer, offset, count - sizeof(UInt16));
            if (givenCrc16 != computedCrc16)
            {
                errorCode = TelloErrorCode.BadCrc16;
                return null;
            }
            errorCode = command.DeserializeBody(data, 0, data.Length);
            return command;
        }
    }
}