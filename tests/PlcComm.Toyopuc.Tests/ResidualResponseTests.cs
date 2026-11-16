using System.Net;
using System.Net.Sockets;

namespace PlcComm.Toyopuc.Tests;

public sealed class ResidualResponseTests
{
    public static TheoryData<ToyopucTransportMode, bool, bool, int> ResidualCases
    {
        get
        {
            var cases = new TheoryData<ToyopucTransportMode, bool, bool, int>();
            foreach (var transport in new[] { ToyopucTransportMode.Tcp, ToyopucTransportMode.Udp })
                foreach (var useAsync in new[] { false, true })
                    foreach (var write in new[] { false, true })
                        foreach (var length in transport == ToyopucTransportMode.Udp ? new[] { 0, 1, 7 } : new[] { 1, 7 })
                            cases.Add(transport, useAsync, write, length);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ResidualCases))]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public async Task BeforeSend_RejectsResidualWithoutSendingOrRetrying(
        ToyopucTransportMode transport, bool useAsync, bool write, int extraLength)
    {
        using var server = new LoopbackServer(transport);
        using var client = server.CreateClient();
        Socket? clientSocket = null;
        client.ConnectedSocketHook = socket => clientSocket = socket;
        var first = Invoke(client, useAsync, write: false);
        await server.ReceiveAsync();
        await server.SendAsync(Response(write: false));
        await first;

        await server.SendAsync(Extra(extraLength));
        Assert.NotNull(clientSocket);
        Assert.True(SpinWait.SpinUntil(
            () => clientSocket.Poll(0, SelectMode.SelectRead), TimeSpan.FromSeconds(3)));

        await Assert.ThrowsAsync<ToyopucProtocolError>(() => Invoke(client, useAsync, write));
        Assert.False(client.IsOpen);
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.Equal(0, server.PendingRequestBytes);
        await Assert.ThrowsAsync<ToyopucNotConnectedException>(() => Invoke(client, useAsync, write));
        Assert.Equal(1UL, client.TrafficStats.RequestCount);

        server.ResetPeer();
        if (useAsync)
            await client.OpenAsync();
        else
            client.Open();
        var recovered = Invoke(client, useAsync, write: false);
        await server.ReceiveAsync();
        await server.SendAsync(Response(write: false));
        await recovered;
        Assert.Equal(2UL, client.TrafficStats.RequestCount);
    }

    [Theory]
    [MemberData(nameof(ResidualCases))]
    [Trait("Category", "LinuxNetworkContractSmoke")]
    public async Task AfterReceive_RejectsExtraAndPreservesWriteOutcomeUnknown(
        ToyopucTransportMode transport, bool useAsync, bool write, int extraLength)
    {
        using var server = new LoopbackServer(transport);
        using var client = server.CreateClient();
        using var responsesQueued = new ManualResetEventSlim();
        Socket? clientSocket = null;
        client.ConnectedSocketHook = socket => clientSocket = socket;
        client.SocketDeadlineAppliedHook = (direction, _) =>
        {
            if (direction == ToyopucSocketDeadlineDirection.Receive)
                Assert.True(responsesQueued.Wait(TimeSpan.FromSeconds(3)));
        };
        var operation = Invoke(client, useAsync, write);
        await server.ReceiveAsync();
        var response = Response(write);
        var extra = Extra(extraLength);
        if (transport == ToyopucTransportMode.Tcp)
        {
            await server.SendAsync([.. response, .. extra]);
            Assert.NotNull(clientSocket);
            Assert.True(SpinWait.SpinUntil(
                () => clientSocket.Available >= response.Length + extra.Length, TimeSpan.FromSeconds(3)));
        }
        else
        {
            await server.SendAsync(response);
            await server.SendAsync(extra);
        }
        responsesQueued.Set();

        if (write)
        {
            var error = await Assert.ThrowsAsync<ToyopucOperationOutcomeUnknownException>(() => operation);
            Assert.Equal(ToyopucOutcomeUnknownReason.MalformedResponse, error.Reason);
            Assert.IsType<ToyopucProtocolError>(error.InnerException);
        }
        else
        {
            await Assert.ThrowsAsync<ToyopucProtocolError>(() => operation);
        }
        Assert.False(client.IsOpen);
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.Equal(0, server.PendingRequestBytes);
        await Assert.ThrowsAsync<ToyopucNotConnectedException>(() => Invoke(client, useAsync, write));
    }

    private static Task Invoke(ToyopucClient client, bool useAsync, bool write)
        => useAsync
            ? write ? client.WriteWordsAsync(1, [0x5678]) : ReadAsync(client)
            : Task.Run(() =>
            {
                if (write)
                    client.WriteWords(1, [0x5678]);
                else
                    Assert.Equal([0x1234], client.ReadWords(1, 1));
            });

    private static async Task ReadAsync(ToyopucClient client)
    {
        var values = await client.ReadWordsAsync(1, 1);
        Assert.Equal([0x1234], values);
    }

    private static byte[] Response(bool write)
        => write ? [0x80, 0, 1, 0, 0x1d] : [0x80, 0, 3, 0, 0x1c, 0x34, 0x12];

    private static byte[] Extra(int length) => Response(write: false)[..length];

    private sealed class LoopbackServer : IDisposable
    {
        private readonly ToyopucTransportMode _transport;
        private readonly Socket _socket;
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(10));
        private Socket? _peer;
        private EndPoint _remote = new IPEndPoint(IPAddress.Any, 0);

        public LoopbackServer(ToyopucTransportMode transport)
        {
            _transport = transport;
            _socket = new Socket(AddressFamily.InterNetwork,
                transport == ToyopucTransportMode.Tcp ? SocketType.Stream : SocketType.Dgram,
                transport == ToyopucTransportMode.Tcp ? ProtocolType.Tcp : ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            if (transport == ToyopucTransportMode.Tcp)
                _socket.Listen(1);
        }

        public int PendingRequestBytes => (_peer ?? _socket).Available;

        public ToyopucClient CreateClient() => new("127.0.0.1",
            ((IPEndPoint)_socket.LocalEndPoint!).Port, _transport,
            timeout: TimeSpan.FromSeconds(3), retries: 2, retryDelay: TimeSpan.Zero);

        public async Task ReceiveAsync()
        {
            var buffer = new byte[256];
            if (_transport == ToyopucTransportMode.Tcp)
            {
                _peer ??= await _socket.AcceptAsync(_stop.Token);
                using var stream = new NetworkStream(_peer, ownsSocket: false);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, 4), _stop.Token);
                await stream.ReadExactlyAsync(buffer.AsMemory(4, buffer[2] | (buffer[3] << 8)), _stop.Token);
            }
            else
            {
                var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, _remote, _stop.Token);
                _remote = received.RemoteEndPoint;
            }
        }

        public async Task SendAsync(byte[] data)
        {
            if (_transport == ToyopucTransportMode.Tcp)
            {
                using var stream = new NetworkStream(_peer!, ownsSocket: false);
                await stream.WriteAsync(data, _stop.Token);
            }
            else
                await _socket.SendToAsync(data, SocketFlags.None, _remote, _stop.Token);
        }

        public void ResetPeer()
        {
            _peer?.Dispose();
            _peer = null;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _peer?.Dispose();
            _socket.Dispose();
            _stop.Dispose();
        }
    }
}
