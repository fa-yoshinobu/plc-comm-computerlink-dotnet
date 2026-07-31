namespace PlcComm.Toyopuc;

/// <summary>
/// Factory helpers for creating connected TOYOPUC clients.
/// </summary>
/// <remarks>
/// This factory is the preferred application entry point when you want explicit profile,
/// relay, retry, and timeout settings captured in one documented type.
/// </remarks>
public static class ToyopucDeviceClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a TOYOPUC client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected client whose ordinary async operations use one FIFO queue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The host is empty, whitespace, or an IPv6 literal. Connections are IPv4-only.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A configured port, local port, timeout, retry count, or retry delay is invalid.
    /// </exception>
    /// <remarks>
    /// The returned client keeps the required direct or relay route for every ordinary high-level operation.
    /// </remarks>
    public static async Task<ToyopucDeviceClient> OpenAndConnectAsync(
        ToyopucConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var normalizedHost = ToyopucAddressFamilyValidation.NormalizeIPv4Host(options.Host, nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");
        if (options.LocalPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "LocalPort must be in the range 0-65535.");
        if (!Enum.IsDefined(options.Transport) || options.Transport == ToyopucTransportMode.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(options), "Transport must be explicitly Tcp or Udp.");
        if (options.Transport == ToyopucTransportMode.Tcp && options.LocalPort != 0)
            throw new ArgumentException("LocalPort can only be specified for UDP.", nameof(options));
        if (options.Timeout is { } timeout)
            ToyopucTimerValidation.RequirePositive(timeout, nameof(options), "Timeout");
        if (options.Retries < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Retries must be zero or greater.");
        if (options.RetryDelay is { } retryDelay)
            ToyopucTimerValidation.RequireNonNegative(retryDelay, nameof(options), "RetryDelay");

        if (string.IsNullOrWhiteSpace(options.PlcProfile))
            throw new ArgumentException("PlcProfile is required.", nameof(options));
        ArgumentNullException.ThrowIfNull(options.Route);

        var normalizedProfile = ToyopucPlcProfiles.NormalizeName(options.PlcProfile);

        var inner = new ToyopucDeviceClient(
            normalizedHost,
            options.Port,
            options.Transport,
            normalizedProfile,
            options.LocalPort,
            options.EffectiveTimeout,
            options.Retries,
            options.EffectiveRetryDelay,
            options.Route);

        try
        {
            await inner.OpenAsync(cancellationToken).ConfigureAwait(false);
            return inner;
        }
        catch
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
