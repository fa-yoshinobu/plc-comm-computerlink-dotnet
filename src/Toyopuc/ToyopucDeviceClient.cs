using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;

namespace PlcComm.Toyopuc;

public partial class ToyopucDeviceClient : ToyopucClient
{
    private const int DeviceCacheMaxEntries = 512;
    private const int RunPlanCacheMaxEntries = 256;

    private sealed class ReadOnlyListSlice<T>(IReadOnlyList<T> source, int offset, int count) : IReadOnlyList<T>
    {
        public int Count { get; } = count;

        public T this[int index] => source[offset + index];

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return source[offset + i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private static readonly IReadOnlyDictionary<string, int> ProgramPrefixExNo = new Dictionary<string, int>
    {
        ["P1"] = 0x0D,
        ["P2"] = 0x0E,
        ["P3"] = 0x0F,
    };

    internal ToyopucAddressingOptions AddressingOptions { get; }
    public string PlcProfile { get; }
    private readonly ConcurrentDictionary<string, ResolvedDevice> _resolvedDeviceCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int[]> _runPlanCache = new(StringComparer.Ordinal);

    public ToyopucDeviceClient(
        string host,
        int port,
        ToyopucTransportMode transport,
        string plcProfile,
        int localPort = 0,
        TimeSpan? timeout = null,
        int retries = 0,
        TimeSpan? retryDelay = null)
        : base(host, port, transport, localPort, timeout, retries, retryDelay)
    {
        if (string.IsNullOrWhiteSpace(plcProfile))
        {
            throw new ArgumentException(
                "PLC profile is required. Use an explicit canonical profile name.",
                nameof(plcProfile));
        }

        PlcProfile = ToyopucPlcProfiles.NormalizeName(plcProfile);
        AddressingOptions = ToyopucAddressingOptions.FromProfile(PlcProfile);
    }

    internal ToyopucDeviceClient(
        string host,
        int port,
        ToyopucAddressingOptions addressingOptions,
        string plcProfile,
        ToyopucTransportMode transport,
        int localPort = 0,
        TimeSpan? timeout = null,
        int retries = 0,
        TimeSpan? retryDelay = null)
        : base(host, port, transport, localPort, timeout, retries, retryDelay)
    {
        ArgumentNullException.ThrowIfNull(addressingOptions);
        PlcProfile = ToyopucPlcProfiles.NormalizeName(plcProfile);
        AddressingOptions = addressingOptions;
    }

    public ResolvedDevice ResolveDevice(string device)
    {
        var key = NormalizeDeviceCacheKey(device);
        if (_resolvedDeviceCache.Count >= DeviceCacheMaxEntries)
        {
            _resolvedDeviceCache.Clear();
        }

        return _resolvedDeviceCache.GetOrAdd(
            key,
            static (cacheKey, state) => ToyopucDeviceResolver.ResolveDevice(
                cacheKey,
                state.AddressingOptions,
                state.PlcProfile),
            (AddressingOptions, PlcProfile));
    }

    public object RelayReadOne(object hops, object device)
    {
        var resolved = ResolveDeviceObject(device);
        return RelayReadOne(hops, resolved);
    }

    internal object RelayRead(object hops, object device, int count = 1) =>
        count == 1 ? RelayReadOne(hops, device) : RelayReadMany(hops, device, count);

    public void RelayWrite(object hops, object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit == "bit")
        {
            if (TryEnumerateSequence(value, out var bitValues))
            {
                var items = ResolveSequentialWriteItems(resolved, MaterializeSequence(bitValues));
                RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(RelayWrite));
                RelayWriteRuns(hops, items, splitPc10BlockBoundaries: true);
                return;
            }

            RelayWriteOne(hops, resolved, value);
            return;
        }

        if (value is byte[] bytes)
        {
            var items = ResolveSequentialWriteItems(resolved, BoxBytes(bytes));
            RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(RelayWrite));
            RelayWriteRuns(hops, items, splitPc10BlockBoundaries: true);
            return;
        }

