using System.Threading;
using System.Threading.Tasks;

namespace PlcComm.Toyopuc;

public partial class ToyopucDeviceClient
{
    public uint ReadDWord(object device)
    {
        return ReadDWords(device, 1)[0];
    }

    public void WriteDWord(object device, uint value)
    {
        WriteDWords(device, new[] { value });
    }

    public uint[] ReadDWords(object device, int count)
    {
        if (device is int address)
        {
            return base.ReadDWords(address, count);
        }

        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "ReadDWords()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackUInt32LowWordFirst(ReadResolvedWordValues(resolved, checked(count * 2)));
    }

    public void WriteDWords(object device, IEnumerable<uint> values)
    {
        if (device is int address)
        {
            base.WriteDWords(address, values);
            return;
        }

        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "WriteDWords()");
        RequireGenericWriteDevice(resolved);
        WriteResolvedWordValues(resolved, PackUInt32LowWordFirstToWords(values));
    }

    public float ReadFloat32(object device)
    {
        return ReadFloat32s(device, 1)[0];
    }

    public void WriteFloat32(object device, float value)
    {
        WriteFloat32s(device, new[] { value });
    }

    public float[] ReadFloat32s(object device, int count)
    {
        if (device is int address)
        {
            return base.ReadFloat32s(address, count);
        }

        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "ReadFloat32s()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackFloat32LowWordFirst(ReadResolvedWordValues(resolved, checked(count * 2)));
    }

    public void WriteFloat32s(object device, IEnumerable<float> values)
    {
        if (device is int address)
        {
            base.WriteFloat32s(address, values);
            return;
        }

        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "WriteFloat32s()");
        RequireGenericWriteDevice(resolved);
        WriteResolvedWordValues(resolved, PackFloat32LowWordFirstToWords(values));
    }

    public uint RelayReadDWord(object hops, object device)
    {
        return RelayReadDWords(hops, device, 1)[0];
    }

    public void RelayWriteDWord(object hops, object device, uint value)
    {
        RelayWriteDWords(hops, device, new[] { value });
    }

    public uint[] RelayReadDWords(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayReadDWords()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackUInt32LowWordFirst(RelayReadResolvedWordValues(hops, resolved, checked(count * 2)));
    }

    public void RelayWriteDWords(object hops, object device, IEnumerable<uint> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayWriteDWords()");
        RequireGenericWriteDevice(resolved);
        RelayWriteResolvedWordValues(hops, resolved, PackUInt32LowWordFirstToWords(values));
    }

    public float RelayReadFloat32(object hops, object device)
    {
        return RelayReadFloat32s(hops, device, 1)[0];
    }

    public void RelayWriteFloat32(object hops, object device, float value)
    {
        RelayWriteFloat32s(hops, device, new[] { value });
    }

    public float[] RelayReadFloat32s(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayReadFloat32s()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackFloat32LowWordFirst(RelayReadResolvedWordValues(hops, resolved, checked(count * 2)));
    }

    public void RelayWriteFloat32s(object hops, object device, IEnumerable<float> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayWriteFloat32s()");
        RequireGenericWriteDevice(resolved);
        RelayWriteResolvedWordValues(hops, resolved, PackFloat32LowWordFirstToWords(values));
    }

    public Task<ResolvedDevice> ResolveDeviceAsync(string device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ResolveDevice(device));
    }

    public Task<object> RelayReadOneAsync(object hops, object device, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadOne(hopsSnapshot, deviceSnapshot), cancellationToken);
    }

    public Task RelayWriteAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        var valueSnapshot = SnapshotDeviceValue(value);
        return RunStateChangingAsync(() => RelayWrite(hopsSnapshot, deviceSnapshot, valueSnapshot), cancellationToken);
    }

    public Task<object[]> RelayReadWordsAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadWords(hopsSnapshot, deviceSnapshot, count), cancellationToken);
    }

    public Task RelayWriteWordsAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        var valueSnapshot = SnapshotDeviceValue(value);
        return RunStateChangingAsync(() => RelayWriteWords(hopsSnapshot, deviceSnapshot, valueSnapshot), cancellationToken);
    }

    public Task<object[]> RelayReadManyAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var resolved = ResolveSequentialDevices(ResolveDeviceObject(device), count);
        var plan = GetReadRunPlan(resolved, splitPc10BlockBoundaries: true);
        var prepared = PrepareReadPlan(hopsSnapshot, resolved, plan);
        return ExecutePreparedReadAsync(prepared, resolved.Count, cancellationToken);
    }

    public Task<object[]> RelayReadDevicesAsync(object hops, IEnumerable<object> devices, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var resolved = ResolveDevices(devices);
        RequireReadItems(resolved, nameof(RelayReadDevices));
        var plan = GetReadRunPlan(resolved, splitPc10BlockBoundaries: false);
        var prepared = PrepareReadPlan(hopsSnapshot, resolved, plan);
        return ExecutePreparedReadAsync(prepared, resolved.Length, cancellationToken);
    }

    public Task RelayWriteManyAsync(object hops, IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var resolved = ResolveWriteItems(items);
        RequireSingleWriteRequest(resolved, splitPc10BlockBoundaries: true, nameof(RelayWriteMany));
        return RunStateChangingAsync(
            () => RelayWriteRuns(hopsSnapshot, resolved, splitPc10BlockBoundaries: true),
            cancellationToken);
    }

    public Task<object> ReadFrOneAsync(object device, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadFrOne(RelayHops!, snapshot), cancellationToken)
            : RunAsync(() => ReadFrOne(snapshot), cancellationToken);
    }

    public Task<object[]> ReadFrAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadFr(RelayHops!, snapshot, count), cancellationToken)
            : RunAsync(() => ReadFr(snapshot, count), cancellationToken);
    }

    public Task<object> RelayReadFrOneAsync(object hops, object device, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadFrOne(hopsSnapshot, deviceSnapshot), cancellationToken);
    }

    public Task<object[]> RelayReadFrAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadFr(hopsSnapshot, deviceSnapshot, count), cancellationToken);
    }

    public Task WriteFrWorkAreaAsync(
        object device,
        object value,
        CancellationToken cancellationToken = default)
    {
        var deviceSnapshot = ResolveDeviceObject(device);
        var valueSnapshot = SnapshotDeviceValue(value);
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWriteFrWorkArea(RelayHops!, deviceSnapshot, valueSnapshot), cancellationToken)
            : RunStateChangingAsync(() => WriteFrWorkArea(deviceSnapshot, valueSnapshot), cancellationToken);
    }

    public Task RelayWriteFrWorkAreaAsync(
        object hops,
        object device,
        object value,
        CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        var valueSnapshot = SnapshotDeviceValue(value);
        return RunStateChangingAsync(() => RelayWriteFrWorkArea(hopsSnapshot, deviceSnapshot, valueSnapshot), cancellationToken);
    }

    public Task CommitFrBlockAsync(
        object device,
        CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunStateChangingAsync(() => RelayCommitFrBlock(RelayHops!, snapshot), cancellationToken)
            : RunStateChangingAsync(() => CommitFrBlock(snapshot), cancellationToken);
    }

    public Task RelayCommitFrBlockAsync(
        object hops,
        object device,
        CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunStateChangingAsync(() => RelayCommitFrBlock(hopsSnapshot, deviceSnapshot), cancellationToken);
    }

    public Task<object> ReadOneAsync(object device, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadOne(RelayHops!, snapshot), cancellationToken)
            : RunAsync(() => ReadOne(snapshot), cancellationToken);
    }

    public Task WriteAsync(object device, object value, CancellationToken cancellationToken = default)
    {
        var deviceSnapshot = ResolveDeviceObject(device);
        var valueSnapshot = SnapshotDeviceValue(value);
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWrite(RelayHops!, deviceSnapshot, valueSnapshot), cancellationToken)
            : RunStateChangingAsync(() => Write(deviceSnapshot, valueSnapshot), cancellationToken);
    }

    public Task<object[]> ReadManyAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var resolved = ResolveSequentialDevices(ResolveDeviceObject(device), count);
        var plan = GetReadRunPlan(resolved, splitPc10BlockBoundaries: true);
        var prepared = UsesRelay
            ? PrepareReadPlan(RelayHops!, resolved, plan)
            : PrepareReadPlan(resolved, plan);
        return ExecutePreparedReadAsync(prepared, resolved.Count, cancellationToken);
    }

    public Task<object[]> ReadDevicesAsync(IEnumerable<object> devices, CancellationToken cancellationToken = default)
    {
        var resolved = ResolveDevices(devices);
        RequireReadItems(resolved, nameof(ReadDevices));
        var plan = GetReadRunPlan(resolved, splitPc10BlockBoundaries: false);
        var prepared = UsesRelay
            ? PrepareReadPlan(RelayHops!, resolved, plan)
            : PrepareReadPlan(resolved, plan);
        return ExecutePreparedReadAsync(prepared, resolved.Length, cancellationToken);
    }

    internal Task<object[]> ReadResolvedDevicesSingleRequestAsync(
        IReadOnlyList<ResolvedDevice> devices,
        object? relayHops,
        string operation,
        CancellationToken cancellationToken)
    {
        var snapshot = devices.ToArray();
        RequireReadItems(snapshot, operation);
        var plan = GetReadRunPlan(snapshot, splitPc10BlockBoundaries: false);
        if (plan.Length != 1 || plan[0] != snapshot.Length || !CanReadAsSingleRequest(snapshot))
        {
            RaiseImplicitSplitError(operation);
        }

        var prepared = relayHops is null
            ? PrepareReadPlan(snapshot, plan)
            : PrepareReadPlan(ToyopucRelay.NormalizeRelayHops(relayHops).ToArray(), snapshot, plan);
        return ExecutePreparedReadAsync(prepared, snapshot.Length, cancellationToken);
    }

    public Task WriteManyAsync(IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
    {
        var resolved = ResolveWriteItems(items);
        RequireSingleWriteRequest(resolved, splitPc10BlockBoundaries: true, nameof(WriteMany));
        return UsesRelay
            ? RunStateChangingAsync(
                () => RelayWriteRuns(RelayHops!, resolved, splitPc10BlockBoundaries: true),
                cancellationToken)
            : RunStateChangingAsync(
                () => WriteRuns(resolved, splitPc10BlockBoundaries: true),
                cancellationToken);
    }

    private Task<object[]> ExecutePreparedReadAsync(
        IReadOnlyList<PreparedReadSegment> prepared,
        int resultCount,
        CancellationToken cancellationToken)
    {
        return RunNativeSequenceAsync(
            async exchange =>
            {
                var results = new object[resultCount];
                foreach (var segment in prepared)
                {
                    if (segment.Relay is null)
                    {
                        var response = await exchange(segment.Payload, false).ConfigureAwait(false);
                        DecodePreparedReadSegment(response, segment, results);
                    }
                    else
                    {
                        var outer = await exchange(segment.Relay.OuterPayload, false).ConfigureAwait(false);
                        DecodePreparedRelayReadResponse(
                            segment.Relay,
                            outer,
                            response =>
                            {
                                DecodePreparedReadSegment(response, segment, results);
                                return true;
                            });
                    }
                }
                return results;
            },
            outcomeUnknownAfterSend: false,
            cancellationToken);
    }

    public Task<uint> ReadDWordAsync(object device, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadDWord(RelayHops!, snapshot), cancellationToken)
            : RunAsync(() => ReadDWord(snapshot), cancellationToken);
    }

    public Task WriteDWordAsync(object device, uint value, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWriteDWord(RelayHops!, snapshot, value), cancellationToken)
            : RunStateChangingAsync(() => WriteDWord(snapshot, value), cancellationToken);
    }

    public Task<uint[]> ReadDWordsAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadDWords(RelayHops!, snapshot, count), cancellationToken)
            : RunAsync(() => ReadDWords(snapshot, count), cancellationToken);
    }

    public Task WriteDWordsAsync(object device, IEnumerable<uint> values, CancellationToken cancellationToken = default)
    {
        var deviceSnapshot = ResolveDeviceObject(device);
        var valuesSnapshot = values.ToArray();
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWriteDWords(RelayHops!, deviceSnapshot, valuesSnapshot), cancellationToken)
            : RunStateChangingAsync(() => WriteDWords(deviceSnapshot, valuesSnapshot), cancellationToken);
    }

    public Task<float> ReadFloat32Async(object device, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadFloat32(RelayHops!, snapshot), cancellationToken)
            : RunAsync(() => ReadFloat32(snapshot), cancellationToken);
    }

    public Task WriteFloat32Async(object device, float value, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWriteFloat32(RelayHops!, snapshot, value), cancellationToken)
            : RunStateChangingAsync(() => WriteFloat32(snapshot, value), cancellationToken);
    }

    public Task<float[]> ReadFloat32sAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolveDeviceObject(device);
        return UsesRelay
            ? RunAsync(() => RelayReadFloat32s(RelayHops!, snapshot, count), cancellationToken)
            : RunAsync(() => ReadFloat32s(snapshot, count), cancellationToken);
    }

    public Task WriteFloat32sAsync(object device, IEnumerable<float> values, CancellationToken cancellationToken = default)
    {
        var deviceSnapshot = ResolveDeviceObject(device);
        var valuesSnapshot = values.ToArray();
        return UsesRelay
            ? RunStateChangingAsync(() => RelayWriteFloat32s(RelayHops!, deviceSnapshot, valuesSnapshot), cancellationToken)
            : RunStateChangingAsync(() => WriteFloat32s(deviceSnapshot, valuesSnapshot), cancellationToken);
    }

    public Task<uint> RelayReadDWordAsync(object hops, object device, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadDWord(hopsSnapshot, deviceSnapshot), cancellationToken);
    }

    public Task RelayWriteDWordAsync(object hops, object device, uint value, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunStateChangingAsync(() => RelayWriteDWord(hopsSnapshot, deviceSnapshot, value), cancellationToken);
    }

    public Task<uint[]> RelayReadDWordsAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadDWords(hopsSnapshot, deviceSnapshot, count), cancellationToken);
    }

    public Task RelayWriteDWordsAsync(object hops, object device, IEnumerable<uint> values, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        var valuesSnapshot = values.ToArray();
        return RunStateChangingAsync(() => RelayWriteDWords(hopsSnapshot, deviceSnapshot, valuesSnapshot), cancellationToken);
    }

    public Task<float> RelayReadFloat32Async(object hops, object device, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadFloat32(hopsSnapshot, deviceSnapshot), cancellationToken);
    }

    public Task RelayWriteFloat32Async(object hops, object device, float value, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunStateChangingAsync(() => RelayWriteFloat32(hopsSnapshot, deviceSnapshot, value), cancellationToken);
    }

    public Task<float[]> RelayReadFloat32sAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        return RunAsync(() => RelayReadFloat32s(hopsSnapshot, deviceSnapshot, count), cancellationToken);
    }

    public Task RelayWriteFloat32sAsync(object hops, object device, IEnumerable<float> values, CancellationToken cancellationToken = default)
    {
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var deviceSnapshot = ResolveDeviceObject(device);
        var valuesSnapshot = values.ToArray();
        return RunStateChangingAsync(() => RelayWriteFloat32s(hopsSnapshot, deviceSnapshot, valuesSnapshot), cancellationToken);
    }

    /// <summary>Reads the PLC clock through the route selected for this client.</summary>
    public new Task<ClockData> ReadClockAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunAsync(() => RelayReadClock(RelayHops!), cancellationToken)
            : RunAsync(ReadClock, cancellationToken);

    /// <summary>Writes the PLC clock through the route selected for this client.</summary>
    public new Task WriteClockAsync(DateTime value, int yearBase, CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunStateChangingAsync(() => RelayWriteClock(RelayHops!, value, yearBase), cancellationToken)
            : RunStateChangingAsync(() => WriteClock(value, yearBase), cancellationToken);

    /// <summary>Resumes PLC scan through the route selected for this client.</summary>
    public new Task ResumeScanAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunStateChangingAsync(() => RelayResumeScan(RelayHops!), cancellationToken)
            : RunStateChangingAsync(ResumeScan, cancellationToken);

    /// <summary>Stops PLC scan through the route selected for this client.</summary>
    public new Task StopScanAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunStateChangingAsync(() => RelayStopScan(RelayHops!), cancellationToken)
            : RunStateChangingAsync(StopScan, cancellationToken);

    /// <summary>Releases PLC scan stop through the route selected for this client.</summary>
    public new Task ReleaseScanStopAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunStateChangingAsync(() => RelayReleaseScanStop(RelayHops!), cancellationToken)
            : RunStateChangingAsync(ReleaseScanStop, cancellationToken);

    /// <summary>Reads PLC CPU status through the route selected for this client.</summary>
    public new Task<CpuStatusData> ReadCpuStatusAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunAsync(() => RelayReadCpuStatus(RelayHops!), cancellationToken)
            : RunAsync(ReadCpuStatus, cancellationToken);

    /// <summary>Reads raw A0 CPU status through the route selected for this client.</summary>
    public new Task<byte[]> ReadCpuStatusA0RawAsync(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunAsync(() => RelayReadCpuStatusA0Raw(RelayHops!), cancellationToken)
            : RunAsync(ReadCpuStatusA0Raw, cancellationToken);

    /// <summary>Reads parsed A0 CPU status through the route selected for this client.</summary>
    public new Task<CpuStatusData> ReadCpuStatusA0Async(CancellationToken cancellationToken = default)
        => UsesRelay
            ? RunAsync(() => RelayReadCpuStatusA0(RelayHops!), cancellationToken)
            : RunAsync(ReadCpuStatusA0, cancellationToken);

    private static object SnapshotDeviceValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is string)
            return value;
        if (value is byte[] bytes)
            return bytes.ToArray();
        return value is System.Collections.IEnumerable sequence
            ? MaterializeSequence(sequence)
            : value;
    }

    private int[] ReadResolvedWordValues(ResolvedDevice resolved, int wordCount)
    {
        return ExecuteSynchronousExclusive(() => ReadResolvedWordValuesCore(resolved, wordCount));
    }

    private int[] ReadResolvedWordValuesCore(ResolvedDevice resolved, int wordCount)
    {
        if (wordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "wordCount must be >= 1");
        }

        if (wordCount == 1)
        {
            return new[] { ToInt32Invariant(ReadOne(resolved)) & 0xFFFF };
        }

        var devices = ResolveSequentialDevices(resolved, wordCount);
        RequireSingleEntryAtomicReadRequest(devices, splitPc10BlockBoundaries: true, nameof(ReadDWords));
        var plan = GetRunPlan(devices, splitPc10BlockBoundaries: true);
        var prepared = PrepareReadPlan(devices, plan);
        var values = ExecutePreparedRead(prepared, devices.Count);
        var words = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            words[i] = ToInt32Invariant(values[i]) & 0xFFFF;
        }

        return words;
    }

    private int[] RelayReadResolvedWordValues(object hops, ResolvedDevice resolved, int wordCount)
    {
        return ExecuteSynchronousExclusive(() => RelayReadResolvedWordValuesCore(hops, resolved, wordCount));
    }

    private int[] RelayReadResolvedWordValuesCore(object hops, ResolvedDevice resolved, int wordCount)
    {
        if (wordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "wordCount must be >= 1");
        }

        if (wordCount == 1)
        {
            return new[] { ToInt32Invariant(RelayReadOne(hops, resolved)) & 0xFFFF };
        }

        var devices = ResolveSequentialDevices(resolved, wordCount);
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        RequireSingleEntryAtomicReadRequest(devices, splitPc10BlockBoundaries: true, nameof(RelayReadDWords));
        var plan = GetRunPlan(devices, splitPc10BlockBoundaries: true);
        var prepared = PrepareReadPlan(hopsSnapshot, devices, plan);
        var values = ExecutePreparedRead(prepared, devices.Count);
        var words = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            words[i] = ToInt32Invariant(values[i]) & 0xFFFF;
        }

        return words;
    }

    private void WriteResolvedWordValues(ResolvedDevice resolved, IReadOnlyList<int> wordValues)
    {
        if (wordValues.Count < 1)
        {
            throw new ArgumentException("values must not be empty", nameof(wordValues));
        }

        RequireGenericWriteDevice(resolved);

        var boxedValues = new object[wordValues.Count];
        for (var i = 0; i < wordValues.Count; i++)
        {
            boxedValues[i] = wordValues[i];
        }

        var items = ResolveSequentialWriteItems(resolved, boxedValues);
        RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(WriteDWords));
        WriteRuns(items, splitPc10BlockBoundaries: true);
    }

    private void RelayWriteResolvedWordValues(object hops, ResolvedDevice resolved, IReadOnlyList<int> wordValues)
    {
        if (wordValues.Count < 1)
        {
            throw new ArgumentException("values must not be empty", nameof(wordValues));
        }

        RequireGenericWriteDevice(resolved);

        var boxedValues = new object[wordValues.Count];
        for (var i = 0; i < wordValues.Count; i++)
        {
            boxedValues[i] = wordValues[i];
        }

        var items = ResolveSequentialWriteItems(resolved, boxedValues);
        RequireSingleWriteRequest(items, splitPc10BlockBoundaries: true, nameof(RelayWriteDWords));
        RelayWriteRuns(hops, items, splitPc10BlockBoundaries: true);
    }

    private static void EnsureWordResolvedDevice(ResolvedDevice resolved, string paramName, string methodName)
    {
        if (resolved.Unit != "word")
        {
            throw new ArgumentException($"{methodName} requires a word device", paramName);
        }
    }
}
