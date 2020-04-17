using System.Text;

public abstract class GodDatagram
{
    public GodDatagramType Type { get; }

    protected GodDatagram(GodDatagramType type)
    {
        Type = type;
    }

    public static bool TryParse(string text, out GodDatagram datagram)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var headerLength = text.IndexOf(' ');
            var header = headerLength >= 0 ? text.Substring(0, headerLength) : text;
            switch (header)
            {
                case GodMessages.Update:
                {
                    var result = GodUpdateDatagram.TryParse(text, out GodUpdateDatagram updateDatagram);
                    datagram = updateDatagram;
                    return result;
                }
            }
        }
        // invalid or unsupported message.
        datagram = default;
        return false;
    }

    public static bool TryDeserialize(byte[] buffer, int offset, int count, out GodDatagram datagram)
    {
        var text = Encoding.ASCII.GetString(buffer, offset, count);
        return TryParse(text, out datagram);
    }

    public static bool TryDeserialize(byte[] buffer, out GodDatagram datagram)
    {
        return TryDeserialize(buffer, 0, buffer.Length, out datagram);
    }

    public int Serialize(byte[] buffer, int offset)
    {
        var text = ToString();
        return Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, offset);
    }

    public byte[] Serialize()
    {
        var text = ToString();
        return Encoding.ASCII.GetBytes(text);
    }
}
