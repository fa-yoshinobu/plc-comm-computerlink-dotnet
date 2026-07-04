using System.Globalization;
using System.IO;
using System.Net.Sockets;
using PlcComm.Toyopuc;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: dotnet run --project examples/PlcComm.Toyopuc.PollingReconnectSample -- <host> <port> <tcp|udp> <profile> [device] [dtype] [interval-seconds]");
    Console.Error.WriteLine("Example: dotnet run --project examples/PlcComm.Toyopuc.PollingReconnectSample -- 192.168.250.100 1025 tcp toyopuc:plus:extended P1-D0100 U 1");
    return;
}

var host = args[0];
var port = int.Parse(args[1], CultureInfo.InvariantCulture);
var transport = Enum.Parse<ToyopucTransportMode>(args[2], ignoreCase: true);
var plcProfile = args[3];
var device = args.ElementAtOrDefault(4) ?? "P1-D0100";
var dtype = args.ElementAtOrDefault(5) ?? "U";
var interval = TimeSpan.FromSeconds(ParseDouble(args.ElementAtOrDefault(6), 1.0));
var initialBackoff = TimeSpan.FromSeconds(1);
var maxBackoff = TimeSpan.FromSeconds(30);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

QueuedToyopucDeviceClient? client = null;
var backoff = initialBackoff;
var connectedOnce = false;

try
{
    while (!shutdown.IsCancellationRequested)
    {
        if (client is null)
        {
            Log("reconnecting", $"{transport.ToString().ToLowerInvariant()} {host}:{port} profile={plcProfile}");
            try
            {
                var options = new ToyopucConnectionOptions(host)
                {
                    Port = port,
                    Transport = transport,
                    Timeout = TimeSpan.FromSeconds(3),
                    PlcProfile = plcProfile,
                };
                client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options, shutdown.Token);
            }
            catch (Exception ex) when (IsRetryable(ex) && !shutdown.IsCancellationRequested)
            {
                Log("reconnecting", $"connect failed: {ex.Message}; retry in {backoff.TotalSeconds:0.0}s");
                await Delay(backoff, shutdown.Token);
                backoff = NextBackoff(backoff, maxBackoff);
                continue;
            }

            Log(connectedOnce ? "recovered" : "connected", $"{device}:{dtype}");
            connectedOnce = true;
            backoff = initialBackoff;
        }

        try
        {
            var value = await client.ReadTypedAsync(device, dtype, shutdown.Token);
            Log("read", $"{device}:{dtype}={FormatValue(value)}");
            await Task.Delay(interval, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex) when (IsRetryable(ex) && !shutdown.IsCancellationRequested)
        {
            Log("lost", ex.Message);
            await DisposeClientAsync(client);
            client = null;
            Log("reconnecting", $"retry in {backoff.TotalSeconds:0.0}s");
            await Delay(backoff, shutdown.Token);
            backoff = NextBackoff(backoff, maxBackoff);
        }
    }
}
finally
{
    await DisposeClientAsync(client);
}

Log("closed", "stopped");

static bool IsRetryable(Exception ex)
{
    if (ex is IOException or SocketException or TimeoutException or OperationCanceledException or ToyopucTimeoutError)
    {
        return true;
    }

    if (ex is ToyopucProtocolError
        && (ex.Message == "Connection closed while receiving" || ex.Message == "Connection closed while sending"))
    {
        return true;
    }

    return ex is ToyopucError && ex.Message == "Socket error";
}

static async Task DisposeClientAsync(QueuedToyopucDeviceClient? client)
{
    if (client is not null)
    {
        await client.DisposeAsync();
    }
}

static async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(delay, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static TimeSpan NextBackoff(TimeSpan current, TimeSpan max)
    => TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2.0, max.TotalSeconds));

static double ParseDouble(string? value, double fallback)
    => string.IsNullOrWhiteSpace(value) ? fallback : double.Parse(value, CultureInfo.InvariantCulture);

static string FormatValue(object value)
    => value switch
    {
        float f => f.ToString("G9", CultureInfo.InvariantCulture),
        double d => d.ToString("G17", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

static void Log(string state, string message)
    => Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss} [{state}] {message}");
