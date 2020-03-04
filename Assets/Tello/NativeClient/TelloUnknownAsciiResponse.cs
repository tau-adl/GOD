using System;

public class TelloUnknownAsciiResponse
    : TelloAsciiDatagram
{
    public byte[] Body { get; private set; }

    public TelloUnknownAsciiResponse(string text)
        : base(text)
    {
    }

    protected override TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count)
    {
        Body = new byte[count];
        Buffer.BlockCopy(buffer, offset, Body, 0, count);
        return TelloErrorCode.NoError;
    }

    protected override byte[] SerializeBody()
    {
        return Body;
    }
}