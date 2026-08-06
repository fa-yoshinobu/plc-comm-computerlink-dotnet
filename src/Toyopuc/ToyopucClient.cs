using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PlcComm.Toyopuc;

internal enum ToyopucSocketDeadlineDirection
{
    Both,
    Send,
    Receive,
}

internal static class ToyopucAddressFamilyValidation
{
    internal static string NormalizeIPv4Host(string host, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", parameterName);

        var normalized = host.Trim();
        if (IPAddress.TryParse(normalized, out var literal) && literal.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Host must be an IPv4 address or a hostname that resolves to IPv4.", parameterName);

        return normalized;
    }
}

internal static class ToyopucTimerValidation
{
    internal const int MaximumMilliseconds = int.MaxValue;

    internal static readonly TimeSpan MaximumDuration = TimeSpan.FromMilliseconds(MaximumMilliseconds);

    internal static TimeSpan RequirePositive(TimeSpan value, string parameterName, string label)
    {
        if (value <= TimeSpan.Zero || value > MaximumDuration)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{label} must be greater than zero and must not exceed {MaximumMilliseconds} milliseconds.");
        }

        return value;
    }

    internal static TimeSpan RequireNonNegative(TimeSpan value, string parameterName, string label)
    {
        if (value < TimeSpan.Zero || value > MaximumDuration)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{label} must be zero or greater and must not exceed {MaximumMilliseconds} milliseconds.");
        }

        return value;
    }
}

