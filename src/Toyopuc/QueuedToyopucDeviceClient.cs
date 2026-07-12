using System.Threading;

namespace PlcComm.Toyopuc;

/// <summary>
/// A wrapper for <see cref="ToyopucDeviceClient"/> that serializes compound async operations.
/// </summary>
/// <remarks>
/// Use this wrapper when one TOYOPUC transport session must serve multiple high-level reads,
/// writes, poll cycles, or relay operations without overlapping command lifetimes.
/// </remarks>
public sealed class QueuedToyopucDeviceClient : IAsyncDisposable, IDisposable
{
    private readonly ToyopucDeviceClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;
    private readonly ToyopucRoute _route;

    private async Task EnterAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) != 0)
        {
            _gate.Release();
            ObjectDisposedException.ThrowIf(true, this);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedToyopucDeviceClient"/> class.
    /// </summary>
    /// <param name="client">The underlying TOYOPUC client.</param>
    /// <param name="route">Required direct or relay route.</param>
    public QueuedToyopucDeviceClient(ToyopucDeviceClient client, ToyopucRoute route)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _route = route ?? throw new ArgumentNullException(nameof(route));
    }

    /// <summary>Gets the wrapped low-level client.</summary>
    /// <remarks>
    /// Use <see cref="ExecuteAsync{T}(Func{ToyopucDeviceClient, Task{T}}, CancellationToken)"/> when you need
    /// direct client access while preserving serialized execution.
    /// </remarks>
    internal ToyopucDeviceClient InnerClient => _client;

    /// <summary>Gets the configured relay hops, if any.</summary>
    /// <remarks>The returned sequence is already normalized to link/station tuples.</remarks>
    public IReadOnlyList<(int LinkNo, int StationNo)>? RelayHops => _route.RelayHops;

    /// <summary>Gets the explicit direct or relay route.</summary>
    public ToyopucRoute Route => _route;

    /// <summary>Gets a value indicating whether relay mode is enabled.</summary>
    public bool UsesRelay => _route.UsesRelay;

    /// <summary>Gets the PLC host.</summary>
    public string Host => _client.Host;

    /// <summary>Gets the PLC port.</summary>
    public int Port => _client.Port;

    /// <summary>Gets the selected transport protocol.</summary>
    public ToyopucTransportMode Transport => _client.Transport;

    /// <summary>Gets the validated per-attempt operation timeout.</summary>
    public TimeSpan Timeout => _client.Timeout;

    /// <summary>Gets the normalized PLC profile name.</summary>
    public string PlcProfile => _client.PlcProfile;

    /// <summary>Gets a value indicating whether the underlying transport is open.</summary>
    public bool IsOpen => _client.IsOpen;

    /// <summary>Opens the connection asynchronously with exclusive access.</summary>
    /// <remarks>Relay and direct sessions both flow through this gate-protected open path.</remarks>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes an async operation with exclusive access to the wrapped client.</summary>
    /// <typeparam name="T">Result type produced by the custom operation.</typeparam>
    /// <param name="operation">Delegate that receives the wrapped <see cref="ToyopucDeviceClient"/>.</param>
    /// <param name="cancellationToken">Cancellation token used while waiting for exclusive access.</param>
    /// <returns>The value returned by <paramref name="operation"/>.</returns>
    internal async Task<T> ExecuteAsync<T>(
        Func<ToyopucDeviceClient, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes an async operation with exclusive access to the wrapped client.</summary>
    /// <param name="operation">Delegate that receives the wrapped <see cref="ToyopucDeviceClient"/>.</param>
    /// <param name="cancellationToken">Cancellation token used while waiting for exclusive access.</param>
    internal async Task ExecuteAsync(
        Func<ToyopucDeviceClient, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disposes the wrapper and the underlying client.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _gate.Wait();
        try
        {
            _client.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disposes the wrapper and the underlying client asynchronously.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
