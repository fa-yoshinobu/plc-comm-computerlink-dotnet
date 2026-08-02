namespace PlcComm.Toyopuc;

/// <summary>
/// Explicit connection options for a stable TOYOPUC device session.
/// </summary>
/// <remarks>
/// This type keeps transport, profile, retry, and relay settings explicit for the
/// unified high-level connection flow and generated API documentation.
/// </remarks>
/// <param name="Host">PLC IPv4 address or a hostname that resolves to IPv4.</param>
/// <param name="Port">PLC port number.</param>
/// <param name="Transport">Explicit TCP or UDP transport.</param>
/// <param name="PlcProfile">Required canonical PLC profile name.</param>
/// <param name="Route">Explicit direct or relay route.</param>
public sealed record ToyopucConnectionOptions(
    string Host,
    int Port,
    ToyopucTransportMode Transport,
    string PlcProfile,
    ToyopucRoute Route)
{
    /// <summary>Gets or initializes the communication timeout.</summary>
    /// <remarks>When omitted, each communication attempt uses three seconds. The inclusive maximum is 2,147,483,647 milliseconds.</remarks>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets or initializes the local UDP port. TCP requires zero; a nonzero value is rejected.</summary>
    public int LocalPort { get; init; }

    /// <summary>Gets or initializes the retry count for transport operations.</summary>
    public int Retries { get; init; }

    /// <summary>Gets or initializes the retry delay. The inclusive maximum is 2,147,483,647 milliseconds.</summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>Gets the effective timeout used for a new client instance.</summary>
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(3);

    /// <summary>Gets the effective retry delay used for a new client instance.</summary>
    public TimeSpan EffectiveRetryDelay => RetryDelay ?? TimeSpan.FromMilliseconds(200);
}