/// <summary>Provides direct Computer Link operations over one TCP or UDP session.</summary>
/// <remarks>
/// Public asynchronous live operations enter one arrival-order FIFO queue per client.
/// At most one operation owns the transport, queue waiting does not consume the transaction
/// timeout, and cancellation while waiting performs no transport activity.
/// </remarks>
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
    private bool _fixedUdpSessionTainted;
    private int _disposed;
    private long _requestCount;
    private long _txBytes;
    private long _rxBytes;
    private readonly AsyncLocal<CancellationToken> _operationCancellation = new();
    private readonly Queue<TransportTraceFrame> _traceFrames = new();
    private int _traceFrameCapacity;
    private readonly ConcurrentDictionary<string, IReadOnlyList<(int LinkNo, int StationNo)>> _relayHopCache =
        new(StringComparer.Ordinal);
    internal Func<string, CancellationToken, Task<IPAddress[]>> HostAddressResolver { get; set; } =
        static (host, cancellationToken) => Dns.GetHostAddressesAsync(
            host,
            AddressFamily.InterNetwork,
            cancellationToken);
    internal Action<Socket>? SocketConnectStartedHook { get; set; }
    internal Action<Socket>? ConnectedSocketHook { get; set; }
    internal Action<ToyopucSocketDeadlineDirection, int>? SocketDeadlineAppliedHook { get; set; }

    public ToyopucClient(
        string host,
        int port,
        ToyopucTransportMode transport,
        int localPort = 0,
        TimeSpan? timeout = null,
        int retries = 0,
        TimeSpan? retryDelay = null)
    {
        var normalizedHost = ToyopucAddressFamilyValidation.NormalizeIPv4Host(host, nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be in the range 1-65535.");
        if (!Enum.IsDefined(transport) || transport == ToyopucTransportMode.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(transport), "Transport must be explicitly Tcp or Udp.");
        if (localPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(localPort), "LocalPort must be in the range 0-65535.");
        if (transport == ToyopucTransportMode.Tcp && localPort != 0)
            throw new ArgumentException("LocalPort can only be specified for UDP.", nameof(localPort));
        if (timeout is { } explicitTimeout)
            ToyopucTimerValidation.RequirePositive(explicitTimeout, nameof(timeout), "Timeout");
        if (retries < 0)
            throw new ArgumentOutOfRangeException(nameof(retries), "Retries must be zero or greater.");
        if (retryDelay is { } explicitRetryDelay)
            ToyopucTimerValidation.RequireNonNegative(explicitRetryDelay, nameof(retryDelay), "RetryDelay");

        Host = normalizedHost;
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
    public ToyopucTrafficStats TrafficStats => new(
        unchecked((ulong)Interlocked.Read(ref _requestCount)),
        unchecked((ulong)Interlocked.Read(ref _txBytes)),
        unchecked((ulong)Interlocked.Read(ref _rxBytes)));
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
        ExecuteSynchronousExclusive(
            () =>
            {
                ThrowIfDisposed();
                OpenCore(explicitRequest: true, CreateDeadline(Timeout));
            });
    }

    private void OpenCore(bool explicitRequest, long deadline)
    {
        OperationGeneration generation = RequireCurrentOperationGeneration();
        if (_fixedUdpSessionTainted)
        {
            throw new InvalidOperationException(
                "This fixed-port UDP session cannot be reused after an uncertain request; " +
                "create a new client only after late responses can no longer be present.");
        }
        if (explicitRequest)
            _explicitReconnectRequired = false;
        else if (_explicitReconnectRequired)
            throw new ToyopucNotConnectedException(
                "The canceled session requires an explicit Open/OpenAsync before another command.");

        if (_socket is not null)
        {
            return;
        }

        var remoteAddress = ResolveRemoteAddress(Host, deadline);
        ThrowIfOpenGenerationInvalid(generation);
        ThrowIfDeadlineExpired(deadline, "Connect timeout");
        var remoteEndPoint = new IPEndPoint(remoteAddress, Port);

        var socket = Transport == ToyopucTransportMode.Tcp
            ? new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            : new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            ConfigureSocket(socket);
            if (Transport == ToyopucTransportMode.Tcp)
                ConnectWithTimeout(socket, remoteEndPoint, deadline, generation);
            else
            {
                SetSocketDeadlines(socket, deadline, "Connect timeout");
                socket.Bind(new IPEndPoint(IPAddress.Any, LocalPort));
                socket.Connect(remoteEndPoint);
                ThrowIfOpenGenerationInvalid(generation);
            }
            ConnectedSocketHook?.Invoke(socket);
            PublishConnectedSocket(socket, remoteEndPoint, generation);
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_socket, socket))
                _socket = null;
            socket.Dispose();
            if (exception is ToyopucError)
                throw;
            throw new ToyopucTransportError("Socket connection failed", exception);
        }
    }

    private OperationGeneration RequireCurrentOperationGeneration()
    {
        OperationContext? context = _operationContext.Value;
        if (context is null || !ReferenceEquals(context.Client, this))
            throw new InvalidOperationException("Open must run inside the client operation lifecycle.");
        return context.Generation;
    }

    private void ThrowIfOpenGenerationInvalid(OperationGeneration generation)
    {
        _operationCancellation.Value.ThrowIfCancellationRequested();
        lock (_operationSync)
        {
            if (generation.IsRetired || !ReferenceEquals(_operationGeneration, generation))
                throw generation.CreateFailure(this);
        }
    }

    private void PublishConnectedSocket(
        Socket socket,
        IPEndPoint remoteEndPoint,
        OperationGeneration generation)
    {
        _operationCancellation.Value.ThrowIfCancellationRequested();
        lock (_operationSync)
        {
            if (generation.IsRetired || !ReferenceEquals(_operationGeneration, generation))
                throw generation.CreateFailure(this);
            _remoteEndPoint = remoteEndPoint;
            _socket = socket;
        }
    }

    /// <summary>Closes the connection and rejects active and queued operations from its transport generation.</summary>
    public virtual void Close()
    {
        RetireOperationGeneration(disposed: false);
        CloseCore(clearTraceConfiguration: true);
    }

    private void CloseTransport()
    {
        CloseCore(clearTraceConfiguration: false);
    }

    private void CloseCore(bool clearTraceConfiguration)
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
        if (clearTraceConfiguration)
        {
            _traceFrames.Clear();
            _traceFrameCapacity = 0;
            TraceHook = null;
        }
    }

    private void CancelActiveOperation()
    {
        _explicitReconnectRequired = true;
        CloseTransport();
    }

    /// <summary>Permanently disposes the client and rejects active and queued operations.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        RetireOperationGeneration(disposed: true);
        CloseCore(clearTraceConfiguration: true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

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
        return SendAndReceiveStateChanging(ToyopucProtocol.BuildCommand(cmd, data));
    }

    public int[] ReadWords(int address, int count)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildWordRead(address, count),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x1C);
                EnsureResponseDataLength(response, checked(count * 2), "word-read");
                return ToyopucProtocol.UnpackU16LittleEndian(response.Data.Span);
            });
    }

    public void WriteWords(int address, IEnumerable<int> values)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildWordWrite(address, values),
            response => EnsureEmptyResponse(response, 0x1D, "word-write"));
    }

    public byte[] ReadBytes(int address, int count)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildByteRead(address, count),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x1E);
                EnsureResponseDataLength(response, count, "byte-read");
                return response.Data.ToArray();
            });
    }

    public void WriteBytes(int address, IEnumerable<int> values)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildByteWrite(address, values),
            response => EnsureEmptyResponse(response, 0x1F, "byte-write"));
    }

    public bool ReadBit(int address)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildBitRead(address),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x20);
                EnsureResponseDataLength(response, 1, "bit-read");
                return response.Data.Span[0] != 0;
            });
    }

    public void WriteBit(int address, bool value)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildBitWrite(address, value ? 1 : 0),
            response => EnsureEmptyResponse(response, 0x21, "bit-write"));
    }

    public int[] ReadWordsMulti(IEnumerable<int> addresses)
    {
        var items = addresses as int[] ?? addresses.ToArray();
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildMultiWordRead(items),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x22);
                EnsureResponseDataLength(response, checked(items.Length * 2), "multi-word-read");
                return ToyopucProtocol.UnpackU16LittleEndian(response.Data.Span);
            });
    }

    public void WriteWordsMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildMultiWordWrite(pairs),
            response => EnsureEmptyResponse(response, 0x23, "multi-word-write"));
    }

    public byte[] ReadBytesMulti(IEnumerable<int> addresses)
    {
        var items = addresses as int[] ?? addresses.ToArray();
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildMultiByteRead(items),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x24);
                EnsureResponseDataLength(response, items.Length, "multi-byte-read");
                return response.Data.ToArray();
            });
    }

    public void WriteBytesMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildMultiByteWrite(pairs),
            response => EnsureEmptyResponse(response, 0x25, "multi-byte-write"));
    }

    public int[] ReadExtWords(int number, int address, int count)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildExtWordRead(number, address, count),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x94);
                EnsureResponseDataLength(response, checked(count * 2), "extended word-read");
                return ToyopucProtocol.UnpackU16LittleEndian(response.Data.Span);
            });
    }

    public void WriteExtWords(int number, int address, IEnumerable<int> values)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildExtWordWrite(number, address, values),
            response => EnsureEmptyResponse(response, 0x95, "extended word-write"));
    }

    public byte[] ReadExtBytes(int number, int address, int count)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildExtByteRead(number, address, count),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x96);
                EnsureResponseDataLength(response, count, "extended byte-read");
                return response.Data.ToArray();
            });
    }

    public void WriteExtBytes(int number, int address, IEnumerable<int> values)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildExtByteWrite(number, address, values),
            response => EnsureEmptyResponse(response, 0x97, "extended byte-write"));
    }

    public byte[] ReadExtMulti(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints)
    {
        var bits = bitPoints as (int No, int Bit, int Address)[] ?? bitPoints.ToArray();
        var bytes = bytePoints as (int No, int Address)[] ?? bytePoints.ToArray();
        var words = wordPoints as (int No, int Address)[] ?? wordPoints.ToArray();
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildExtMultiRead(bits, bytes, words),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x98);
                EnsureResponseDataLength(
                    response,
                    checked(((bits.Length + 7) / 8) + bytes.Length + (words.Length * 2)),
                    "extended multi-read");
                return response.Data.ToArray();
            });
    }

    public void WriteExtMulti(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildExtMultiWrite(bitPoints, bytePoints, wordPoints),
            response => EnsureEmptyResponse(response, 0x99, "extended multi-write"));
    }

    public byte[] Pc10BlockRead(int address32, int count)
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildPc10BlockRead(address32, count),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0xC2);
                EnsureResponseDataLength(response, count, "PC10 block-read");
                return response.Data.ToArray();
            });
    }

    public void Pc10BlockWrite(int address32, byte[] dataBytes)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildPc10BlockWrite(address32, dataBytes),
            response => EnsureEmptyResponse(response, 0xC3, "PC10 block-write"));
    }

    public byte[] Pc10MultiRead(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length < 4)
            throw new ArgumentException("PC10 multi-read payload must contain the four count header bytes", nameof(payload));
        var expectedLength = checked(4 + ((payload[0] + 7) / 8) + payload[1] + (payload[2] * 2));
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildPc10MultiRead(payload),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0xC4);
                EnsureResponseDataLength(response, expectedLength, "PC10 multi-read");
                return response.Data.ToArray();
            });
    }

    public void Pc10MultiWrite(byte[] payload)
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildPc10MultiWrite(payload),
            response => EnsureEmptyResponse(response, 0xC5, "PC10 multi-write"));
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
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildFrRegister(exNo),
            response => EnsureEmptyResponse(response, 0xCA, "FR register"));
    }

    internal ResponseFrame RelayCommand(int linkNo, int stationNo, byte[] innerPayload)
    {
        var request = ToyopucProtocol.ParseRelayInnerRequest(innerPayload);
        return RelayCommand(linkNo, stationNo, request);
    }

    private ResponseFrame RelayCommand(
        int linkNo,
        int stationNo,
        ToyopucProtocol.RelayInnerRequest request)
    {
        return SendAndReceiveCore(
            ToyopucProtocol.BuildRelayCommand(linkNo, stationNo, request),
            allowRetry: request.IsReadOnly,
            outcomeUnknownAfterSend: !request.IsReadOnly,
            static response => response.ToOwned());
    }

    internal ResponseFrame RelayNested(IEnumerable<(int LinkNo, int StationNo)> hops, byte[] innerPayload)
    {
        var request = ToyopucProtocol.ParseRelayInnerRequest(innerPayload);
        return RelayNested(ToyopucRelay.NormalizeRelayHops(hops), request);
    }

    private ResponseFrame RelayNested(
        IReadOnlyList<(int LinkNo, int StationNo)> hops,
        ToyopucProtocol.RelayInnerRequest request)
    {
        return SendAndReceiveCore(
            ToyopucProtocol.BuildRelayNested(hops, request),
            allowRetry: request.IsReadOnly,
            outcomeUnknownAfterSend: !request.IsReadOnly,
            static response => response.ToOwned());
    }

    internal ResponseFrame SendViaRelay(object hops, byte[] innerPayload)
    {
        return SendViaRelayCore(hops, innerPayload, static response => response.ToOwned());
    }

    internal ResponseFrame SendViaRelayRead(object hops, byte[] innerPayload)
    {
        return SendViaRelayCore(hops, innerPayload, static response => response.ToOwned());
    }

    private protected sealed record PreparedRelayRead(
        byte[] OuterPayload,
        ToyopucProtocol.RelayInnerRequest Request,
        IReadOnlyList<(int LinkNo, int StationNo)> Hops);

    private protected static PreparedRelayRead PrepareRelayRead(
        IReadOnlyList<(int LinkNo, int StationNo)> normalizedHops,
        byte[] innerPayload)
    {
        var request = ToyopucProtocol.ParseRelayInnerRequest(innerPayload);
        if (!request.IsReadOnly)
            throw new ArgumentException("Prepared relay read requires a read-only request.", nameof(innerPayload));
        return new PreparedRelayRead(
            ToyopucProtocol.BuildRelayNested(normalizedHops, request),
            request,
            normalizedHops);
    }

    private protected T SendPreparedRelayReadDecoded<T>(
        PreparedRelayRead prepared,
        Func<ResponseFrameView, T> decode)
    {
        return SendAndReceiveCore(
            prepared.OuterPayload,
            allowRetry: true,
            outcomeUnknownAfterSend: false,
            outer => DecodePreparedRelayReadResponse(prepared, outer, decode));
    }

    private protected static T DecodePreparedRelayReadResponse<T>(
        PreparedRelayRead prepared,
        ResponseFrameView outer,
        Func<ResponseFrameView, T> decode)
    {
        var (layers, finalResponse) = ToyopucRelay.UnwrapRelayResponseChainView(outer);
        if (finalResponse is null)
        {
            var lastLayer = layers[^1];
            throw new ToyopucProtocolError(
                $"Relay NAK at link=0x{lastLayer.LinkNo:X2}, station=0x{lastLayer.StationNo:X4}, ack=0x{lastLayer.Ack:X2}");
        }
        ValidateRelayRoute(layers, prepared.Hops);
        var final = finalResponse.Value;
        if (final.Cmd != prepared.Request.Command)
        {
            throw new ToyopucProtocolError(
                $"Unexpected relay response command: expected 0x{prepared.Request.Command:X2}, got 0x{final.Cmd:X2}");
        }
        if (prepared.Request.ExpectedReadResponseLength is int expected
            && final.Data.Length != expected)
        {
            throw new ToyopucProtocolError(
                $"relay read response data size mismatch: expected={expected}, actual={final.Data.Length}");
        }
        return decode(final);
    }

    private protected T SendPreparedReadDecoded<T>(byte[] payload, Func<ResponseFrameView, T> decode)
        => SendAndReceiveDecoded(payload, allowRetry: true, decode);

    private protected T SendViaRelayReadDecoded<T>(object hops, byte[] innerPayload, Func<ResponseFrameView, T> decode)
    {
        return SendViaRelayCore(hops, innerPayload, decode);
    }

    private protected void SendViaRelayStateChanging(object hops, byte[] innerPayload, Action<ResponseFrameView> validate)
    {
        SendViaRelayCore(
            hops,
            innerPayload,
            response =>
            {
                validate(response);
                return true;
            });
    }

    private T SendViaRelayCore<T>(object hops, byte[] innerPayload, Func<ResponseFrameView, T> decode)
    {
        var request = ToyopucProtocol.ParseRelayInnerRequest(innerPayload);
        return SendViaRelayCore(hops, request, decode);
    }

    private T SendViaRelayCore<T>(
        object hops,
        ToyopucProtocol.RelayInnerRequest request,
        Func<ResponseFrameView, T> decode)
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

        var outerPayload = ToyopucProtocol.BuildRelayNested(normalizedHops, request);
        T DecodeOuter(ResponseFrameView outer)
        {
            var (layers, finalResponse) = ToyopucRelay.UnwrapRelayResponseChainView(outer);
            if (finalResponse is null)
            {
                var lastLayer = layers[^1];
                throw new ToyopucProtocolError(
                    $"Relay NAK at link=0x{lastLayer.LinkNo:X2}, station=0x{lastLayer.StationNo:X4}, ack=0x{lastLayer.Ack:X2}");
            }

            ValidateRelayRoute(layers, normalizedHops);
            var final = finalResponse.Value;
            if (final.Cmd != request.Command)
            {
                throw new ToyopucProtocolError(
                    $"Unexpected relay response command: expected 0x{request.Command:X2}, got 0x{final.Cmd:X2}");
            }

            if (request.ExpectedReadResponseLength is int expectedReadResponseLength)
            {
                EnsureResponseDataLength(final, expectedReadResponseLength, "relay read");
            }
            else if (request.ExpectedStateResponseData is byte[] expectedStateResponseData
                && !final.Data.Span.SequenceEqual(expectedStateResponseData))
            {
                throw new ToyopucProtocolError(
                    $"Unexpected relay state-changing response body for CMD={request.Command:X2}");
            }

            return decode(final);
        }

        return SendAndReceiveCore(
            outerPayload,
            allowRetry: request.IsReadOnly,
            outcomeUnknownAfterSend: !request.IsReadOnly,
            DecodeOuter);
    }

    private static void ValidateRelayRoute(
        IReadOnlyList<ToyopucRelay.RelayLayerView> layers,
        IReadOnlyList<(int LinkNo, int StationNo)> expectedHops)
    {
        if (layers.Count != expectedHops.Count)
        {
            throw new ToyopucProtocolError(
                $"Unexpected relay response depth: expected {expectedHops.Count}, got {layers.Count}");
        }
        for (var index = 0; index < layers.Count; index++)
        {
            var actual = layers[index];
            var expected = expectedHops[index];
            if (actual.LinkNo != expected.LinkNo || actual.StationNo != expected.StationNo)
            {
                throw new ToyopucProtocolError(
                    $"Unexpected relay response route at layer {index}: " +
                    $"expected link=0x{expected.LinkNo:X2}, station=0x{expected.StationNo:X4}; " +
                    $"got link=0x{actual.LinkNo:X2}, station=0x{actual.StationNo:X4}");
            }
        }
    }

    public int[] RelayReadWords(object hops, int address, int count)
    {
        return SendViaRelayReadDecoded(
            hops,
            ToyopucProtocol.BuildWordRead(address, count),
            response =>
            {
                EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
                EnsureResponseDataLength(response, checked(count * 2), "relay word-read");
                return ToyopucProtocol.UnpackU16LittleEndian(response.Data.Span);
            });
    }

    public void RelayWriteWords(object hops, int address, IEnumerable<int> values)
    {
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildWordWrite(address, values),
            response => EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response"));
    }

    public ClockData RelayReadClock(object hops)
    {
        return SendViaRelayReadDecoded(
            hops,
            ToyopucProtocol.BuildClockRead(),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay clock response");
                try
                {
                    return ToyopucProtocol.ParseClockData(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError($"Failed to parse relay clock response data={ToHexStringLower(response.Data)}", exception);
                }
            });
    }

    public void RelayWriteClock(object hops, DateTime value, int yearBase)
    {
        ValidateClockWrite(value, yearBase);
        var weekday = (int)value.DayOfWeek;
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay clock-write response");
                EnsureCommand32Data(response, new byte[] { 0x71, 0x00 }, "Unexpected relay clock-write response body");
            });
    }

    public void RelayResumeScan(object hops)
    {
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildScanResume(),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-resume response");
                EnsureCommand32Data(response, new byte[] { 0x01, 0x00 }, "Unexpected relay scan-resume response body");
            });
    }

    public void RelayStopScan(object hops)
    {
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildScanStop(),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-stop response");
                EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected relay scan-stop response body");
            });
    }

    public void RelayReleaseScanStop(object hops)
    {
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildScanStopRelease(),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay scan-stop-release response");
                EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected relay scan-stop-release response body");
            });
    }

    public CpuStatusData RelayReadCpuStatus(object hops)
    {
        return SendViaRelayReadDecoded(
            hops,
            ToyopucProtocol.BuildCpuStatusRead(),
            response =>
            {
                EnsureCommand(response, 0x32, "Unexpected CMD in relay CPU status response");
                try
                {
                    return ToyopucProtocol.ParseCpuStatusData(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError($"Failed to parse relay CPU status response data={ToHexStringLower(response.Data)}", exception);
                }
            });
    }

    public byte[] RelayReadCpuStatusA0Raw(object hops)
    {
        return SendViaRelayReadDecoded(
            hops,
            ToyopucProtocol.BuildCpuStatusReadA0(),
            response =>
            {
                EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
                try
                {
                    return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
                }
            });
    }

    public CpuStatusData RelayReadCpuStatusA0(object hops)
    {
        return SendViaRelayReadDecoded(
            hops,
            ToyopucProtocol.BuildCpuStatusReadA0(),
            response =>
            {
                EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
                try
                {
                    return ToyopucProtocol.ParseCpuStatusDataA0(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={ToHexStringLower(response.Data)}", exception);
                }
            });
    }

    public void RelayWriteFrWorkArea(object hops, int index, IEnumerable<int> values)
    {
        ValidateFrIndex(index);
        var normalizedValues = NormalizeWordValues(values);
        if (normalizedValues.Length == 0)
            throw new ArgumentException("values must contain at least one word", nameof(values));
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildPc10BlockWrite(
                ToyopucAddress.EncodeFrWordAddr32(index),
                PackWordSlice(normalizedValues, 0, normalizedValues.Length)),
            response => EnsureCommand(response, 0xC3, "Unexpected CMD in relay FR block-write response"));
    }

    private void RelayFrRegister(object hops, int exNo)
    {
        SendViaRelayStateChanging(
            hops,
            ToyopucProtocol.BuildFrRegister(exNo),
            response => EnsureCommand(response, 0xCA, "Unexpected CMD in relay FR-register response"));
    }

    public void RelayCommitFrBlock(object hops, int index)
    {
        RelayFrRegister(hops, ToyopucAddress.FrBlockExNo(index));
    }

    public ClockData ReadClock()
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildClockRead(),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x32);
                try
                {
                    return ToyopucProtocol.ParseClockData(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError(
                        $"Failed to parse clock response data={ToHexStringLower(response.Data)}",
                        exception);
                }
            });
    }

    public CpuStatusData ReadCpuStatus()
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildCpuStatusRead(),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0x32);
                try
                {
                    return ToyopucProtocol.ParseCpuStatusData(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError(
                        $"Failed to parse CPU status response data={ToHexStringLower(response.Data)}",
                        exception);
                }
            });
    }

    public byte[] ReadCpuStatusA0Raw()
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildCpuStatusReadA0(),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0xA0);
                try
                {
                    return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError(
                        $"Failed to parse A0 CPU status response data={ToHexStringLower(response.Data)}",
                        exception);
                }
            });
    }

    public CpuStatusData ReadCpuStatusA0()
    {
        return SendAndReceiveDecoded(
            ToyopucProtocol.BuildCpuStatusReadA0(),
            allowRetry: true,
            response =>
            {
                EnsureCommand(response, 0xA0);
                try
                {
                    return ToyopucProtocol.ParseCpuStatusDataA0(response.Data.Span);
                }
                catch (Exception exception)
                {
                    throw new ToyopucProtocolError(
                        $"Failed to parse A0 CPU status response data={ToHexStringLower(response.Data)}",
                        exception);
                }
            });
    }

    public void WriteClock(DateTime value, int yearBase)
    {
        ValidateClockWrite(value, yearBase);
        var weekday = (int)value.DayOfWeek;
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday),
            response =>
            {
                EnsureCommand(response, 0x32);
                EnsureCommand32Data(response, new byte[] { 0x71, 0x00 }, "Unexpected clock write response body");
            });
    }

    public void ResumeScan()
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildScanResume(),
            response =>
            {
                EnsureCommand(response, 0x32);
                EnsureCommand32Data(response, new byte[] { 0x01, 0x00 }, "Unexpected scan-resume response body");
            });
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
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildScanStop(),
            response =>
            {
                EnsureCommand(response, 0x32);
                EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected scan-stop response body");
            });
    }

    public void ReleaseScanStop()
    {
        SendAndReceiveStateChanging(
            ToyopucProtocol.BuildScanStopRelease(),
            response =>
            {
                EnsureCommand(response, 0x32);
                EnsureCommand32Data(response, new byte[] { 0x02, 0x00 }, "Unexpected scan-stop-release response body");
            });
    }

    protected static void EnsureCommand(ResponseFrame response, int expectedCommand, string? message = null)
    {
        if (response.Cmd != expectedCommand)
        {
            throw new ToyopucProtocolError(message ?? "Unexpected CMD in response");
        }
    }

    private static void EnsureCommand(ResponseFrameView response, int expectedCommand, string? message = null)
    {
        if (response.Cmd != expectedCommand)
            throw new ToyopucProtocolError(message ?? "Unexpected CMD in response");
    }

    protected static void EnsureCommand32Data(ResponseFrame response, byte[] expectedData, string message)
    {
        if (!response.Data.SequenceEqual(expectedData))
        {
            throw new ToyopucProtocolError(message);
        }
    }

    private static void EnsureCommand32Data(ResponseFrameView response, byte[] expectedData, string message)
    {
        if (!response.Data.Span.SequenceEqual(expectedData))
            throw new ToyopucProtocolError(message);
    }

    private static void EnsureResponseDataLength(ResponseFrame response, int expectedLength, string operation)
    {
        if (response.Data.Length != expectedLength)
        {
            throw new ToyopucProtocolError(
                $"Unexpected {operation} response data length: expected {expectedLength}, got {response.Data.Length}");
        }
    }

    private static void EnsureResponseDataLength(ResponseFrameView response, int expectedLength, string operation)
    {
        if (response.Data.Length != expectedLength)
        {
            throw new ToyopucProtocolError(
                $"Unexpected {operation} response data length: expected {expectedLength}, got {response.Data.Length}");
        }
    }

    private static void EnsureEmptyResponse(ResponseFrame response, int expectedCommand, string operation)
    {
        EnsureCommand(response, expectedCommand);
        EnsureResponseDataLength(response, 0, operation);
    }

    private static void EnsureEmptyResponse(ResponseFrameView response, int expectedCommand, string operation)
    {
        EnsureCommand(response, expectedCommand);
        EnsureResponseDataLength(response, 0, operation);
    }

    private static string ToHexStringLower(byte[] bytes)
    {
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(bytes);
#else
        return Convert.ToHexString(bytes).ToLowerInvariant();
#endif
    }

    private static string ToHexStringLower(ReadOnlyMemory<byte> bytes)
        => ToHexStringLower(bytes.ToArray());

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

    private static string FormatResponseError(ResponseFrameView response)
    {
        var message = $"Response error rc=0x{response.Rc:X2}";
        var data = response.Data.Span;
        if (response.Rc == 0x10)
        {
            var error = data.Length > 0 ? data[^1] : response.Cmd;
            return $"{message}, error_code=0x{error:X2}, data={ToHexStringLower(response.Data)}";
        }

        return $"{message}, data={ToHexStringLower(response.Data)}";
    }

    protected ResponseFrame SendAndReceive(byte[] payload, bool allowRetry = false)
    {
        return SendAndReceiveCore(
            payload,
            allowRetry,
            outcomeUnknownAfterSend: false,
            static response => response.ToOwned());
    }

    private ResponseFrame SendAndReceiveStateChanging(byte[] payload)
    {
        return SendAndReceiveCore(
            payload,
            allowRetry: false,
            outcomeUnknownAfterSend: true,
            static response => response.ToOwned());
    }

    private T SendAndReceiveDecoded<T>(
        byte[] payload,
        bool allowRetry,
        Func<ResponseFrameView, T> decode)
    {
        return SendAndReceiveCore(payload, allowRetry, outcomeUnknownAfterSend: false, decode);
    }

    private void SendAndReceiveStateChanging(byte[] payload, Action<ResponseFrameView> validate)
    {
        SendAndReceiveCore(
            payload,
            allowRetry: false,
            outcomeUnknownAfterSend: true,
            response =>
            {
                validate(response);
                return true;
            });
    }

    private T SendAndReceiveCore<T>(
        byte[] payload,
        bool allowRetry,
        bool outcomeUnknownAfterSend,
        Func<ResponseFrameView, T> decode)
    {
        return ExecuteSynchronousExclusive(
            () => SendAndReceiveCoreOwned(payload, allowRetry, outcomeUnknownAfterSend, decode));
    }

    private T SendAndReceiveCoreOwned<T>(
        byte[] payload,
        bool allowRetry,
        bool outcomeUnknownAfterSend,
        Func<ResponseFrameView, T> decode)
    {
        if (_asyncTransportScript.Value is { } asyncScript)
        {
            var frame = asyncScript.Exchange(payload, outcomeUnknownAfterSend);
            _requestMayHaveBeenSent = true;
            return DecodePreparedAsyncResponse(payload, frame, decode);
        }
        _ = allowRetry; // Retries are now limited to failures proven to occur before any send attempt.
        if (_explicitReconnectRequired)
            throw new ToyopucNotConnectedException(
                "The canceled session requires an explicit Open/OpenAsync before another command.");

        var deadline = _operationContext.Value?.Deadline ?? CreateDeadline(Timeout);
        var attempt = 0;
        Exception? lastError = null;

        while (attempt <= Retries)
        {
            attempt++;
            _requestMayHaveBeenSent = false;

            try
            {
                if (_socket is null)
                    OpenCore(explicitRequest: false, deadline);

                _lastTx = payload;
                _lastRx = null;
                FireTrace(ToyopucTraceDirection.Send, payload);

                byte[] frame;
                if (Transport == ToyopucTransportMode.Tcp)
                {
                    Span<byte> header = stackalloc byte[4];
                    SendAll(payload, deadline);
                    RecordSend(payload.Length);
                    ReceiveExact(header, deadline);
                    var length = header[2] | (header[3] << 8);
                    frame = GC.AllocateUninitializedArray<byte>(header.Length + length);
                    header.CopyTo(frame);
                    ReceiveExact(frame.AsSpan(header.Length, length), deadline);
                }
                else
                {
                    frame = SendAndReceiveUdp(payload, deadline);
                }

                ThrowIfDeadlineExpired(deadline, "Send/receive timeout");
                _lastRx = frame;
                Interlocked.Add(ref _rxBytes, frame.Length);
                FireTrace(ToyopucTraceDirection.Receive, frame);
                if (_traceFrameCapacity > 0)
                {
                    while (_traceFrames.Count >= _traceFrameCapacity)
                        _traceFrames.Dequeue();
                    _traceFrames.Enqueue(new TransportTraceFrame(payload.ToArray(), frame.ToArray()));
                }
                var response = ToyopucProtocol.ParseResponseView(frame);
                ThrowIfDeadlineExpired(deadline, "Response decode timeout");
                if (response.Ft != ToyopucProtocol.FtResponse)
                {
                    throw new ToyopucProtocolError($"Unexpected frame type: 0x{response.Ft:X2}");
                }

                if (response.Rc != 0x00)
                {
                    if (response.Data.Length > 0 && payload.Length >= 5 && response.Cmd != payload[4])
                    {
                        _explicitReconnectRequired = true;
                        throw new ToyopucProtocolError(
                            $"Unexpected response command in data-bearing NG response: expected 0x{payload[4]:X2}, got 0x{response.Cmd:X2}");
                    }
                    throw new ToyopucPlcError(FormatResponseError(response));
                }

                if (payload.Length >= 5 && response.Cmd != payload[4])
                {
                    throw new ToyopucProtocolError(
                        $"Unexpected response command: expected 0x{payload[4]:X2}, got 0x{response.Cmd:X2}");
                }

                T result;
                try
                {
                    result = decode(response);
                }
                catch (ToyopucError)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is ArgumentException or InvalidOperationException or
                    IndexOutOfRangeException or OverflowException)
                {
                    throw new ToyopucProtocolError("Command-specific response decode failed", exception);
                }

                ThrowIfDeadlineExpired(deadline, "Command-specific response decode timeout");
                return result;
            }
            catch (ToyopucTimeoutError exception)
            {
                MarkFixedUdpSessionTaintedIfNeeded();
                lastError = exception;
                var requestMayHaveBeenSent = _requestMayHaveBeenSent;
                CloseTransport();
                if (!requestMayHaveBeenSent && attempt <= Retries)
                {
                    RetryDelaySleep(deadline);
                    continue;
                }

                if (outcomeUnknownAfterSend && requestMayHaveBeenSent)
                    throw CreateOutcomeUnknownException(exception);
                throw;
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
            {
                MarkFixedUdpSessionTaintedIfNeeded();
                lastError = new ToyopucTimeoutError("Send/receive timeout", exception);
                var requestMayHaveBeenSent = _requestMayHaveBeenSent;
                CloseTransport();
                if (!_fixedUdpSessionTainted && !requestMayHaveBeenSent && attempt <= Retries)
                {
                    RetryDelaySleep(deadline);
                    continue;
                }

                if (outcomeUnknownAfterSend && requestMayHaveBeenSent)
                {
                    throw CreateOutcomeUnknownException(lastError);
                }
                throw (ToyopucTimeoutError)lastError;
            }
            catch (TimeoutException exception)
            {
                MarkFixedUdpSessionTaintedIfNeeded();
                lastError = new ToyopucTimeoutError("Connect timeout", exception);
                var requestMayHaveBeenSent = _requestMayHaveBeenSent;
                CloseTransport();
                if (!_fixedUdpSessionTainted && !requestMayHaveBeenSent && attempt <= Retries)
                {
                    RetryDelaySleep(deadline);
                    continue;
                }

                if (outcomeUnknownAfterSend && requestMayHaveBeenSent)
                {
                    throw CreateOutcomeUnknownException(lastError);
                }
                throw (ToyopucTimeoutError)lastError;
            }
            catch (ToyopucError exception)
            {
                if (exception is ToyopucProtocolError)
                {
                    MarkFixedUdpSessionTaintedIfNeeded();
                }

                lastError = exception;
                if (attempt <= Retries && !_requestMayHaveBeenSent && exception is not ToyopucPlcError)
                {
                    CloseTransport();
                    RetryDelaySleep(deadline);
                    continue;
                }

                if (exception is ToyopucProtocolError)
                {
                    var requestMayHaveBeenSent = _requestMayHaveBeenSent;
                    CloseTransport();
                    if (outcomeUnknownAfterSend && requestMayHaveBeenSent)
                    {
                        throw CreateOutcomeUnknownException(exception);
                    }
                }

                throw;
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                MarkFixedUdpSessionTaintedIfNeeded();
                lastError = new ToyopucTransportError("Socket error", exception);
                var requestMayHaveBeenSent = _requestMayHaveBeenSent;
                CloseTransport();
                if (!_fixedUdpSessionTainted && !requestMayHaveBeenSent && attempt <= Retries)
                {
                    RetryDelaySleep(deadline);
                    continue;
                }

                if (outcomeUnknownAfterSend && requestMayHaveBeenSent)
                {
                    throw CreateOutcomeUnknownException(lastError);
                }
                throw (ToyopucError)lastError;
            }
        }

        if (lastError is not null)
        {
            throw lastError;
        }

        throw new ToyopucError("Send/receive failed");
    }

    private T DecodePreparedAsyncResponse<T>(
        byte[] payload,
        byte[] frame,
        Func<ResponseFrameView, T> decode)
    {
        var response = ToyopucProtocol.ParseResponseView(frame);
        if (response.Ft != ToyopucProtocol.FtResponse)
            throw new ToyopucProtocolError($"Unexpected frame type: 0x{response.Ft:X2}");
        if (response.Rc != 0x00)
        {
            if (response.Data.Length > 0 && payload.Length >= 5 && response.Cmd != payload[4])
            {
                _explicitReconnectRequired = true;
                throw new ToyopucProtocolError(
                    $"Unexpected response command in data-bearing NG response: expected 0x{payload[4]:X2}, got 0x{response.Cmd:X2}");
            }
            throw new ToyopucPlcError(FormatResponseError(response));
        }
        if (payload.Length >= 5 && response.Cmd != payload[4])
        {
            throw new ToyopucProtocolError(
                $"Unexpected response command: expected 0x{payload[4]:X2}, got 0x{response.Cmd:X2}");
        }
        try
        {
            return decode(response);
        }
        catch (ToyopucError)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            IndexOutOfRangeException or OverflowException)
        {
            throw new ToyopucProtocolError("Command-specific response decode failed", exception);
        }
    }

    private ToyopucOperationOutcomeUnknownException CreateOutcomeUnknownException(Exception innerException)
    {
        var reason = _operationContext.Value?.Generation.IsRetired == true
            ? ToyopucOutcomeUnknownReason.Closed
            : _operationCancellation.Value.IsCancellationRequested
                ? ToyopucOutcomeUnknownReason.Cancellation
                : innerException switch
                {
                    ToyopucTimeoutError => ToyopucOutcomeUnknownReason.Timeout,
                    OperationCanceledException => ToyopucOutcomeUnknownReason.Cancellation,
                    ToyopucProtocolError => ToyopucOutcomeUnknownReason.MalformedResponse,
                    _ => ToyopucOutcomeUnknownReason.Transport,
                };
        return new ToyopucOperationOutcomeUnknownException(
            reason,
            "A state-changing request may have been sent, but no confirmed response was received; the PLC state is unknown.",
            innerException);
    }

    private ToyopucOperationOutcomeUnknownException? RetireMalformedPostSendResponse(
        ToyopucProtocolError exception,
        bool outcomeUnknownAfterSend)
    {
        if (!_requestMayHaveBeenSent)
        {
            return null;
        }

        MarkFixedUdpSessionTaintedIfNeeded();
        CloseTransport();
        return outcomeUnknownAfterSend ? CreateOutcomeUnknownException(exception) : null;
    }

    private IPAddress ResolveRemoteAddress(string host, long deadline)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address;
            throw new ToyopucError($"Host is not an IPv4 address: {host}");
        }

        using var deadlineCancellation = new CancellationTokenSource(
            GetRemainingTime(deadline, "Host resolution timeout"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _operationCancellation.Value,
            deadlineCancellation.Token);
        try
        {
            return SelectResolvedIPv4(
                host,
                HostAddressResolver(host, cancellation.Token).GetAwaiter().GetResult());
        }
        catch (OperationCanceledException) when (
            deadlineCancellation.IsCancellationRequested && !_operationCancellation.Value.IsCancellationRequested)
        {
            throw new ToyopucTimeoutError("Host resolution timeout");
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            throw new ToyopucTransportError($"Host resolution failed: {host}", exception);
        }
    }

    private static IPAddress SelectResolvedIPv4(string host, IReadOnlyList<IPAddress> addresses)
    {
        return addresses.FirstOrDefault(static address => address.AddressFamily == AddressFamily.InterNetwork)
            ?? throw new ToyopucTransportError(
                $"Host did not resolve to an IPv4 address: {host}",
                new SocketException((int)SocketError.HostNotFound));
    }

    private void ConnectWithTimeout(
        Socket socket,
        EndPoint endPoint,
        long deadline,
        OperationGeneration generation)
    {
        var result = socket.BeginConnect(endPoint, null, null);
        SocketConnectStartedHook?.Invoke(socket);
        var cancellationToken = _operationCancellation.Value;
        int completed = cancellationToken.CanBeCanceled
            ? WaitHandle.WaitAny(
                [result.AsyncWaitHandle, cancellationToken.WaitHandle],
                GetRemainingTime(deadline, "Connect timeout"))
            : result.AsyncWaitHandle.WaitOne(GetRemainingTime(deadline, "Connect timeout")) ? 0 : WaitHandle.WaitTimeout;
        if (completed == 1)
        {
            ThrowIfOpenGenerationInvalid(generation);
            throw new OperationCanceledException(cancellationToken);
        }
        if (completed == WaitHandle.WaitTimeout)
        {
            socket.Dispose();
            throw new ToyopucTimeoutError("Connect timeout");
        }

        socket.EndConnect(result);
        ThrowIfDeadlineExpired(deadline, "Connect timeout");
        ThrowIfOpenGenerationInvalid(generation);
    }

    private void ConfigureSocket(Socket socket)
    {
        var timeoutMs = checked((int)Math.Ceiling(Timeout.TotalMilliseconds));
        socket.ReceiveTimeout = timeoutMs;
        socket.SendTimeout = timeoutMs;
        SocketDeadlineAppliedHook?.Invoke(ToyopucSocketDeadlineDirection.Both, timeoutMs);
        if (socket.SocketType == SocketType.Stream && socket.ProtocolType == ProtocolType.Tcp)
        {
            socket.NoDelay = true;
        }
    }

    private void SendAll(byte[] payload, long deadline)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        var offset = 0;
        while (offset < payload.Length)
        {
            SetSocketSendDeadline(_socket, deadline, "Send timeout");
            _requestMayHaveBeenSent = true;
            var sent = _socket.Send(payload, offset, payload.Length - offset, SocketFlags.None);
            if (sent <= 0)
            {
                throw new ToyopucProtocolError("Connection closed while sending");
            }

            offset += sent;
        }
    }

    private void ReceiveExact(Span<byte> buffer, long deadline)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        while (!buffer.IsEmpty)
        {
            SetSocketReceiveDeadline(_socket, deadline, "Receive timeout");
            var received = _socket.Receive(buffer, SocketFlags.None);
            if (received <= 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            buffer = buffer[received..];
        }
    }

    private byte[] ReceiveExact(int count, long deadline)
    {
        var buffer = GC.AllocateUninitializedArray<byte>(count);
        ReceiveExact(buffer, deadline);
        return buffer;
    }

    private byte[] SendAndReceiveUdp(byte[] payload, long deadline)
    {
        if (_socket is null || _remoteEndPoint is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        _requestMayHaveBeenSent = true;
        SetSocketSendDeadline(_socket, deadline, "UDP send timeout");
        if (_socket.Send(payload, SocketFlags.None) != payload.Length)
            throw new ToyopucProtocolError("UDP send did not accept the complete datagram");
        RecordSend(payload.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(UdpReceiveBufferSize);
        try
        {
            SetSocketReceiveDeadline(_socket, deadline, "UDP receive timeout");
            var received = _socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
            return buffer.AsSpan(0, received).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void RecordSend(int length)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _txBytes, length);
    }

    private void MarkFixedUdpSessionTaintedIfNeeded()
    {
        if (_requestMayHaveBeenSent && Transport == ToyopucTransportMode.Udp && LocalPort != 0)
            _fixedUdpSessionTainted = true;
    }

    private void RetryDelaySleep(long deadline)
    {
        if (RetryDelay > TimeSpan.Zero)
        {
            var remaining = GetRemainingTime(deadline, "Transaction timeout during retry delay");
            var delay = RetryDelay < remaining ? RetryDelay : remaining;
            var cancellationToken = _operationCancellation.Value;
            if (cancellationToken.CanBeCanceled && cancellationToken.WaitHandle.WaitOne(delay))
                cancellationToken.ThrowIfCancellationRequested();
            if (!cancellationToken.CanBeCanceled)
                Thread.Sleep(delay);
            ThrowIfDeadlineExpired(deadline, "Transaction timeout during retry delay");
        }
    }

    private static long CreateDeadline(TimeSpan timeout)
    {
        var duration = checked((long)Math.Ceiling(timeout.TotalSeconds * Stopwatch.Frequency));
        return checked(Stopwatch.GetTimestamp() + duration);
    }

    private static TimeSpan GetRemainingTime(long deadline, string message)
    {
        var remaining = deadline - Stopwatch.GetTimestamp();
        if (remaining <= 0)
            throw new ToyopucTimeoutError(message);
        var timeSpanTicks = Math.Max(
            1L,
            checked((long)Math.Ceiling((double)remaining * TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
        return TimeSpan.FromTicks(timeSpanTicks);
    }

    private static void ThrowIfDeadlineExpired(long deadline, string message)
    {
        if (Stopwatch.GetTimestamp() >= deadline)
            throw new ToyopucTimeoutError(message);
    }

    private static int GetSocketDeadlineMilliseconds(long deadline, string message)
    {
        var remaining = GetRemainingTime(deadline, message);
        return Math.Max(1, checked((int)Math.Ceiling(remaining.TotalMilliseconds)));
    }

    private void SetSocketDeadlines(Socket socket, long deadline, string message)
    {
        var milliseconds = GetSocketDeadlineMilliseconds(deadline, message);
        socket.SendTimeout = milliseconds;
        socket.ReceiveTimeout = milliseconds;
        SocketDeadlineAppliedHook?.Invoke(ToyopucSocketDeadlineDirection.Both, milliseconds);
    }

    private void SetSocketSendDeadline(Socket socket, long deadline, string message)
    {
        var milliseconds = GetSocketDeadlineMilliseconds(deadline, message);
        socket.SendTimeout = milliseconds;
        SocketDeadlineAppliedHook?.Invoke(ToyopucSocketDeadlineDirection.Send, milliseconds);
    }

    private void SetSocketReceiveDeadline(Socket socket, long deadline, string message)
    {
        var milliseconds = GetSocketDeadlineMilliseconds(deadline, message);
        socket.ReceiveTimeout = milliseconds;
        SocketDeadlineAppliedHook?.Invoke(ToyopucSocketDeadlineDirection.Receive, milliseconds);
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
