using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using PlcComm.Toyopuc;
using PlcComm.Toyopuc.Examples;

namespace PlcComm.Toyopuc.Tests;

public sealed class OverhaulContractTests
{
    private const string Profile = "toyopuc:pc10g:pc10";

    [Fact]
    public void SocketDeadlineSettersChangeOnlyTheirOwnedDirection()
    {
        var type = typeof(ToyopucClient);
        var setSend = type.GetMethod(
            "SetSocketSendDeadline",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var setReceive = type.GetMethod(
            "SetSocketReceiveDeadline",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(setSend);
        Assert.NotNull(setReceive);

        using var client = new ToyopucClient(
            "127.0.0.1", 8501, ToyopucTransportMode.Tcp, timeout: TimeSpan.FromSeconds(2));
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            SendTimeout = 111,
            ReceiveTimeout = 222,
        };
        var deadline = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);

        setSend.Invoke(client, [socket, deadline, "send timeout"]);
        var sendTimeout = socket.SendTimeout;
        Assert.InRange(sendTimeout, 1, 2000);
        Assert.Equal(222, socket.ReceiveTimeout);

        setReceive.Invoke(client, [socket, deadline, "receive timeout"]);
        Assert.InRange(socket.ReceiveTimeout, 1, 2000);
        Assert.Equal(sendTimeout, socket.SendTimeout);

        foreach (MethodInfo setter in new[] { setSend, setReceive })
        {
            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
                setter.Invoke(client, [socket, Stopwatch.GetTimestamp() - 1, "expired deadline"]));
            Assert.IsType<ToyopucTimeoutError>(invocation.InnerException);
        }
    }

    [Fact]
    public async Task TcpRequestUsesConnectionThenDirectionSpecificDeadlineUpdates()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        byte[] response = BuildResponse(0x1C, [0x34, 0x12]);
        var serverTask = Task.Run(async () =>
        {
            using TcpClient server = await listener.AcceptTcpClientAsync();
            await using NetworkStream stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            foreach (byte[] fragment in response.Chunk(2))
            {
                await stream.WriteAsync(fragment);
                await Task.Delay(5);
            }
        });
        var events = new List<(ToyopucSocketDeadlineDirection Direction, int Milliseconds)>();
        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TimeSpan.FromSeconds(2));
        client.SocketDeadlineAppliedHook = (direction, milliseconds) =>
            events.Add((direction, milliseconds));

        Assert.Equal(new[] { 0x1234 }, await client.ReadWordsAsync(0x2000, 1));
        await serverTask;

        int firstDirectional = events.FindIndex(static item =>
            item.Direction != ToyopucSocketDeadlineDirection.Both);
        Assert.True(firstDirectional > 0);
        Assert.All(events.Take(firstDirectional), static item =>
            Assert.Equal(ToyopucSocketDeadlineDirection.Both, item.Direction));
        Assert.Equal(ToyopucSocketDeadlineDirection.Send, events[firstDirectional].Direction);
        Assert.DoesNotContain(
            events.Skip(firstDirectional),
            static item => item.Direction == ToyopucSocketDeadlineDirection.Both);
        Assert.True(events.Count(static item => item.Direction == ToyopucSocketDeadlineDirection.Receive) >= 2);
        AssertDeadlineValuesDoNotIncrease(events);
    }

    [Fact]
    public async Task UdpRequestUsesConnectionThenDirectionSpecificDeadlineUpdates()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var serverTask = Task.Run(async () =>
        {
            UdpReceiveResult request = await server.ReceiveAsync();
            byte[] response = BuildResponse(0x1C, [0x78, 0x56]);
            await server.SendAsync(response, response.Length, request.RemoteEndPoint);
        });
        var events = new List<(ToyopucSocketDeadlineDirection Direction, int Milliseconds)>();
        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Udp, timeout: TimeSpan.FromSeconds(2));
        client.SocketDeadlineAppliedHook = (direction, milliseconds) =>
            events.Add((direction, milliseconds));

        Assert.Equal(new[] { 0x5678 }, await client.ReadWordsAsync(0x2000, 1));
        await serverTask;

        int firstSend = events.FindIndex(static item =>
            item.Direction == ToyopucSocketDeadlineDirection.Send);
        Assert.True(firstSend > 0);
        Assert.All(events.Take(firstSend), static item =>
            Assert.Equal(ToyopucSocketDeadlineDirection.Both, item.Direction));
        Assert.Equal(
            [ToyopucSocketDeadlineDirection.Send, ToyopucSocketDeadlineDirection.Receive],
            events.Skip(firstSend).Select(static item => item.Direction));
        AssertDeadlineValuesDoNotIncrease(events);
    }

    [Fact]
    public void SyncOpenDiscardsLateSocketWhenCloseRetiresDnsGeneration()
    {
        using var resolverEntered = new ManualResetEventSlim();
        using var releaseResolver = new ManualResetEventSlim();
        using var client = new ToyopucClient(
            "localhost", 8501, ToyopucTransportMode.Udp, timeout: TimeSpan.FromSeconds(2));
        client.HostAddressResolver = (_, _) =>
        {
            resolverEntered.Set();
            releaseResolver.Wait();
            return Task.FromResult(new[] { IPAddress.Loopback });
        };

        Exception? openError = null;
        var openThread = new Thread(() =>
        {
            try
            {
                client.Open();
            }
            catch (Exception error)
            {
                openError = error;
            }
        });
        openThread.Start();
        Assert.True(resolverEntered.Wait(TimeSpan.FromSeconds(2)));

        client.Close();
        releaseResolver.Set();
        Assert.True(openThread.Join(TimeSpan.FromSeconds(2)));

        Assert.IsType<ToyopucConnectionClosedException>(openError);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void SyncOpenPublishesOnlyAfterConnectAndDisposesPostConnectCandidateRetiredByClose()
    {
        using var connected = new ManualResetEventSlim();
        using var releasePublication = new ManualResetEventSlim();
        using var client = new ToyopucClient(
            "127.0.0.1", 8501, ToyopucTransportMode.Udp, timeout: TimeSpan.FromSeconds(2));
        Socket? candidate = null;
        client.ConnectedSocketHook = socket =>
        {
            candidate = socket;
            connected.Set();
            releasePublication.Wait();
        };

        Exception? openError = null;
        var openThread = new Thread(() =>
        {
            try
            {
                client.Open();
            }
            catch (Exception error)
            {
                openError = error;
            }
        });
        openThread.Start();
        Assert.True(connected.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(client.IsOpen);

        client.Close();
        releasePublication.Set();
        Assert.True(openThread.Join(TimeSpan.FromSeconds(2)));

        Assert.IsType<ToyopucConnectionClosedException>(openError);
        Assert.False(client.IsOpen);
        Assert.NotNull(candidate);
        Assert.True(candidate.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task EveryNamedFrGenericAndTypedWriteRejectsBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromSeconds(2));

        var resolved = client.ResolveDevice("FR000000");
        foreach (var device in new object[] { "FR000000", resolved })
        {
            var syncWrites = new (string Name, Action Write)[]
            {
                ("Write scalar", () => client.Write(device, 1)),
                ("Write sequence", () => client.Write(device, new[] { 1, 2 })),
                ("WriteMany", () => client.WriteMany(new Dictionary<object, object> { [device] = 1 })),
                ("WriteDWord", () => client.WriteDWord(device, 1)),
                ("WriteDWords", () => client.WriteDWords(device, new uint[] { 1, 2 })),
                ("WriteFloat32", () => client.WriteFloat32(device, 1)),
                ("WriteFloat32s", () => client.WriteFloat32s(device, new float[] { 1, 2 })),
                ("RelayWrite scalar", () => client.RelayWrite("P1-L2:N2", device, 1)),
                ("RelayWrite sequence", () => client.RelayWrite("P1-L2:N2", device, new[] { 1, 2 })),
                ("RelayWriteWords", () => client.RelayWriteWords("P1-L2:N2", device, new[] { 1, 2 })),
                ("RelayWriteMany", () => client.RelayWriteMany("P1-L2:N2", new Dictionary<object, object> { [device] = 1 })),
                ("RelayWriteDWord", () => client.RelayWriteDWord("P1-L2:N2", device, 1)),
                ("RelayWriteDWords", () => client.RelayWriteDWords("P1-L2:N2", device, new uint[] { 1, 2 })),
                ("RelayWriteFloat32", () => client.RelayWriteFloat32("P1-L2:N2", device, 1)),
                ("RelayWriteFloat32s", () => client.RelayWriteFloat32s("P1-L2:N2", device, new float[] { 1, 2 })),
            };

            foreach (var (name, write) in syncWrites)
            {
                var error = Record.Exception(write);
                Assert.True(error is ArgumentException, $"{name} did not reject FR before transport: {error}");
                Assert.Equal(default, client.TrafficStats);
                Assert.Null(client.LastTx);
            }

            var asyncWrites = new (string Name, Func<Task> Write)[]
            {
                ("WriteAsync scalar", () => client.WriteAsync(device, 1)),
                ("WriteAsync sequence", () => client.WriteAsync(device, new[] { 1, 2 })),
                ("WriteManyAsync", () => client.WriteManyAsync(new Dictionary<object, object> { [device] = 1 })),
                ("WriteDWordAsync", () => client.WriteDWordAsync(device, 1)),
                ("WriteDWordsAsync", () => client.WriteDWordsAsync(device, new uint[] { 1, 2 })),
                ("WriteFloat32Async", () => client.WriteFloat32Async(device, 1)),
                ("WriteFloat32sAsync", () => client.WriteFloat32sAsync(device, new float[] { 1, 2 })),
                ("RelayWriteAsync scalar", () => client.RelayWriteAsync("P1-L2:N2", device, 1)),
                ("RelayWriteAsync sequence", () => client.RelayWriteAsync("P1-L2:N2", device, new[] { 1, 2 })),
                ("RelayWriteWordsAsync", () => client.RelayWriteWordsAsync("P1-L2:N2", device, new[] { 1, 2 })),
                ("RelayWriteManyAsync", () => client.RelayWriteManyAsync("P1-L2:N2", new Dictionary<object, object> { [device] = 1 })),
                ("RelayWriteDWordAsync", () => client.RelayWriteDWordAsync("P1-L2:N2", device, 1)),
                ("RelayWriteDWordsAsync", () => client.RelayWriteDWordsAsync("P1-L2:N2", device, new uint[] { 1, 2 })),
                ("RelayWriteFloat32Async", () => client.RelayWriteFloat32Async("P1-L2:N2", device, 1)),
                ("RelayWriteFloat32sAsync", () => client.RelayWriteFloat32sAsync("P1-L2:N2", device, new float[] { 1, 2 })),
            };

            foreach (var (name, write) in asyncWrites)
            {
                var error = await Record.ExceptionAsync(write);
                Assert.True(error is ArgumentException, $"{name} did not reject FR before transport: {error}");
                Assert.Equal(default, client.TrafficStats);
                Assert.Null(client.LastTx);
            }
        }

        var extensionWrites = new (string Name, Func<Task> Write)[]
        {
            ("WriteTypedAsync", () => ToyopucDeviceClientExtensions.WriteTypedAsync(client, "FR000000", "U", 1)),
            ("WriteWordsSingleRequestAsync extension", () => ToyopucDeviceClientExtensions.WriteWordsSingleRequestAsync(client, "FR000000", new ushort[] { 1 })),
            ("WriteDWordsSingleRequestAsync extension", () => ToyopucDeviceClientExtensions.WriteDWordsSingleRequestAsync(client, "FR000000", new uint[] { 1 })),
            ("WriteBitInWordAsync", () => ToyopucDeviceClientExtensions.WriteBitInWordAsync(client, "FR000000", 0, true)),
        };

        foreach (var (name, write) in extensionWrites)
        {
            var error = await Record.ExceptionAsync(write);
            Assert.True(error is ArgumentException, $"{name} did not reject FR before transport: {error}");
            Assert.Equal(default, client.TrafficStats);
            Assert.Null(client.LastTx);
        }

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task ExplicitFrWorkAreaWrites_AreAllowedWithoutImplicitCommit()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var receivedCommands = new List<int>();
        var relayRequests = new[] { false, false, true, true };

        var serverTask = Task.Run(async () =>
        {
            foreach (var relay in relayRequests)
            {
                using var server = await listener.AcceptTcpClientAsync();
                await using var stream = server.GetStream();
                var request = await ReadFrameAsync(stream);
                receivedCommands.Add(request[4]);
                Assert.Equal(relay ? 0x60 : 0xC3, request[4]);
                await stream.WriteAsync(relay
                    ? BuildRelayResponse(0x12, 2, 0xC3, [])
                    : BuildResponse(0xC3, []));
            }
        });

        using (var client = new ToyopucDeviceClient("127.0.0.1", port, ToyopucTransportMode.Tcp, Profile))
        {
            client.WriteFrWorkArea("FR000000", new[] { 0x1234 });
            Assert.Equal(1UL, client.TrafficStats.RequestCount);
        }

        await using (var client = new ToyopucDeviceClient("127.0.0.1", port, ToyopucTransportMode.Tcp, Profile))
        {
            var resolved = client.ResolveDevice("FR000000");
            await client.WriteFrWorkAreaAsync(resolved, new[] { 0x1234 });
            Assert.Equal(1UL, client.TrafficStats.RequestCount);
        }

        using (var client = new ToyopucDeviceClient("127.0.0.1", port, ToyopucTransportMode.Tcp, Profile))
        {
            client.RelayWriteFrWorkArea("P1-L2:N2", "FR000000", new[] { 0x1234 });
            Assert.Equal(1UL, client.TrafficStats.RequestCount);
        }

        await using (var client = new ToyopucDeviceClient("127.0.0.1", port, ToyopucTransportMode.Tcp, Profile))
        {
            var resolved = client.ResolveDevice("FR000000");
            await client.RelayWriteFrWorkAreaAsync("P1-L2:N2", resolved, new[] { 0x1234 });
            Assert.Equal(1UL, client.TrafficStats.RequestCount);
        }

        await serverTask;
        Assert.Equal(new[] { 0xC3, 0xC3, 0x60, 0x60 }, receivedCommands);
    }

    [Fact]
    public async Task ReconnectPolicy_RetriesOnlyTransportAndTimeoutAfterBackoff()
    {
        var backoff = TimeSpan.FromMilliseconds(125);
        foreach (var exception in new Exception[]
        {
            new ToyopucTransportError("transport", new IOException("socket")),
            new ToyopucTimeoutError("timeout"),
        })
        {
            var delayCount = 0;
            var retry = await ReconnectPolicy.WaitBeforeRetryAsync(
                exception,
                backoff,
                CancellationToken.None,
                (delay, cancellationToken) =>
                {
                    Assert.Equal(backoff, delay);
                    Assert.False(cancellationToken.IsCancellationRequested);
                    delayCount++;
                    return Task.CompletedTask;
                });

            Assert.True(retry);
            Assert.Equal(1, delayCount);
        }

        Assert.Equal(TimeSpan.FromSeconds(2), ReconnectPolicy.NextBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(30), ReconnectPolicy.NextBackoff(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task ReconnectPolicy_CancellationAndNonTransportFailuresDoNotSleepOrRetry()
    {
        foreach (var exception in new Exception[]
        {
            new OperationCanceledException(),
            new ToyopucPlcError("plc"),
            new ArgumentException("argument"),
            new ToyopucProtocolError("protocol"),
        })
        {
            var delayCount = 0;
            var retry = await ReconnectPolicy.WaitBeforeRetryAsync(
                exception,
                TimeSpan.FromSeconds(1),
                CancellationToken.None,
                (_, _) =>
                {
                    delayCount++;
                    return Task.CompletedTask;
                });

            Assert.False(retry);
            Assert.Equal(0, delayCount);
        }

        using var cancelled = new CancellationTokenSource();
        var delayStarted = false;
        var retryAfterCancellation = await ReconnectPolicy.WaitBeforeRetryAsync(
            new ToyopucTimeoutError("timeout"),
            TimeSpan.FromSeconds(1),
            cancelled.Token,
            (_, cancellationToken) =>
            {
                delayStarted = true;
                cancelled.Cancel();
                return Task.FromCanceled(cancellationToken);
            });

        Assert.True(delayStarted);
        Assert.False(retryAfterCancellation);
    }

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
        var maximumTimer = TimeSpan.FromMilliseconds(int.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Unspecified));
        Assert.Throws<ArgumentException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, localPort: 1234));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, timeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, retries: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp, retryDelay: TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient(
            "127.0.0.1", 1025, ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient(
            "127.0.0.1", 1025, ToyopucTransportMode.Tcp,
            retryDelay: TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyopucClient(
            "127.0.0.1", 1025, ToyopucTransportMode.Tcp,
            timeout: maximumTimer + TimeSpan.FromTicks(1)));

        using var boundary = new ToyopucClient(
            "127.0.0.1",
            1025,
            ToyopucTransportMode.Tcp,
            timeout: maximumTimer,
            retryDelay: maximumTimer);
        Assert.Equal(maximumTimer, boundary.Timeout);
        Assert.Equal(maximumTimer, boundary.RetryDelay);
    }

    [Fact]
    public async Task FactoryAndPoll_EnforceTheCommonTimerBoundaryBeforeTransport()
    {
        var maximumTimer = TimeSpan.FromMilliseconds(int.MaxValue);
        var invalidOptions = new ToyopucConnectionOptions(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            ToyopucRoute.Direct)
        {
            Timeout = maximumTimer + TimeSpan.FromTicks(1),
        };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ToyopucDeviceClientFactory.OpenAndConnectAsync(invalidOptions));
        var invalidRetryOptions = invalidOptions with
        {
            Timeout = null,
            RetryDelay = maximumTimer + TimeSpan.FromTicks(1),
        };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ToyopucDeviceClientFactory.OpenAndConnectAsync(invalidRetryOptions));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile);
        foreach (var invalidInterval in new[] { TimeSpan.Zero, TimeSpan.FromTicks(-1), maximumTimer + TimeSpan.FromTicks(1) })
        {
            await using var invalidPoll = client.PollAsync(["P1-D0000:U"], invalidInterval).GetAsyncEnumerator();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await invalidPoll.MoveNextAsync());
        }

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await using var boundaryPoll = client.PollAsync(["P1-D0000:U"], maximumTimer, cancelled.Token).GetAsyncEnumerator();
        Assert.False(await boundaryPoll.MoveNextAsync());
        Assert.False(client.IsOpen);

    }

    [Theory]
    [InlineData(ToyopucTransportMode.Tcp)]
    [InlineData(ToyopucTransportMode.Udp)]
    public void ConnectionValidation_RejectsIPv6LiteralBeforeSocketCreation(ToyopucTransportMode transport)
    {
        foreach (var host in new[] { "::1", "[::1]" })
        {
            Assert.Throws<ArgumentException>(() =>
                new ToyopucClient(host, 1025, transport));
        }
        Assert.Throws<ArgumentException>(() =>
            new ToyopucDeviceClient("::ffff:127.0.0.1", 1025, transport, Profile));
    }

    [Fact]
    public async Task Factory_RejectsIPv6LiteralBeforeOpeningTransport()
    {
        var options = new ToyopucConnectionOptions(
            "::1",
            1025,
            ToyopucTransportMode.Tcp,
            Profile,
            ToyopucRoute.Direct);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ToyopucDeviceClientFactory.OpenAndConnectAsync(options));
    }

    [Fact]
    public async Task Factory_ReturnsOrdinaryClientWithImmutableSelectedRoute()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync();
        var routeSource = new[] { (0x12, 2) };
        var options = new ToyopucConnectionOptions(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            Profile,
            ToyopucRoute.Relay(routeSource));

        await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
        using var server = await accepted;
        routeSource[0] = (0x34, 4);

        Assert.IsType<ToyopucDeviceClient>(client);
        Assert.True(client.UsesRelay);
        Assert.Equal([(0x12, 2)], client.RelayHops);
    }

    [Fact]
    public async Task HostnameResolution_UsesIPv4ForTcpAndUdp()
    {
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync();
            using var client = new ToyopucClient(
                "localhost",
                port,
                ToyopucTransportMode.Tcp,
                timeout: TimeSpan.FromSeconds(2));

            client.Open();
            using var accepted = await acceptTask;
            Assert.Equal(AddressFamily.InterNetwork, accepted.Client.RemoteEndPoint!.AddressFamily);
        }

        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var udpPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            Assert.Equal(AddressFamily.InterNetwork, request.RemoteEndPoint.AddressFamily);
            await server.SendAsync(BuildResponse(0x1C, [0x34, 0x12]), request.RemoteEndPoint);
        });
        using var udpClient = new ToyopucClient(
            "localhost",
            udpPort,
            ToyopucTransportMode.Udp,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal([0x1234], udpClient.ReadWords(0, 1));
        await serverTask;
    }

    [Theory]
    [InlineData(ToyopucTransportMode.Tcp)]
    [InlineData(ToyopucTransportMode.Udp)]
    public void HostnameResolutionFailure_UsesLibraryErrorAndLeavesClientClosed(ToyopucTransportMode transport)
    {
        using var client = new ToyopucClient("ipv4-resolution-test.invalid", 1025, transport);

        Assert.Throws<ToyopucTransportError>(client.Open);
        Assert.False(client.IsOpen);
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public void LoopbackConnectFailure_UsesStructuredErrorAndLeavesClientClosed()
    {
        using var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        reservation.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)reservation.LocalEndPoint!).Port;
        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromMilliseconds(250));

        Exception connectError = Record.Exception(client.Open);
        Assert.True(
            connectError is ToyopucTransportError or ToyopucTimeoutError,
            $"Expected connection refusal or a bounded connect timeout, got {connectError.GetType().FullName}.");
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void RelayRoute_RequiresStrictValidatedHops()
    {
        var source = new[] { (0x12, 2), (0x34, 10) };
        var route = ToyopucRoute.Relay(source);
        source[0] = (0x55, 55);
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
            timeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100"]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100:"]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["B0100:Q"]));
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
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

        Assert.DoesNotContain(
            typeof(ToyopucClient).Assembly.GetExportedTypes(),
            static type => type.Name == "QueuedToyopucDeviceClient");

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
                method.GetParameters().FirstOrDefault()?.ParameterType.Name.Contains("Queued", StringComparison.Ordinal) == true)
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(queuedExtensionMethods);
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
            timeout: TimeSpan.FromSeconds(2));

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
            timeout: TimeSpan.FromSeconds(2));

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
    public async Task AsyncFrWorkAreaWrite_RejectsValuesBeforeTransportForDirectAndRelayRoutes(object value)
    {
        await using var directClient = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => directClient.WriteFrWorkAreaAsync("FR000000", value));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => directClient.RelayWriteFrWorkAreaAsync("P1-L2:N2", "FR000000", value));
        Assert.False(directClient.IsOpen);

        await using var relayClient = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromSeconds(2),
            route: ToyopucRoute.Relay("P1-L2:N2"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => relayClient.WriteFrWorkAreaAsync("FR000000", value));
        Assert.False(relayClient.IsOpen);
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
        var error = Assert.Throws<ToyopucTransportError>(second.Open);
        Assert.IsType<SocketException>(error.InnerException);
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
    public async Task ReadPlcNg_DoesNotRetryAfterAnyRequestBytesWereSent()
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

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ToyopucPlcError>(
            async () => await client.ReadWordsAsync(0x2000, 1));
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
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

        Assert.Throws<ToyopucPlcError>(() => client.SendRaw(0x7F, []));
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.True(client.TrafficStats.TxBytes > 0);
        Assert.Equal((ulong)BuildErrorResponse(0x7F, 0x73).Length, client.TrafficStats.RxBytes);
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
    public async Task RelayRead_DoesNotRetryAfterPlcNgResponse()
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
            await stream.WriteAsync(BuildErrorResponse(0x60, 0x73));
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Throws<ToyopucPlcError>(
            () => client.RelayReadWords("P1-L2:N2", 0x2000, 1));
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.True(client.TrafficStats.TxBytes > 0);
        Assert.True(client.TrafficStats.RxBytes > 0);
    }

    [Fact]
    public async Task ClockRead_DoesNotRetryAfterPlcNgResponse()
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
            await stream.WriteAsync(BuildErrorResponse(0x32, 0x73));
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);
        client.EnableMaintainerTrace(4);

        Assert.Throws<ToyopucPlcError>(client.ReadClock);
        await serverTask;

        Assert.Equal(1, Volatile.Read(ref requestCount));
        Assert.True(client.CaptureTraceFrames);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task FrRead_DoesNotRetryAfterPlcNgResponse()
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
            await stream.WriteAsync(BuildErrorResponse(0xC2, 0x73));
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Throws<ToyopucPlcError>(() => client.ReadFrWords(0, 1));
        await serverTask;
        Assert.Equal(1, Volatile.Read(ref requestCount));
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

        var error = Assert.Throws<ToyopucPlcError>(() =>
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
        Assert.IsNotType<ToyopucOperationOutcomeUnknownException>(error);

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
    public async Task ReadAggregate_SplitsOnlyWhenRequiredAndReturnsCallerOrder()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCommands = new List<byte>();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            var first = await ReadFrameAsync(stream);
            requestCommands.Add(first[4]);
            var firstData = Enumerable.Range(0, 0x0200)
                .SelectMany(static value => new[] { (byte)(value & 0xFF), (byte)(value >> 8) })
                .ToArray();
            await stream.WriteAsync(BuildResponse(0x94, firstData));

            var second = await ReadFrameAsync(stream);
            requestCommands.Add(second[4]);
            await stream.WriteAsync(BuildResponse(0x94, [0x34, 0x12]));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromSeconds(3));

        var values = client.ReadMany("P1-D0000", 0x0201);
        await serverTask;
        Assert.Equal(0x0201, values.Length);
        Assert.Equal(0, values[0]);
        Assert.Equal(0x01FF, values[0x01FF]);
        Assert.Equal(0x1234, values[0x0200]);
        Assert.Equal(new byte[] { 0x94, 0x94 }, requestCommands);
    }

    [Fact]
    public async Task ReadAggregate_HoldsOneFifoTurnAcrossEverySplitRequest()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var firstRequestReceived = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var requestCommands = new List<byte>();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            var first = await ReadFrameAsync(stream);
            requestCommands.Add(first[4]);
            firstRequestReceived.Set();
            releaseFirst.Wait();
            await stream.WriteAsync(BuildResponse(0x94, [0x11, 0x11]));

            var second = await ReadFrameAsync(stream);
            requestCommands.Add(second[4]);
            await stream.WriteAsync(BuildResponse(0xC2, [0x22, 0x22]));

            var queued = await ReadFrameAsync(stream);
            requestCommands.Add(queued[4]);
            await stream.WriteAsync(BuildResponse(0x1C, [0x33, 0x33]));
        });

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(3));
        var aggregate = client.ReadManyAsync("U07FFF", 2);
        Assert.True(firstRequestReceived.Wait(TimeSpan.FromSeconds(2)));
        var queued = client.ReadWordsAsync(0, 1);
        releaseFirst.Set();

        Assert.Equal(new object[] { 0x1111, 0x2222 }, await aggregate);
        Assert.Equal(new[] { 0x3333 }, await queued);
        await serverTask;
        Assert.Equal(new byte[] { 0x94, 0xC2, 0x1C }, requestCommands);
    }

    [Fact]
    public async Task ReadAggregate_DoesNotExposePartialResultsWhenALaterSplitFails()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x94, [0x11, 0x11]));
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildErrorResponse(0xC2, 0x73));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(3));

        Assert.Throws<ToyopucPlcError>(() => client.ReadMany("U07FFF", 2));
        await serverTask;
        Assert.Equal(2UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public void ReadAggregate_PreflightsEveryPlannedEntryBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Profile);
        var valid = client.ResolveDevice("B0100");
        var invalidLaterEntry = valid with
        {
            Text = "BROKEN-LATER-ENTRY",
            BasicAddress = null,
        };

        Assert.Throws<ArgumentException>(() =>
            client.ReadDevices(new object[] { valid, invalidLaterEntry }));
        Assert.False(client.IsOpen);
        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public void GenericAndProtocolWrites_RejectMaskingAndCoercionBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name);

        Assert.Equal("bit", client.ResolveDevice("P1-M0000").Unit);
        Assert.Equal("word", client.ResolveDevice("P1-D0000").Unit);
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-M0000", 0));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-M0000", 1));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-M0000", 2));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-M0000", "1"));
        Assert.ThrowsAny<ArgumentException>(() => client.RelayWrite("P1-L2:N2", "P1-M0000", 1));
        Assert.ThrowsAny<ArgumentException>(() => client.WriteMany(new Dictionary<object, object> { ["P1-M0000"] = 1 }));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-D0000", -1));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-D0000", 65536));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-D0000", true));
        Assert.ThrowsAny<ArgumentException>(() => client.Write("P1-D0000", 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildBitWrite(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildWordWrite(0, [-1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildByteWrite(0, [256]));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task GenericBitWrite_AcceptsBooleanValue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var request = await ReadFrameAsync(server.GetStream());
            Assert.Equal(0x99, request[4]);
            Assert.Equal(1, request[^1]);
            await server.GetStream().WriteAsync(BuildResponse(0x99, []));
        });
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(2));

        client.Write("P1-M0000", true);

        await serverTask;
        listener.Stop();
    }

    [Fact]
    public async Task GenericWriteManyAsync_PreservesBooleanUntilWireEncoding()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var request = await ReadFrameAsync(server.GetStream());
            Assert.Equal(0x99, request[4]);
            Assert.Equal(1, request[^1]);
            await server.GetStream().WriteAsync(BuildResponse(0x99, []));
        });
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(2));

        await client.WriteManyAsync(
            new Dictionary<object, object> { ["P1-M0000"] = true });

        await serverTask;
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public void FixedExtendedSegments_RejectAddressesThatWouldAliasAnotherDevice()
    {
        foreach (var device in new[] { "EX0800", "EX080W", "EX080L" })
        {
            Assert.Throws<ArgumentException>(() =>
                ToyopucDeviceResolver.ResolveDevice(device, ToyopucPlcProfiles.Nano10GxCompatible.Name));
        }
    }

    [Fact]
    public void WireWidthValidators_RejectValuesInsteadOfMaskingThem()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildExtWordRead(256, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ToyopucProtocol.BuildExtMultiRead([], [(256, 0)], []));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.BuildFrRegister(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucAddress.EncodeExNoByteU32(256, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucAddress.EncodeExNoBitU32(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.PackU16LittleEndian(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToyopucProtocol.PackU16LittleEndian(65536));
    }

    [Fact]
    public void LowLevelFloatWrites_RejectNonFiniteValuesBeforeTransport()
    {
        using var client = new ToyopucClient("127.0.0.1", 1, ToyopucTransportMode.Tcp);

        Assert.Throws<ArgumentOutOfRangeException>(() => client.WriteFloat32(0, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.WriteFloat32s(0, [1.0f, float.PositiveInfinity]));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void WriteMany_RejectsDifferentTextForTheSameWireAddressBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name);
        var values = new[]
        {
            new KeyValuePair<object, object>("P1-D0", 1),
            new KeyValuePair<object, object>("P1-D0000", 2),
        };

        Assert.Throws<ToyopucProtocolError>(() => client.WriteMany(values));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public async Task WordRead_RequiresExactResponseLength(int returnedBytes)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[returnedBytes]));
        });
        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Throws<ToyopucProtocolError>(() => client.ReadWords(0, 2));
        await serverTask;
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public async Task GracefulEof_DoesNotRetryAndDistinguishesReadFromWriteOutcome()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using (var read = await listener.AcceptTcpClientAsync())
            {
                _ = await ReadFrameAsync(read.GetStream());
            }

            using var write = await listener.AcceptTcpClientAsync();
            _ = await ReadFrameAsync(write.GetStream());
        });
        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2),
            retries: 1,
            retryDelay: TimeSpan.Zero);

        Assert.Throws<ToyopucTransportError>(() => client.ReadWords(0, 1));
        var writeError = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
            () => client.WriteWords(0, [1]));
        Assert.Equal(ToyopucOutcomeUnknownReason.Transport, writeError.Reason);
        Assert.Equal(2UL, client.TrafficStats.RequestCount);
        await serverTask;
        listener.Stop();
    }

    [Fact]
    public async Task FixedPortUdpSession_IsTaintedByMalformedStateChangingResponseBody()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(BuildResponse(0x32, [0x01, 0x00]), request.RemoteEndPoint);
        });
        using var client = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Udp,
            localPort: localPort,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Throws<ToyopucOperationOutcomeUnknownException>(client.StopScan);
        Assert.Throws<InvalidOperationException>(client.Open);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task TypedWrites_RejectBooleanFractionStringRangeAndNonFiniteBeforeTransport()
    {
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "U", true));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "U", 1.5));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "U", "12"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "U", 65536));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "S", 32768));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "D", -1));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "L", (long)int.MaxValue + 1));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "F", double.PositiveInfinity));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.WriteTypedAsync("P1-D0000", "F", 1e40));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task TypedFloatRead_RejectsNonFinitePlcValue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var stream = server.GetStream();
            var request = await ReadFrameAsync(stream);
            Assert.Equal(0x94, request[4]);
            await stream.WriteAsync(BuildResponse(0x94, [0x00, 0x00, 0xC0, 0x7F]));
        });
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name);

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadTypedAsync("P1-D0000", "F"));
        Assert.False(client.IsOpen);
        await serverTask;
        listener.Stop();
    }

    [Fact]
    public async Task RelayTypedFloatRead_RetiresTransportOnSemanticMalformedResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var stream = server.GetStream();
            var request = await ReadFrameAsync(stream);
            Assert.Equal(0x60, request[4]);
            await stream.WriteAsync(BuildRelayResponse(0x12, 2, 0x94, [0x00, 0x00, 0xC0, 0x7F]));
        });
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(2),
            route: ToyopucRoute.Relay("P1-L2:N2"));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadTypedAsync("P1-D0000", "F"));

        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task RelayReadFrOne_MalformedResponseCompletesBeforeQueuedReadUsesNewTransport()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var firstRequestReceived = new ManualResetEventSlim();
        using var releaseMalformedResponse = new ManualResetEventSlim();
        var requestCommands = new List<byte>();

        var serverTask = Task.Run(async () =>
        {
            using (var malformedServer = await listener.AcceptTcpClientAsync())
            {
                await using var stream = malformedServer.GetStream();
                var request = await ReadFrameAsync(stream);
                requestCommands.Add(request[4]);
                firstRequestReceived.Set();
                releaseMalformedResponse.Wait();
                await stream.WriteAsync(BuildRelayResponse(0x12, 2, 0xC2, [0x34]));
            }

            using var validServer = await listener.AcceptTcpClientAsync();
            await using var validStream = validServer.GetStream();
            var validRequest = await ReadFrameAsync(validStream);
            requestCommands.Add(validRequest[4]);
            await validStream.WriteAsync(BuildRelayResponse(0x12, 2, 0xC2, [0x78, 0x56]));
        });

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            Profile,
            timeout: TimeSpan.FromSeconds(2));

        var malformed = Task.Run(() => client.RelayReadFrOne("P1-L2:N2", "FR000000"));
        Assert.True(firstRequestReceived.Wait(TimeSpan.FromSeconds(2)));
        var queued = Task.Run(() => client.RelayReadFrOne("P1-L2:N2", "FR000000"));
        releaseMalformedResponse.Set();

        await Assert.ThrowsAsync<ToyopucProtocolError>(async () => await malformed);
        Assert.Equal(0x5678, await queued);
        await serverTask;
        Assert.Equal(new byte[] { 0x60, 0x60 }, requestCommands);
    }

    [Fact]
    public async Task DirectHighLevelMalformedStateResponse_IsOutcomeUnknownWithCause()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var stream = server.GetStream();
            var request = await ReadFrameAsync(stream);
            Assert.Equal(0x95, request[4]);
            await stream.WriteAsync(BuildResponse(0x95, [0x00]));
        });
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(2));

        var error = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
            () => client.Write("P1-D0000", 0x1234));

        Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
        Assert.IsType<ToyopucProtocolError>(error.InnerException);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task RelayMalformedStateResponse_IsOutcomeUnknownAndValidResponseRemainsUsable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serverTask = Task.Run(async () =>
        {
            using (var malformedServer = await listener.AcceptTcpClientAsync())
            {
                var stream = malformedServer.GetStream();
                _ = await ReadFrameAsync(stream);
                await stream.WriteAsync(BuildRelayResponse(0x12, 2, 0x1D, [0x00]));
            }

            using (var asyncMalformedServer = await listener.AcceptTcpClientAsync())
            {
                var stream = asyncMalformedServer.GetStream();
                _ = await ReadFrameAsync(stream);
                await stream.WriteAsync(BuildRelayResponse(0x12, 2, 0x1D, [0x00]));
            }

            using var validServer = await listener.AcceptTcpClientAsync();
            var validStream = validServer.GetStream();
            _ = await ReadFrameAsync(validStream);
            await validStream.WriteAsync(BuildRelayResponse(0x12, 2, 0x1D, []));
        });

        using (var malformedClient = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2)))
        {
            var error = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
                () => malformedClient.RelayWriteWords("P1-L2:N2", 0x2000, [0x1234]));
            Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
            Assert.IsType<ToyopucProtocolError>(error.InnerException);
            Assert.False(malformedClient.IsOpen);
        }

        await using (var asyncMalformedClient = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2)))
        {
            var error = await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
                () => asyncMalformedClient.RelayWriteWordsAsync("P1-L2:N2", 0x2000, [0x3456]));
            Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
            Assert.IsType<ToyopucProtocolError>(error.InnerException);
            Assert.False(asyncMalformedClient.IsOpen);
        }

        using (var validClient = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2)))
        {
            validClient.RelayWriteWords("P1-L2:N2", 0x2000, [0x5678]);
            Assert.True(validClient.IsOpen);
        }

        await serverTask;
    }

    [Fact]
    public async Task FixedPortUdpTypedMalformedResponse_TaintsSession()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(BuildResponse(0x94, [0x00, 0x00, 0xC0, 0x7F]), request.RemoteEndPoint);
        });
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Udp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            localPort: localPort,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadTypedAsync("P1-D0000", "F"));

        Assert.Throws<InvalidOperationException>(client.Open);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public void FixedPortUdpRelayPreflightFailure_DoesNotTaintSession()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();
        using var client = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Udp,
            localPort: localPort,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => client.RelayReadWords("P16-L0:N1", 0x2000, 1));
        Assert.Throws<ArgumentException>(
            () => client.RelayCommand(0x12, 2, [0x02, 0x00, 0x1C]));

        client.Open();
        Assert.True(client.IsOpen);
        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public void FixedPortUdpSession_CannotBeReusedAfterUncertainTimeout()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();
        using var client = new ToyopucClient(
            "127.0.0.1",
            endpoint.Port,
            ToyopucTransportMode.Udp,
            localPort: localPort,
            timeout: TimeSpan.FromMilliseconds(50));

        Assert.Throws<ToyopucTimeoutError>(() => client.ReadWords(0, 1));
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.True(client.TrafficStats.TxBytes > 0);
        Assert.Equal(0UL, client.TrafficStats.RxBytes);
        Assert.Throws<InvalidOperationException>(() => client.Open());
        Assert.False(client.IsOpen);
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
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
        var thirdRan = false;
        var third = client.QueueAction(() => thirdRan = true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await second);
        Assert.Equal(0, client.CloseCalls);

        release.Set();
        await first;
        await third;
        Assert.True(thirdRan);
    }

    [Fact]
    public async Task OrdinaryClient_AdmitsOperationsInFifoOrderAcrossFailureAndSupportsReentrancy()
    {
        using var client = new TrackingClient();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var order = new List<int>();

        var first = client.QueueAction(
            () =>
            {
                entered.Set();
                release.Wait();
                order.Add(1);
            });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var second = client.QueueAction(
            () =>
            {
                order.Add(2);
                throw new ToyopucProtocolError("expected test failure");
            });
        var third = client.QueueAction(() => order.Add(3));

        release.Set();
        await first;
        await Assert.ThrowsAsync<ToyopucProtocolError>(async () => await second);
        await third;
        Assert.Equal([1, 2, 3], order);

        order.Clear();
        await client.QueueNestedAsync(order);
        Assert.Equal([1, 2, 3], order);
    }

    [Fact]
    public async Task SeparateOrdinaryClients_ProgressIndependently()
    {
        using var firstClient = new TrackingClient();
        using var secondClient = new TrackingClient();
        using var firstEntered = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var first = firstClient.QueueAction(() => { firstEntered.Set(); release.Wait(); });
        var second = secondClient.QueueAction(() => { secondEntered.Set(); release.Wait(); });

        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(secondEntered.Wait(TimeSpan.FromSeconds(2)));
        release.Set();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task ExecuteExclusiveAsync_DeadlineInitializationFailureReleasesLease()
    {
        await using var client = new TrackingClient();
        ForceTimeoutForDeadlineInitializationTest(client);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<OverflowException>(
                () => client.QueueExclusiveNoopAsync().WaitAsync(TimeSpan.FromSeconds(1)));
        }
    }

    [Fact]
    public async Task ExecuteSynchronousExclusive_DeadlineInitializationFailureReleasesLease()
    {
        await using var client = new TrackingClient();
        ForceTimeoutForDeadlineInitializationTest(client);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<OverflowException>(
                () => Task.Run(client.QueueSynchronousNoop).WaitAsync(TimeSpan.FromSeconds(1)));
        }
    }

    [Fact]
    public async Task RunAsyncLifecycleCore_DeadlineInitializationFailureReleasesLease()
    {
        await using var client = new TrackingClient();
        ForceTimeoutForDeadlineInitializationTest(client);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<OverflowException>(
                () => client.ReadWordsAsync(0, 1).WaitAsync(TimeSpan.FromSeconds(1)));
        }
    }

    [Fact]
    public async Task QueuedWrite_SnapshotsValuesAtAdmission()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var firstRequestReceived = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        byte[]? writeRequest = null;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            firstRequestReceived.Set();
            releaseFirst.Wait();
            await stream.WriteAsync(BuildResponse(0x1C, [0x34, 0x12]));
            writeRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1D, []));
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        var first = client.ReadWordsAsync(0, 1);
        Assert.True(firstRequestReceived.Wait(TimeSpan.FromSeconds(2)));
        var values = new List<int> { 0x1111 };
        var write = client.WriteWordsAsync(0x2000, values);
        values[0] = 0x2222;

        releaseFirst.Set();
        Assert.Equal(new[] { 0x1234 }, await first);
        await write;
        await serverTask;
        Assert.Equal(ToyopucProtocol.BuildWordWrite(0x2000, [0x1111]), writeRequest);
    }

    [Fact]
    public async Task SynchronousCall_CannotOverlapActiveAsyncTransaction()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var firstRequestReceived = new ManualResetEventSlim();
        using var inspectSecondSend = new ManualResetEventSlim();
        using var inspected = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var secondSentBeforeRelease = false;

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            firstRequestReceived.Set();
            inspectSecondSend.Wait();
            await Task.Delay(100);
            secondSentBeforeRelease = stream.DataAvailable;
            inspected.Set();
            releaseFirst.Wait();
            await stream.WriteAsync(BuildResponse(0x1C, [0x11, 0x11]));
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, [0x22, 0x22]));
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        var asyncRead = client.ReadWordsAsync(0, 1);
        Assert.True(firstRequestReceived.Wait(TimeSpan.FromSeconds(2)));
        var syncRead = Task.Run(() => client.ReadWords(1, 1));
        inspectSecondSend.Set();
        Assert.True(inspected.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(secondSentBeforeRelease);

        releaseFirst.Set();
        Assert.Equal(new[] { 0x1111 }, await asyncRead);
        Assert.Equal(new[] { 0x2222 }, await syncRead);
        await serverTask;
    }

    [Theory]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseOrDispose_RejectsActiveAndQueuedGenerationWithoutSecondSend(bool dispose)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var requestReceived = new ManualResetEventSlim();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            requestReceived.Set();
            var buffer = new byte[1];
            try
            {
                _ = await stream.ReadAsync(buffer);
            }
            catch (IOException)
            {
                // The client deliberately retires this transport generation.
            }
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        var active = client.ReadWordsAsync(0, 1);
        Assert.True(requestReceived.Wait(TimeSpan.FromSeconds(2)));
        var queued = client.ReadWordsAsync(1, 1);

        if (dispose)
            client.Dispose();
        else
            client.Close();

        var activeError = await Record.ExceptionAsync(async () => await active);
        var queuedError = await Record.ExceptionAsync(async () => await queued);
        if (dispose)
        {
            Assert.IsType<ObjectDisposedException>(activeError);
            Assert.IsType<ObjectDisposedException>(queuedError);
        }
        else
        {
            Assert.IsType<ToyopucConnectionClosedException>(activeError);
            Assert.IsType<ToyopucConnectionClosedException>(queuedError);
        }
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        await serverTask;
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

        var cancellationError = await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
            async () => await write);
        Assert.Equal(ToyopucOutcomeUnknownReason.Cancellation, cancellationError.Reason);
        Assert.False(client.IsOpen);
        Assert.Throws<ToyopucNotConnectedException>(() => client.ReadWords(0x2000, 1));
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
    public async Task WriteTimeoutAfterSend_ReportsUnknownOutcomeForSyncAndAsyncCalls()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            for (var request = 0; request < 2; request++)
            {
                using var server = await listener.AcceptTcpClientAsync();
                await using var stream = server.GetStream();
                _ = await ReadFrameAsync(stream);
                await Task.Delay(TimeSpan.FromMilliseconds(150));
            }
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromMilliseconds(50));

        var syncError = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
            () => client.WriteWords(0x2000, [0x1234]));
        Assert.Equal(ToyopucOutcomeUnknownReason.Timeout, syncError.Reason);
        Assert.False(client.IsOpen);

        var asyncError = await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
            async () => await client.WriteWordsAsync(0x2000, [0x5678]));
        Assert.Equal(ToyopucOutcomeUnknownReason.Timeout, asyncError.Reason);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public async Task TcpFragmentedResponse_IsReassembledWithinOneTransaction()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var response = BuildResponse(0x1C, [0x34, 0x12]);
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            foreach (var fragment in response.Chunk(2))
            {
                await stream.WriteAsync(fragment);
                await Task.Delay(TimeSpan.FromMilliseconds(5));
            }
        });
        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2));

        var words = await client.ReadWordsAsync(0x2000, 1);
        Assert.Equal([0x1234], words);
        await serverTask;
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public async Task TcpTrickleCannotRestartTheSingleTransactionDeadline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var response = BuildResponse(0x1C, [0x34, 0x12]);

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            try
            {
                foreach (var value in response)
                {
                    await stream.WriteAsync(new[] { value });
                    await Task.Delay(TimeSpan.FromMilliseconds(60));
                }
            }
            catch (IOException)
            {
                // Deadline retirement closes the stream while the test peer is still trickling.
            }
            catch (SocketException)
            {
            }
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromMilliseconds(120));
        var deadlineEvents = new List<(ToyopucSocketDeadlineDirection Direction, int Milliseconds)>();
        client.SocketDeadlineAppliedHook = (direction, milliseconds) =>
            deadlineEvents.Add((direction, milliseconds));
        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<ToyopucTimeoutError>(() => client.ReadWords(0x2000, 1));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(300), $"Elapsed: {stopwatch.Elapsed}");
        var receiveDeadlines = deadlineEvents
            .Where(static item => item.Direction == ToyopucSocketDeadlineDirection.Receive)
            .ToArray();
        Assert.True(receiveDeadlines.Length >= 2);
        AssertDeadlineValuesDoNotIncrease(receiveDeadlines);
        Assert.True(receiveDeadlines[^1].Milliseconds < receiveDeadlines[0].Milliseconds);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task MismatchedResponseCommand_IsUnknownOnlyForStateChangingCalls()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using (var writeServer = await listener.AcceptTcpClientAsync())
            {
                await using var stream = writeServer.GetStream();
                _ = await ReadFrameAsync(stream);
                await stream.WriteAsync(BuildResponse(0x1C, []));
            }

            using var readServer = await listener.AcceptTcpClientAsync();
            await using var readStream = readServer.GetStream();
            _ = await ReadFrameAsync(readStream);
            await readStream.WriteAsync(BuildResponse(0x1D, []));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2));

        var malformedError = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
            () => client.WriteWords(0x2000, [0x1234]));
        Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, malformedError.Reason);
        var readError = Assert.Throws<ToyopucProtocolError>(() => client.ReadWords(0x2000, 1));
        Assert.IsNotType<ToyopucOperationOutcomeUnknownException>(readError);
        await serverTask;
    }

    [Theory]
    [InlineData(ToyopucTransportMode.Tcp, false, false)]
    [InlineData(ToyopucTransportMode.Tcp, false, true)]
    [InlineData(ToyopucTransportMode.Tcp, true, false)]
    [InlineData(ToyopucTransportMode.Tcp, true, true)]
    [InlineData(ToyopucTransportMode.Udp, false, false)]
    [InlineData(ToyopucTransportMode.Udp, false, true)]
    [InlineData(ToyopucTransportMode.Udp, true, false)]
    [InlineData(ToyopucTransportMode.Udp, true, true)]
    public async Task DataBearingNgCommandMismatchIsCorrelatedBeforePlcError(
        ToyopucTransportMode transport,
        bool stateChanging,
        bool asynchronous)
    {
        using var listener = transport == ToyopucTransportMode.Tcp
            ? new TcpListener(IPAddress.Loopback, 0)
            : null;
        using var udp = transport == ToyopucTransportMode.Udp
            ? new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))
            : null;
        listener?.Start();
        var port = transport == ToyopucTransportMode.Tcp
            ? ((IPEndPoint)listener!.LocalEndpoint).Port
            : ((IPEndPoint)udp!.Client.LocalEndPoint!).Port;
        var requestCommand = stateChanging ? 0x1D : 0x1C;
        var serverTask = Task.Run(async () =>
        {
            if (transport == ToyopucTransportMode.Tcp)
            {
                using (var first = await listener!.AcceptTcpClientAsync())
                {
                    await using var stream = first.GetStream();
                    _ = await ReadFrameAsync(stream);
                    await stream.WriteAsync(BuildNgResponse(requestCommand ^ 1, [0x5A]));
                }
                using var second = await listener!.AcceptTcpClientAsync();
                await using var secondStream = second.GetStream();
                _ = await ReadFrameAsync(secondStream);
                await secondStream.WriteAsync(BuildResponse(0x1C, [0x34, 0x12]));
            }
            else
            {
                var first = await udp!.ReceiveAsync();
                await udp.SendAsync(BuildNgResponse(requestCommand ^ 1, [0x5A]), first.RemoteEndPoint);
                var second = await udp.ReceiveAsync();
                await udp.SendAsync(BuildResponse(0x1C, [0x34, 0x12]), second.RemoteEndPoint);
            }
        });
        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            transport,
            timeout: TimeSpan.FromSeconds(2));

        if (stateChanging)
        {
            var error = asynchronous
                ? await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
                    () => client.WriteWordsAsync(0x2000, [0x1234]))
                : Assert.Throws<ToyopucOperationOutcomeUnknownException>(
                    () => client.WriteWords(0x2000, [0x1234]));
            Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
            Assert.IsType<ToyopucProtocolError>(error.InnerException);
        }
        else if (asynchronous)
        {
            await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadWordsAsync(0x2000, 1));
        }
        else
        {
            Assert.Throws<ToyopucProtocolError>(() => client.ReadWords(0x2000, 1));
        }

        Assert.False(client.IsOpen);
        Assert.Throws<ToyopucNotConnectedException>(() => client.ReadWords(0x2000, 1));
        await client.OpenAsync();
        var reopenedWords = await client.ReadWordsAsync(0x2000, 1);
        Assert.Equal(new[] { 0x1234 }, reopenedWords);
        await serverTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceClientPreservesDataBearingNgCommandMismatchClassification(bool stateChanging)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCommand = stateChanging ? 0x1D : 0x1C;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildNgResponse(requestCommand ^ 1, [0x5A]));
        });
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            ToyopucPlcProfiles.Nano10GxCompatible.Name,
            timeout: TimeSpan.FromSeconds(2));

        if (stateChanging)
        {
            var error = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
                () => client.Write("P1-D0000", 0x1234));
            Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
            Assert.IsType<ToyopucProtocolError>(error.InnerException);
        }
        else
        {
            Assert.Throws<ToyopucProtocolError>(() => client.ReadOne("P1-D0000"));
        }

        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Theory]
    [InlineData(ToyopucTransportMode.Tcp, false)]
    [InlineData(ToyopucTransportMode.Tcp, true)]
    [InlineData(ToyopucTransportMode.Udp, false)]
    [InlineData(ToyopucTransportMode.Udp, true)]
    public async Task NgResponseCommandRulesPreserveMatchingDataAndNoDataSpecialForm(
        ToyopucTransportMode transport,
        bool noDataSpecialForm)
    {
        using var listener = transport == ToyopucTransportMode.Tcp
            ? new TcpListener(IPAddress.Loopback, 0)
            : null;
        using var udp = transport == ToyopucTransportMode.Udp
            ? new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))
            : null;
        listener?.Start();
        var port = transport == ToyopucTransportMode.Tcp
            ? ((IPEndPoint)listener!.LocalEndpoint).Port
            : ((IPEndPoint)udp!.Client.LocalEndPoint!).Port;
        var response = noDataSpecialForm
            ? BuildNgResponse(0x5A, [])
            : BuildNgResponse(0x1C, [0x5A]);
        var serverTask = Task.Run(async () =>
        {
            if (transport == ToyopucTransportMode.Tcp)
            {
                using var server = await listener!.AcceptTcpClientAsync();
                await using var stream = server.GetStream();
                _ = await ReadFrameAsync(stream);
                await stream.WriteAsync(response);
            }
            else
            {
                var request = await udp!.ReceiveAsync();
                await udp.SendAsync(response, request.RemoteEndPoint);
            }
        });
        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            transport,
            timeout: TimeSpan.FromSeconds(2));

        var error = Assert.Throws<ToyopucPlcError>(() => client.ReadWords(0x2000, 1));
        Assert.Contains("error_code=0x5A", error.Message, StringComparison.Ordinal);
        Assert.True(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task StateChangingTrailingResponseData_IsMalformedUnknownOutcome()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1D, [0x00]));
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2));
        var error = Assert.Throws<ToyopucOperationOutcomeUnknownException>(
            () => client.WriteWords(0x2000, [0x1234]));

        Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
        Assert.False(client.IsOpen);
        await serverTask;
    }

    [Fact]
    public async Task ClosingAnActiveWriteReportsOutcomeUnknownWithClosedReason()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var requestReceived = new ManualResetEventSlim();

        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            await using var stream = server.GetStream();
            _ = await ReadFrameAsync(stream);
            requestReceived.Set();
            var probe = new byte[1];
            try
            {
                _ = await stream.ReadAsync(probe);
            }
            catch (IOException)
            {
            }
        });

        await using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(3));
        var write = client.WriteWordsAsync(0x2000, [0x1234]);
        Assert.True(requestReceived.Wait(TimeSpan.FromSeconds(2)));
        client.Close();

        var error = await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(
            async () => await write);
        Assert.Equal(ToyopucOutcomeUnknownReason.Closed, error.Reason);
        Assert.False(client.IsOpen);
        await serverTask;
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
    [Trait("Category", "LinuxNetworkContractSmoke")]
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
            using var connectStarted = new ManualResetEventSlim();
            client.SocketConnectStartedHook = _ => connectStarted.Set();

            var open = client.OpenAsync(cancellation.Token);
            Assert.True(connectStarted.Wait(TimeSpan.FromSeconds(2)));
            Assert.False(client.IsOpen);
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

    [Fact]
    public void TypedResponseViewBorrowsFrameWhilePublicResponseOwnsPayload()
    {
        var raw = BuildResponse(0x1C, [0x34, 0x12]);
        var view = ToyopucProtocol.ParseResponseView(raw);
        var ownedFromView = view.ToOwned();
        var publicResponse = ToyopucProtocol.ParseResponse(raw);

        raw[5] = 0x78;

        Assert.Equal(0x78, view.Data.Span[0]);
        Assert.Equal(0x34, ownedFromView.Data[0]);
        Assert.Equal(0x34, publicResponse.Data[0]);
        publicResponse.Data[0] = 0x9A;
        Assert.Equal(0x78, raw[5]);
    }

    [Fact]
    public void RelayViewUnwrapsMultipleLayersWithoutCopyingInnerFrames()
    {
        var final = BuildResponse(0x1C, [0x34, 0x12]);
        var inner = BuildResponse(
            0x60,
            [0x34, 0x03, 0x00, 0x06, .. final.AsSpan(2)]);
        var raw = BuildResponse(
            0x60,
            [0x12, 0x02, 0x00, 0x06, .. inner.AsSpan(2)]);

        var (layers, response) = ToyopucRelay.UnwrapRelayResponseChainView(
            ToyopucProtocol.ParseResponseView(raw));

        Assert.Equal(2, layers.Count);
        Assert.Equal((0x12, 0x02), (layers[0].LinkNo, layers[0].StationNo));
        Assert.Equal((0x34, 0x03), (layers[1].LinkNo, layers[1].StationNo));
        Assert.True(response.HasValue);
        raw[^1] = 0x56;
        Assert.Equal(0x56, response.Value.Data.Span[1]);
    }

    [Fact]
    public async Task NativePreparedSequenceBuildsEachPayloadOnceAndDoesNotReplayItsDelegate()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(
            async () =>
            {
                using var server = await listener.AcceptTcpClientAsync();
                var stream = server.GetStream();
                for (var index = 0; index < 2; index++)
                {
                    var request = await ReadFrameAsync(stream);
                    await stream.WriteAsync(BuildResponse(request[4], [(byte)(index + 1), 0x00]));
                }
            });

        await using var client = new NativeSequenceProbeClient(port);
        var values = await client.ReadPreparedPairAsync();
        await serverTask;

        Assert.Equal([1, 2], values);
        Assert.Equal(2, client.PayloadBuildCount);
        Assert.Equal(1, client.SequenceInvocationCount);
    }

    private static void AssertDeadlineValuesDoNotIncrease(
        IReadOnlyList<(ToyopucSocketDeadlineDirection Direction, int Milliseconds)> events)
    {
        Assert.NotEmpty(events);
        Assert.All(events, static item => Assert.True(item.Milliseconds > 0));
        for (int index = 1; index < events.Count; index++)
        {
            Assert.True(
                events[index].Milliseconds <= events[index - 1].Milliseconds,
                $"Deadline increased from {events[index - 1].Milliseconds} ms to {events[index].Milliseconds} ms.");
        }
    }

    private static void ForceTimeoutForDeadlineInitializationTest(ToyopucClient client)
    {
        var timeoutField = typeof(ToyopucClient).GetField(
            "<Timeout>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(timeoutField);
        timeoutField.SetValue(client, TimeSpan.MaxValue);
        Assert.Equal(TimeSpan.MaxValue, client.Timeout);
    }

    private sealed class TrackingClient()
        : ToyopucClient("127.0.0.1", 1025, ToyopucTransportMode.Tcp)
    {
        public int CloseCalls { get; private set; }

        public Task QueueAction(Action action, CancellationToken cancellationToken = default) =>
            RunAsync(action, cancellationToken);

        public Task QueueExclusiveNoopAsync() =>
            ExecuteExclusiveAsync(static _ => Task.CompletedTask);

        public void QueueSynchronousNoop() =>
            ExecuteSynchronousExclusive(static () => { });

        public Task QueueNestedAsync(List<int> order) =>
            ExecuteExclusiveAsync(
                async token =>
                {
                    order.Add(1);
                    await RunAsync(() => order.Add(2), token);
                    order.Add(3);
                });

        public override void Close()
        {
            CloseCalls++;
            base.Close();
        }
    }

    private sealed class NativeSequenceProbeClient(int port)
        : ToyopucClient(
            "127.0.0.1",
            port,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2))
    {
        public int PayloadBuildCount { get; private set; }
        public int SequenceInvocationCount { get; private set; }

        public Task<int[]> ReadPreparedPairAsync()
        {
            var payloads = new[] { PreparePayload(0), PreparePayload(1) };
            return RunNativeSequenceAsync(
                async exchange =>
                {
                    SequenceInvocationCount++;
                    var results = new int[payloads.Length];
                    for (var index = 0; index < payloads.Length; index++)
                    {
                        var response = await exchange(payloads[index], false);
                        results[index] = response.Data.Span[0] | (response.Data.Span[1] << 8);
                    }
                    return results;
                },
                outcomeUnknownAfterSend: false);
        }

        private byte[] PreparePayload(int address)
        {
            PayloadBuildCount++;
            return ToyopucProtocol.BuildWordRead(address, 1);
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

    private static byte[] BuildRelayResponse(int linkNo, int stationNo, int command, byte[] data)
    {
        var innerLength = checked(1 + data.Length);
        return BuildResponse(
            0x60,
            [
                (byte)linkNo,
                (byte)(stationNo & 0xFF),
                (byte)(stationNo >> 8),
                0x06,
                (byte)(innerLength & 0xFF),
                (byte)(innerLength >> 8),
                (byte)command,
                .. data,
            ]);
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

    private static byte[] BuildNgResponse(int command, byte[] data)
    {
        var length = checked(1 + data.Length);
        return
        [
            ToyopucProtocol.FtResponse,
            0x10,
            (byte)(length & 0xFF),
            (byte)(length >> 8),
            (byte)command,
            .. data,
        ];
    }
}
