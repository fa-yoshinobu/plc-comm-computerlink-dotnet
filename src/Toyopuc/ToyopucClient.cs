using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PlcComm.Toyopuc;

public partial class ToyopucClient : IDisposable, IAsyncDisposable
{
    private const int FrMaxIndex = 0x1FFFFF;
    internal const int UdpReceiveBufferSize = 65535;

    private Socket? _socket;
    private IPEndPoint? _remoteEndPoint;
    private byte[]? _lastTx;
    private byte[]? _lastRx;
    private bool _requestMayHaveBeenSent;
    private volatile bool _explicitReconnectRequired;
    private readonly AsyncLocal<CancellationToken> _operationCancellation = new();
    private readonly Queue<TransportTraceFrame> _traceFrames = new();
    private int _traceFrameCapacity;
    private readonly ConcurrentDictionary<string, IReadOnlyList<(int LinkNo, int StationNo)>> _relayHopCache =
        new(StringComparer.Ordinal);

    public ToyopucClient(
        string host,
        int port,
        ToyopucTransportMode transport,
        int localPort = 0,
        TimeSpan? timeout = null,
        int retries = 0,
        TimeSpan? retryDelay = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be in the range 1-65535.");
        if (!Enum.IsDefined(transport) || transport == ToyopucTransportMode.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(transport), "Transport must be explicitly Tcp or Udp.");
        if (localPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(localPort), "LocalPort must be in the range 0-65535.");
        if (transport == ToyopucTransportMode.Tcp && localPort != 0)
            throw new ArgumentException("LocalPort can only be specified for UDP.", nameof(localPort));
        if (timeout is { } explicitTimeout && explicitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        if (retries < 0)
            throw new ArgumentOutOfRangeException(nameof(retries), "Retries must be zero or greater.");
        if (retryDelay is { } explicitRetryDelay && explicitRetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "RetryDelay must be zero or greater.");

        Host = host;
        Port = port;
        LocalPort = localPort;
        Transport = transport;
        Timeout = timeout ?? TimeSpan.FromSeconds(3);
        Retries = retries;
        RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200);
    }

    public string Host { get; }
    public int Port { get; }
    public int LocalPort { get; }
    public ToyopucTransportMode Transport { get; }
    public TimeSpan Timeout { get; }
    public int Retries { get; }
    public TimeSpan RetryDelay { get; }
    public bool IsOpen => _socket is not null;
    internal Action<ToyopucTraceFrame>? TraceHook { get; private set; }
    internal bool CaptureTraceFrames
    {
        get => _traceFrameCapacity > 0;
        set
        {
            if (value)
                EnableMaintainerTrace(256, TraceHook);
            else
            {
                _traceFrames.Clear();
                _traceFrameCapacity = 0;
            }
        }
    }

    public byte[]? LastTx => _lastTx?.ToArray();
    public byte[]? LastRx => _lastRx?.ToArray();
    internal IReadOnlyList<TransportTraceFrame> TraceFrames =>
        _traceFrames.Select(static frame => new TransportTraceFrame(frame.Tx.ToArray(), frame.Rx?.ToArray())).ToArray();

    internal void EnableMaintainerTrace(int maxFrames, Action<ToyopucTraceFrame>? traceHook = null)
    {
        if (maxFrames < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFrames), "maxFrames must be 1 or greater.");

