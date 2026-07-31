using System.Threading;
using System.Threading.Tasks;

namespace PlcComm.Toyopuc;

public partial class ToyopucClient
{
    private readonly object _operationSync = new();
    private readonly LinkedList<OperationWaiter> _operationWaiters = new();
    private readonly AsyncLocal<OperationContext?> _operationContext = new();
    private OperationGeneration _operationGeneration = new();
    private bool _operationActive;

    private sealed class OperationGeneration
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        internal bool Disposed { get; set; }
        internal bool IsRetired => Cancellation.IsCancellationRequested;

        internal Exception CreateFailure(object client)
            => Disposed
                ? new ObjectDisposedException(client.GetType().FullName)
                : new ToyopucConnectionClosedException();
    }

    private sealed class OperationWaiter(OperationGeneration generation)
    {
        internal OperationGeneration Generation { get; } = generation;
        internal TaskCompletionSource<OperationLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<OperationWaiter>? Node { get; set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
    }

    private readonly record struct OperationLease(OperationGeneration Generation, bool OwnsTurn);
    private sealed record OperationContext(ToyopucClient Client, OperationGeneration Generation);

    public uint ReadDWord(int address)
    {
        return ReadDWords(address, 1)[0];
    }

    public void WriteDWord(int address, uint value)
    {
        WriteDWords(address, new[] { value });
    }

    public uint[] ReadDWords(int address, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackUInt32LowWordFirst(ReadWords(address, checked(count * 2)));
    }

    public void WriteDWords(int address, IEnumerable<uint> values)
    {
        WriteWords(address, PackUInt32LowWordFirstToWords(values));
    }

    public float ReadFloat32(int address)
    {
        return ReadFloat32s(address, 1)[0];
    }

    public void WriteFloat32(int address, float value)
    {
        WriteFloat32s(address, new[] { value });
    }

    public float[] ReadFloat32s(int address, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackFloat32LowWordFirst(ReadWords(address, checked(count * 2)));
    }

    public void WriteFloat32s(int address, IEnumerable<float> values)
    {
        WriteWords(address, PackFloat32LowWordFirstToWords(values));
    }

    public virtual Task OpenAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(Open, cancellationToken);
    }

    /// <summary>Closes the connection and rejects active and queued operations from its transport generation.</summary>
    public virtual Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <summary>Asynchronously completes terminal disposal of the client.</summary>
    public virtual ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    internal Task ClearTraceFramesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClearTraceFrames();
        return Task.CompletedTask;
    }

    internal Task<ResponseFrame> SendRawAsync(int cmd, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var snapshot = data.ToArray();
        return RunStateChangingAsync(() => SendRaw(cmd, snapshot), cancellationToken);
    }

    public Task<int[]> ReadWordsAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadWords(address, count), cancellationToken);
    }

    public Task WriteWordsAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteWords(address, snapshot), cancellationToken);
    }

    public Task<byte[]> ReadBytesAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadBytes(address, count), cancellationToken);
    }

    public Task WriteBytesAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteBytes(address, snapshot), cancellationToken);
    }

    public Task<bool> ReadBitAsync(int address, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadBit(address), cancellationToken);
    }

    public Task WriteBitAsync(int address, bool value, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteBit(address, value), cancellationToken);
    }

    public Task<uint> ReadDWordAsync(int address, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadDWord(address), cancellationToken);
    }

    public Task WriteDWordAsync(int address, uint value, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteDWord(address, value), cancellationToken);
    }

    public Task<uint[]> ReadDWordsAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadDWords(address, count), cancellationToken);
    }

    public Task WriteDWordsAsync(int address, IEnumerable<uint> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteDWords(address, snapshot), cancellationToken);
    }

    public Task<float> ReadFloat32Async(int address, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFloat32(address), cancellationToken);
    }

    public Task WriteFloat32Async(int address, float value, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteFloat32(address, value), cancellationToken);
    }

    public Task<float[]> ReadFloat32sAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFloat32s(address, count), cancellationToken);
    }

    public Task WriteFloat32sAsync(int address, IEnumerable<float> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteFloat32s(address, snapshot), cancellationToken);
    }

    public Task<int[]> ReadWordsMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
    {
        var snapshot = addresses.ToArray();
        return RunAsync(() => ReadWordsMulti(snapshot), cancellationToken);
    }

    public Task WriteWordsMultiAsync(IEnumerable<(int Address, int Value)> pairs, CancellationToken cancellationToken = default)
    {
        var snapshot = pairs.ToArray();
        return RunStateChangingAsync(() => WriteWordsMulti(snapshot), cancellationToken);
    }

    public Task<byte[]> ReadBytesMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
    {
        var snapshot = addresses.ToArray();
        return RunAsync(() => ReadBytesMulti(snapshot), cancellationToken);
    }

    public Task WriteBytesMultiAsync(IEnumerable<(int Address, int Value)> pairs, CancellationToken cancellationToken = default)
    {
        var snapshot = pairs.ToArray();
        return RunStateChangingAsync(() => WriteBytesMulti(snapshot), cancellationToken);
    }

    public Task<int[]> ReadExtWordsAsync(int number, int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadExtWords(number, address, count), cancellationToken);
    }

    public Task WriteExtWordsAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteExtWords(number, address, snapshot), cancellationToken);
    }

    public Task<byte[]> ReadExtBytesAsync(int number, int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadExtBytes(number, address, count), cancellationToken);
    }

    public Task WriteExtBytesAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteExtBytes(number, address, snapshot), cancellationToken);
    }

    public Task<byte[]> ReadExtMultiAsync(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints,
        CancellationToken cancellationToken = default)
    {
        var bitSnapshot = bitPoints.ToArray();
        var byteSnapshot = bytePoints.ToArray();
        var wordSnapshot = wordPoints.ToArray();
        return RunAsync(() => ReadExtMulti(bitSnapshot, byteSnapshot, wordSnapshot), cancellationToken);
    }

    public Task WriteExtMultiAsync(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints,
        CancellationToken cancellationToken = default)
    {
        var bitSnapshot = bitPoints.ToArray();
        var byteSnapshot = bytePoints.ToArray();
        var wordSnapshot = wordPoints.ToArray();
        return RunStateChangingAsync(() => WriteExtMulti(bitSnapshot, byteSnapshot, wordSnapshot), cancellationToken);
    }

    public Task<byte[]> Pc10BlockReadAsync(int address32, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Pc10BlockRead(address32, count), cancellationToken);
    }

    public Task Pc10BlockWriteAsync(int address32, byte[] dataBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataBytes);
        var snapshot = dataBytes.ToArray();
        return RunStateChangingAsync(() => Pc10BlockWrite(address32, snapshot), cancellationToken);
    }

    public Task<byte[]> Pc10MultiReadAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var snapshot = payload.ToArray();
        return RunAsync(() => Pc10MultiRead(snapshot), cancellationToken);
    }

    public Task Pc10MultiWriteAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var snapshot = payload.ToArray();
        return RunStateChangingAsync(() => Pc10MultiWrite(snapshot), cancellationToken);
    }

    public Task<int[]> ReadFrWordsAsync(int index, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFrWords(index, count), cancellationToken);
    }

    public Task WriteFrWorkAreaAsync(int index, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var snapshot = values.ToArray();
        return RunStateChangingAsync(() => WriteFrWorkArea(index, snapshot), cancellationToken);
    }

    public Task CommitFrBlockAsync(
        int index,
        CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => CommitFrBlock(index), cancellationToken);
    }

    internal Task<ResponseFrame> RelayCommandAsync(int linkNo, int stationNo, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(innerPayload);
        var payloadSnapshot = innerPayload.ToArray();
        return RunStateChangingAsync(() => RelayCommand(linkNo, stationNo, payloadSnapshot), cancellationToken);
    }

    internal Task<ResponseFrame> RelayNestedAsync(
        IEnumerable<(int LinkNo, int StationNo)> hops,
        byte[] innerPayload,
        CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = hops.ToArray();
        ArgumentNullException.ThrowIfNull(innerPayload);
        var payloadSnapshot = innerPayload.ToArray();
        return RunStateChangingAsync(() => RelayNested(hopsSnapshot, payloadSnapshot), cancellationToken);
    }

    internal Task<ResponseFrame> SendViaRelayAsync(object hops, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        ArgumentNullException.ThrowIfNull(innerPayload);
        var payloadSnapshot = innerPayload.ToArray();
        return RunStateChangingAsync(() => SendViaRelay(hopsSnapshot, payloadSnapshot), cancellationToken);
    }

    internal Task<ResponseFrame> SendViaRelayReadAsync(object hops, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        ArgumentNullException.ThrowIfNull(innerPayload);
        var payloadSnapshot = innerPayload.ToArray();
        return RunAsync(() => SendViaRelayRead(hopsSnapshot, payloadSnapshot), cancellationToken);
    }

    public Task<int[]> RelayReadWordsAsync(object hops, int address, int count, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => RelayReadWords(hopsSnapshot, address, count), cancellationToken);
    }

    public Task RelayWriteWordsAsync(object hops, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var valuesSnapshot = values.ToArray();
        return RunStateChangingAsync(() => RelayWriteWords(hopsSnapshot, address, valuesSnapshot), cancellationToken);
    }

    public Task<ClockData> RelayReadClockAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => RelayReadClock(snapshot), cancellationToken);
    }

    public Task RelayWriteClockAsync(object hops, DateTime value, int yearBase, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunStateChangingAsync(() => RelayWriteClock(snapshot, value, yearBase), cancellationToken);
    }

    public Task RelayResumeScanAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunStateChangingAsync(() => RelayResumeScan(snapshot), cancellationToken);
    }

    public Task RelayStopScanAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunStateChangingAsync(() => RelayStopScan(snapshot), cancellationToken);
    }

    public Task RelayReleaseScanStopAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunStateChangingAsync(() => RelayReleaseScanStop(snapshot), cancellationToken);
    }

    public Task<CpuStatusData> RelayReadCpuStatusAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => RelayReadCpuStatus(snapshot), cancellationToken);
    }

    public Task<byte[]> RelayReadCpuStatusA0RawAsync(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => RelayReadCpuStatusA0Raw(snapshot), cancellationToken);
    }

    public Task<CpuStatusData> RelayReadCpuStatusA0Async(object hops, CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => RelayReadCpuStatusA0(snapshot), cancellationToken);
    }

    public Task RelayWriteFrWorkAreaAsync(object hops, int index, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var valuesSnapshot = values.ToArray();
        return RunStateChangingAsync(() => RelayWriteFrWorkArea(hopsSnapshot, index, valuesSnapshot), cancellationToken);
    }

    public Task RelayCommitFrBlockAsync(
        object hops,
        int index,
        CancellationToken cancellationToken = default)
    {
        var snapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunStateChangingAsync(() => RelayCommitFrBlock(snapshot, index), cancellationToken);
    }

    public Task<ClockData> ReadClockAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(ReadClock, cancellationToken);
    }

    public Task<CpuStatusData> ReadCpuStatusAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(ReadCpuStatus, cancellationToken);
    }

    public Task<byte[]> ReadCpuStatusA0RawAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(ReadCpuStatusA0Raw, cancellationToken);
    }

    public Task<CpuStatusData> ReadCpuStatusA0Async(CancellationToken cancellationToken = default)
    {
        return RunAsync(ReadCpuStatusA0, cancellationToken);
    }

    public Task WriteClockAsync(DateTime value, int yearBase, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteClock(value, yearBase), cancellationToken);
    }

    public Task ResumeScanAsync(CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(ResumeScan, cancellationToken);
    }

    public Task StopScanAsync(CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(StopScan, cancellationToken);
    }

    public Task ReleaseScanStopAsync(CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(ReleaseScanStop, cancellationToken);
    }

    protected Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        return RunAsyncCore(action, outcomeUnknownAfterSend: false, cancellationToken);
    }

    protected Task RunStateChangingAsync(Action action, CancellationToken cancellationToken = default)
    {
        return RunAsyncCore(action, outcomeUnknownAfterSend: true, cancellationToken);
    }

    protected Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        return RunAsyncCore(action, outcomeUnknownAfterSend: false, cancellationToken);
    }

    protected Task<T> RunStateChangingAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        return RunAsyncCore(action, outcomeUnknownAfterSend: true, cancellationToken);
    }

    private ValueTask<OperationLease> EnterOperationAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var nested = _operationContext.Value;
        if (nested is not null && ReferenceEquals(nested.Client, this))
        {
            if (nested.Generation.IsRetired)
                throw nested.Generation.CreateFailure(this);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new OperationLease(nested.Generation, OwnsTurn: false));
        }

        lock (_operationSync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var generation = _operationGeneration;
            if (!_operationActive)
            {
                _operationActive = true;
                return ValueTask.FromResult(new OperationLease(generation, OwnsTurn: true));
            }

            var waiter = new OperationWaiter(generation);
            waiter.Node = _operationWaiters.AddLast(waiter);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.CancellationRegistration = cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (client, queued, token) = ((ToyopucClient, OperationWaiter, CancellationToken))state!;
                        lock (client._operationSync)
                        {
                            if (queued.Node is null)
                                return;
                            client._operationWaiters.Remove(queued.Node);
                            queued.Node = null;
                            queued.Completion.TrySetCanceled(token);
                        }
                    },
                    (this, waiter, cancellationToken));
            }
            return new ValueTask<OperationLease>(waiter.Completion.Task);
        }
    }

    private void ExitOperation(OperationLease lease)
    {
        if (!lease.OwnsTurn)
            return;

        OperationWaiter? next = null;
        lock (_operationSync)
        {
            if (_operationWaiters.First is { } first)
            {
                next = first.Value;
                _operationWaiters.RemoveFirst();
                next.Node = null;
            }
            else
            {
                _operationActive = false;
            }
        }

        if (next is not null)
        {
            next.CancellationRegistration.Dispose();
            next.Completion.TrySetResult(new OperationLease(next.Generation, OwnsTurn: true));
        }
    }

    private void RetireOperationGeneration(bool disposed)
    {
        OperationGeneration retired;
        OperationWaiter[] rejected;
        lock (_operationSync)
        {
            retired = _operationGeneration;
            retired.Disposed |= disposed;
            _operationGeneration = new OperationGeneration();
            rejected = _operationWaiters
                .Where(waiter => ReferenceEquals(waiter.Generation, retired))
                .ToArray();
            foreach (var waiter in rejected)
            {
                if (waiter.Node is not null)
                {
                    _operationWaiters.Remove(waiter.Node);
                    waiter.Node = null;
                }
            }
        }

        retired.Cancellation.Cancel();
        foreach (var waiter in rejected)
        {
            waiter.CancellationRegistration.Dispose();
            waiter.Completion.TrySetException(retired.CreateFailure(this));
        }
    }

    internal async Task<T> ExecuteExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var lease = await EnterOperationAsync(cancellationToken).ConfigureAwait(false);
        var priorContext = _operationContext.Value;
        if (lease.OwnsTurn)
            _operationContext.Value = new OperationContext(this, lease.Generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.Generation.Cancellation.Token);
        try
        {
            var result = await operation(linked.Token).ConfigureAwait(false);
            if (lease.Generation.IsRetired)
                throw lease.Generation.CreateFailure(this);
            return result;
        }
        catch (ToyopucOperationOutcomeUnknownException)
        {
            throw;
        }
        catch when (lease.Generation.IsRetired)
        {
            throw lease.Generation.CreateFailure(this);
        }
        finally
        {
            if (lease.OwnsTurn)
                _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    internal async Task ExecuteExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteExclusiveAsync(
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private protected T ExecuteSynchronousExclusive<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var lease = EnterOperationAsync(CancellationToken.None).GetAwaiter().GetResult();
        var priorContext = _operationContext.Value;
        var priorCancellation = _operationCancellation.Value;
        if (lease.OwnsTurn)
            _operationContext.Value = new OperationContext(this, lease.Generation);
        _operationCancellation.Value = priorCancellation.CanBeCanceled
            ? priorCancellation
            : lease.Generation.Cancellation.Token;
        try
        {
            var result = operation();
            if (lease.Generation.IsRetired)
                throw lease.Generation.CreateFailure(this);
            return result;
        }
        catch (ToyopucOperationOutcomeUnknownException)
        {
            throw;
        }
        catch when (lease.Generation.IsRetired)
        {
            throw lease.Generation.CreateFailure(this);
        }
        finally
        {
            _operationCancellation.Value = priorCancellation;
            if (lease.OwnsTurn)
                _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    private protected void ExecuteSynchronousExclusive(Action operation)
    {
        ExecuteSynchronousExclusive(
            () =>
            {
                operation();
                return true;
            });
    }

    private async Task RunAsyncCore(
        Action action,
        bool outcomeUnknownAfterSend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var lease = await EnterOperationAsync(cancellationToken).ConfigureAwait(false);
        var priorContext = _operationContext.Value;
        if (lease.OwnsTurn)
            _operationContext.Value = new OperationContext(this, lease.Generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.Generation.Cancellation.Token);
        try
        {
            await Task.Run(
                () =>
                {
                    linked.Token.ThrowIfCancellationRequested();
                    _requestMayHaveBeenSent = false;
                    _operationCancellation.Value = linked.Token;
                    using var cancellationRegistration = linked.Token.Register(
                        static state => ((ToyopucClient)state!).CancelActiveOperation(),
                        this);
                    try
                    {
                        action();
                    }
                    catch (Exception exception) when (linked.IsCancellationRequested)
                    {
                        CancelActiveOperation();
                        if (exception is ToyopucOperationOutcomeUnknownException)
                        {
                            throw;
                        }
                        if (outcomeUnknownAfterSend && _requestMayHaveBeenSent)
                        {
                            throw new ToyopucOperationOutcomeUnknownException(
                                lease.Generation.IsRetired
                                    ? ToyopucOutcomeUnknownReason.Closed
                                    : ToyopucOutcomeUnknownReason.Cancellation,
                                "The operation was canceled after its request may have been sent; the PLC state is unknown.",
                                exception);
                        }
                        throw new OperationCanceledException(linked.Token);
                    }
                    finally
                    {
                        _operationCancellation.Value = default;
                    }
                }).ConfigureAwait(false);
            if (lease.Generation.IsRetired)
                throw lease.Generation.CreateFailure(this);
        }
        catch (ToyopucOperationOutcomeUnknownException)
        {
            throw;
        }
        catch when (lease.Generation.IsRetired)
        {
            throw lease.Generation.CreateFailure(this);
        }
        finally
        {
            if (lease.OwnsTurn)
                _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    private async Task<T> RunAsyncCore<T>(
        Func<T> action,
        bool outcomeUnknownAfterSend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var lease = await EnterOperationAsync(cancellationToken).ConfigureAwait(false);
        var priorContext = _operationContext.Value;
        if (lease.OwnsTurn)
            _operationContext.Value = new OperationContext(this, lease.Generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.Generation.Cancellation.Token);
        try
        {
            var result = await Task.Run(
                () =>
                {
                    linked.Token.ThrowIfCancellationRequested();
                    _requestMayHaveBeenSent = false;
                    _operationCancellation.Value = linked.Token;
                    using var cancellationRegistration = linked.Token.Register(
                        static state => ((ToyopucClient)state!).CancelActiveOperation(),
                        this);
                    try
                    {
                        return action();
                    }
                    catch (Exception exception) when (linked.IsCancellationRequested)
                    {
                        CancelActiveOperation();
                        if (exception is ToyopucOperationOutcomeUnknownException)
                        {
                            throw;
                        }
                        if (outcomeUnknownAfterSend && _requestMayHaveBeenSent)
                        {
                            throw new ToyopucOperationOutcomeUnknownException(
                                lease.Generation.IsRetired
                                    ? ToyopucOutcomeUnknownReason.Closed
                                    : ToyopucOutcomeUnknownReason.Cancellation,
                                "The operation was canceled after its request may have been sent; the PLC state is unknown.",
                                exception);
                        }
                        throw new OperationCanceledException(linked.Token);
                    }
                    finally
                    {
                        _operationCancellation.Value = default;
                    }
                }).ConfigureAwait(false);
            if (lease.Generation.IsRetired)
                throw lease.Generation.CreateFailure(this);
            return result;
        }
        catch (ToyopucOperationOutcomeUnknownException)
        {
            throw;
        }
        catch when (lease.Generation.IsRetired)
        {
            throw lease.Generation.CreateFailure(this);
        }
        finally
        {
            if (lease.OwnsTurn)
                _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    protected static uint[] UnpackUInt32LowWordFirst(IReadOnlyList<int> words)
    {
        if (words.Count % 2 != 0)
        {
            throw new ArgumentException("word count must be even", nameof(words));
        }

        var values = new uint[words.Count / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = PackUInt32LowWordFirst(words[i * 2], words[(i * 2) + 1]);
        }

        return values;
    }

    protected static float[] UnpackFloat32LowWordFirst(IReadOnlyList<int> words)
    {
        var dwords = UnpackUInt32LowWordFirst(words);
        var values = new float[dwords.Length];
        for (var i = 0; i < dwords.Length; i++)
        {
            values[i] = BitConverter.Int32BitsToSingle(unchecked((int)dwords[i]));
        }

        return values;
    }

    protected static int[] PackUInt32LowWordFirstToWords(IEnumerable<uint> values)
    {
        var items = values as uint[] ?? values.ToArray();
        var words = new int[items.Length * 2];
        for (var i = 0; i < items.Length; i++)
        {
            var value = items[i];
            words[i * 2] = (int)(value & 0xFFFF);
            words[(i * 2) + 1] = (int)((value >> 16) & 0xFFFF);
        }

        return words;
    }

    protected static int[] PackFloat32LowWordFirstToWords(IEnumerable<float> values)
    {
        var items = values as float[] ?? values.ToArray();
        var words = new int[items.Length * 2];
        for (var i = 0; i < items.Length; i++)
        {
            if (!float.IsFinite(items[i]))
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Float32 values must be finite.");
            }

            var bits = unchecked((uint)BitConverter.SingleToInt32Bits(items[i]));
            words[i * 2] = (int)(bits & 0xFFFF);
            words[(i * 2) + 1] = (int)((bits >> 16) & 0xFFFF);
        }

        return words;
    }

    private static uint PackUInt32LowWordFirst(int lowWord, int highWord)
    {
        return (uint)((lowWord & 0xFFFF) | ((highWord & 0xFFFF) << 16));
    }
}
