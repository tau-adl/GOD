using System;
using System.Runtime.Serialization;

public class GodException
    : ApplicationException
{
    public GodException(string message)
        : base(message)
    {
    }

    public GodException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GodException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
