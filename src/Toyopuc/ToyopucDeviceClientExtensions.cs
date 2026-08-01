using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Toyopuc;

/// <summary>
/// High-level typed, named, polling, and contiguous-range operations for
/// <see cref="ToyopucDeviceClient"/>.
/// </summary>
/// <remarks>
/// Typed and contiguous-range methods issue exactly one protocol request.
/// <see cref="ReadNamedAsync"/> and each <see cref="PollAsync"/> cycle accept
/// exactly one named address and therefore issue one request. Only
/// <see cref="WriteBitInWordAsync"/> is a multi-request helper: it performs an
/// explicit read followed by a write while holding one local client FIFO turn.
/// </remarks>
public static class ToyopucDeviceClientExtensions
{
    /// <summary>Reads one typed value using exactly one protocol request.</summary>
    public static Task<object> ReadTypedAsync(this ToyopucDeviceClient client, string device, string dtype, CancellationToken ct = default)
        => client.ExecuteExclusiveAsync(token => ReadTypedCoreAsync(client, client.RelayHops, device, dtype, token), ct);

    /// <summary>Writes one typed value using exactly one protocol request.</summary>
    public static Task WriteTypedAsync(this ToyopucDeviceClient client, string device, string dtype, object value, CancellationToken ct = default)
        => client.ExecuteExclusiveAsync(
            token => WriteTypedCoreAsync(client, client.RelayHops, device, dtype, value, token),
            ct,
            outcomeUnknownAfterSend: true);

