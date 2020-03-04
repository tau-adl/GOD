using System;

public class TelloException
    : Exception
{
    public TelloException()
    {
    }

    public TelloException(string message)
        : base(message)
    {
    }

    public TelloException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TelloException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}