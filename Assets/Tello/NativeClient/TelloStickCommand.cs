using System;

public class TelloStickCommand
    : TelloCommand
{
    public const int Size = 11;

    private TelloStickData _data;

    public short Fast { get => _data.Fast; set => _data.Fast = value; }
    public short Throttle { get => _data.Throttle; set => _data.Throttle = value; }
    public short Roll { get => _data.Roll; set => _data.Roll = value; }
    public short Pitch { get => _data.Pitch; set => _data.Pitch = value; }
    public short Yaw { get => _data.Yaw; set => _data.Yaw = value; }

    public TelloStickData Data
    {
        get => _data;
        set => _data = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TelloStickCommand(TelloStickData data = null)
        : base(TelloPacketType.PacketType60, TelloCommandId.Stick, true)
    {
        _data = data ?? new TelloStickData();
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        if (count != Size)
            return count < Size 
                ? TelloErrorCode.PacketTooShort
                : TelloErrorCode.PacketTooLong;
        Roll = unchecked((sbyte)buffer[offset + 0]);
        Pitch = unchecked((sbyte)buffer[offset + 1]);
        Throttle = buffer[2];
        Yaw = buffer[3];
        Fast = buffer[4];
        return TelloErrorCode.NoError;
    }

    protected override byte[] SerializeBody()
    {
        SequenceNumber = 0;
        var body = new byte[Size];
        var now = DateTime.Now;            
        var roll = unchecked((ulong)(Roll + 256));
        var pitch = unchecked((ulong)(Pitch + 256));
        var throttle = unchecked((ulong)(Throttle + 256));
        var yaw = unchecked((ulong)(Yaw + 256));
        var fast = unchecked((ulong)(Fast + 256));
        var data = ((ulong)fast << 46) | (yaw << 35) | (throttle << 24) | (pitch << 13) | (roll << 2);
        body[0] = unchecked((byte)data);
        body[1] = unchecked((byte)(data >> 8));
        body[2] = unchecked((byte)(data >> 16));
        body[3] = unchecked((byte)(data >> 24));
        body[4] = unchecked((byte)(data >> 32));
        body[5] = unchecked((byte)(data >> 40));
        body[6] = unchecked((byte)now.Hour);
        body[7] = unchecked((byte)now.Minute);
        body[8] = unchecked((byte)now.Second);
        body[9] = unchecked((byte)(now.Millisecond & 0xff));
        body[10] = unchecked((byte)(now.Millisecond >> 8));
        return body;
    }
}
