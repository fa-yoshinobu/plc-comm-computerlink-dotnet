using System.Net;
using System.Net.Sockets;

namespace PlcComm.Toyopuc.Tests;

public sealed class ComputerlinkDefectRegressionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Pc10MultiRead_LongResponseLengthUsesFourBytesPerPoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var responseData = new byte[]
        {
            0x00, 0x00, 0x00, 0x02,
            0x11, 0x22, 0x33, 0x44,
            0x55, 0x66, 0x77, 0x88,
        };
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(0xC4, request[4]);
            return BuildResponse(0xC4, responseData);
        });

        await using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TestTimeout);

        var actual = await client.Pc10MultiReadAsync([0x00, 0x00, 0x00, 0x02]);

        Assert.Equal(responseData, actual);
        await serverTask;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task DirectPc10MultiRead_RejectsLongResponseLengthMismatch(int dataDelta)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var responseData = new byte[12 + dataDelta];
        responseData[3] = 0x02;
        var serverTask = RespondOnceAsync(listener, _ => BuildResponse(0xC4, responseData));

        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TestTimeout);

        Assert.Throws<ToyopucProtocolError>(() =>
            client.Pc10MultiRead([0x00, 0x00, 0x00, 0x02]));

        await serverTask;
    }

    [Fact]
    public async Task DirectPc10MultiRead_UsesOneSnapshotWhenCallerMutatesPayloadDuringCommunication()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var payload = new byte[] { 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x02, 0x04, 0x00 };
        var responseData = new byte[] { 0x00, 0x00, 0x02, 0x00, 0x34, 0x12, 0x78, 0x56 };
        var expectedRequest = new byte[]
        {
            ToyopucProtocol.FtCommand, 0x00, 0x0D, 0x00, 0xC4,
            0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x04, 0x00,
            0x00, 0x02, 0x04, 0x00,
        };
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(expectedRequest, request);
            payload[2] = 0x00;
            payload[3] = 0x01;
            return BuildResponse(0xC4, responseData);
        });

        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TestTimeout);

        var actual = await Task.Run(() => client.Pc10MultiRead(payload));

        Assert.Equal(responseData, actual);
        await serverTask;
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 8)]
    [InlineData(3, 16)]
    public void RelayPc10MultiRead_ExpectedLengthUsesFourBytesPerLongPoint(
        byte longCount,
        int expectedLength)
    {
        var request = ToyopucProtocol.ParseRelayInnerRequest(
            ToyopucProtocol.BuildPc10MultiRead([0x00, 0x00, 0x00, longCount]));

        Assert.Equal(expectedLength, request.ExpectedReadResponseLength);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task RelayPc10MultiRead_RejectsLongResponseLengthMismatch(int dataDelta)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var responseData = new byte[12 + dataDelta];
        responseData[3] = 0x02;
        var serverTask = RespondOnceAsync(listener, _ =>
            BuildRelayResponse(0x12, 2, 0xC4, responseData));

        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TestTimeout);

        Assert.Throws<ToyopucProtocolError>(() =>
            client.SendViaRelayRead(
                "P1-L2:N2",
                ToyopucProtocol.BuildPc10MultiRead([0x00, 0x00, 0x00, 0x02])));

        await serverTask;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Pc10MultiRead_RejectsEachMismatchedCountWithTheExpectedTotalLength(int countIndex)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestCounts = new byte[] { 0x08, 0x02, 0x01, 0x01 };
        var responseCounts = requestCounts.ToArray();
        responseCounts[countIndex]--;
        var responseData = responseCounts.Concat(new byte[9]).ToArray();
        var serverTask = RespondOnceAsync(listener, _ => BuildResponse(0xC4, responseData));

        using var client = new ToyopucClient(
            "127.0.0.1", port, ToyopucTransportMode.Tcp, timeout: TestTimeout);

        var error = Assert.Throws<ToyopucProtocolError>(() => client.Pc10MultiRead(requestCounts));

        Assert.Contains("point counts", error.Message, StringComparison.Ordinal);
        await serverTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedDirectPc10Read_RejectsMismatchedCountForSyncAndAsync(bool useAsync)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(0xC4, request[4]);
            return BuildResponse(0xC4, [0x00, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56]);
        });

        await using var client = CreateNanoClient(port);
        if (useAsync)
        {
            await Assert.ThrowsAsync<ToyopucProtocolError>(
                () => client.ReadDevicesAsync(["U08000", "U08100"]));
        }
        else
        {
            Assert.Throws<ToyopucProtocolError>(
                () => client.ReadDevices(["U08000", "U08100"]));
        }

        await serverTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedRelayPc10Read_RejectsMismatchedCountForSyncAndAsync(bool useAsync)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(0x60, request[4]);
            return BuildRelayResponse(
                0x12,
                2,
                0xC4,
                [0x00, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56]);
        });

        await using var client = CreateNanoClient(port);
        if (useAsync)
        {
            await Assert.ThrowsAsync<ToyopucProtocolError>(
                () => client.RelayReadDevicesAsync("P1-L2:N2", ["U08000", "U08100"]));
        }
        else
        {
            Assert.Throws<ToyopucProtocolError>(
                () => client.RelayReadDevices("P1-L2:N2", ["U08000", "U08100"]));
        }

        await serverTask;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SparsePc10WordWrite_UsesInterleavedC5ForDirectRelaySyncAndAsync(
        bool useRelay,
        bool useAsync)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var client = CreateNanoClient(port);
        var expectedRequest = useRelay
            ? new byte[]
            {
                0x00, 0x00, 0x19, 0x00, 0x60,
                0x12, 0x02, 0x00, 0x05, 0x11, 0x00, 0xC5,
                0x00, 0x00, 0x02, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x34, 0x12,
                0x00, 0x02, 0x04, 0x00, 0x78, 0x56,
                0x00,
            }
            : new byte[]
            {
                0x00, 0x00, 0x11, 0x00, 0xC5,
                0x00, 0x00, 0x02, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x34, 0x12,
                0x00, 0x02, 0x04, 0x00, 0x78, 0x56,
            };
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(expectedRequest, request);
            return useRelay
                ? BuildRelayResponse(0x12, 2, 0xC5, [])
                : BuildResponse(0xC5, []);
        });
        var writes = new[]
        {
            new KeyValuePair<object, object>("U08000", 0x1234),
            new KeyValuePair<object, object>("U08100", 0x5678),
        };

        if (useRelay)
        {
            if (useAsync)
                await client.RelayWriteManyAsync("P1-L2:N2", writes);
            else
                client.RelayWriteMany("P1-L2:N2", writes);
        }
        else if (useAsync)
        {
            await client.WriteManyAsync(writes);
        }
        else
        {
            client.WriteMany(writes);
        }

        await serverTask;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Pc10BitWrite_IsReachableForDirectRelaySyncAndAsync(
        bool useRelay,
        bool useAsync)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var client = CreateNanoClient(port);
        var first = ManualPc10Bit(0x04000000, "manual-bit-0");
        var second = ManualPc10Bit(0x04000001, "manual-bit-1");
        var inner = new byte[]
        {
            0x00, 0x00, 0x0F, 0x00, 0xC5,
            0x02, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x04, 0x01,
            0x01, 0x00, 0x00, 0x04, 0x00,
        };
        var expectedRequest = useRelay
            ? ToyopucProtocol.BuildRelayNested([(0x12, 2)], inner)
            : inner;
        var serverTask = RespondOnceAsync(listener, request =>
        {
            Assert.Equal(expectedRequest, request);
            return useRelay
                ? BuildRelayResponse(0x12, 2, 0xC5, [])
                : BuildResponse(0xC5, []);
        });
        var writes = new[]
        {
            new KeyValuePair<object, object>(first, true),
            new KeyValuePair<object, object>(second, false),
        };

        if (useRelay)
        {
            if (useAsync)
                await client.RelayWriteManyAsync("P1-L2:N2", writes);
            else
                client.RelayWriteMany("P1-L2:N2", writes);
        }
        else if (useAsync)
        {
            await client.WriteManyAsync(writes);
        }
        else
        {
            client.WriteMany(writes);
        }

        await serverTask;
    }

    [Fact]
    public async Task FrCommitValidationNamesTheFourPublicOperationsWithoutSending()
    {
        await using var client = CreateNanoClient(1);

        var direct = Assert.Throws<ArgumentException>(() => client.CommitFrBlock("P1-D0000"));
        Assert.Contains("CommitFrBlock()", direct.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("CommitFr()", direct.Message, StringComparison.Ordinal);

        var relay = Assert.Throws<ArgumentException>(
            () => client.RelayCommitFrBlock("P1-L2:N2", "P1-D0000"));
        Assert.Contains("RelayCommitFrBlock()", relay.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RelayCommitFr()", relay.Message, StringComparison.Ordinal);

        var directAsync = await Assert.ThrowsAsync<ArgumentException>(
            () => client.CommitFrBlockAsync("P1-D0000"));
        Assert.Contains("CommitFrBlock()", directAsync.Message, StringComparison.Ordinal);

        var relayAsync = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RelayCommitFrBlockAsync("P1-L2:N2", "P1-D0000"));
        Assert.Contains("RelayCommitFrBlock()", relayAsync.Message, StringComparison.Ordinal);

        Assert.Equal(0UL, client.TrafficStats.RequestCount);
    }

    private static ToyopucDeviceClient CreateNanoClient(int port)
        => new(
            "127.0.0.1",
            port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TestTimeout,
            addressingOptions: ToyopucAddressingOptions.Nano10GxCompatible,
            plcProfile: ToyopucPlcProfiles.Nano10GxCompatible.Name);

    private static ResolvedDevice ManualPc10Bit(int address32, string text)
        => new(text, "pc10-bit", "bit", "manual", address32, Address32: address32)
        {
            PlcProfile = ToyopucPlcProfiles.Nano10GxCompatible.Name,
        };

    private static async Task RespondOnceAsync(
        TcpListener listener,
        Func<byte[], byte[]> responseFactory)
    {
        using var server = await listener.AcceptTcpClientAsync();
        await using var stream = server.GetStream();
        var request = await ReadFrameAsync(stream);
        await stream.WriteAsync(responseFactory(request));
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

    private static byte[] BuildRelayResponse(
        int linkNo,
        int stationNo,
        int command,
        byte[] data)
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
}