        if (TryEnumerateSequence(value, out var values))
        {
            var items = ResolveSequentialWriteItems(resolved, MaterializeSequence(values));
            RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(RelayWrite));
            RelayWriteRuns(hops, items, splitPc10BlockBoundaries: true);
            return;
        }

        RelayWriteOne(hops, resolved, value);
    }

    public object[] RelayReadWords(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit != "word")
        {
            throw new ArgumentException("RelayReadWords() requires a word device", nameof(device));
        }

        return RelayReadMany(hops, resolved, count);
    }

    public void RelayWriteWords(object hops, object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit != "word")
        {
            throw new ArgumentException("RelayWriteWords() requires a word device", nameof(device));
        }

        RelayWrite(hops, resolved, value);
    }

    public object[] RelayReadMany(object hops, object device, int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var resolved = ResolveSequentialDevices(ResolveDeviceObject(device), count);
        RequireSingleReadRequest(resolved, splitPc10BlockBoundaries: true, nameof(RelayReadMany));
        return RelayReadRuns(hops, resolved, splitPc10BlockBoundaries: true);
    }

    public object[] RelayReadDevices(object hops, IEnumerable<object> devices)
    {
        var resolved = ResolveDevices(devices);
        RequireSingleReadRequest(resolved, splitPc10BlockBoundaries: false, nameof(RelayReadDevices));
        return RelayReadRuns(hops, resolved, splitPc10BlockBoundaries: false);
    }

    internal object[] RelayReadMany(object hops, IEnumerable<object> devices) => RelayReadDevices(hops, devices);

    public void RelayWriteMany(object hops, IEnumerable<KeyValuePair<object, object>> items)
    {
        var resolved = ResolveWriteItems(items);
        RequireSingleWriteRequest(resolved, splitPc10BlockBoundaries: true, nameof(RelayWriteMany));
        RelayWriteRuns(hops, resolved, splitPc10BlockBoundaries: true);
    }

    public object ReadFrOne(object device)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("ReadFr() requires an FR word device such as FR000000", nameof(device));
        }

        return ReadOne(resolved);
    }

    public object[] ReadFr(object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
            throw new ArgumentException("ReadFr() requires an FR word device such as FR000000", nameof(device));
        return ReadMany(resolved, count);
    }

    internal object ReadFr(object device) => ReadFrOne(device);

    public object RelayReadFrOne(object hops, object device)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayReadFr() requires an FR word device such as FR000000", nameof(device));
        }

        return RelayReadOne(hops, resolved);
    }

    public object[] RelayReadFr(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
            throw new ArgumentException("RelayReadFr() requires an FR word device such as FR000000", nameof(device));
        return RelayReadMany(hops, resolved, count);
    }

    internal object RelayReadFr(object hops, object device) => RelayReadFrOne(hops, device);

    public void WriteFrWorkArea(object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("WriteFrWorkArea() requires an FR word device such as FR000000", nameof(device));
        }

        var values = NormalizeWordValues(value);
        var boxed = values.Select(static value => (object)value).ToArray();
        var items = ResolveSequentialWriteItems(resolved, boxed);
        RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(WriteFrWorkArea));
        WriteRuns(items, splitPc10BlockBoundaries: true);
    }

    public void RelayWriteFrWorkArea(object hops, object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayWriteFrWorkArea() requires an FR word device such as FR000000", nameof(device));
        }

        var values = NormalizeWordValues(value);
        var boxed = values.Select(static value => (object)value).ToArray();
        var items = ResolveSequentialWriteItems(resolved, boxed);
        RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(RelayWriteFrWorkArea));
        RelayWriteRuns(hops, items, splitPc10BlockBoundaries: true);
    }

    public void CommitFrBlock(object device)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("CommitFr() requires an FR word device such as FR000000", nameof(device));
        }

        base.CommitFrBlock(resolved.Index);
    }

    public void RelayCommitFrBlock(object hops, object device)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayCommitFr() requires an FR word device such as FR000000", nameof(device));
        }

        base.RelayCommitFrBlock(hops, resolved.Index);
    }

    public object ReadOne(object device)
    {
        var resolved = ResolveDeviceObject(device);
        return ReadOne(resolved);
    }

    internal object Read(object device, int count = 1) =>
        count == 1 ? ReadOne(device) : ReadMany(device, count);

    public void Write(object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area == "FR")
        {
            RaiseGenericFrWriteError();
        }

        if (resolved.Unit == "bit")
        {
            if (TryEnumerateSequence(value, out var bitValues))
            {
                var items = ResolveSequentialWriteItems(resolved, MaterializeSequence(bitValues));
                RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(Write));
                WriteRuns(items, splitPc10BlockBoundaries: true);
                return;
            }

            WriteOne(resolved, value);
            return;
        }

        if (value is byte[] bytes)
        {
            var items = ResolveSequentialWriteItems(resolved, BoxBytes(bytes));
            RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(Write));
            WriteRuns(items, splitPc10BlockBoundaries: true);
            return;
        }

        if (TryEnumerateSequence(value, out var values))
        {
            var items = ResolveSequentialWriteItems(resolved, MaterializeSequence(values));
            RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(Write));
            WriteRuns(items, splitPc10BlockBoundaries: true);
            return;
        }

        WriteOne(resolved, value);
    }

    public object[] ReadMany(object device, int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var resolved = ResolveSequentialDevices(ResolveDeviceObject(device), count);
        RequireSingleReadRequest(resolved, splitPc10BlockBoundaries: true, nameof(ReadMany));
        return ReadRuns(resolved, splitPc10BlockBoundaries: true);
    }

    public object[] ReadDevices(IEnumerable<object> devices)
    {
        var resolved = ResolveDevices(devices);
        RequireSingleReadRequest(resolved, splitPc10BlockBoundaries: false, nameof(ReadDevices));
        return ReadRuns(resolved, splitPc10BlockBoundaries: false);
    }

    internal object[] ReadMany(IEnumerable<object> devices) => ReadDevices(devices);

    public void WriteMany(IEnumerable<KeyValuePair<object, object>> items)
    {
        var resolved = ResolveWriteItems(items);
        RequireSingleWriteRequest(resolved, splitPc10BlockBoundaries: true, nameof(WriteMany));
        WriteRuns(resolved, splitPc10BlockBoundaries: true);
    }

    private ResolvedDevice ResolveDeviceObject(object device)
    {
        return device switch
        {
            string text => ResolveDevice(text),
            ResolvedDevice resolved when string.Equals(resolved.PlcProfile, PlcProfile, StringComparison.Ordinal) => resolved,
            ResolvedDevice resolved => throw new ArgumentException(
                $"ResolvedDevice belongs to profile '{resolved.PlcProfile}', not '{PlcProfile}'.",
                nameof(device)),
            _ => throw new ArgumentException("device must be a string address or ResolvedDevice", nameof(device)),
        };
    }

    private ResolvedDevice[] ResolveDevices(IEnumerable<object> devices)
    {
        if (devices is ICollection<object> collection)
        {
            var resolved = new ResolvedDevice[collection.Count];
            var index = 0;
            foreach (var device in collection)
            {
                resolved[index++] = ResolveDeviceObject(device);
            }

            return resolved;
        }

        var list = new List<ResolvedDevice>();
        foreach (var device in devices)
        {
            list.Add(ResolveDeviceObject(device));
        }

        return list.ToArray();
    }

    private (ResolvedDevice Device, object Value)[] ResolveWriteItems(IEnumerable<KeyValuePair<object, object>> items)
    {
        if (items is ICollection<KeyValuePair<object, object>> collection)
        {
            var resolved = new (ResolvedDevice Device, object Value)[collection.Count];
            var index = 0;
            foreach (var item in collection)
            {
                var device = ResolveDeviceObject(item.Key);
                resolved[index++] = (device, NormalizeDeviceValue(device, item.Value));
            }

            return resolved;
        }

        var list = new List<(ResolvedDevice Device, object Value)>();
        foreach (var item in items)
        {
            var device = ResolveDeviceObject(item.Key);
            list.Add((device, NormalizeDeviceValue(device, item.Value)));
        }

        return list.ToArray();
    }

    private static bool TryEnumerateSequence(object value, out IEnumerable sequence)
    {
        if (value is string)
        {
            sequence = Array.Empty<object>();
            return false;
        }

        if (value is byte[])
        {
            sequence = Array.Empty<object>();
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            sequence = enumerable;
            return true;
        }

        sequence = Array.Empty<object>();
        return false;
    }

    private static int[] NormalizeWordValues(object value)
    {
        if (TryEnumerateSequence(value, out var sequence))
        {
            var list = new List<int>();
            foreach (var item in sequence)
            {
                list.Add(RequireFrWordValue(item));
            }

            return list.ToArray();
        }

        return new[] { RequireFrWordValue(value) };
    }

    private static int RequireFrWordValue(object? value)
    {
        return value switch
        {
            byte item => item,
            sbyte item when item >= 0 => item,
            short item when item >= 0 => item,
            ushort item => item,
            int item when item is >= 0 and <= 0xFFFF => item,
            uint item when item <= 0xFFFF => (int)item,
            long item when item is >= 0 and <= 0xFFFF => (int)item,
            ulong item when item <= 0xFFFF => (int)item,
            _ => throw new ArgumentException("FR word values must be integral values in the range 0..65535.", nameof(value)),
        };
    }

    private static object[] MaterializeSequence(IEnumerable sequence)
    {
        if (sequence is ICollection collection)
        {
            var values = new object[collection.Count];
            var index = 0;
            foreach (var item in sequence)
            {
                values[index++] = item!;
            }

            return values;
        }

        var list = new List<object>();
        foreach (var item in sequence)
        {
            list.Add(item!);
        }

        return list.ToArray();
    }

    private static int Require(int? value, string label)
    {
        return value ?? throw new ArgumentException($"Resolved device missing {label}");
    }

    private static int ToInt32Invariant(object value)
    {
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static int NormalizeDeviceValue(ResolvedDevice device, object value)
    {
        return device.Unit switch
        {
            "bit" => RequireBitValue(value),
            "byte" => RequireUnsignedDeviceValue(value, byte.MaxValue, "byte"),
            "word" => RequireUnsignedDeviceValue(value, ushort.MaxValue, "word"),
            _ => throw new ArgumentException($"Unsupported device unit: {device.Unit}", nameof(device)),
        };
    }

    private static int RequireBitValue(object value)
    {
        if (value is bool flag)
            return flag ? 1 : 0;
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
            _ => ulong.MaxValue,
        };
        if (candidate <= 1)
            return (int)candidate;
        throw new ArgumentOutOfRangeException(nameof(value), value, "Bit value must be Boolean or integer 0 or 1.");
    }

    private static int RequireUnsignedDeviceValue(object value, int maximum, string label)
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
            _ => throw new ArgumentException($"{label} value must be an integer; Boolean, fractional, and string values are not accepted.", nameof(value)),
        };
        if (candidate > (ulong)maximum)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{label} value must be in the range 0..{maximum}.");
        return (int)candidate;
    }

    private static int[] ReadPc10MultiWords(ToyopucClient client, IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        return ParsePc10MultiWordData(client.Pc10MultiRead(Pc10Payloads.BuildMultiWordReadPayload(items)), items.Length);
    }

    private static int ReadPc10BlockWord(ToyopucClient client, int address32)
    {
        var data = client.Pc10BlockRead(address32, 2);
        if (data.Length < 2)
        {
            throw new ToyopucProtocolError("PC10 word-read response too short");
        }

        return data[0] | (data[1] << 8);
    }

    private static void WritePc10BlockWord(ToyopucClient client, int address32, int value)
    {
        client.Pc10BlockWrite(address32, new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) });
    }

    private static int[] ReadPc10MultiBits(ToyopucClient client, IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        return ParsePc10MultiBitData(client.Pc10MultiRead(Pc10Payloads.BuildMultiBitReadPayload(items)), items.Length);
    }

    private object[] ReadRuns(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var results = new object[devices.Count];
        var index = 0;
        foreach (var runLength in GetRunPlan(devices, splitPc10BlockBoundaries))
        {
            var batchResults = ReadBatch(new ReadOnlyListSlice<ResolvedDevice>(devices, index, runLength));
            Array.Copy(batchResults, 0, results, index, runLength);
            index += runLength;
        }

        return results;
    }

    private object[] RelayReadRuns(object hops, IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var results = new object[devices.Count];
        var index = 0;
        foreach (var runLength in GetRunPlan(devices, splitPc10BlockBoundaries))
        {
            var batchResults = RelayReadBatch(hops, new ReadOnlyListSlice<ResolvedDevice>(devices, index, runLength));
            Array.Copy(batchResults, 0, results, index, runLength);
            index += runLength;
        }

        return results;
    }

    private void WriteRuns(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var index = 0;
        foreach (var runLength in GetRunPlan(items, splitPc10BlockBoundaries))
        {
            WriteBatch(new ReadOnlyListSlice<(ResolvedDevice Device, object Value)>(items, index, runLength));
            index += runLength;
        }
    }

    private void RelayWriteRuns(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var index = 0;
        foreach (var runLength in GetRunPlan(items, splitPc10BlockBoundaries))
        {
            RelayWriteBatch(hops, new ReadOnlyListSlice<(ResolvedDevice Device, object Value)>(items, index, runLength));
            index += runLength;
        }
    }

    private IReadOnlyList<ResolvedDevice> ResolveSequentialDevices(ResolvedDevice resolved, int count)
    {
        var devices = new ResolvedDevice[count];
        devices[0] = resolved;
        for (var i = 1; i < count; i++)
        {
            devices[i] = Offset(devices[i - 1], 1);
        }

        return devices;
    }

    private IReadOnlyList<(ResolvedDevice Device, object Value)> ResolveSequentialWriteItems(ResolvedDevice resolved, IReadOnlyList<object> values)
    {
        var items = new (ResolvedDevice Device, object Value)[values.Count];
        if (values.Count == 0)
        {
            return items;
        }

        items[0] = (resolved, NormalizeDeviceValue(resolved, values[0]));
        for (var i = 1; i < values.Count; i++)
        {
            var device = Offset(items[i - 1].Device, 1);
            items[i] = (device, NormalizeDeviceValue(device, values[i]));
        }

        return items;
    }

    private int[] GetRunPlan(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var key = DeviceRunPlanner.BuildRunPlanKey(devices, splitPc10BlockBoundaries);
        if (_runPlanCache.Count >= RunPlanCacheMaxEntries)
        {
            _runPlanCache.Clear();
        }

        return _runPlanCache.GetOrAdd(
            key,
            static (_, state) => DeviceRunPlanner.CompileRunPlan(state.Devices, state.SplitPc10BlockBoundaries),
            (Devices: devices, SplitPc10BlockBoundaries: splitPc10BlockBoundaries));
    }

    private int[] GetRunPlan(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var key = DeviceRunPlanner.BuildRunPlanKey(items, splitPc10BlockBoundaries);
        if (_runPlanCache.Count >= RunPlanCacheMaxEntries)
        {
            _runPlanCache.Clear();
        }

        return _runPlanCache.GetOrAdd(
            key,
            static (_, state) => DeviceRunPlanner.CompileRunPlan(state.Items, state.SplitPc10BlockBoundaries),
            (Items: items, SplitPc10BlockBoundaries: splitPc10BlockBoundaries));
    }

    private void RequireSingleReadRequest(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries, string operation)
    {
        if (devices.Count == 0)
        {
            throw new ArgumentException($"{operation} requires at least one device.", nameof(devices));
        }

        if (devices.Count == 1)
        {
            return;
        }

        var plan = GetRunPlan(devices, splitPc10BlockBoundaries);
        if (plan.Length != 1 || !CanReadAsSingleRequest(devices))
        {
            RaiseImplicitSplitError(operation);
        }
    }

    private void RequireSingleWriteRequest(
        IReadOnlyList<(ResolvedDevice Device, object Value)> items,
        bool splitPc10BlockBoundaries,
        string operation)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException($"{operation} requires at least one value.", nameof(items));
        }

        if (items.Count == 1)
        {
            return;
        }

        var plan = GetRunPlan(items, splitPc10BlockBoundaries);
        if (plan.Length != 1 || !CanWriteAsSingleRequest(items))
        {
            RaiseImplicitSplitError(operation);
        }
    }

    private static bool CanReadAsSingleRequest(IReadOnlyList<ResolvedDevice> devices)
    {
        var group = DeviceRunPlanner.GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
        {
            return false;
        }

        return group switch
        {
            "pc10-byte" => DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 1, out _),
            "pc10-word" when ContainsPackedWordDevice(devices) =>
                DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 2, out _),
            _ => true,
        };
    }

    private static bool CanWriteAsSingleRequest(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var group = DeviceRunPlanner.GetBatchGroupKey(items[0].Device);
        if (group is null || !AllItemsInGroup(items, group) || HasDuplicateDevices(items))
        {
            return false;
        }

        return group switch
        {
            "pc10-byte" => DeviceRunPlanner.TryGetConsecutivePc10BlockStart(items, 1, out _),
            _ => true,
        };
    }

    private static void RaiseImplicitSplitError(string operation)
    {
        throw new ToyopucProtocolError(
            $"{operation} requires one compatible protocol request. Split the operation into explicit calls when multiple requests are intentional.");
    }

    private static string NormalizeDeviceCacheKey(string device)
    {
        return device.Trim().ToUpperInvariant();
    }

    private object[] ReadBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (devices.Count == 0)
        {
            return Array.Empty<object>();
        }

        var group = DeviceRunPlanner.GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
        {
            return ReadIndividually(devices);
        }

        return group switch
        {
            "basic-word" => ReadBasicWordBatch(devices),
            "basic-byte" => BoxBytes(ReadBytesMulti(CollectBasicAddresses(devices))),
            "ext-word" => ReadExtWordBatch(devices),
            "ext-byte" => ReadExtByteBatch(devices),
            "ext-bit" => ReadExtBitBatch(devices),
            "pc10-word" => ReadPc10WordBatch(devices),
            "pc10-bit" => BoxBooleanBits(ReadPc10MultiBits(this, CollectAddress32Values(devices))),
            "pc10-byte" => ReadPc10ByteBatch(devices),
            _ => ReadIndividually(devices),
        };
    }

    private object[] RelayReadBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (devices.Count == 0)
        {
            return Array.Empty<object>();
        }

        var group = DeviceRunPlanner.GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
        {
            return RelayReadIndividually(hops, devices);
        }

        return group switch
        {
            "basic-word" => RelayReadBasicWordBatch(hops, devices),
            "basic-byte" => RelayReadBasicByteBatch(hops, devices),
            "ext-word" => RelayReadExtWordBatch(hops, devices),
            "ext-byte" => RelayReadExtByteBatch(hops, devices),
            "ext-bit" => RelayReadExtBitBatch(hops, devices),
            "pc10-word" => RelayReadPc10WordBatch(hops, devices),
            "pc10-bit" => RelayReadPc10BitBatch(hops, devices),
            "pc10-byte" => RelayReadPc10ByteBatch(hops, devices),
            _ => RelayReadIndividually(hops, devices),
        };
    }

    private void WriteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var group = DeviceRunPlanner.GetBatchGroupKey(items[0].Device);
        if (group is null
            || !AllItemsInGroup(items, group)
            || HasDuplicateDevices(items))
        {
            foreach (var item in items)
            {
                WriteOne(item.Device, item.Value);
            }

            return;
        }

        switch (group)
        {
            case "basic-word":
                WriteBasicWordBatch(items);
                return;
            case "basic-byte":
                WriteBytesMulti(CollectBasicAddressValues(items));
                return;
            case "ext-word":
                WriteExtWordBatch(items);
                return;
            case "ext-byte":
                WriteExtByteBatch(items);
                return;
            case "ext-bit":
                WriteExtBitBatch(items);
                return;
            case "pc10-word":
                WritePc10WordBatch(items);
                return;
            case "pc10-bit":
                Pc10MultiWrite(Pc10Payloads.PackMultiBitPayload(CollectAddress32BitValues(items)));
                return;
            case "pc10-byte":
                WritePc10ByteBatch(items);
                return;
            default:
                foreach (var item in items)
                {
                    WriteOne(item.Device, item.Value);
                }

                return;
        }
    }

    private void RelayWriteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var group = DeviceRunPlanner.GetBatchGroupKey(items[0].Device);
        if (group is null
            || !AllItemsInGroup(items, group)
            || HasDuplicateDevices(items))
        {
            foreach (var item in items)
            {
                RelayWriteOne(hops, item.Device, item.Value);
            }

            return;
        }

        switch (group)
        {
            case "basic-word":
                RelayWriteBasicWordBatch(hops, items);
                return;
            case "basic-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildMultiByteWrite(CollectBasicAddressValues(items)));
                    EnsureCommand(response, 0x25, "Unexpected CMD in relay multi-byte-write response");
                    return;
                }
            case "ext-word":
                RelayWriteExtWordBatch(hops, items);
                return;
            case "ext-byte":
                RelayWriteExtByteBatch(hops, items);
                return;
            case "ext-bit":
                RelayWriteExtBitBatch(hops, items);
                return;
            case "pc10-word":
                RelayWritePc10WordBatch(hops, items);
                return;
            case "pc10-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10MultiWrite(Pc10Payloads.PackMultiBitPayload(CollectAddress32BitValues(items))));
                    EnsureCommand(response, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
                    return;
                }
            case "pc10-byte":
                RelayWritePc10ByteBatch(hops, items);
                return;
            default:
                foreach (var item in items)
                {
                    RelayWriteOne(hops, item.Device, item.Value);
                }

                return;
        }
    }

    private object[] ReadBasicWordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var startAddress))
        {
            return BoxWords(ReadWords(startAddress, devices.Count));
        }

        return BoxWords(ReadWordsMulti(CollectBasicAddresses(devices)));
    }

    private object[] RelayReadBasicWordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var startAddress))
        {
            var response = SendViaRelayRead(hops, ToyopucProtocol.BuildWordRead(startAddress, devices.Count));
            EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        var multiResponse = SendViaRelayRead(hops, ToyopucProtocol.BuildMultiWordRead(CollectBasicAddresses(devices)));
        EnsureCommand(multiResponse, 0x22, "Unexpected CMD in relay multi-word-read response");
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(multiResponse.Data));
    }

    private object[] RelayReadBasicByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelayRead(hops, ToyopucProtocol.BuildMultiByteRead(CollectBasicAddresses(devices)));
        EnsureCommand(response, 0x24, "Unexpected CMD in relay multi-byte-read response");
        return BoxBytes(response.Data);
    }

    private object[] ReadExtWordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            return BoxWords(ReadExtWords(number, startAddress, devices.Count));
        }

        var data = ReadExtMulti(
            Array.Empty<(int No, int Bit, int Address)>(),
            Array.Empty<(int No, int Address)>(),
            CollectNoWordMonitorAddresses(devices));
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(data));
    }

    private object[] RelayReadExtWordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            var response = SendViaRelayRead(hops, ToyopucProtocol.BuildExtWordRead(number, startAddress, devices.Count));
            EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        var responseMulti = SendViaRelayRead(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                Array.Empty<(int No, int Bit, int Address)>(),
                Array.Empty<(int No, int Address)>(),
                CollectNoWordMonitorAddresses(devices)));
        EnsureCommand(responseMulti, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(responseMulti.Data));
    }

    private object[] ReadExtByteBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            return BoxBytes(ReadExtBytes(number, startAddress, devices.Count));
        }

        var data = ReadExtMulti(
            Array.Empty<(int No, int Bit, int Address)>(),
            CollectNoAddresses(devices),
            Array.Empty<(int No, int Address)>());
        return BoxBytes(data);
    }

    private object[] RelayReadExtByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            var response = SendViaRelayRead(hops, ToyopucProtocol.BuildExtByteRead(number, startAddress, devices.Count));
            EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
            return BoxBytes(response.Data);
        }

        var responseMulti = SendViaRelayRead(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                Array.Empty<(int No, int Bit, int Address)>(),
                CollectNoAddresses(devices),
                Array.Empty<(int No, int Address)>()));
        EnsureCommand(responseMulti, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxBytes(responseMulti.Data);
    }

    private object[] ReadExtBitBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        var data = ReadExtMulti(
            CollectNoBitAddresses(devices),
            Array.Empty<(int No, int Address)>(),
            Array.Empty<(int No, int Address)>());
        return BoxBooleanBits(ParseExtMultiBitData(data, devices.Count));
    }

    private object[] RelayReadExtBitBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelayRead(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                CollectNoBitAddresses(devices),
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>()));
        EnsureCommand(response, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxBooleanBits(ParseExtMultiBitData(response.Data, devices.Count));
    }

    private object[] ReadPc10WordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 2, out var startAddress))
        {
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(Pc10BlockRead(startAddress, devices.Count * 2)));
        }

        if (ContainsPackedWordDevice(devices))
        {
            throw new ToyopucProtocolError(
                "PC10 packed-word read requires more than one protocol request; split it into explicit calls.");
        }

        return BoxWords(ReadPc10MultiWords(this, CollectAddress32Values(devices)));
    }

    private object[] RelayReadPc10WordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 2, out var startAddress))
        {
            var response = SendViaRelayRead(hops, ToyopucProtocol.BuildPc10BlockRead(startAddress, devices.Count * 2));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        if (ContainsPackedWordDevice(devices))
        {
            throw new ToyopucProtocolError(
                "Relay PC10 packed-word read requires more than one protocol request; split it into explicit calls.");
        }

        var responseMulti = SendViaRelayRead(hops, ToyopucProtocol.BuildPc10MultiRead(Pc10Payloads.BuildMultiWordReadPayload(CollectAddress32Values(devices))));
        EnsureCommand(responseMulti, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
        return BoxWords(ParsePc10MultiWordData(responseMulti.Data, devices.Count));
    }

    private object[] RelayReadPc10BitBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelayRead(
            hops,
            ToyopucProtocol.BuildPc10MultiRead(Pc10Payloads.BuildMultiBitReadPayload(CollectAddress32Values(devices))));
        EnsureCommand(response, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
        return BoxBooleanBits(ParsePc10MultiBitData(response.Data, devices.Count));
    }

    private object[] ReadPc10ByteBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 1, out var startAddress))
        {
            return BoxBytes(Pc10BlockRead(startAddress, devices.Count));
        }

        return ReadIndividually(devices);
    }

    private object[] RelayReadPc10ByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(devices, 1, out var startAddress))
        {
            var response = SendViaRelayRead(hops, ToyopucProtocol.BuildPc10BlockRead(startAddress, devices.Count));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
            return BoxBytes(response.Data);
        }

        return RelayReadIndividually(hops, devices);
    }

    private void WriteBasicWordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutiveStart(items, static item => item.Device.BasicAddress, 1, out var startAddress))
        {
            WriteWords(startAddress, values);
            return;
        }

        WriteWordsMulti(CollectBasicAddressValues(items));
    }

    private void RelayWriteBasicWordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutiveStart(items, static item => item.Device.BasicAddress, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(startAddress, values));
            EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
            return;
        }

        var multiResponse = SendViaRelay(hops, ToyopucProtocol.BuildMultiWordWrite(CollectBasicAddressValues(items)));
        EnsureCommand(multiResponse, 0x23, "Unexpected CMD in relay multi-word-write response");
    }

    private void WriteExtWordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = items.Select(static item => ToInt32Invariant(item.Value)).ToArray();
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            WriteExtWords(number, startAddress, values);
            return;
        }

        WriteExtMulti(
            Array.Empty<(int No, int Bit, int Address, int Value)>(),
            Array.Empty<(int No, int Address, int Value)>(),
            CollectNoWordMonitorAddressValues(items));
    }

    private void RelayWriteExtWordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtWordWrite(number, startAddress, values));
            EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                Array.Empty<(int No, int Bit, int Address, int Value)>(),
                Array.Empty<(int No, int Address, int Value)>(),
                CollectNoWordMonitorAddressValues(items)));
        EnsureCommand(responseMulti, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WriteExtByteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = items.Select(static item => ToInt32Invariant(item.Value)).ToArray();
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            WriteExtBytes(number, startAddress, values);
            return;
        }

        WriteExtMulti(
            Array.Empty<(int No, int Bit, int Address, int Value)>(),
            CollectNoAddressValues(items),
            Array.Empty<(int No, int Address, int Value)>());
    }

    private void RelayWriteExtByteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtByteWrite(number, startAddress, values));
            EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                Array.Empty<(int No, int Bit, int Address, int Value)>(),
                CollectNoAddressValues(items),
                Array.Empty<(int No, int Address, int Value)>()));
        EnsureCommand(responseMulti, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WriteExtBitBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        WriteExtMulti(
            CollectNoBitAddressValues(items),
            Array.Empty<(int No, int Address, int Value)>(),
            Array.Empty<(int No, int Address, int Value)>());
    }

    private void RelayWriteExtBitBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                CollectNoBitAddressValues(items),
                Array.Empty<(int No, int Address, int Value)>(),
                Array.Empty<(int No, int Address, int Value)>()));
        EnsureCommand(response, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WritePc10WordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(items, 2, out var startAddress))
        {
            Pc10BlockWrite(startAddress, Pc10Payloads.PackWordValues(values));
            return;
        }

        Pc10MultiWrite(Pc10Payloads.PackMultiWordPayload(CollectAddress32WordValues(items)));
    }

    private void RelayWritePc10WordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(items, 2, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockWrite(startAddress, Pc10Payloads.PackWordValues(values)));
            EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildPc10MultiWrite(Pc10Payloads.PackMultiWordPayload(CollectAddress32WordValues(items))));
        EnsureCommand(responseMulti, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
    }

    private void WritePc10ByteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(items, 1, out var startAddress))
        {
            Pc10BlockWrite(startAddress, CollectByteValues(items));
            return;
        }

        foreach (var item in items)
        {
            WriteOne(item.Device, item.Value);
        }
    }

    private void RelayWritePc10ByteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (DeviceRunPlanner.TryGetConsecutivePc10BlockStart(items, 1, out var startAddress))
        {
            var response = SendViaRelay(
                hops,
                ToyopucProtocol.BuildPc10BlockWrite(startAddress, CollectByteValues(items)));
            EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
            return;
        }

        foreach (var item in items)
        {
            RelayWriteOne(hops, item.Device, item.Value);
        }
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<ResolvedDevice> devices, Func<ResolvedDevice, int?> selector, int step, out int start)
    {
        start = default;
        if (devices.Count == 0)
        {
            return false;
        }

        var first = selector(devices[0]);
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            var current = selector(devices[i]);
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<(ResolvedDevice Device, object Value)> items, Func<(ResolvedDevice Device, object Value), int?> selector, int step, out int start)
    {
        start = default;
        if (items.Count == 0)
        {
            return false;
        }

        var first = selector(items[0]);
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < items.Count; i++)
        {
            var current = selector(items[i]);
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsPackedWordDevice(IReadOnlyList<ResolvedDevice> devices)
    {
        for (var i = 0; i < devices.Count; i++)
        {
            if (devices[i].Scheme == "pc10-word" && devices[i].Unit == "word" && devices[i].Packed)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<ResolvedDevice> devices, out int number)
    {
        number = default;
        if (devices.Count == 0)
        {
            return false;
        }

        var firstNo = devices[0].No;
        if (firstNo is null)
        {
            return false;
        }

        var uniformNumber = firstNo.Value;
        number = uniformNumber;
        for (var i = 1; i < devices.Count; i++)
        {
            if (devices[i].No != uniformNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<(ResolvedDevice Device, object Value)> items, out int number)
    {
        number = default;
        if (items.Count == 0)
        {
            return false;
        }

        var firstNo = items[0].Device.No;
        if (firstNo is null)
        {
            return false;
        }

        var uniformNumber = firstNo.Value;
        number = uniformNumber;
        for (var i = 1; i < items.Count; i++)
        {
            if (items[i].Device.No != uniformNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDuplicateDevices(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!seen.Add(item.Device.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AllDevicesInGroup(IReadOnlyList<ResolvedDevice> devices, string group)
    {
        for (var i = 1; i < devices.Count; i++)
        {
            if (DeviceRunPlanner.GetBatchGroupKey(devices[i]) != group)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllItemsInGroup(IReadOnlyList<(ResolvedDevice Device, object Value)> items, string group)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (DeviceRunPlanner.GetBatchGroupKey(items[i].Device) != group)
            {
                return false;
            }
        }

        return true;
    }

    private object[] ReadIndividually(IReadOnlyList<ResolvedDevice> devices)
    {
        var values = new object[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            values[i] = ReadOne(devices[i]);
        }

        return values;
    }

    private object[] RelayReadIndividually(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var values = new object[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            values[i] = RelayReadOne(hops, devices[i]);
        }

        return values;
    }

    private static int[] CollectBasicAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var addresses = new int[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            addresses[i] = Require(devices[i].BasicAddress, "basic_addr");
        }

        return addresses;
    }

    private static int[] CollectAddress32Values(IReadOnlyList<ResolvedDevice> devices)
    {
        var addresses = new int[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            addresses[i] = Require(devices[i].Address32, "pc10 addr32");
        }

        return addresses;
    }

    private static (int No, int Address)[] CollectNoAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var points = new (int No, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            points[i] = (Require(devices[i].No, "extended number"), Require(devices[i].Address, "extended addr"));
        }

        return points;
    }

    // CMD=98/99 word points carry monitor byte addresses (manual: "byte address N"),
    // while ResolvedDevice.Address holds the CMD=94/95 word address.
    private static (int No, int Address)[] CollectNoWordMonitorAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var points = new (int No, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            points[i] = (Require(devices[i].No, "extended number"), Require(devices[i].Address, "extended addr") * 2);
        }

        return points;
    }

    private static (int No, int Bit, int Address)[] CollectNoBitAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var points = new (int No, int Bit, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            points[i] = (
                Require(devices[i].No, "extended number"),
                Require(devices[i].BitNo, "extended bit"),
                Require(devices[i].Address, "extended addr"));
        }

        return points;
    }

    private static (int Address, int Value)[] CollectBasicAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.BasicAddress, "basic_addr"), ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int No, int Address, int Value)[] CollectNoAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int No, int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (
                Require(items[i].Device.No, "extended number"),
                Require(items[i].Device.Address, "extended addr"),
                ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    // See CollectNoWordMonitorAddresses: CMD=99 word points carry monitor byte addresses.
    private static (int No, int Address, int Value)[] CollectNoWordMonitorAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int No, int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (
                Require(items[i].Device.No, "extended number"),
                Require(items[i].Device.Address, "extended addr") * 2,
                ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int No, int Bit, int Address, int Value)[] CollectNoBitAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int No, int Bit, int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (
                Require(items[i].Device.No, "extended number"),
                Require(items[i].Device.BitNo, "extended bit"),
                Require(items[i].Device.Address, "extended addr"),
                ToBitInt(items[i].Value));
        }

        return values;
    }

    private static (int Address32, int Value)[] CollectAddress32WordValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address32, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.Address32, "pc10 addr32"), ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int Address32, int Value)[] CollectAddress32BitValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address32, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.Address32, "pc10 addr32"), ToBitInt(items[i].Value));
        }

        return values;
    }

    private static int[] CollectIntValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new int[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = ToInt32Invariant(items[i].Value);
        }

        return values;
    }

    private static byte[] CollectByteValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new byte[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (byte)(ToInt32Invariant(items[i].Value) & 0xFF);
        }

        return values;
    }

    private static int[] SliceWordValues(IReadOnlyList<int> values, int offset, int count)
    {
        var slice = new int[count];
        for (var i = 0; i < count; i++)
        {
            slice[i] = values[offset + i];
        }

        return slice;
    }

    private static object[] BoxWords(IReadOnlyList<int> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBytes(IReadOnlyList<byte> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBooleans(IReadOnlyList<bool> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBooleanBits(IReadOnlyList<int> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = (values[i] & 0x01) != 0;
        }

        return boxed;
    }

    private static object[] BoxBooleanBytes(IReadOnlyList<byte> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = (values[i] & 0x01) != 0;
        }

        return boxed;
    }

    private static int[] ParsePc10MultiWordData(byte[] data, int count)
    {
        if (data.Length < 4 + (count * 2))
        {
            throw new ToyopucProtocolError("PC10 multi-word response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + (i * 2);
            values[i] = data[offset] | (data[offset + 1] << 8);
        }

        return values;
    }

    private static int[] ParsePc10MultiBitData(byte[] data, int count)
    {
        if (data.Length < 4 + ((count + 7) / 8))
        {
            throw new ToyopucProtocolError("PC10 multi-bit response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (data[4 + (i / 8)] >> (i % 8)) & 0x01;
        }

        return values;
    }

    private static int[] ParseExtMultiBitData(byte[] data, int count)
    {
        if (data.Length < (count + 7) / 8)
        {
            throw new ToyopucProtocolError("Extended multi-bit response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (data[i / 8] >> (i % 8)) & 0x01;
        }

        return values;
    }

    private static int ToBitInt(object value)
    {
        return RequireBitValue(value);
    }

    private static void RaiseGenericFrWriteError()
    {
        throw new ArgumentException(
            "Generic FR writes are disabled; use WriteFrWorkArea() and CommitFrBlock() as separate operations.");
    }

    private object ReadOne(ResolvedDevice resolved)
    {
        return resolved.Scheme switch
        {
            "basic-bit" => ReadBit(Require(resolved.BasicAddress, "basic_addr")),
            "basic-word" => ReadWords(Require(resolved.BasicAddress, "basic_addr"), 1)[0],
            "basic-byte" => ReadBytes(Require(resolved.BasicAddress, "basic_addr"), 1)[0],
            "program-bit" => (ReadExtMulti(
                new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr")) },
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>())[0] & 0x01) != 0,
            "program-word" => ReadExtWords(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1)[0],
            "program-byte" => ReadExtBytes(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1)[0],
            "ext-bit" => (ReadExtMulti(
                new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr")) },
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>())[0] & 0x01) != 0,
            "ext-word" => ReadExtWords(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1)[0],
            "ext-byte" => ReadExtBytes(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1)[0],
            "pc10-bit" => ReadPc10MultiBits(this, new[] { Require(resolved.Address32, "pc10 addr32") })[0] != 0,
            "pc10-word" => ReadPc10BlockWord(this, Require(resolved.Address32, "pc10 addr32")),
            "pc10-byte" => Pc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 1)[0],
            _ => throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved)),
        };
    }

    private object RelayReadOne(object hops, ResolvedDevice resolved)
    {
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    var response = SendViaRelayRead(hops, ToyopucProtocol.BuildBitRead(Require(resolved.BasicAddress, "basic_addr")));
                    EnsureCommand(response, 0x20, "Unexpected CMD in relay bit-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay bit-read response must be 1 byte");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "basic-word":
                {
                    var response = SendViaRelayRead(hops, ToyopucProtocol.BuildWordRead(Require(resolved.BasicAddress, "basic_addr"), 1));
                    EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "basic-byte":
                {
                    var response = SendViaRelayRead(hops, ToyopucProtocol.BuildByteRead(Require(resolved.BasicAddress, "basic_addr"), 1));
                    EnsureCommand(response, 0x1E, "Unexpected CMD in relay byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "program-bit":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtMultiRead(
                            new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr")) },
                            Array.Empty<(int No, int Address)>(),
                            Array.Empty<(int No, int Address)>()));
                    EnsureCommand(response, 0x98, "Unexpected CMD in relay multi-read response");
                    if (response.Data.Length == 0)
                    {
                        throw new ToyopucProtocolError("Relay multi-read response missing bit payload");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "program-word":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtWordRead(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1));
                    EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "program-byte":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtByteRead(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1));
                    EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay ext byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "ext-bit":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtMultiRead(
                            new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr")) },
                            Array.Empty<(int No, int Address)>(),
                            Array.Empty<(int No, int Address)>()));
                    EnsureCommand(response, 0x98, "Unexpected CMD in relay multi-read response");
                    if (response.Data.Length == 0)
                    {
                        throw new ToyopucProtocolError("Relay multi-read response missing bit payload");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "ext-word":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtWordRead(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1));
                    EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "ext-byte":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildExtByteRead(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1));
                    EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay ext byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "pc10-bit":
                {
                    var response = SendViaRelayRead(
                        hops,
                        ToyopucProtocol.BuildPc10MultiRead(Pc10Payloads.BuildMultiBitReadPayload(new[] { Require(resolved.Address32, "pc10 addr32") })));
                    EnsureCommand(response, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
                    if (response.Data.Length < 5)
                    {
                        throw new ToyopucProtocolError("Relay PC10 bit-read response too short");
                    }

                    return (response.Data[4] & 0x01) != 0;
                }
            case "pc10-word":
                {
                    var response = SendViaRelayRead(hops, ToyopucProtocol.BuildPc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 2));
                    EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
                    if (response.Data.Length < 2)
                    {
                        throw new ToyopucProtocolError("Relay PC10 word-read response too short");
                    }

                    return response.Data[0] | (response.Data[1] << 8);
                }
            case "pc10-byte":
                {
                    var response = SendViaRelayRead(hops, ToyopucProtocol.BuildPc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 1));
                    EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
                    if (response.Data.Length < 1)
                    {
                        throw new ToyopucProtocolError("Relay PC10 byte-read response too short");
                    }

                    return response.Data[0];
                }
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private void WriteOne(ResolvedDevice resolved, object value)
    {
        if (resolved.Area == "FR")
        {
            RaiseGenericFrWriteError();
        }
        value = NormalizeDeviceValue(resolved, value);

        switch (resolved.Scheme)
        {
            case "basic-bit":
                WriteBit(Require(resolved.BasicAddress, "basic_addr"), ToInt32Invariant(value) == 1);
                return;
            case "basic-word":
                WriteWords(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) });
                return;
            case "basic-byte":
                WriteBytes(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) });
                return;
            case "program-bit":
                WriteExtMulti(
                    new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr"), ToInt32Invariant(value)) },
                    Array.Empty<(int No, int Address, int Value)>(),
                    Array.Empty<(int No, int Address, int Value)>());
                return;
            case "program-word":
                WriteExtWords(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) });
                return;
            case "program-byte":
                WriteExtBytes(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) });
                return;
            case "ext-bit":
                WriteExtMulti(
                    new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr"), ToInt32Invariant(value)) },
                    Array.Empty<(int No, int Address, int Value)>(),
                    Array.Empty<(int No, int Address, int Value)>());
                return;
            case "ext-word":
                WriteExtWords(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) });
                return;
            case "ext-byte":
                WriteExtBytes(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) });
                return;
            case "pc10-bit":
                Pc10MultiWrite(Pc10Payloads.PackMultiBitPayload(new[] { (Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value)) }));
                return;
            case "pc10-word":
                WritePc10BlockWord(this, Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value));
                return;
            case "pc10-byte":
                Pc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), new[] { (byte)ToInt32Invariant(value) });
                return;
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private void RelayWriteOne(object hops, ResolvedDevice resolved, object value)
    {
        value = NormalizeDeviceValue(resolved, value);
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildBitWrite(Require(resolved.BasicAddress, "basic_addr"), ToInt32Invariant(value)));
                    EnsureCommand(response, 0x21, "Unexpected CMD in relay bit-write response");
                    return;
                }
            case "basic-word":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
                    return;
                }
            case "basic-byte":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildByteWrite(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x1F, "Unexpected CMD in relay byte-write response");
                    return;
                }
            case "program-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiWrite(
                            new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr"), ToInt32Invariant(value)) },
                            Array.Empty<(int No, int Address, int Value)>(),
                            Array.Empty<(int No, int Address, int Value)>()));
                    EnsureCommand(response, 0x99, "Unexpected CMD in relay multi-write response");
                    return;
                }
            case "program-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordWrite(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
                    return;
                }
            case "program-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteWrite(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
                    return;
                }
            case "ext-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiWrite(
                            new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr"), ToInt32Invariant(value)) },
                            Array.Empty<(int No, int Address, int Value)>(),
                            Array.Empty<(int No, int Address, int Value)>()));
                    EnsureCommand(response, 0x99, "Unexpected CMD in relay multi-write response");
                    return;
                }
            case "ext-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordWrite(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
                    return;
                }
            case "ext-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteWrite(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
                    return;
                }
            case "pc10-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10MultiWrite(Pc10Payloads.PackMultiBitPayload(new[] { (Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value)) })));
                    EnsureCommand(response, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
                    return;
                }
            case "pc10-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), ToyopucProtocol.PackU16LittleEndian(ToInt32Invariant(value))));
                    EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
                    return;
                }
            case "pc10-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), new[] { (byte)ToInt32Invariant(value) }));
                    EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
                    return;
                }
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private ResolvedDevice Offset(ResolvedDevice resolved, int delta)
    {
        if (delta == 0)
        {
            return resolved;
        }

        var nextIndex = checked(resolved.Index + delta);
        if (nextIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Offset would move address index below zero");
        }

        if (TryOffsetFast(resolved, nextIndex, out var next))
        {
            return next;
        }

        return ResolveDevice(BuildResolvedText(resolved, nextIndex));
    }

    private bool TryOffsetFast(ResolvedDevice resolved, int nextIndex, out ResolvedDevice next)
    {
        var nextText = BuildResolvedText(resolved, nextIndex);
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    if ((resolved.Area is "L" or "M") && nextIndex >= 0x1000)
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeBitAddress(parsed),
                    };
                    return true;
                }
            case "basic-word":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "word", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeWordAddress(parsed),
                    };
                    return true;
                }
            case "basic-byte":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "byte", resolved.High, resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeByteAddress(parsed),
                    };
                    return true;
                }
            case "program-bit":
                {
                    if (resolved.Prefix is null || !ProgramPrefixExNo.TryGetValue(resolved.Prefix, out var exNo))
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    var (bitNo, address) = ToyopucAddress.EncodeProgramBitAddress(parsed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = address,
                        BitNo = bitNo,
                        Address32 = ToyopucAddress.EncodePc10BitAddress(parsed) | (exNo << 19),
                    };
                    return true;
                }
            case "program-word":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "word", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = ToyopucAddress.EncodeProgramWordAddress(parsed),
                    };
                    return true;
                }
            case "program-byte":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "byte", resolved.High, resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = ToyopucAddress.EncodeProgramByteAddress(parsed),
                    };
                    return true;
                }
            case "ext-word":
                {
                    if (resolved.Area == "U" && AddressingOptions.UseUpperUPc10 && nextIndex >= 0x08000)
                    {
                        break;
                    }

                    var ext = ToyopucAddress.EncodeExtNoAddress(resolved.Area, nextIndex, "word");
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        No = ext.No,
                        Address = ext.Address,
                    };
                    return true;
                }
            case "ext-byte":
                {
                    if (resolved.Area == "FR")
                    {
                        break;
                    }

                    if (resolved.Area == "U" && AddressingOptions.UseUpperUPc10 && nextIndex >= 0x08000)
                    {
                        break;
                    }

                    var ext = ToyopucAddress.EncodeExtNoAddress(
                        resolved.Area,
                        checked((nextIndex * 2) + (resolved.High ? 1 : 0)),
                        "byte");
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        No = ext.No,
                        Address = ext.Address,
                    };
                    return true;
                }
            case "pc10-bit":
                {
                    if (nextIndex < 0x1000)
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = ToyopucAddress.EncodePc10BitAddress(parsed),
                    };
                    return true;
                }
            case "pc10-word":
                {
                    if (!TryEncodePc10WordAddress32(resolved.Area, nextIndex, out var address32))
                    {
                        break;
                    }

                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = address32,
                    };
                    return true;
                }
            case "pc10-byte":
                {
                    if (!TryEncodePc10ByteAddress32(resolved.Area, nextIndex, resolved.High, out var address32))
                    {
                        break;
                    }

                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = address32,
                    };
                    return true;
                }
        }

        next = null!;
        return false;
    }

    private bool TryEncodePc10WordAddress32(string area, int index, out int address32)
    {
        address32 = default;
        switch (area)
        {
            case "U" when AddressingOptions.UseUpperUPc10 && index >= 0x08000 && index <= 0x1FFFF:
                address32 = EncodePc10UAddress32(index);
                return true;
            case "EB" when AddressingOptions.UseEbPc10 && index >= 0x00000 && index <= 0x3FFFF:
                address32 = EncodePc10EbAddress32(index);
                return true;
            case "FR" when AddressingOptions.UseFrPc10 && index >= 0x000000 && index <= 0x1FFFFF:
                address32 = ToyopucAddress.EncodeFrWordAddr32(index);
                return true;
            default:
                return false;
        }
    }

    private bool TryEncodePc10ByteAddress32(string area, int index, bool high, out int address32)
    {
        address32 = default;
        switch (area)
        {
            case "U" when AddressingOptions.UseUpperUPc10 && index >= 0x08000 && index <= 0x1FFFF:
                address32 = EncodePc10UAddress32(index, byteAddress: true, high: high);
                return true;
            case "EB" when AddressingOptions.UseEbPc10 && index >= 0x00000 && index <= 0x3FFFF:
                address32 = EncodePc10EbAddress32(index, byteAddress: true, high: high);
                return true;
            default:
                return false;
        }
    }

    private static int EncodePc10UAddress32(int index, bool byteAddress = false, bool high = false)
    {
        var block = index / 0x8000;
        var exNo = 0x03 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private static int EncodePc10EbAddress32(int index, bool byteAddress = false, bool high = false)
    {
        var block = index / 0x8000;
        var exNo = 0x10 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private string BuildResolvedText(ResolvedDevice resolved, int index)
    {
        return ToyopucAddress.Format(resolved, index);
    }
}