    /// <summary>Sets or clears one bit in a word by an explicit read-modify-write sequence.</summary>
    /// <remarks>
    /// The read and write occupy one FIFO turn on this client, so its other
    /// operations cannot interleave. They remain two PLC requests and are not
    /// PLC-atomic: another client, PLC logic, or external writer can change the
    /// word between them. Applications that require atomic coordination must
    /// implement it in the PLC contract.
    /// </remarks>
    public static Task WriteBitInWordAsync(this ToyopucDeviceClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
        => client.ExecuteExclusiveAsync(token => WriteBitInWordCoreAsync(client, client.RelayHops, device, bitIndex, value, token), ct);

    /// <summary>Reads exactly one named address using one protocol request.</summary>
    /// <remarks>Multiple named addresses are rejected before transport; split reads must be explicit.</remarks>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(this ToyopucDeviceClient client, IEnumerable<string> addresses, CancellationToken ct = default)
    {
        var addrList = addresses.ToArray();
        return client.ExecuteExclusiveAsync(token => ReadNamedCoreAsync(client, client.RelayHops, addrList, token), ct);
    }

    /// <summary>Repeatedly reads exactly one named address, one request per cycle.</summary>
    /// <remarks>Each cycle is independent; no atomicity is implied across polling cycles.</remarks>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this ToyopucDeviceClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        interval = ToyopucTimerValidation.RequirePositive(interval, nameof(interval), "Polling interval");
        var addrList = addresses.ToArray();
        ValidateNamedAddresses(addrList);
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ExecuteExclusiveAsync(
                token => ReadNamedCoreAsync(client, client.RelayHops, addrList, token),
                ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Reads a contiguous word range using exactly one protocol request.</summary>
    public static async Task<ushort[]> ReadWordsAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
    {
        return await client.ExecuteExclusiveAsync(
            token => ReadWordsSingleRequestCoreAsync(client, client.RelayHops, device, count, token),
            ct).ConfigureAwait(false);
    }

    /// <summary>Reads a contiguous double-word range using exactly one protocol request.</summary>
    public static Task<uint[]> ReadDWordsAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteExclusiveAsync(
            token => ReadDWordsSingleRequestCoreAsync(client, client.RelayHops, device, count, token),
            ct);

    /// <summary>Writes a contiguous word range using exactly one protocol request.</summary>
    public static Task WriteWordsAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
    {
        var snapshot = values.ToArray();
        return client.ExecuteExclusiveAsync(
            token => WriteWordsSingleRequestCoreAsync(client, client.RelayHops, device, snapshot, token),
            ct,
            outcomeUnknownAfterSend: true);
    }

    /// <summary>Writes a contiguous double-word range using exactly one protocol request.</summary>
    public static Task WriteDWordsAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
    {
        var snapshot = ExpandDWords(values.ToArray());
        return client.ExecuteExclusiveAsync(
            token => WriteWordsSingleRequestCoreAsync(client, client.RelayHops, device, snapshot, token),
            ct,
            outcomeUnknownAfterSend: true);
    }

    internal static Task<ushort[]> ReadWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteExclusiveAsync(
            token => ReadWordsSingleRequestCoreAsync(client, client.RelayHops, device, count, token),
            ct);

    internal static Task WriteWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
    {
        var snapshot = values.ToArray();
        return client.ExecuteExclusiveAsync(
            token => WriteWordsSingleRequestCoreAsync(client, client.RelayHops, device, snapshot, token),
            ct,
            outcomeUnknownAfterSend: true);
    }

    private static async Task<object> ReadTypedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, string dtype, CancellationToken ct)
    {
        switch (NormalizeDType(dtype))
        {
            case "F":
                {
                    float value = BitConverter.Int32BitsToSingle(unchecked((int)(await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]));
                    if (!float.IsFinite(value))
                        throw new ToyopucProtocolError("PLC returned a non-finite float32 value.");
                    return value;
                }
            case "D":
                return (await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
            case "L":
                return unchecked((int)(await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]);
            case "S":
                return unchecked((short)(await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]);
            default:
                return (await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
        }
    }

    private static Task WriteTypedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, string dtype, object value, CancellationToken ct)
    {
        switch (NormalizeDType(dtype))
        {
            case "F":
                {
                    float single = RequireFiniteSingle(value);
                    return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([unchecked((uint)BitConverter.SingleToInt32Bits(single))]), ct);
                }
            case "D":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([(uint)RequireUnsignedIntegral(value, uint.MaxValue, "D")]), ct);
            case "L":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([unchecked((uint)(int)RequireSignedIntegral(value, int.MinValue, int.MaxValue, "L"))]), ct);
            case "S":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, [unchecked((ushort)(short)RequireSignedIntegral(value, short.MinValue, short.MaxValue, "S"))], ct);
            default:
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, [(ushort)RequireUnsignedIntegral(value, ushort.MaxValue, "U")], ct);
        }
    }

    private static long RequireSignedIntegral(object value, long minimum, long maximum, string dtype)
    {
        long candidate = value switch
        {
            sbyte v => v,
            byte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v when v <= long.MaxValue => (long)v,
            _ => throw new ArgumentException($"{dtype} requires an integer value; Boolean, fractional, and string values are not accepted.", nameof(value)),
        };
        if (candidate < minimum || candidate > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"{dtype} value must be in the range {minimum}..{maximum}.");
        return candidate;
    }

    private static ulong RequireUnsignedIntegral(object value, ulong maximum, string dtype)
    {
        ulong candidate = value switch
        {
            sbyte v when v >= 0 => (ulong)v,
            byte v => v,
            short v when v >= 0 => (ulong)v,
            ushort v => v,
            int v when v >= 0 => (ulong)v,
            uint v => v,
            long v when v >= 0 => (ulong)v,
            ulong v => v,
            sbyte or short or int or long => throw new ArgumentOutOfRangeException(nameof(value), $"{dtype} value must be non-negative."),
            _ => throw new ArgumentException($"{dtype} requires an integer value; Boolean, fractional, and string values are not accepted.", nameof(value)),
        };
        if (candidate > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"{dtype} value must be in the range 0..{maximum}.");
        return candidate;
    }

    private static float RequireFiniteSingle(object value)
    {
        if (value is bool or string || value is not (sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal))
            throw new ArgumentException("F requires a finite numeric value; Boolean and string values are not accepted.", nameof(value));
        float result;
        try
        {
            result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "F value is outside the finite float32 range.");
        }
        if (!float.IsFinite(result))
            throw new ArgumentOutOfRangeException(nameof(value), "F value must be finite and representable as float32.");
        return result;
    }

    private static async Task WriteBitInWordCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int bitIndex, bool value, CancellationToken ct)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        ToyopucDeviceClient.RequireGenericWriteDevice(client.ResolveDevice(device));
        ushort current = (await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
        int raw = value ? current | (1 << bitIndex) : current & ~(1 << bitIndex);
        await WriteWordsSingleRequestCoreAsync(client, relayHops, device, [(ushort)(raw & 0xFFFF)], ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedCoreAsync(ToyopucDeviceClient client, object? relayHops, IEnumerable<string> addresses, CancellationToken ct)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        if (addrList.Count != 1)
        {
            throw new ToyopucProtocolError(
                "ReadNamedAsync requires exactly one named address per call. Split the operation into explicit calls when multiple requests are intentional.");
        }

        var result = new Dictionary<string, object>();
        foreach (var address in addrList)
        {
            var (baseAddr, dtype, bitIdx) = ParseLogicalAddress(address);
            if (dtype == "BIT_IN_WORD")
            {
                if (!bitIdx.HasValue)
                    throw new ToyopucProtocolError($"Bit index is required for bit-in-word address '{address}'.");
                var raw = (await ReadWordsSingleRequestCoreAsync(client, relayHops, baseAddr, 1, ct).ConfigureAwait(false))[0];
                result[address] = ((raw >> bitIdx.Value) & 1) != 0;
            }
            else
            {
                result[address] = await ReadTypedCoreAsync(client, relayHops, baseAddr, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    private static async Task<ushort[]> ReadWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, CancellationToken ct)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");

        var devices = BuildSequentialWordDevices(client, device, count);
        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
            throw new ToyopucProtocolError("Single-request word access cannot cross incompatible protocol groups.");

        return relayHops is null
            ? await ReadWordsSingleRequestDirectAsync(client, devices, group, ct).ConfigureAwait(false)
            : await ReadWordsSingleRequestViaRelayAsync(client, relayHops, devices, group, ct).ConfigureAwait(false);
    }

    private static async Task<uint[]> ReadDWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, CancellationToken ct)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var words = await ReadWordsSingleRequestCoreAsync(client, relayHops, device, checked(count * 2), ct).ConfigureAwait(false);
        return PackDWords(words);
    }

    private static async Task WriteWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");

        var devices = BuildSequentialWordDevices(client, device, values.Count);
        ToyopucDeviceClient.RequireGenericWriteDevice(devices[0]);
        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
            throw new ToyopucProtocolError("Single-request word write cannot cross incompatible protocol groups.");

        if (relayHops is null)
        {
            await WriteWordsSingleRequestDirectAsync(client, devices, group, values, ct).ConfigureAwait(false);
            return;
        }

        await WriteWordsSingleRequestViaRelayAsync(client, relayHops, devices, group, values, ct).ConfigureAwait(false);
    }

    private static async Task<ushort[]> ReadWordsSingleRequestDirectAsync(ToyopucDeviceClient client, IReadOnlyList<ResolvedDevice> devices, string group, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                    return ToUShortArray(await client.ReadWordsAsync(basicStart, devices.Count, ct).ConfigureAwait(false));
                return ToUShortArray(await client.ReadWordsMultiAsync(CollectBasicAddresses(devices), ct).ConfigureAwait(false));
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                    return ToUShortArray(await client.ReadExtWordsAsync(number, extStart, devices.Count, ct).ConfigureAwait(false));
                var extData = await client.ReadExtMultiAsync(Array.Empty<(int No, int Bit, int Address)>(), Array.Empty<(int No, int Address)>(), CollectNoAddresses(devices), ct).ConfigureAwait(false);
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(extData));
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(await client.Pc10BlockReadAsync(pc10Start, devices.Count * 2, ct).ConfigureAwait(false)));
                var multiData = await client.Pc10MultiReadAsync(BuildPc10MultiWordReadPayload(CollectAddress32Values(devices)), ct).ConfigureAwait(false);
                return ToUShortArray(ParsePc10MultiWordData(multiData, devices.Count));
            default:
                throw new ToyopucProtocolError($"Single-request word access does not support group '{group}'.");
        }
    }

    private static async Task<ushort[]> ReadWordsSingleRequestViaRelayAsync(ToyopucDeviceClient client, object relayHops, IReadOnlyList<ResolvedDevice> devices, string group, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    var response = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildWordRead(basicStart, devices.Count), ct).ConfigureAwait(false);
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiBasic = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildMultiWordRead(CollectBasicAddresses(devices)), ct).ConfigureAwait(false);
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(multiBasic.Data));
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    var response = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildExtWordRead(number, extStart, devices.Count), ct).ConfigureAwait(false);
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiExt = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildExtMultiRead(Array.Empty<(int No, int Bit, int Address)>(), Array.Empty<(int No, int Address)>(), CollectNoAddresses(devices)), ct).ConfigureAwait(false);
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(multiExt.Data));
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    var response = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildPc10BlockRead(pc10Start, devices.Count * 2), ct).ConfigureAwait(false);
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiPc10 = await client.SendViaRelayReadAsync(relayHops, ToyopucProtocol.BuildPc10MultiRead(BuildPc10MultiWordReadPayload(CollectAddress32Values(devices))), ct).ConfigureAwait(false);
                return ToUShortArray(ParsePc10MultiWordData(multiPc10.Data, devices.Count));
            default:
                throw new ToyopucProtocolError($"Single-request word access does not support group '{group}'.");
        }
    }

    private static async Task WriteWordsSingleRequestDirectAsync(ToyopucDeviceClient client, IReadOnlyList<ResolvedDevice> devices, string group, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    await client.WriteWordsAsync(basicStart, ToIntArray(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.WriteWordsMultiAsync(CollectBasicAddressValues(devices, values), ct).ConfigureAwait(false);
                return;
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    await client.WriteExtWordsAsync(number, extStart, ToIntArray(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.WriteExtMultiAsync(Array.Empty<(int No, int Bit, int Address, int Value)>(), Array.Empty<(int No, int Address, int Value)>(), CollectNoAddressValues(devices, values), ct).ConfigureAwait(false);
                return;
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    await client.Pc10BlockWriteAsync(pc10Start, PackWordValues(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.Pc10MultiWriteAsync(PackPc10MultiWordPayload(CollectAddress32WordValues(devices, values)), ct).ConfigureAwait(false);
                return;
            default:
                throw new ToyopucProtocolError($"Single-request word write does not support group '{group}'.");
        }
    }

    private static async Task WriteWordsSingleRequestViaRelayAsync(ToyopucDeviceClient client, object relayHops, IReadOnlyList<ResolvedDevice> devices, string group, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildWordWrite(basicStart, ToIntArray(values)), ct).ConfigureAwait(false);
                    return;
                }
                _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildMultiWordWrite(CollectBasicAddressValues(devices, values)), ct).ConfigureAwait(false);
                return;
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtWordWrite(number, extStart, ToIntArray(values)), ct).ConfigureAwait(false);
                    return;
                }
                _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtMultiWrite(Array.Empty<(int No, int Bit, int Address, int Value)>(), Array.Empty<(int No, int Address, int Value)>(), CollectNoAddressValues(devices, values)), ct).ConfigureAwait(false);
                return;
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10BlockWrite(pc10Start, PackWordValues(values)), ct).ConfigureAwait(false);
                    return;
                }
                _ = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10MultiWrite(PackPc10MultiWordPayload(CollectAddress32WordValues(devices, values))), ct).ConfigureAwait(false);
                return;
            default:
                throw new ToyopucProtocolError($"Single-request word write does not support group '{group}'.");
        }
    }

    private static ResolvedDevice[] BuildSequentialWordDevices(ToyopucDeviceClient client, string device, int count)
    {
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "Word access");
        var devices = new ResolvedDevice[count];
        devices[0] = start;
        for (var i = 1; i < count; i++)
        {
            devices[i] = client.ResolveDevice(ToyopucAddress.Format(start, checked(start.Index + i)));
        }
        return devices;
    }

    private static void EnsureWordResolved(ResolvedDevice resolved, string paramName, string methodName)
    {
        if (resolved.Unit != "word")
            throw new ArgumentException($"{methodName} requires a word device", paramName);
    }

    private static string? GetBatchGroupKey(ResolvedDevice device) => device.Scheme switch
    {
        "basic-word" => "basic-word",
        "ext-word" or "program-word" => "ext-word",
        "pc10-word" => "pc10-word",
        _ => null,
    };

    private static bool AllDevicesInGroup(IReadOnlyList<ResolvedDevice> devices, string group)
    {
        for (var i = 1; i < devices.Count; i++)
        {
            if (GetBatchGroupKey(devices[i]) != group)
                return false;
        }
        return true;
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<ResolvedDevice> devices, Func<ResolvedDevice, int?> selector, int step, out int start)
    {
        start = default;
        if (devices.Count == 0)
            return false;
        var first = selector(devices[0]);
        if (first is null)
            return false;
        start = first.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            var current = selector(devices[i]);
            if (current != start + (i * step))
                return false;
        }
        return true;
    }

    private static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<ResolvedDevice> devices, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveStart(devices, static device => device.Address32, step, out start))
            return false;
        var block = devices[0].Address32!.Value >> 16;
        for (var i = 1; i < devices.Count; i++)
        {
            if ((devices[i].Address32!.Value >> 16) != block)
                return false;
        }
        return true;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<ResolvedDevice> devices, out int number)
    {
        number = default;
        if (devices.Count == 0)
            return false;
        var firstNo = devices[0].No;
        if (!firstNo.HasValue)
            return false;
        number = firstNo.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            if (devices[i].No != number)
                return false;
        }
        return true;
    }

    private static int[] CollectBasicAddresses(IReadOnlyList<ResolvedDevice> devices)
        => devices.Select(static device => device.BasicAddress ?? throw new ToyopucProtocolError("Missing basic address")).ToArray();

    private static (int Address, int Value)[] CollectBasicAddressValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int Address, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].BasicAddress ?? throw new ToyopucProtocolError("Missing basic address"), values[i]);
        return result;
    }

    private static (int No, int Address)[] CollectNoAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var result = new (int No, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (
                devices[i].No ?? throw new ToyopucProtocolError("Missing extended number"),
                checked((devices[i].Address ?? throw new ToyopucProtocolError("Missing extended address")) * 2));
        return result;
    }

    private static (int No, int Address, int Value)[] CollectNoAddressValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int No, int Address, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (
                devices[i].No ?? throw new ToyopucProtocolError("Missing extended number"),
                checked((devices[i].Address ?? throw new ToyopucProtocolError("Missing extended address")) * 2),
                values[i]);
        return result;
    }

    private static int[] CollectAddress32Values(IReadOnlyList<ResolvedDevice> devices)
        => devices.Select(static device => device.Address32 ?? throw new ToyopucProtocolError("Missing PC10 address")).ToArray();

    private static (int Address32, int Value)[] CollectAddress32WordValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int Address32, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].Address32 ?? throw new ToyopucProtocolError("Missing PC10 address"), values[i]);
        return result;
    }

    private static ushort[] ConvertWordReadResult(object raw, int count)
    {
        if (count == 1)
            return [Convert.ToUInt16(raw, CultureInfo.InvariantCulture)];
        var array = (object[])raw;
        var result = new ushort[array.Length];
        for (var i = 0; i < array.Length; i++)
            result[i] = Convert.ToUInt16(array[i], CultureInfo.InvariantCulture);
        return result;
    }

    private static ushort[] ToUShortArray(IReadOnlyList<int> values)
    {
        var result = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++)
            result[i] = unchecked((ushort)(values[i] & 0xFFFF));
        return result;
    }

    private static int[] ToIntArray(IReadOnlyList<ushort> values)
    {
        var result = new int[values.Count];
        for (var i = 0; i < values.Count; i++)
            result[i] = values[i];
        return result;
    }

    private static uint[] PackDWords(IReadOnlyList<ushort> words)
    {
        if ((words.Count & 1) != 0)
            throw new ToyopucProtocolError("Expected an even number of words for DWord conversion.");
        var result = new uint[words.Count / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = (uint)(words[i * 2] | (words[(i * 2) + 1] << 16));
        return result;
    }

    private static ushort[] ExpandDWords(IReadOnlyList<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        var result = new ushort[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
        {
            result[i * 2] = unchecked((ushort)(values[i] & 0xFFFF));
            result[(i * 2) + 1] = unchecked((ushort)((values[i] >> 16) & 0xFFFF));
        }
        return result;
    }

    private static byte[] PackWordValues(IReadOnlyList<ushort> values)
    {
        var data = new byte[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
            WriteU16LittleEndian(data, i * 2, values[i]);
        return data;
    }

    private static byte[] BuildPc10MultiWordReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        var payload = new byte[4 + (items.Length * 4)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        return payload;
    }

    private static byte[] PackPc10MultiWordPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var payload = new byte[4 + (items.Length * 4) + (items.Length * 2)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
        var valuesOffset = 4 + (items.Length * 4);
        for (var i = 0; i < items.Length; i++)
            WriteU16LittleEndian(payload, valuesOffset + (i * 2), items[i].Value);
        return payload;
    }

    private static int[] ParsePc10MultiWordData(byte[] data, int count)
    {
        if (data.Length < 4 + (count * 2))
            throw new ToyopucProtocolError("PC10 multi-word response too short");
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + (i * 2);
            values[i] = data[offset] | (data[offset + 1] << 8);
        }
        return values;
    }

    private static void WriteU16LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteAddress32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static string NormalizeDType(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ToyopucProtocolError("Logical data type is required.");
        var dtype = text.Trim().ToUpperInvariant();
        return dtype switch
        {
            "U" => "U",
            "S" => "S",
            "D" => "D",
            "L" => "L",
            "F" => "F",
            _ => throw new ToyopucProtocolError($"Unsupported logical data type '{text}'."),
        };
    }

    private static (string Base, string DType, int? BitIdx) ParseLogicalAddress(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (address.Contains(':'))
        {
            var index = address.IndexOf(':');
            if (index == 0)
                throw new ToyopucProtocolError("Named address must include a device before ':'.");
            return (address[..index], NormalizeDType(address[(index + 1)..]), null);
        }
        if (address.Contains('.'))
        {
            var index = address.LastIndexOf('.');
            var bitText = address[(index + 1)..].Trim();
            if (bitText.Length == 1 &&
                int.TryParse(bitText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bitIndex) &&
                bitIndex is >= 0 and <= 15)
                return (address[..index], "BIT_IN_WORD", bitIndex);
            throw new ToyopucProtocolError($"Invalid bit-in-word index in '{address}'. Use one hex digit 0-F or ':' for data type.");
        }
        throw new ToyopucProtocolError(
            $"Named address '{address}' must specify :U, :S, :D, :L, :F, or a word bit .0-.F.");
    }

    private static void ValidateNamedAddresses(IList<string> addresses)
    {
        if (addresses.Count != 1)
            throw new ToyopucProtocolError(
                "A named read must contain exactly one address so the operation remains one request.");
        _ = ParseLogicalAddress(addresses[0]);
    }
}
