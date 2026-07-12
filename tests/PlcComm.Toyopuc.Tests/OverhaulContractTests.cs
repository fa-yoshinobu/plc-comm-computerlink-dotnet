using System.Net;
using System.Net.Sockets;
using System.Reflection;
using PlcComm.Toyopuc;

namespace PlcComm.Toyopuc.Tests;

public sealed class OverhaulContractTests
{
    private const string Profile = "toyopuc:pc10g:pc10";

    [Fact]
    public void ConnectionOptions_RequireEndpointTransportProfileAndRoute()
    {
        var constructor = Assert.Single(typeof(ToyopucConnectionOptions).GetConstructors());
        Assert.Equal(
            [typeof(string), typeof(int), typeof(ToyopucTransportMode), typeof(string), typeof(ToyopucRoute)],
            constructor.GetParameters().Select(static parameter => parameter.ParameterType));

        var direct = new ToyopucConnectionOptions(
            "127.0.0.1",
            1025,
            ToyopucTransportMode.Tcp,
            Profile,
            ToyopucRoute.Direct);

        Assert.False(direct.Route.UsesRelay);
        Assert.Equal(TimeSpan.FromSeconds(3), direct.EffectiveTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(200), direct.EffectiveRetryDelay);
    }

    [Fact]
    public void ConnectionValidation_RejectsUnspecifiedTransportAndTcpLocalPort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Unspecified));
        Assert.Throws<ArgumentException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, localPort: 1234));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, timeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, retries: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, retryDelay: TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void RelayRoute_RequiresStrictValidatedHops()
    {
        var route = ToyopucRoute.Relay("P1-L2:N2,P3-L4:N10");
        Assert.Equal([(0x12, 2), (0x34, 10)], route.RelayHops);

        Assert.Throws<ArgumentException>(() => ToyopucRoute.Relay(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucRoute.Relay(new[] { (-1, 1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucRoute.Relay(new[] { (256, 1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucRoute.Relay(new[] { (1, 0) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucRoute.Relay(new[] { (1, 65536) }));
    }

    [Fact]
    public void ResolvedDevice_IsBoundToItsCanonicalProfile()
    {
        var resolved = ToyopucDeviceResolver.ResolveDevice("B0100", Profile);
        Assert.Equal(Profile, resolved.PlcProfile);

        using var otherClient = new ToyopucDeviceClient(
            "127.0.0.1",
            1025,
            ToyopucTransportMode.Tcp,
            "toyopuc:generic");
        Assert.Throws<ArgumentException>(() => otherClient.ReadOne(resolved));
        Assert.False(otherClient.IsOpen);
    }

    [Fact]
    public async Task NamedRead_RejectsMissingOrUnknownDtypeBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100"]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100:"]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100:Q"]));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void PublicSurface_RemovesChunkAtomicRawAndCombinedFrOperations()
    {
        var publicMethods = typeof(ToyopucDeviceClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(publicMethods, static name => name.Contains("Chunked", StringComparison.Ordinal));
        Assert.DoesNotContain("SendRaw", publicMethods);
        Assert.DoesNotContain("WriteFr", publicMethods);
        Assert.DoesNotContain("CommitFrRange", publicMethods);
        Assert.DoesNotContain("WaitFrWriteComplete", publicMethods);
        Assert.Contains("WriteFrWorkArea", publicMethods);
        Assert.Contains("CommitFrBlock", publicMethods);

        Assert.DoesNotContain(
            typeof(ToyopucDeviceClient).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            static method => method.GetParameters().Any(parameter => parameter.Name == "atomicTransfer"));

        var queuedPublicMembers = typeof(QueuedToyopucDeviceClient)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(static member => member.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("ExecuteAsync", queuedPublicMembers);
        Assert.DoesNotContain("InnerClient", queuedPublicMembers);

        Assert.DoesNotContain(
            typeof(ToyopucClient).Assembly.GetExportedTypes(),
            static type => type.Name == "ToyopucAddressingOptions");
        Assert.Empty(typeof(ToyopucPlcProfile).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(
            typeof(ToyopucPlcProfile).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => property.Name == "AddressingOptions");

        var queuedExtensionMethods = typeof(ToyopucDeviceClientExtensions)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(static method =>
                method.GetParameters().FirstOrDefault()?.ParameterType == typeof(QueuedToyopucDeviceClient))
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ReadOneAsync", queuedExtensionMethods);
        Assert.Contains("ReadManyAsync", queuedExtensionMethods);
        Assert.Contains("ReadDevicesAsync", queuedExtensionMethods);
        Assert.Contains("WriteAsync", queuedExtensionMethods);
        Assert.Contains("ReadFrOneAsync", queuedExtensionMethods);
        Assert.Contains("CommitFrBlockAsync", queuedExtensionMethods);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(true)]
    [InlineData(1.5)]
    [InlineData("1")]
    public void FrWorkAreaWrite_RejectsValuesThatWouldBeCoercedOrMasked(object value)
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromMilliseconds(10));

        Assert.ThrowsAny<ArgumentException>(() => client.WriteFrWorkArea("FR000000", value));
        Assert.ThrowsAny<ArgumentException>(() => client.RelayWriteFrWorkArea("P1-L2:N2", "FR000000", value));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void LowLevelFrWorkAreaWrite_RejectsOutOfRangeWordsBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromMilliseconds(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => client.WriteFrWorkArea(0, [-1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => client.RelayWriteFrWorkArea("P1-L2:N2", 0, [65536]));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(true)]
    [InlineData(1.5)]
    [InlineData("1")]
    public async Task AsyncAndQueuedFrWorkAreaWrite_RejectValuesBeforeTransport(object value)
    {
        await using var directClient = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => directClient.WriteFrWorkAreaAsync("FR000000", value));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => directClient.RelayWriteFrWorkAreaAsync("P1-L2:N2", "FR000000", value));
        Assert.False(directClient.IsOpen);

        await using var queuedDirect = new QueuedToyopucDeviceClient(directClient, ToyopucRoute.Direct);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => queuedDirect.WriteFrWorkAreaAsync("FR000000", value));
        Assert.False(queuedDirect.IsOpen);

        await using var relayClient = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromMilliseconds(10));
        await using var queuedRelay = new QueuedToyopucDeviceClient(relayClient, ToyopucRoute.Relay("P1-L2:N2"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => queuedRelay.WriteFrWorkAreaAsync("FR000000", value));
        Assert.False(queuedRelay.IsOpen);
    }

    [Fact]
    public void RawBuilder_RequiresExplicitDataAndStrictBoundariesBeforeTransport()
    {
        Assert.Throws<ArgumentNullException>(() => ToyopucProtocol.BuildCommand(0x1C, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildCommand(-1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildCommand(256, []));
        Assert.Equal([0x00, 0x00, 0x01, 0x00, 0x00], ToyopucProtocol.BuildCommand(0, []));
        Assert.Equal([0x00, 0x00, 0x01, 0x00, 0xFF], ToyopucProtocol.BuildCommand(255, []));

        var maximumData = new byte[65534];
        var maximumFrame = ToyopucProtocol.BuildCommand(0x7F, maximumData);
        Assert.Equal(65539, maximumFrame.Length);
        Assert.Equal(0xFF, maximumFrame[2]);
        Assert.Equal(0xFF, maximumFrame[3]);
        Assert.Equal(0x7F, maximumFrame[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildCommand(0x7F, new byte[65535]));

        using var client = new ToyopucClient("127.0.0.1", 1, ToyopucTransportMode.Tcp);
        Assert.Throws<ArgumentOutOfRangeException>(() => client.SendRaw(256, []));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void UdpFixedLocalPort_CollisionIsNotSilentlySubstituted()
    {
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();

        using var first = new ToyopucClient(
            "127.0.0.1",
            1025,
            ToyopucTransportMode.Udp,
            localPort: localPort);
        using var second = new ToyopucClient(
            "127.0.0.1",
            1025,
            ToyopucTransportMode.Udp,
            localPort: localPort);

        first.Open();
        Assert.Throws<SocketException>(second.Open);
        Assert.Equal(localPort, first.LocalPort);
        Assert.False(second.IsOpen);
    }

    [Fact]
    public async Task MaintainerTrace_SlowThrowingCallbackDoesNotChangeReadResult()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var callbackEntered = new CountdownEvent(2);
        using var releaseCallbacks = new ManualResetEventSlim();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, [0x34, 0x12]));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        client.EnableMaintainerTrace(
            1,
            _ =>
            {
                callbackEntered.Signal();
                releaseCallbacks.Wait();
                throw new InvalidOperationException("diagnostic failure");
            });

        var result = client.ReadWords(0x2000, 1);
        Assert.Equal([0x1234], result);
        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)));
        releaseCallbacks.Set();
        await serverTask;
    }

    [Fact]
    public async Task MaintainerTraceHistory_IsBoundedCopiedClearedAndReusableAfterReconnect()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            foreach (var requestCount in new[] { 3, 1 })
            {
                using var server = await listener.AcceptTcpClientAsync();
                await using var stream = server.GetStream();
                for (var index = 0; index < requestCount; index++)
                {
                    _ = await ReadFrameAsync(stream);
                    await stream.WriteAsync(BuildResponse(0x1C, [(byte)(index + 1), 0x00]));
                }
            }
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        Assert.False(client.CaptureTraceFrames);
        Assert.Throws<ArgumentOutOfRangeException>(() => client.EnableMaintainerTrace(0));

        client.EnableMaintainerTrace(2);
        _ = client.ReadWords(0x2000, 1);
        _ = client.ReadWords(0x2001, 1);
        _ = client.ReadWords(0x2002, 1);

        var history = client.TraceFrames;
        Assert.Equal(2, history.Count);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x2001, 1), history[0].Tx);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x2002, 1), history[1].Tx);
        history[0].Tx[0] = 0xFF;
        Assert.Equal(ToyopucProtocol.FtCommand, client.TraceFrames[0].Tx[0]);

        client.ClearTraceFrames();
        Assert.Empty(client.TraceFrames);
        client.Close();
        Assert.False(client.CaptureTraceFrames);
        Assert.Empty(client.TraceFrames);

        client.EnableMaintainerTrace(1);
        Assert.Equal([1], client.ReadWords(0x2003, 1));
        Assert.Single(client.TraceFrames);

        client.Close();
        await serverTask;
    }

    [Fact]
    public async Task CancelingDuringRetryDelay_PreventsAnotherReadRequest()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            Interlocked.Increment(ref requestCount);
            await stream.WriteAsync(BuildErrorResponse(0x1C, 0x73));

            using var waitForRetry = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try
            {
                using var retry = await listener.AcceptTcpClientAsync(waitForRetry.Token);
                Interlocked.Increment(ref requestCount);
            }
            catch (OperationCanceledException)
            {
            }
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();

        var read = client.ReadWordsAsync(0x2000, 1, cancellation.Token);
        Assert.True(SpinWait.SpinUntil(() => client.LastRx is not null, TimeSpan.FromSeconds(2)));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task RawRequest_DoesNotRetryAfterRetryablePlcResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            Interlocked.Increment(ref requestCount);
            await stream.WriteAsync(BuildErrorResponse(0x7F, 0x73));

            using var waitForRetry = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try
            {
                using var retry = await listener.AcceptTcpClientAsync(waitForRetry.Token);
                Interlocked.Increment(ref requestCount);
            }
            catch (OperationCanceledException)
            {
            }
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Throws<ToyopucError>(() => client.SendRaw(0x7F, []));
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
    }

    [Fact]
    public async Task RawAsync_SendsExactValidatedFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        byte[]? request = null;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            request = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x7F, []));
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));

        var response = await client.SendRawAsync(0x7F, [0x01, 0x02]);
        await serverTask;

        Assert.Equal(ToyopucProtocol.BuildCommand(0x7F, [0x01, 0x02]), request);
        Assert.Equal(0x7F, response.Cmd);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task RelayRead_RetriesRetryableOuterResponseAndReturnsInnerValue()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using (var first = await listener.AcceptTcpClientAsync())
            {
                await using var stream = first.GetStream();
                _ = await ReadFrameAsync(stream);
                Interlocked.Increment(ref requestCount);
                await stream.WriteAsync(BuildErrorResponse(0x60, 0x73));
            }

            using var second = await listener.AcceptTcpClientAsync();
            await using var secondStream = second.GetStream();
            _ = await ReadFrameAsync(secondStream);
            Interlocked.Increment(ref requestCount);
            await secondStream.WriteAsync(
                BuildResponse(
                    0x60,
                    [
                        0x12,
                        0x02,
                        0x00,
                        0x06,
                        0x03,
                        0x00,
                        0x1C,
                        0x34,
                        0x12,
                    ]));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Equal([0x1234], client.RelayReadWords("P1-L2:N2", 0x2000, 1));
        await serverTask;
        Assert.Equal(2, Volatile.Read(ref requestCount));
    }

    [Fact]
    public async Task ClockRead_RetriesRetryableResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using (var first = await listener.AcceptTcpClientAsync())
            {
                await using var stream = first.GetStream();
                _ = await ReadFrameAsync(stream);
                Interlocked.Increment(ref requestCount);
                await stream.WriteAsync(BuildErrorResponse(0x32, 0x73));
            }

            using var second = await listener.AcceptTcpClientAsync();
            await using var secondStream = second.GetStream();
            _ = await ReadFrameAsync(secondStream);
            Interlocked.Increment(ref requestCount);
            await secondStream.WriteAsync(BuildResponse(0x32, [0x70, 0x00, 0x56, 0x34, 0x12, 0x11, 0x07, 0x26, 0x06]));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        var clock = client.ReadClock();
        await serverTask;

        Assert.Equal(new ClockData(56, 34, 12, 11, 7, 26, 6), clock);
        Assert.Equal(2, Volatile.Read(ref requestCount));
    }

    [Fact]
    public async Task FrRead_RetriesRetryableResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using (var first = await listener.AcceptTcpClientAsync())
            {
                await using var stream = first.GetStream();
                _ = await ReadFrameAsync(stream);
                Interlocked.Increment(ref requestCount);
                await stream.WriteAsync(BuildErrorResponse(0xC2, 0x73));
            }

            using var second = await listener.AcceptTcpClientAsync();
            await using var secondStream = second.GetStream();
            _ = await ReadFrameAsync(secondStream);
            Interlocked.Increment(ref requestCount);
            await secondStream.WriteAsync(BuildResponse(0xC2, [0x34, 0x12]));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Equal([0x1234], client.ReadFrWords(0, 1));
        await serverTask;
        Assert.Equal(2, Volatile.Read(ref requestCount));
    }

    [Theory]
    [InlineData("fr-write", 0xC3)]
    [InlineData("fr-commit", 0xCA)]
    [InlineData("clock-write", 0x32)]
    [InlineData("scan", 0x32)]
    [InlineData("relay-scan", 0x60)]
    public async Task StateChangingOperation_DoesNotRetryRetryableResponse(string operation, int expectedCommand)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCount = 0;

        var serverTask = Task.Run(async () =>
        {
            using (var server = await listener.AcceptTcpClientAsync())
            {
                await using var stream = server.GetStream();
                var request = await ReadFrameAsync(stream);
                Assert.Equal(expectedCommand, request[4]);
                Interlocked.Increment(ref requestCount);
                await stream.WriteAsync(BuildErrorResponse(expectedCommand, 0x73));
            }

            using var waitForRetry = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try
            {
                using var retry = await listener.AcceptTcpClientAsync(waitForRetry.Token);
                Interlocked.Increment(ref requestCount);
            }
            catch (OperationCanceledException)
            {
            }
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Throws<ToyopucError>(() =>
        {
            switch (operation)
            {
                case "fr-write":
                    client.WriteFrWorkArea(0, [0x1234]);
                    break;
                case "fr-commit":
                    client.CommitFrBlock(0);
                    break;
                case "clock-write":
                    client.WriteClock(new DateTime(2026, 7, 11, 12, 34, 56, DateTimeKind.Unspecified), 2000);
                    break;
                case "scan":
                    client.ResumeScan();
                    break;
                case "relay-scan":
                    client.RelayResumeScan("P1-L2:N2");
                    break;
                default:
                    throw new InvalidOperationException($"Unknown test operation: {operation}");
            }
        });

        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
    }

    [Fact]
    public void ClockConversionAndWrite_RequireExplicitValidCentury()
    {
        var raw = new ClockData(1, 2, 3, 4, 5, 26, 1);
        Assert.Equal(2026, raw.AsDateTime(2000).Year);
        Assert.Equal(1926, raw.AsDateTime(1900).Year);
        Assert.Throws<ArgumentOutOfRangeException>(() => raw.AsDateTime(1950));

        using var client = new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.WriteClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), 1900));
        Assert.Throws<ArgumentException>(() =>
            client.WriteClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 2000));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void HighLevelCollections_RejectEmptyAndImplicitlySplitWritesBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name);

        Assert.Throws<ArgumentException>(() => client.ReadDevices([]));
        Assert.Throws<ArgumentException>(() => client.WriteMany([]));
        Assert.Throws<ArgumentException>(() => client.Write("B0100", Array.Empty<int>()));
        Assert.Throws<ToyopucProtocolError>(() => client.Write("U07FFF", new[] { 0x1234, 0x5678 }));
        Assert.Throws<ToyopucProtocolError>(() => client.RelayWrite("P1-L2:N2", "U07FFF", new[] { 0x1234, 0x5678 }));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task CancelingQueuedLowLevelOperation_DoesNotCloseRunningOperation()
    {
        using var client = new TrackingClient();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var first = client.QueueAction(
            () =>
            {
                entered.Set();
                release.Wait();
            });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));

        using var cancellation = new CancellationTokenSource();
        var second = client.QueueAction(static () => { }, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await second);
        Assert.Equal(0, client.CloseCalls);

        release.Set();
        await first;
    }

    [Fact]
    public async Task CancelingWriteAfterSend_ReportsUnknownOutcomeAndClosesSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var requestReceived = new ManualResetEventSlim();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            var buffer = new byte[64];
            _ = await stream.ReadAsync(buffer);
            requestReceived.Set();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        using var cancellation = new CancellationTokenSource();

        var write = client.WriteWordsAsync(0x2000, [0x1234], cancellation.Token);
        Assert.True(requestReceived.Wait(TimeSpan.FromSeconds(2)));
        cancellation.Cancel();

        await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(async () => await write);
        Assert.False(client.IsOpen);
        Assert.Throws<InvalidOperationException>(() => client.ReadWords(0x2000, 1));
        await serverTask;

        var reconnectServer = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, [0x78, 0x56]));
        });

        await client.OpenAsync();
        Assert.Equal([0x5678], client.ReadWords(0x2000, 1));
        await reconnectServer;
    }

    [Fact]
    public async Task PreCanceledOperation_DoesNotOpenTransport()
    {
        await using var client = new ToyopucClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.ReadWordsAsync(0x2000, 1, cancellation.Token));
        Assert.False(client.IsOpen);
        Assert.Null(client.LastTx);
    }

    [Fact]
    public async Task AsyncTimeoutWithoutToken_DiscardsLateResponseAndUsesNewSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var first = await listener.AcceptTcpClientAsync();
            var firstResponse = Task.Run(async () =>
            {
                try
                {
                    await using var stream = first.GetStream();
                    _ = await ReadFrameAsync(stream);
                    await Task.Delay(TimeSpan.FromMilliseconds(400));
                    await stream.WriteAsync(BuildResponse(0x1C, [0x34, 0x12]));
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
            });

            using var second = await listener.AcceptTcpClientAsync();
            await using var secondStream = second.GetStream();
            _ = await ReadFrameAsync(secondStream);
            await secondStream.WriteAsync(BuildResponse(0x1C, [0x78, 0x56]));
            await firstResponse;
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<ToyopucTimeoutError>(
            async () => await client.ReadWordsAsync(0x2000, 1));
        Assert.False(client.IsOpen);
        Assert.Equal([0x5678], client.ReadWords(0x2000, 1));
        await serverTask;
    }

    [Fact]
    public async Task CancelingPendingTcpConnect_ClosesSessionAndAwaitsWorkerCompletion()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var backlogSockets = new List<Socket>();

        try
        {
            var connected = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            backlogSockets.Add(connected);
            await connected.ConnectAsync(IPAddress.Loopback, port);

            var pending = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            backlogSockets.Add(pending);
            var pendingConnect = pending.BeginConnect(IPAddress.Loopback, port, null, null);
            Assert.False(pendingConnect.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200)));

            await using var client = new ToyopucClient(
                "127.0.0.1",
                port,
                ToyopucTransportMode.Tcp,
                timeout: TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();

            var open = client.OpenAsync(cancellation.Token);
            Assert.True(SpinWait.SpinUntil(() => client.IsOpen, TimeSpan.FromSeconds(2)));
            Assert.False(open.IsCompleted);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await open.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(client.IsOpen);
        }
        finally
        {
            foreach (var socket in backlogSockets)
            {
                socket.Dispose();
            }
        }
    }

    [Fact]
    public async Task CancelingBlockedTcpSend_ReportsUnknownOutcomeAndAwaitsWorkerCompletion()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Server.ReceiveBufferSize = 1024;
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(10));
        client.Open();
        using var server = await listener.AcceptTcpClientAsync();
        server.Client.ReceiveBufferSize = 1024;

        var socketField = typeof(ToyopucClient).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic);
        var socket = Assert.IsType<Socket>(socketField!.GetValue(client));
        socket.SendBufferSize = 1024;
        socket.Blocking = false;
        var filler = new byte[65536];
        var saturated = false;
        for (var attempt = 0; attempt < 10000; attempt++)
        {
            try
            {
                _ = socket.Send(filler);
            }
            catch (SocketException exception) when (
                exception.SocketErrorCode is SocketError.WouldBlock or SocketError.NoBufferSpaceAvailable)
            {
                saturated = true;
                break;
            }
        }

        Assert.True(saturated, "The local TCP send buffer did not reach a blocked state.");
        socket.Blocking = true;
        using var cancellation = new CancellationTokenSource();

        var send = client.SendRawAsync(0x7F, [], cancellation.Token);
        Assert.True(SpinWait.SpinUntil(() => client.LastTx is not null, TimeSpan.FromSeconds(2)));
        cancellation.Cancel();

        await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
            async () => await send.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(client.IsOpen);
    }

    private sealed class TrackingClient()
        : ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp)
    {
        public int CloseCalls { get; private set; }

        public Task QueueAction(Action action, CancellationToken cancellationToken = default) =>
            RunAsync(action, cancellationToken);

        public override void Close()
        {
            CloseCalls++;
            base.Close();
        }
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header);
        var length = header[2] | (header[3] << 8);
        var body = new byte[length];
        await stream.ReadExactlyAsync(body);
        return [.. header, .. body];
    }

    private static byte[] BuildResponse(int command, byte[] data)
    {
        var length = checked(1 + data.Length);
        return
        [
            ToyopucProtocol.FtResponse,
            0x00,
            (byte)(length & 0xFF),
            (byte)(length >> 8),
            (byte)command,
            .. data,
        ];
    }

    private static byte[] BuildErrorResponse(int command, byte errorCode)
    {
        return
        [
            ToyopucProtocol.FtResponse,
            0x10,
            0x02,
            0x00,
            (byte)command,
            errorCode,
        ];
    }
}
