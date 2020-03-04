using System;
using System.Text;

public abstract class TelloAsciiDatagram
    : TelloDatagram
{
    public string Text { get; private set; }

    protected TelloAsciiDatagram(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        Text = text;
    }

    public static TelloAsciiDatagram Create(string text)
    {
        switch (text)
        {
            case TelloConnectionRequest.Text:
                return new TelloConnectionRequest();
            case TelloConnectionResponse.Text:
                return new TelloConnectionResponse();
            default:
                return new TelloUnknownAsciiResponse(text);
        }
    }

    protected abstract byte[] SerializeBody();

    protected abstract TelloErrorCode DeserializeBody(byte[] buffer, int offset, int count);

    protected override byte[] SerializeCore()
    {
        var body = SerializeBody();
        var textSize = Encoding.ASCII.GetByteCount(Text);
        var size = textSize + 1 + body.Length;
        byte[] bytes = new byte[size];
        Encoding.ASCII.GetBytes(Text, 0, Text.Length, bytes, 0);
        bytes[textSize] = (byte)':';
        Buffer.BlockCopy(body, 0, bytes, textSize + 1, body.Length);
        return bytes;
    }

    private static bool IsPrintableAscii(byte b)
    {
        return b >= 32 && b <= 126;
    }

    public static new TelloAsciiDatagram DeserializeNew(byte[] buffer, int offset, int count, out TelloErrorCode errorCode)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Argument {nameof(count)} must be positive.");
        if (buffer.Length < offset + count)
            throw new ArgumentException(
                $"The length of '{nameof(buffer)}' can't be " +
                $"less than the sum of '{nameof(offset)}' and '{nameof(count)}'.");
        // find the colon character in the buffer:
        int asciiLength;
        bool found = false;
        for (asciiLength = 0; asciiLength < count; ++asciiLength)
        {
            var b = buffer[offset + asciiLength];
            found = b == (byte)':';
            if (found)
                break;
            else if (!IsPrintableAscii(b))
            {
                errorCode = TelloErrorCode.InvalidAsciiCharacter;
                return null;
            }
        }
        if (!found)
        {
            errorCode = TelloErrorCode.UnexpectedAsciiSequence;
            return null;
        }
        var text = Encoding.ASCII.GetString(buffer, offset, asciiLength);
        var datagram = Create(text);
        errorCode = datagram.DeserializeBody(buffer, asciiLength + 1, count - asciiLength - 1);
        return datagram;
    }
}