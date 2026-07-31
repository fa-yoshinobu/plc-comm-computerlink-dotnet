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

/// <summary>Indicates that the configured connect or transaction deadline expired.</summary>
public class ToyopucTimeoutError : ToyopucError
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

/// <summary>Indicates a transport I/O failure distinct from timeout and protocol decoding.</summary>
public sealed class ToyopucTransportError : ToyopucError
{
    public ToyopucTransportError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Indicates that an operation requires an explicit reconnect before it can run.</summary>
public sealed class ToyopucNotConnectedException : InvalidOperationException
{
    public ToyopucNotConnectedException(string message)
        : base(message)
    {
    }
}

/// <summary>Indicates that the PLC returned a syntactically valid NG response.</summary>
public sealed class ToyopucPlcError : ToyopucError
{
    public ToyopucPlcError(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Indicates that <see cref="ToyopucClient.Close"/> retired the transport generation
/// that owned an active or queued operation.
/// </summary>
public sealed class ToyopucConnectionClosedException : InvalidOperationException
{
    public ToyopucConnectionClosedException()
        : base("The TOYOPUC connection was closed before the operation completed.")
    {
    }
}

/// <summary>Machine-readable reason why a state-changing operation has an unknown outcome.</summary>
public enum ToyopucOutcomeUnknownReason
{
    Timeout,
    Cancellation,
    Closed,
    Transport,
    MalformedResponse,
}

/// <summary>
/// Indicates that cancellation or transport loss occurred after a state-changing request may have been sent.
/// </summary>
public sealed class ToyopucOperationOutcomeUnknownException : ToyopucError
{
    public ToyopucOperationOutcomeUnknownException(
        ToyopucOutcomeUnknownReason reason,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    public ToyopucOutcomeUnknownReason Reason { get; }
}
