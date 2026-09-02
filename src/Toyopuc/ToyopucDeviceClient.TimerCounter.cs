namespace PlcComm.Toyopuc;

public partial class ToyopucDeviceClient
{
    private readonly record struct ProgramTimerCounterTarget(int ProgramNumber, int Address);

    /// <summary>Reads the preset and current values of one program timer/counter.</summary>
    public TimerCounterValues ReadProgramTimerCounterValues(object device)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        return ReadProgramTimerCounterValuesCore(UsesRelay ? RelayHops : null, target);
    }

    /// <summary>Writes both preset and current values of one program timer/counter.</summary>
    public void WriteProgramTimerCounterValues(object device, int preset, int current)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        RequireTimerCounterValue(current, nameof(current));
        WriteProgramTimerCounterCore(
            UsesRelay ? RelayHops : null,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWriteBoth(target.ProgramNumber, target.Address, preset, current),
            0x41);
    }

    /// <summary>Writes only the preset value of one program timer/counter.</summary>
    public void WriteProgramTimerCounterPreset(object device, int preset)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        WriteProgramTimerCounterCore(
            UsesRelay ? RelayHops : null,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWritePreset(target.ProgramNumber, target.Address, preset),
            0x42);
    }

    /// <summary>Writes only the current value of one program timer/counter.</summary>
    public void WriteProgramTimerCounterCurrent(object device, int current)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(current, nameof(current));
        WriteProgramTimerCounterCore(
            UsesRelay ? RelayHops : null,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(target.ProgramNumber, target.Address, current),
            0x43);
    }

    /// <summary>Reads the preset and current values through explicit relay hops.</summary>
    public TimerCounterValues RelayReadProgramTimerCounterValues(object hops, object device)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return ReadProgramTimerCounterValuesCore(hopsSnapshot, target);
    }

    /// <summary>Writes both values through explicit relay hops.</summary>
    public void RelayWriteProgramTimerCounterValues(object hops, object device, int preset, int current)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        WriteProgramTimerCounterCore(
            hopsSnapshot,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWriteBoth(target.ProgramNumber, target.Address, preset, current),
            0x41);
    }

    /// <summary>Writes only the preset value through explicit relay hops.</summary>
    public void RelayWriteProgramTimerCounterPreset(object hops, object device, int preset)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        WriteProgramTimerCounterCore(
            hopsSnapshot,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWritePreset(target.ProgramNumber, target.Address, preset),
            0x42);
    }

    /// <summary>Writes only the current value through explicit relay hops.</summary>
    public void RelayWriteProgramTimerCounterCurrent(object hops, object device, int current)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        WriteProgramTimerCounterCore(
            hopsSnapshot,
            target,
            ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(target.ProgramNumber, target.Address, current),
            0x43);
    }

    /// <summary>Asynchronously reads the preset and current values of one program timer/counter.</summary>
    public Task<TimerCounterValues> ReadProgramTimerCounterValuesAsync(
        object device,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        var hopsSnapshot = UsesRelay ? RelayHops!.ToArray() : null;
        return RunAsync(() => ReadProgramTimerCounterValuesCore(hopsSnapshot, target), cancellationToken);
    }

    /// <summary>Asynchronously writes both preset and current values of one program timer/counter.</summary>
    public Task WriteProgramTimerCounterValuesAsync(
        object device,
        int preset,
        int current,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = UsesRelay ? RelayHops!.ToArray() : null;
        var payload = ToyopucProtocol.BuildProgramTimerCounterWriteBoth(
            target.ProgramNumber,
            target.Address,
            preset,
            current);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x41),
            cancellationToken);
    }

    /// <summary>Asynchronously writes only the preset value of one program timer/counter.</summary>
    public Task WriteProgramTimerCounterPresetAsync(
        object device,
        int preset,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        var hopsSnapshot = UsesRelay ? RelayHops!.ToArray() : null;
        var payload = ToyopucProtocol.BuildProgramTimerCounterWritePreset(
            target.ProgramNumber,
            target.Address,
            preset);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x42),
            cancellationToken);
    }

    /// <summary>Asynchronously writes only the current value of one program timer/counter.</summary>
    public Task WriteProgramTimerCounterCurrentAsync(
        object device,
        int current,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = UsesRelay ? RelayHops!.ToArray() : null;
        var payload = ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(
            target.ProgramNumber,
            target.Address,
            current);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x43),
            cancellationToken);
    }

    /// <summary>Asynchronously reads both values through explicit relay hops.</summary>
    public Task<TimerCounterValues> RelayReadProgramTimerCounterValuesAsync(
        object hops,
        object device,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        return RunAsync(() => ReadProgramTimerCounterValuesCore(hopsSnapshot, target), cancellationToken);
    }

    /// <summary>Asynchronously writes both values through explicit relay hops.</summary>
    public Task RelayWriteProgramTimerCounterValuesAsync(
        object hops,
        object device,
        int preset,
        int current,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var payload = ToyopucProtocol.BuildProgramTimerCounterWriteBoth(
            target.ProgramNumber,
            target.Address,
            preset,
            current);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x41),
            cancellationToken);
    }

    /// <summary>Asynchronously writes only the preset value through explicit relay hops.</summary>
    public Task RelayWriteProgramTimerCounterPresetAsync(
        object hops,
        object device,
        int preset,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(preset, nameof(preset));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var payload = ToyopucProtocol.BuildProgramTimerCounterWritePreset(
            target.ProgramNumber,
            target.Address,
            preset);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x42),
            cancellationToken);
    }

    /// <summary>Asynchronously writes only the current value through explicit relay hops.</summary>
    public Task RelayWriteProgramTimerCounterCurrentAsync(
        object hops,
        object device,
        int current,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveProgramTimerCounterTarget(device);
        RequireTimerCounterValue(current, nameof(current));
        var hopsSnapshot = ToyopucRelay.NormalizeRelayHops(hops).ToArray();
        var payload = ToyopucProtocol.BuildProgramTimerCounterWriteCurrent(
            target.ProgramNumber,
            target.Address,
            current);
        return RunStateChangingAsync(
            () => WriteProgramTimerCounterCore(hopsSnapshot, target, payload, 0x43),
            cancellationToken);
    }

    private TimerCounterValues ReadProgramTimerCounterValuesCore(
        object? hops,
        ProgramTimerCounterTarget target)
    {
        var payload = ToyopucProtocol.BuildProgramTimerCounterRead(target.ProgramNumber, target.Address);
        var response = hops is null
            ? SendAndReceive(payload, allowRetry: false)
            : SendViaRelayRead(hops, payload);
        if (response.Cmd != 0xA0)
            throw new ToyopucProtocolError($"Unexpected timer/counter response command 0x{response.Cmd:X2}.");
        return ToyopucProtocol.ParseProgramTimerCounterValues(response.Data, target.ProgramNumber);
    }

    private void WriteProgramTimerCounterCore(
        object? hops,
        ProgramTimerCounterTarget target,
        byte[] payload,
        int selector)
    {
        var response = hops is null
            ? SendRaw(0xA0, payload[5..])
            : SendViaRelay(hops, payload);
        if (response.Cmd != 0xA0)
            throw new ToyopucProtocolError($"Unexpected timer/counter response command 0x{response.Cmd:X2}.");
        ToyopucProtocol.ValidateProgramTimerCounterWriteResponse(
            response.Data,
            target.ProgramNumber,
            selector);
    }

    private ProgramTimerCounterTarget ResolveProgramTimerCounterTarget(object device)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Scheme != "program-bit"
            || resolved.Area is not ("T" or "C")
            || resolved.No is < 1 or > 3
            || resolved.Prefix is not ("P1" or "P2" or "P3"))
        {
            throw new ArgumentException(
                "Program timer/counter access requires a P1-/P2-/P3-prefixed T or C device.",
                nameof(device));
        }
        if (resolved.Index is < 0 or > 0x01FF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(device),
                "A0 program timer/counter access supports device numbers 000-1FF.");
        }
        return new ProgramTimerCounterTarget(resolved.No.GetValueOrDefault(), 0x0600 + resolved.Index);
    }

    private static void RequireTimerCounterValue(int value, string paramName)
    {
        if (value is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(paramName, "Timer/counter values must be in the range 0..65535.");
    }
}