        _traceFrames.Clear();
        _traceFrameCapacity = maxFrames;
        TraceHook = traceHook;
    }

    public virtual void Open()
    {
        OpenCore(explicitRequest: true);
    }

    private void OpenCore(bool explicitRequest)
    {
        if (explicitRequest)
            _explicitReconnectRequired = false;
        else if (_explicitReconnectRequired)
            throw new InvalidOperationException("The canceled session requires an explicit Open/OpenAsync before another command.");

        if (_socket is not null)
        {
            return;
        }

        var remoteAddress = ResolveRemoteAddress(Host);
        _remoteEndPoint = new IPEndPoint(remoteAddress, Port);

        Socket socket;
        if (Transport == ToyopucTransportMode.Tcp)
        {
            socket = new Socket(remoteAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        else
        {
            socket = new Socket(remoteAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        _socket = socket;
        try
        {
            ConfigureSocket(socket);
            if (Transport == ToyopucTransportMode.Tcp)
                ConnectWithTimeout(socket, _remoteEndPoint, Timeout);
            else
                socket.Bind(CreateAnyEndPoint(remoteAddress.AddressFamily, LocalPort));
        }
        catch
        {
            if (ReferenceEquals(_socket, socket))
                _socket = null;
            socket.Dispose();
            throw;
        }
    }

    public virtual void Close()
    {
        if (_socket is not null)
        {
            try
            {
                _socket.Dispose();
            }
            finally
            {
                _socket = null;
            }
        }

        _lastTx = null;
        _lastRx = null;
        _traceFrames.Clear();
        _traceFrameCapacity = 0;
        TraceHook = null;
    }

    private void CancelActiveOperation()
    {
        _explicitReconnectRequired = true;
        Close();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    internal void ClearTraceFrames()
    {
        _traceFrames.Clear();
    }

    private void FireTrace(ToyopucTraceDirection direction, byte[] data)
    {
        var hook = TraceHook;
        if (hook is null)
            return;

        var frame = new ToyopucTraceFrame(direction, data.ToArray(), DateTime.UtcNow);
        ThreadPool.UnsafeQueueUserWorkItem(
            static state =>
            {
                try
                {
                    state.Hook(state.Frame);
                }
                catch
                {
                    // Maintainer diagnostics must never change a communication result.
                }
            },
            (Hook: hook, Frame: frame),
            preferLocal: false);
    }

    internal ResponseFrame SendRaw(int cmd, byte[] data)
    {
        return SendAndReceive(ToyopucProtocol.BuildCommand(cmd, data));
    }

    public int[] ReadWords(int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildWordRead(address, count), allowRetry: true);
        EnsureCommand(response, 0x1C);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteWords(int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildWordWrite(address, values));
        EnsureCommand(response, 0x1D);
    }

    public byte[] ReadBytes(int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildByteRead(address, count), allowRetry: true);
        EnsureCommand(response, 0x1E);
        return response.Data;
    }

    public void WriteBytes(int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildByteWrite(address, values));
        EnsureCommand(response, 0x1F);
    }

    public bool ReadBit(int address)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildBitRead(address), allowRetry: true);
        EnsureCommand(response, 0x20);
        if (response.Data.Length != 1)
        {
            throw new ToyopucProtocolError("Bit read response must be 1 byte");
        }

        return response.Data[0] != 0;
    }

    public void WriteBit(int address, bool value)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildBitWrite(address, value ? 1 : 0));
        EnsureCommand(response, 0x21);
    }

    public int[] ReadWordsMulti(IEnumerable<int> addresses)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiWordRead(addresses), allowRetry: true);
        EnsureCommand(response, 0x22);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteWordsMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiWordWrite(pairs));
        EnsureCommand(response, 0x23);
    }

    public byte[] ReadBytesMulti(IEnumerable<int> addresses)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiByteRead(addresses), allowRetry: true);
        EnsureCommand(response, 0x24);
        return response.Data;
    }

    public void WriteBytesMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiByteWrite(pairs));
        EnsureCommand(response, 0x25);
    }

    public int[] ReadExtWords(int number, int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtWordRead(number, address, count), allowRetry: true);
        EnsureCommand(response, 0x94);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteExtWords(int number, int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtWordWrite(number, address, values));
        EnsureCommand(response, 0x95);
    }

    public byte[] ReadExtBytes(int number, int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtByteRead(number, address, count), allowRetry: true);
        EnsureCommand(response, 0x96);
        return response.Data;
    }

    public void WriteExtBytes(int number, int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtByteWrite(number, address, values));
        EnsureCommand(response, 0x97);
    }

    public byte[] ReadExtMulti(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtMultiRead(bitPoints, bytePoints, wordPoints), allowRetry: true);
        EnsureCommand(response, 0x98);
        return response.Data;
    }

    public void WriteExtMulti(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtMultiWrite(bitPoints, bytePoints, wordPoints));
        EnsureCommand(response, 0x99);
    }

    public byte[] Pc10BlockRead(int address32, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10BlockRead(address32, count), allowRetry: true);
        EnsureCommand(response, 0xC2);
        return response.Data;
    }

    public void Pc10BlockWrite(int address32, byte[] dataBytes)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10BlockWrite(address32, dataBytes));
        EnsureCommand(response, 0xC3);
    }

    public byte[] Pc10MultiRead(byte[] payload)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10MultiRead(payload), allowRetry: true);
        EnsureCommand(response, 0xC4);
        return response.Data;
    }

    public void Pc10MultiWrite(byte[] payload)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10MultiWrite(payload));
        EnsureCommand(response, 0xC5);
    }

    public int[] ReadFrWords(int index, int count)
    {
        ValidateFrIndex(index);
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        return ToyopucProtocol.UnpackU16LittleEndian(
            Pc10BlockRead(ToyopucAddress.EncodeFrWordAddr32(index), checked(count * 2)));
    }

    public void WriteFrWorkArea(int index, IEnumerable<int> values)
    {
        ValidateFrIndex(index);
        var normalizedValues = NormalizeWordValues(values);
        if (normalizedValues.Length == 0)
            throw new ArgumentException("values must contain at least one word", nameof(values));
        Pc10BlockWrite(
            ToyopucAddress.EncodeFrWordAddr32(index),
            PackWordSlice(normalizedValues, 0, normalizedValues.Length));
    }

    public void CommitFrBlock(int index)
    {
        FrRegister(ToyopucAddress.FrBlockExNo(index));
    }

    private void FrRegister(int exNo)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildFrRegister(exNo));
        EnsureCommand(response, 0xCA);
    }

    internal ResponseFrame RelayCommand(int linkNo, int stationNo, byte[] innerPayload)
    {
        return SendAndReceive(ToyopucProtocol.BuildRelayCommand(linkNo, stationNo, innerPayload));
    }

    internal ResponseFrame RelayNested(IEnumerable<(int LinkNo, int StationNo)> hops, byte[] innerPayload)
    {
        return SendAndReceive(ToyopucProtocol.BuildRelayNested(hops, innerPayload));
    }

    internal ResponseFrame SendViaRelay(object hops, byte[] innerPayload)
    {
        return SendViaRelayCore(hops, innerPayload, allowRetry: false);
    }

    internal ResponseFrame SendViaRelayRead(object hops, byte[] innerPayload)
    {
        return SendViaRelayCore(hops, innerPayload, allowRetry: true);
    }

    private ResponseFrame SendViaRelayCore(object hops, byte[] innerPayload, bool allowRetry)
    {
        IReadOnlyList<(int LinkNo, int StationNo)> normalizedHops;
        if (hops is string text)
        {
            normalizedHops = _relayHopCache.GetOrAdd(text, static key => ToyopucRelay.ParseRelayHops(key));
        }
        else
        {
            normalizedHops = ToyopucRelay.NormalizeRelayHops(hops);
        }

        var outerPayload = ToyopucProtocol.BuildRelayNested(normalizedHops, innerPayload);
        var outer = SendAndReceive(outerPayload, allowRetry);
        var (layers, finalResponse) = ToyopucRelay.UnwrapRelayResponseChain(outer);
        if (finalResponse is null)
        {
            var lastLayer = layers[^1];
            throw new ToyopucProtocolError(
                $"Relay NAK at link=0x{lastLayer.LinkNo:X2}, station=0x{lastLayer.StationNo:X4}, ack=0x{lastLayer.Ack:X2}");
        }

        return finalResponse;
    }

    public int[] RelayReadWords(object hops, int address, int count)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildWordRead(address, count));
        EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void RelayWriteWords(object hops, int address, IEnumerable<int> values)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(address, values));
        EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
    }

    public ClockData RelayReadClock(object hops)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildClockRead());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay clock response");
        try
        {
            return ToyopucProtocol.ParseClockData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay clock response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public void RelayWriteClock(object hops, DateTime value, int yearBase)
    {
        ValidateClockWrite(value, yearBase);
        var weekday = (int)value.DayOfWeek;
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday));
        EnsureCommand(response, 0x32, "Unexpected CMD in relay clock-write response");
        if (!response.Data.SequenceEqual(new byte[] { 0x71, 0x00 }))
        {
            throw new ToyopucProtocolError("Unexpected relay clock-write response body");
        }
    }

    public void RelayResumeScan(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildScanResume());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-resume response");
        EnsureCommand32Data(response, new byte[] { 0x01, 0x00 }, "Unexpected relay scan-resume response body");
    }

    public void RelayStopScan(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildScanStop());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-stop response");
        EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected relay scan-stop response body");
    }

    public void RelayReleaseScanStop(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildScanStopRelease());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-stop-release response");
        EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected relay scan-stop-release response body");
    }

    public CpuStatusData RelayReadCpuStatus(object hops)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildCpuStatusRead());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public byte[] RelayReadCpuStatusA0Raw(object hops)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData RelayReadCpuStatusA0(object hops)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public void RelayWriteFrWorkArea(object hops, int index, IEnumerable<int> values)
    {
        ValidateFrIndex(index);
        var normalizedValues = NormalizeWordValues(values);
        if (normalizedValues.Length == 0)
            throw new ArgumentException("values must contain at least one word", nameof(values));
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildPc10BlockWrite(
                ToyopucAddress.EncodeFrWordAddr32(index),
                PackWordSlice(normalizedValues, 0, normalizedValues.Length)));
        EnsureCommand(response, 0xC3, "Unexpected CMD in relay FR block-write response");
    }

    private void RelayFrRegister(object hops, int exNo)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildFrRegister(exNo));
        EnsureCommand(response, 0xCA, "Unexpected CMD in relay FR-register response");
    }

    public void RelayCommitFrBlock(object hops, int index)
    {
        RelayFrRegister(hops, ToyopucAddress.FrBlockExNo(index));
    }

    public ClockData ReadClock()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildClockRead(), allowRetry: true);
        EnsureCommand(response, 0x32);
        try
        {
            return ToyopucProtocol.ParseClockData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse clock response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData ReadCpuStatus()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusRead(), allowRetry: true);
        EnsureCommand(response, 0x32);
        try
        {
            return ToyopucProtocol.ParseCpuStatusData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public byte[] ReadCpuStatusA0Raw()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusReadA0(), allowRetry: true);
        EnsureCommand(response, 0xA0);
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData ReadCpuStatusA0()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusReadA0(), allowRetry: true);
        EnsureCommand(response, 0xA0);
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
        }
    }

    public void WriteClock(DateTime value, int yearBase)
    {
        ValidateClockWrite(value, yearBase);
        var weekday = (int)value.DayOfWeek;
        var response = SendAndReceive(
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday));
        EnsureCommand(response, 0x32);
        if (!response.Data.SequenceEqual(new byte[] { 0x71, 0x00 }))
        {
            throw new ToyopucProtocolError("Unexpected clock write response body");
        }
    }

    public void ResumeScan()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildScanResume());
        EnsureCommand(response, 0x32);
        EnsureCommand32Data(response, new byte[] { 0x01, 0x00 }, "Unexpected scan-resume response body");
    }

    private static void ValidateClockWrite(DateTime value, int yearBase)
    {
        if (yearBase < 0 || yearBase % 100 != 0)
            throw new ArgumentOutOfRangeException(nameof(yearBase), "yearBase must be a non-negative century divisible by 100.");
        if (value.Year < yearBase || value.Year > yearBase + 99)
            throw new ArgumentOutOfRangeException(nameof(value), "Clock year must be inside the explicitly selected century.");
        if (value.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException("PLC clock values must be timezone-unspecified local clock values.", nameof(value));
    }

    public void StopScan()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildScanStop());
        EnsureCommand(response, 0x32);
        EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected scan-stop response body");
    }

    public void ReleaseScanStop()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildScanStopRelease());
        EnsureCommand(response, 0x32);
        EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected scan-stop-release response body");
    }

    protected static void EnsureCommand(ResponseFrame response, int expectedCommand, string? message = null)
    {
        if (response.Cmd != expectedCommand)
        {
            throw new ToyopucProtocolError(message ?? "Unexpected CMD in response");
        }
    }

    protected static void EnsureCommand32Data(ResponseFrame response, byte[] expectedData, string message)
    {
        if (!response.Data.SequenceEqual(expectedData))
        {
            throw new ToyopucProtocolError(message);
        }
    }

    private static string ToHexStringLower(byte[] bytes)
    {
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(bytes);
#else
        return Convert.ToHexString(bytes).ToLowerInvariant();
#endif
    }

    protected static string FormatResponseError(ResponseFrame response)
    {
        var message = $"Response error rc=0x{response.Rc:X2}";
        if (response.Rc == 0x10)
        {
            var error = response.Data.Length > 0 ? response.Data[^1] : response.Cmd;
            return $"{message}, error_code=0x{error:X2}, data={ToHexStringLower(response.Data)}";
        }

        return $"{message}, data={ToHexStringLower(response.Data)}";
    }

    protected ResponseFrame SendAndReceive(byte[] payload, bool allowRetry = false)
    {
        if (_explicitReconnectRequired)
            throw new InvalidOperationException("The canceled session requires an explicit Open/OpenAsync before another command.");

        var attempt = 0;
        Exception? lastError = null;

        while (attempt <= Retries)
        {
            attempt++;
            _requestMayHaveBeenSent = false;

            try
            {
                if (_socket is null)
                    OpenCore(explicitRequest: false);

                _lastTx = payload;
                _lastRx = null;
                FireTrace(ToyopucTraceDirection.Send, payload);

                byte[] frame;
                if (Transport == ToyopucTransportMode.Tcp)
                {
                    Span<byte> header = stackalloc byte[4];
                    SendAll(payload);
                    ReceiveExact(header);
                    var length = header[2] | (header[3] << 8);
                    frame = GC.AllocateUninitializedArray<byte>(header.Length + length);
                    header.CopyTo(frame);
                    ReceiveExact(frame.AsSpan(header.Length, length));
                }
                else
                {
                    frame = SendAndReceiveUdp(payload);
                }

                _lastRx = frame;
                FireTrace(ToyopucTraceDirection.Receive, frame);
                if (_traceFrameCapacity > 0)
                {
                    while (_traceFrames.Count >= _traceFrameCapacity)
                        _traceFrames.Dequeue();
                    _traceFrames.Enqueue(new TransportTraceFrame(payload.ToArray(), frame.ToArray()));
                }
                var response = ToyopucProtocol.ParseResponse(frame);
                if (response.Ft != ToyopucProtocol.FtResponse)
                {
                    throw new ToyopucProtocolError($"Unexpected frame type: 0x{response.Ft:X2}");
                }

                if (response.Rc != 0x00)
                {
                    throw new ToyopucError(FormatResponseError(response));
                }

                return response;
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
            {
                lastError = new ToyopucTimeoutError("Send/receive timeout", exception);
                if ((allowRetry || !_requestMayHaveBeenSent) && attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                Close();
                throw (ToyopucTimeoutError)lastError;
            }
            catch (TimeoutException exception)
            {
                lastError = new ToyopucTimeoutError("Connect timeout", exception);
                if ((allowRetry || !_requestMayHaveBeenSent) && attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                Close();
                throw (ToyopucTimeoutError)lastError;
            }
            catch (ToyopucError exception)
            {
                lastError = exception;
                if (attempt <= Retries
                    && (!_requestMayHaveBeenSent || (allowRetry && IsRetryableResponseError(exception))))
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                if (exception is ToyopucProtocolError)
                {
                    Close();
                }

                throw;
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                lastError = new ToyopucError("Socket error", exception);
                if ((allowRetry || !_requestMayHaveBeenSent) && attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                Close();
                throw (ToyopucError)lastError;
            }
        }

        if (lastError is not null)
        {
            throw lastError;
        }

        throw new ToyopucError("Send/receive failed");
    }

    private static IPAddress ResolveRemoteAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        var addresses = Dns.GetHostAddresses(host);
        if (addresses.Length == 0)
        {
            throw new ToyopucError($"Failed to resolve host: {host}");
        }

        return addresses.FirstOrDefault(static address => address.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses[0];
    }

    private static EndPoint CreateAnyEndPoint(AddressFamily addressFamily, int port)
    {
        return new IPEndPoint(addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, port);
    }

    private static void ConnectWithTimeout(Socket socket, EndPoint endPoint, TimeSpan timeout)
    {
        var result = socket.BeginConnect(endPoint, null, null);
        if (!result.AsyncWaitHandle.WaitOne(timeout))
        {
            socket.Dispose();
            throw new TimeoutException("Timed out while connecting");
        }

        socket.EndConnect(result);
    }

    private void ConfigureSocket(Socket socket)
    {
        var timeoutMs = checked((int)Math.Ceiling(Timeout.TotalMilliseconds));
        socket.ReceiveTimeout = timeoutMs;
        socket.SendTimeout = timeoutMs;
        if (socket.SocketType == SocketType.Stream && socket.ProtocolType == ProtocolType.Tcp)
        {
            socket.NoDelay = true;
        }
    }

    private void SendAll(byte[] payload)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        var offset = 0;
        while (offset < payload.Length)
        {
            _requestMayHaveBeenSent = true;
            var sent = _socket.Send(payload, offset, payload.Length - offset, SocketFlags.None);
            if (sent <= 0)
            {
                throw new ToyopucProtocolError("Connection closed while sending");
            }

            offset += sent;
        }
    }

    private void ReceiveExact(Span<byte> buffer)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        while (!buffer.IsEmpty)
        {
            var received = _socket.Receive(buffer, SocketFlags.None);
            if (received <= 0)
            {
                throw new ToyopucProtocolError("Connection closed while receiving");
            }

            buffer = buffer[received..];
        }
    }

    private byte[] ReceiveExact(int count)
    {
        var buffer = GC.AllocateUninitializedArray<byte>(count);
        ReceiveExact(buffer);
        return buffer;
    }

    private byte[] SendAndReceiveUdp(byte[] payload)
    {
        if (_socket is null || _remoteEndPoint is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        _requestMayHaveBeenSent = true;
        _socket.SendTo(payload, _remoteEndPoint);
        var buffer = ArrayPool<byte>.Shared.Rent(UdpReceiveBufferSize);
        try
        {
            EndPoint remote = CreateAnyEndPoint(_remoteEndPoint.AddressFamily, 0);
            var received = _socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remote);
            return buffer.AsSpan(0, received).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void RetryDelaySleep()
    {
        if (RetryDelay > TimeSpan.Zero)
        {
            var cancellationToken = _operationCancellation.Value;
            if (cancellationToken.CanBeCanceled && cancellationToken.WaitHandle.WaitOne(RetryDelay))
                cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(cancellationToken.CanBeCanceled ? TimeSpan.Zero : RetryDelay);
        }
    }

    private static bool IsRetryableResponseError(ToyopucError exception)
    {
        return exception.Message.Contains("error_code=0x73", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] PackWordSlice(IReadOnlyList<int> values, int offset, int count)
    {
        var data = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var value = values[offset + i] & 0xFFFF;
            data[i * 2] = (byte)(value & 0xFF);
            data[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return data;
    }

    private static int[] NormalizeWordValues(IEnumerable<int> values)
    {
        if (values is ICollection<int> collection)
        {
            var normalized = new int[collection.Count];
            var index = 0;
            foreach (var value in values)
            {
                if (value < 0 || value > 0xFFFF)
                    throw new ArgumentOutOfRangeException(nameof(values), "FR word values must be in the range 0..65535.");
                normalized[index++] = value;
            }

            return normalized;
        }

        var list = new List<int>();
        foreach (var value in values)
        {
            if (value < 0 || value > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(values), "FR word values must be in the range 0..65535.");
            list.Add(value);
        }

        return list.ToArray();
    }

    private static int ValidateFrIndex(int index)
    {
        if (index < 0 || index > FrMaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "FR index out of range (0x000000-0x1FFFFF)");
        }

        return index;
    }

}
