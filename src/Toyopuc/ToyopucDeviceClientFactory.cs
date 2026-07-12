namespace PlcComm.Toyopuc;

/// <summary>
/// Factory helpers for creating connected queued TOYOPUC clients.
/// </summary>
/// <remarks>
/// This factory is the preferred application entry point when you want explicit profile,
/// relay, retry, and timeout settings captured in one documented type.
/// </remarks>
public static class ToyopucDeviceClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued TOYOPUC client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A configured port, local port, timeout, retry count, or retry delay is invalid.
    /// </exception>
    /// <remarks>
    /// The returned queued client keeps the required direct or relay route for every operation.
    /// </remarks>
    public static async Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(
        ToyopucConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");
        if (options.LocalPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "LocalPort must be in the range 0-65535.");
        if (!Enum.IsDefined(options.Transport) || options.Transport == ToyopucTransportMode.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(options), "Transport must be explicitly Tcp or Udp.");
        if (options.Transport == ToyopucTransportMode.Tcp && options.LocalPort != 0)
            throw new ArgumentException("LocalPort can only be specified for UDP.", nameof(options));
        if (options.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be greater than zero.");
        if (options.Retries < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Retries must be zero or greater.");
        if (options.RetryDelay is { } retryDelay && retryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "RetryDelay must be zero or greater.");

        if (string.IsNullOrWhiteSpace(options.PlcProfile))
            throw new ArgumentException("PlcProfile is required.", nameof(options));
        ArgumentNullException.ThrowIfNull(options.Route);

        var normalizedProfile = ToyopucPlcProfiles.NormalizeName(options.PlcProfile);

        var inner = new ToyopucDeviceClient(
            options.Host,
            options.Port,
            options.Transport,
            normalizedProfile,
            options.LocalPort,
            options.EffectiveTimeout,
            options.Retries,
            options.EffectiveRetryDelay);

        var queued = new QueuedToyopucDeviceClient(inner, options.Route);
        await queued.OpenAsync(cancellationToken).ConfigureAwait(false);
        return queued;
    }
}
