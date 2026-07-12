namespace PlcComm.Toyopuc;

public class ToyopucError : Exception
{
    public ToyopucError()
    {
    }

    public ToyopucError(string message)
        : base(message)
    {
    }

    public ToyopucError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class ToyopucProtocolError : ToyopucError
{
    public ToyopucProtocolError()
    {
    }

    public ToyopucProtocolError(string message)
        : base(message)
    {
    }

    public ToyopucProtocolError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class ToyopucTimeoutError : ToyopucProtocolError
{
    public ToyopucTimeoutError()
    {
    }

    public ToyopucTimeoutError(string message)
        : base(message)
    {
    }

    public ToyopucTimeoutError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Indicates that cancellation or transport loss occurred after a state-changing request may have been sent.
/// </summary>
public sealed class ToyopucOperationOutcomeUnknownException : ToyopucError
{
    public ToyopucOperationOutcomeUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
