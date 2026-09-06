using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using PlcComm.Toyopuc;

namespace PlcComm.Toyopuc.Tests;

public sealed class ToyopucClientExtensionsTests
{
    private const double LocalTestTimeoutSeconds = 3.0;
    private const string Pc10Profile = "toyopuc:pc10g:pc10";
    private const string Pc3JgSeparateProfile = "toyopuc:pc3jg:pc3-separate";

    [Fact]
    public async Task WriteBitInWordAsync_AlwaysReadsThenWritesOneWord()
    {
        var read = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 1);
        var write = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [8]);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildResponse(0x94, [0x08, 0x00])
                : frame.SequenceEqual(write)
                    ? BuildResponse(0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        await client.WriteBitInWordAsync("P1-D0000", 3, true);

        Assert.Equal(
            [Convert.ToHexString(read), Convert.ToHexString(write)],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task WriteBitInWord_SynchronousSurfaceUsesTheSameTwoRequestContract()
    {
        var read = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 1);
        var write = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [0]);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildResponse(0x94, [0x08, 0x00])
                : frame.SequenceEqual(write)
                    ? BuildResponse(0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        client.WriteBitInWord("P1-D0000", 3, false);

        Assert.Equal(
            [Convert.ToHexString(read), Convert.ToHexString(write)],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task WriteBitInWord_RejectsInvalidTargetBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteBitInWordAsync("P1-M0000", 0, true));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteBitInWordAsync("P1-D0000", 16, true));
        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task WriteBitInWordAsync_PreservesTheConfiguredRelayRouteForBothRequests()
    {
        var innerRead = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 1);
        var innerWrite = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [8]);
        var read = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerRead);
        var write = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerWrite);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildRelayResponse(0x12, 2, 0x94, [0x00, 0x00])
                : frame.SequenceEqual(write)
                    ? BuildRelayResponse(0x12, 2, 0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            route: ToyopucRoute.Relay("P1-L2:N2"));

        await client.WriteBitInWordAsync("P1-D0000", 3, true);

        Assert.Equal(
            [Convert.ToHexString(read), Convert.ToHexString(write)],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public void ToyopucAddress_Normalize_PreservesPrefixAndSuffix()
    {
        var normalized = ToyopucAddress.Normalize(
            "p1-d0000l",
            "toyopuc:pc10g:pc10");

        Assert.Equal("P1-D0000L", normalized);
    }

    [Fact]
    public async Task OpenAndConnectAsync_ReturnsOrdinaryClientWithRelayConfiguration()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = listener.AcceptTcpClientAsync();
        await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(
            new ToyopucConnectionOptions(
                "127.0.0.1",
                port,
                ToyopucTransportMode.Tcp,
                Pc10Profile,
                ToyopucRoute.Relay("P1-L2:N2"))
            {
            });

        using var server = await acceptTask;

        Assert.True(client.IsOpen);
        Assert.True(client.UsesRelay);
        Assert.Equal("toyopuc:pc10g:pc10", client.PlcProfile);
        Assert.Single(client.RelayHops!);
        Assert.Equal((0x12, 2), client.RelayHops![0]);
    }

    [Fact]
    public async Task OrdinaryHighLevelMethod_UsesClientRelayRoute()
    {
        await using var server = new ScriptedToyopucServer(frame =>
        {
            Assert.Equal(0x60, frame[4]);
            return BuildResponse(
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
                ]);
        });
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            route: ToyopucRoute.Relay("P1-L2:N2"));

        var value = await client.ReadOneAsync("B0100");

        Assert.Equal(0x1234, value);
        Assert.Single(server.ReceivedFrames);
    }

    [Fact]
    public async Task ReadDWordsSingleRequestAsync_UsesExactlyOneRequest()
    {
        await using var server = new ScriptedToyopucServer(frame =>
        {
            if (frame.SequenceEqual(ToyopucProtocol.BuildWordRead(0x6100, 6)))
                return BuildResponse(0x1C, new byte[] { 0x01, 0x00, 0x01, 0x00, 0x02, 0x00, 0x02, 0x00, 0x03, 0x00, 0x03, 0x00 });
            return BuildResponse(0x10, new byte[] { 0x40 });
        });

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            plcProfile: Pc10Profile);

        var values = await client.ReadDWordsSingleRequestAsync("B0100", 3);

        Assert.Equal(new uint[] { 0x00010001, 0x00020002, 0x00030003 }, values);
        Assert.Equal(
            new[] { Convert.ToHexString(ToyopucProtocol.BuildWordRead(0x6100, 6)) },
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ReadWordsSingleRequestAsync_UsesOneExtWordReadForProgramDevices()
    {
        var expected = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2);
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x94, new byte[] { 0x34, 0x12, 0x78, 0x56 }));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        var values = await client.ReadWordsSingleRequestAsync("P1-D0000", 2);

        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, values);
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_DottedDIsBitIndex13NotDwordSuffix()
    {
        var expected = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 1);
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x94, new byte[] { 0x00, 0x20 }));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        var values = await client.ReadNamedAsync(["P1-D0000.D"]);

        Assert.True(Assert.IsType<bool>(values["P1-D0000.D"]));
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_InvalidBitIndex_ThrowsBeforeTransportRead()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["P1-D0000.10"]));
    }

    [Fact]
    public async Task ReadNamedAsync_MultipleAddressesRejectsImplicitSplit()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(["P1-D0000", "P1-D0001"]));
    }

    [Fact]
    public async Task ReadNamedAsync_ReadsCompatibleMultipleAddressesInOneRequest()
    {
        var expected = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2);
        await using var server = new ScriptedToyopucServer(_ =>
            BuildResponse(0x94, [0x34, 0x12, 0xFF, 0xFF]));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        var values = await client.ReadNamedAsync(["P1-D0000:U", "P1-D0001:S"]);

        Assert.Equal(["P1-D0000:U", "P1-D0001:S"], values.Keys);
        Assert.Equal((ushort)0x1234, values["P1-D0000:U"]);
        Assert.Equal((short)-1, values["P1-D0001:S"]);
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task PollAsync_ReadsTheCompatibleAddressSetInOneRequestPerCycle()
    {
        var expected = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2);
        await using var server = new ScriptedToyopucServer(_ =>
            BuildResponse(0x94, [0x34, 0x12, 0xFF, 0xFF]));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        await using var enumerator = client
            .PollAsync(["P1-D0000:U", "P1-D0001:S"], TimeSpan.FromSeconds(1))
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(["P1-D0000:U", "P1-D0001:S"], enumerator.Current.Keys);
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_RejectsDuplicatesIncompatibleFamiliesAndCapacityBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() =>
            client.ReadNamedAsync(["P1-D0000:U", "P1-D0000:U"]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(() =>
            client.ReadNamedAsync(["P1-D0000:U", "B0000:U"]));
        var overCapacity = Enumerable.Range(0, 129)
            .Select(static index => $"B{index * 2:X4}:U")
            .ToArray();
        await Assert.ThrowsAsync<ToyopucProtocolError>(() => client.ReadNamedAsync(overCapacity));

        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task ExplicitRelayWriteBitInWord_UsesTheFixedRouteForReadAndWrite()
    {
        var innerRead = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 1);
        var innerWrite = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [8]);
        var read = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerRead);
        var write = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerWrite);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildRelayResponse(0x12, 2, 0x94, [0x00, 0x00])
                : frame.SequenceEqual(write)
                    ? BuildRelayResponse(0x12, 2, 0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        await client.RelayWriteBitInWordAsync("P1-L2:N2", "P1-D0000", 3, true);
        client.RelayWriteBitInWord("P1-L2:N2", "P1-D0000", 3, true);

        Assert.Equal(
            [Convert.ToHexString(read), Convert.ToHexString(write), Convert.ToHexString(read), Convert.ToHexString(write)],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ProgramTimerCounterOperations_UseA0ProgramAndNativeSelectors()
    {
        var read = ToyopucProtocol.BuildProgramTimerCounterRead(0x01, 0x0600);
        var writeBoth = ToyopucProtocol.BuildProgramTimerCounterWriteBoth(0x01, 0x0600, 10, 8);
        var writePreset = ToyopucProtocol.BuildProgramTimerCounterWritePreset(0x01, 0x0600, 11);
        var writeCurrent = ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(0x01, 0x0600, 9);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildResponse(0xA0, [0x01, 0x40, 0x00, 0x0A, 0x00, 0x08, 0x00])
                : frame.SequenceEqual(writeBoth)
                    ? BuildResponse(0xA0, [0x01, 0x41, 0x00])
                    : frame.SequenceEqual(writePreset)
                        ? BuildResponse(0xA0, [0x01, 0x42, 0x00])
                        : frame.SequenceEqual(writeCurrent)
                            ? BuildResponse(0xA0, [0x01, 0x43, 0x00])
                            : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        var values = await client.ReadProgramTimerCounterValuesAsync("P1-T000");
        await client.WriteProgramTimerCounterValuesAsync("P1-T000", 10, 8);
        await client.WriteProgramTimerCounterPresetAsync("P1-C000", 11);
        await client.WriteProgramTimerCounterCurrentAsync("P1-C000", 9);
        var syncValues = client.ReadProgramTimerCounterValues("P1-T000");
        client.WriteProgramTimerCounterValues("P1-T000", 10, 8);
        client.WriteProgramTimerCounterPreset("P1-C000", 11);
        client.WriteProgramTimerCounterCurrent("P1-C000", 9);

        Assert.Equal(new TimerCounterValues(10, 8), values);
        Assert.Equal(new TimerCounterValues(10, 8), syncValues);
        Assert.Equal(
            new[] { read, writeBoth, writePreset, writeCurrent, read, writeBoth, writePreset, writeCurrent }
                .Select(Convert.ToHexString),
            server.ReceivedFrames.ToArray());
        Assert.Throws<ToyopucProtocolError>(() =>
            ToyopucProtocol.ParseProgramTimerCounterValues(
                [0x01, 0x40, 0x00, 0x0A, 0x00, 0x08, 0x00, 0x00],
                0x01));
        Assert.Throws<ToyopucProtocolError>(() =>
            ToyopucProtocol.ValidateProgramTimerCounterWriteResponse([0x01, 0x42, 0x00, 0x00], 0x01, 0x42));
    }

    [Fact]
    public async Task ProgramTimerCounterOperations_UseExplicitRelayForReadAndWrite()
    {
        var innerRead = ToyopucProtocol.BuildProgramTimerCounterRead(0x02, 0x0600);
        var innerWrite = ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(0x02, 0x0600, 9);
        var read = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerRead);
        var write = ToyopucProtocol.BuildRelayCommand(0x12, 2, innerWrite);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildRelayResponse(0x12, 2, 0xA0, [0x02, 0x40, 0x00, 0x0B, 0x00, 0x09, 0x00])
                : frame.SequenceEqual(write)
                    ? BuildRelayResponse(0x12, 2, 0xA0, [0x02, 0x43, 0x00])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        var values = await client.RelayReadProgramTimerCounterValuesAsync("P1-L2:N2", "P2-C000");
        await client.RelayWriteProgramTimerCounterCurrentAsync("P1-L2:N2", "P2-C000", 9);
        var syncValues = client.RelayReadProgramTimerCounterValues("P1-L2:N2", "P2-C000");
        client.RelayWriteProgramTimerCounterCurrent("P1-L2:N2", "P2-C000", 9);

        Assert.Equal(new TimerCounterValues(11, 9), values);
        Assert.Equal(new TimerCounterValues(11, 9), syncValues);
        Assert.Equal(
            [Convert.ToHexString(read), Convert.ToHexString(write), Convert.ToHexString(read), Convert.ToHexString(write)],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ProgramTimerCounterOperations_RejectInvalidTargetsAndValuesBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            ToyopucTransportMode.Tcp,
            Pc10Profile,
            timeout: TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadProgramTimerCounterValuesAsync("T000"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadProgramTimerCounterValuesAsync("P1-D0000"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadProgramTimerCounterValuesAsync("P1-T1000"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteProgramTimerCounterPresetAsync("P1-T000", -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteProgramTimerCounterCurrentAsync("P1-C000", 0x10000));

        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_UsesOneExtWordWriteForProgramDevices()
    {
        var expected = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, new[] { 0x1234, 0x5678 });
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x95, Array.Empty<byte>()));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        await client.WriteWordsSingleRequestAsync("P1-D0000", new ushort[] { 0x1234, 0x5678 });

        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task SingleRequestHelpers_RejectASecondSegmentBeforeTransport()
    {
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            1,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(2),
            plcProfile: Pc10Profile);

        await Assert.ThrowsAsync<ToyopucProtocolError>(
            () => client.ReadWordsSingleRequestAsync("B0000", 513));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteWordsSingleRequestAsync("B0000", new ushort[513]));
        await Assert.ThrowsAsync<ToyopucProtocolError>(
            () => client.ReadDWordsSingleRequestAsync("B0000", 257));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteDWordsSingleRequestAsync("B0000", new uint[257]));

        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task DeprecatedWordAliases_DelegateToTheCanonicalWireContract()
    {
        var read = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2);
        var write = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [0x1234, 0x5678]);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildResponse(0x94, [0x34, 0x12, 0x78, 0x56])
                : frame.SequenceEqual(write)
                    ? BuildResponse(0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        var canonical = await client.ReadWordsSingleRequestAsync("P1-D0000", 2);
#pragma warning disable CS0618
        var compatibility = await client.ReadWordsAsync("P1-D0000", 2);
#pragma warning restore CS0618
        await client.WriteWordsSingleRequestAsync("P1-D0000", [0x1234, 0x5678]);
#pragma warning disable CS0618
        await client.WriteWordsAsync("P1-D0000", [0x1234, 0x5678]);
#pragma warning restore CS0618

        Assert.Equal(canonical, compatibility);
        Assert.Equal(
            [
                Convert.ToHexString(read),
                Convert.ToHexString(read),
                Convert.ToHexString(write),
                Convert.ToHexString(write),
            ],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task DeprecatedDWordAliases_DelegateToTheCanonicalWireContract()
    {
        var read = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 4);
        var write = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, [0x3344, 0x1122, 0x7788, 0x5566]);
        await using var server = new ScriptedToyopucServer(frame =>
            frame.SequenceEqual(read)
                ? BuildResponse(0x94, [0x44, 0x33, 0x22, 0x11, 0x88, 0x77, 0x66, 0x55])
                : frame.SequenceEqual(write)
                    ? BuildResponse(0x95, [])
                    : throw new InvalidOperationException($"Unexpected frame: {Convert.ToHexString(frame)}"));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode,
            plcProfile: Pc10Profile);

        var canonical = await client.ReadDWordsSingleRequestAsync("P1-D0000", 2);
#pragma warning disable CS0618
        var compatibility = await ToyopucDeviceClientExtensions.ReadDWordsAsync(client, "P1-D0000", 2);
#pragma warning restore CS0618
        await client.WriteDWordsSingleRequestAsync("P1-D0000", [0x11223344, 0x55667788]);
#pragma warning disable CS0618
        await ToyopucDeviceClientExtensions.WriteDWordsAsync(client, "P1-D0000", [0x11223344, 0x55667788]);
#pragma warning restore CS0618

        Assert.Equal(new uint[] { 0x11223344, 0x55667788 }, canonical);
        Assert.Equal(canonical, compatibility);
        Assert.Equal(
            [
                Convert.ToHexString(read),
                Convert.ToHexString(read),
                Convert.ToHexString(write),
                Convert.ToHexString(write),
            ],
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public void DWordSingleRequestExtensions_HaveDistinctCanonicalIdentityAndObsoleteAliases()
    {
        var instanceRead = typeof(ToyopucDeviceClient).GetMethod(
            nameof(ToyopucDeviceClient.ReadDWordsAsync),
            [typeof(object), typeof(int), typeof(CancellationToken)]);
        var instanceWrite = typeof(ToyopucDeviceClient).GetMethod(
            nameof(ToyopucDeviceClient.WriteDWordsAsync),
            [typeof(object), typeof(IEnumerable<uint>), typeof(CancellationToken)]);
        var canonicalRead = typeof(ToyopucDeviceClientExtensions).GetMethod(
            nameof(ToyopucDeviceClientExtensions.ReadDWordsSingleRequestAsync),
            [typeof(ToyopucDeviceClient), typeof(string), typeof(int), typeof(CancellationToken)]);
        var canonicalWrite = typeof(ToyopucDeviceClientExtensions).GetMethod(
            nameof(ToyopucDeviceClientExtensions.WriteDWordsSingleRequestAsync),
            [typeof(ToyopucDeviceClient), typeof(string), typeof(IReadOnlyList<uint>), typeof(CancellationToken)]);
        var compatibilityRead = typeof(ToyopucDeviceClientExtensions).GetMethod(
            "ReadDWordsAsync",
            [typeof(ToyopucDeviceClient), typeof(string), typeof(int), typeof(CancellationToken)]);
        var compatibilityWrite = typeof(ToyopucDeviceClientExtensions).GetMethod(
            "WriteDWordsAsync",
            [typeof(ToyopucDeviceClient), typeof(string), typeof(IReadOnlyList<uint>), typeof(CancellationToken)]);

        Assert.NotNull(instanceRead);
        Assert.NotNull(instanceWrite);
        Assert.NotNull(canonicalRead);
        Assert.NotNull(canonicalWrite);
        Assert.NotNull(compatibilityRead);
        Assert.NotNull(compatibilityWrite);
        Assert.Equal(typeof(ToyopucDeviceClient), instanceRead.DeclaringType);
        Assert.Equal(typeof(ToyopucDeviceClient), instanceWrite.DeclaringType);
        Assert.Equal(typeof(ToyopucDeviceClientExtensions), canonicalRead.DeclaringType);
        Assert.Equal(typeof(ToyopucDeviceClientExtensions), canonicalWrite.DeclaringType);
        Assert.NotNull(compatibilityRead.GetCustomAttributes(typeof(ObsoleteAttribute), false).SingleOrDefault());
        Assert.NotNull(compatibilityWrite.GetCustomAttributes(typeof(ObsoleteAttribute), false).SingleOrDefault());
    }

    [Fact]
    public void ComputerLinkHighLevelSurface_DoesNotExposeBitSingleRequestHelpers()
    {
        var names = typeof(ToyopucDeviceClientExtensions)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(static method => method.Name)
            .ToArray();

        Assert.DoesNotContain("ReadBitsSingleRequestAsync", names);
        Assert.DoesNotContain("WriteBitsSingleRequestAsync", names);
    }

    [Fact]
    public async Task ReadWordsSingleRequestAsync_ExtMultiFallbackUsesByteAddresses()
    {
        var expected = ToyopucProtocol.BuildExtMultiRead(
            Array.Empty<(int No, int Bit, int Address)>(),
            Array.Empty<(int No, int Address)>(),
            [(0x09, 0xFFFE), (0x0A, 0x0000)]);
        await using var server = new ScriptedToyopucServer(_ =>
            BuildResponse(0x98, new byte[] { 0x34, 0x12, 0x78, 0x56 }));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucAddressingOptions.Pc3JgPc3Separate,
            Pc3JgSeparateProfile,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        var values = await client.ReadWordsSingleRequestAsync("EB07FFF", 2);

        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, values);
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_ExtMultiFallbackUsesByteAddresses()
    {
        var expected = ToyopucProtocol.BuildExtMultiWrite(
            Array.Empty<(int No, int Bit, int Address, int Value)>(),
            Array.Empty<(int No, int Address, int Value)>(),
            [(0x09, 0xFFFE, 0x1234), (0x0A, 0x0000, 0x5678)]);
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x99, Array.Empty<byte>()));
        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            ToyopucAddressingOptions.Pc3JgPc3Separate,
            Pc3JgSeparateProfile,
            ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        await client.WriteWordsSingleRequestAsync("EB07FFF", new ushort[] { 0x1234, 0x5678 });

        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task SendRaw_UdpAcceptsLargeDatagramResponse()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var responseData = Enumerable.Range(0, 9000).Select(static index => (byte)(index & 0xFF)).ToArray();
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            var response = BuildResponse(0x1C, responseData);
            await server.SendAsync(response, response.Length, request.RemoteEndPoint);
        });

        using var client = new ToyopucClient(
            "127.0.0.1",
            port,
            transport: ToyopucTransportMode.Udp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        Assert.Equal(default, client.TrafficStats);
        var response = client.SendRaw(0x1C, Array.Empty<byte>());
        await serverTask;

        Assert.Equal(0x1C, response.Cmd);
        Assert.Equal(responseData, response.Data);
        Assert.Equal(
            new ToyopucTrafficStats(
                1,
                (ulong)client.LastTx!.Length,
                (ulong)client.LastRx!.Length),
            client.TrafficStats);
        ToyopucTrafficStats stats = client.TrafficStats;
        client.Close();
        Assert.Equal(stats, client.TrafficStats);
    }

    private static byte[] BuildResponse(int cmd, byte[] data)
    {
        var length = 1 + data.Length;
        return new[] { (byte)0x80, (byte)0x00, (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)(cmd & 0xFF) }
            .Concat(data)
            .ToArray();
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

    private sealed class ScriptedToyopucServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<byte[], byte[]> _responseFactory;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public ConcurrentQueue<string> ReceivedFrames { get; } = new();

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public ScriptedToyopucServer(Func<byte[], byte[]> responseFactory)
        {
            _responseFactory = responseFactory;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _serverTask = Task.Run(RunAsync);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch
            {
            }

            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                await using var stream = client.GetStream();
                while (!_cts.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync(stream, _cts.Token);
                    if (frame.Length == 0)
                        break;

                    ReceivedFrames.Enqueue(Convert.ToHexString(frame));
                    var response = _responseFactory(frame);
                    await stream.WriteAsync(response, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            var read = await stream.ReadAsync(header, cancellationToken);
            if (read == 0)
                return Array.Empty<byte>();

            while (read < header.Length)
            {
                var chunk = await stream.ReadAsync(header.AsMemory(read), cancellationToken);
                if (chunk == 0)
                    throw new IOException("Unexpected end of stream");
                read += chunk;
            }

            var length = header[2] | (header[3] << 8);
            var body = new byte[length];
            var offset = 0;
            while (offset < body.Length)
            {
                var chunk = await stream.ReadAsync(body.AsMemory(offset), cancellationToken);
                if (chunk == 0)
                    throw new IOException("Unexpected end of stream");
                offset += chunk;
            }

            return header.Concat(body).ToArray();
        }
    }
}
