using System.Threading;
using System.Threading.Tasks;

namespace PlcComm.Toyopuc;

public partial class ToyopucClient
{
    private readonly SemaphoreSlim _asyncGate = new(1, 1);

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

    public virtual Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(Close, cancellationToken);
    }

    public virtual ValueTask DisposeAsync()
    {
        Close();
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
        return RunStateChangingAsync(() => SendRaw(cmd, data), cancellationToken);
    }

    public Task<int[]> ReadWordsAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadWords(address, count), cancellationToken);
    }

    public Task WriteWordsAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteWords(address, values), cancellationToken);
    }

    public Task<byte[]> ReadBytesAsync(int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadBytes(address, count), cancellationToken);
    }

    public Task WriteBytesAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteBytes(address, values), cancellationToken);
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
        return RunStateChangingAsync(() => WriteDWords(address, values), cancellationToken);
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
        return RunStateChangingAsync(() => WriteFloat32s(address, values), cancellationToken);
    }

    public Task<int[]> ReadWordsMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadWordsMulti(addresses), cancellationToken);
    }

    public Task WriteWordsMultiAsync(IEnumerable<(int Address, int Value)> pairs, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteWordsMulti(pairs), cancellationToken);
    }

    public Task<byte[]> ReadBytesMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadBytesMulti(addresses), cancellationToken);
    }

    public Task WriteBytesMultiAsync(IEnumerable<(int Address, int Value)> pairs, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteBytesMulti(pairs), cancellationToken);
    }

    public Task<int[]> ReadExtWordsAsync(int number, int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadExtWords(number, address, count), cancellationToken);
    }

    public Task WriteExtWordsAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteExtWords(number, address, values), cancellationToken);
    }

    public Task<byte[]> ReadExtBytesAsync(int number, int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadExtBytes(number, address, count), cancellationToken);
    }

    public Task WriteExtBytesAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteExtBytes(number, address, values), cancellationToken);
    }

    public Task<byte[]> ReadExtMultiAsync(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadExtMulti(bitPoints, bytePoints, wordPoints), cancellationToken);
    }

    public Task WriteExtMultiAsync(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints,
        CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteExtMulti(bitPoints, bytePoints, wordPoints), cancellationToken);
    }

    public Task<byte[]> Pc10BlockReadAsync(int address32, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Pc10BlockRead(address32, count), cancellationToken);
    }

    public Task Pc10BlockWriteAsync(int address32, byte[] dataBytes, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => Pc10BlockWrite(address32, dataBytes), cancellationToken);
    }

    public Task<byte[]> Pc10MultiReadAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Pc10MultiRead(payload), cancellationToken);
    }

    public Task Pc10MultiWriteAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => Pc10MultiWrite(payload), cancellationToken);
    }

    public Task<int[]> ReadFrWordsAsync(int index, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFrWords(index, count), cancellationToken);
    }

    public Task WriteFrWorkAreaAsync(int index, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => WriteFrWorkArea(index, values), cancellationToken);
    }

    public Task CommitFrBlockAsync(
        int index,
        CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => CommitFrBlock(index), cancellationToken);
    }

    internal Task<ResponseFrame> RelayCommandAsync(int linkNo, int stationNo, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayCommand(linkNo, stationNo, innerPayload), cancellationToken);
    }

    internal Task<ResponseFrame> RelayNestedAsync(
        IEnumerable<(int LinkNo, int StationNo)> hops,
        byte[] innerPayload,
        CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayNested(hops, innerPayload), cancellationToken);
    }

    internal Task<ResponseFrame> SendViaRelayAsync(object hops, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => SendViaRelay(hops, innerPayload), cancellationToken);
    }

    internal Task<ResponseFrame> SendViaRelayReadAsync(object hops, byte[] innerPayload, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => SendViaRelayRead(hops, innerPayload), cancellationToken);
    }

    public Task<int[]> RelayReadWordsAsync(object hops, int address, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadWords(hops, address, count), cancellationToken);
    }

    public Task RelayWriteWordsAsync(object hops, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayWriteWords(hops, address, values), cancellationToken);
    }

    public Task<ClockData> RelayReadClockAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadClock(hops), cancellationToken);
    }

    public Task RelayWriteClockAsync(object hops, DateTime value, int yearBase, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayWriteClock(hops, value, yearBase), cancellationToken);
    }

    public Task RelayResumeScanAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayResumeScan(hops), cancellationToken);
    }

    public Task RelayStopScanAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayStopScan(hops), cancellationToken);
    }

    public Task RelayReleaseScanStopAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayReleaseScanStop(hops), cancellationToken);
    }

    public Task<CpuStatusData> RelayReadCpuStatusAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadCpuStatus(hops), cancellationToken);
    }

    public Task<byte[]> RelayReadCpuStatusA0RawAsync(object hops, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadCpuStatusA0Raw(hops), cancellationToken);
    }

    public Task<CpuStatusData> RelayReadCpuStatusA0Async(object hops, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadCpuStatusA0(hops), cancellationToken);
    }

    public Task RelayWriteFrWorkAreaAsync(object hops, int index, IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayWriteFrWorkArea(hops, index, values), cancellationToken);
    }

    public Task RelayCommitFrBlockAsync(
        object hops,
        int index,
        CancellationToken cancellationToken = default)
    {
        return RunStateChangingAsync(() => RelayCommitFrBlock(hops, index), cancellationToken);
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

    private async Task RunAsyncCore(
        Action action,
        bool outcomeUnknownAfterSend,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _asyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _requestMayHaveBeenSent = false;
                    _operationCancellation.Value = cancellationToken;
                    using var cancellationRegistration = cancellationToken.Register(
                        static state => ((ToyopucClient)state!).CancelActiveOperation(),
                        this);
                    try
                    {
                        action();
                    }
                    catch (Exception exception) when (cancellationToken.IsCancellationRequested)
                    {
                        if (outcomeUnknownAfterSend && _requestMayHaveBeenSent)
                        {
                            throw new ToyopucOperationOutcomeUnknownException(
                                "The operation was canceled after its request may have been sent; the PLC state is unknown.",
                                exception);
                        }
                        throw new OperationCanceledException(cancellationToken);
                    }
                    finally
                    {
                        _operationCancellation.Value = default;
                    }
                }).ConfigureAwait(false);
        }
        finally
        {
            _asyncGate.Release();
        }
    }

    private async Task<T> RunAsyncCore<T>(
        Func<T> action,
        bool outcomeUnknownAfterSend,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _asyncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _requestMayHaveBeenSent = false;
                    _operationCancellation.Value = cancellationToken;
                    using var cancellationRegistration = cancellationToken.Register(
                        static state => ((ToyopucClient)state!).CancelActiveOperation(),
                        this);
                    try
                    {
                        return action();
                    }
                    catch (Exception exception) when (cancellationToken.IsCancellationRequested)
                    {
                        if (outcomeUnknownAfterSend && _requestMayHaveBeenSent)
                        {
                            throw new ToyopucOperationOutcomeUnknownException(
                                "The operation was canceled after its request may have been sent; the PLC state is unknown.",
                                exception);
                        }
                        throw new OperationCanceledException(cancellationToken);
                    }
                    finally
                    {
                        _operationCancellation.Value = default;
                    }
                }).ConfigureAwait(false);
        }
        finally
        {
            _asyncGate.Release();
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
