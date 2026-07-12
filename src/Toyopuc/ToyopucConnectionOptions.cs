namespace PlcComm.Toyopuc;

/// <summary>
/// Explicit connection options for a stable TOYOPUC device session.
/// </summary>
/// <remarks>
/// This type keeps transport, profile, retry, and relay settings explicit for the
/// unified high-level connection flow and generated API documentation.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
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
    /// <summary>Gets or sets the communication timeout.</summary>
    /// <remarks>When omitted, each communication attempt uses three seconds.</remarks>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets or sets the local UDP port. Ignored for TCP.</summary>
    public int LocalPort { get; init; }

    /// <summary>Gets or sets the retry count for transport operations.</summary>
    public int Retries { get; init; }

    /// <summary>Gets or sets the retry delay.</summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>Gets the effective timeout used for a new client instance.</summary>
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(3);

    /// <summary>Gets the effective retry delay used for a new client instance.</summary>
    public TimeSpan EffectiveRetryDelay => RetryDelay ?? TimeSpan.FromMilliseconds(200);
}
