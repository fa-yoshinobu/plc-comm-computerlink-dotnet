using System.Text;

namespace PlcComm.Toyopuc;

internal static class DeviceRunPlanner
{
    public static int[] CompileRunPlan(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var runLengths = new List<int>();
        var index = 0;
        while (index < devices.Count)
        {
            var runLength = GetBatchRunLength(devices, index, splitPc10BlockBoundaries);
            runLengths.Add(runLength);
            index += runLength;
        }

        return runLengths.ToArray();
    }

    public static int[] CompileRunPlan(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var runLengths = new List<int>();
        var index = 0;
        while (index < items.Count)
        {
            var runLength = GetBatchRunLength(items, index, splitPc10BlockBoundaries);
            runLengths.Add(runLength);
            index += runLength;
        }

        return runLengths.ToArray();
    }

    public static string BuildRunPlanKey(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var builder = new StringBuilder(2 + (devices.Count * 16));
        builder.Append(splitPc10BlockBoundaries ? '1' : '0');
        for (var i = 0; i < devices.Count; i++)
        {
            builder.Append('\u001F');
            builder.Append(devices[i].Text);
        }

        return builder.ToString();
    }

    public static string BuildRunPlanKey(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var builder = new StringBuilder(2 + (items.Count * 16));
        builder.Append(splitPc10BlockBoundaries ? '1' : '0');
        for (var i = 0; i < items.Count; i++)
        {
            builder.Append('\u001F');
            builder.Append(items[i].Device.Text);
        }

        return builder.ToString();
    }

    public static int GetBatchRunLength(IReadOnlyList<ResolvedDevice> devices, int startIndex, bool splitPc10BlockBoundaries)
    {
        var firstDevice = devices[startIndex];
        var key = GetBatchGroupKey(firstDevice);
        if (key is null)
        {
            return 1;
        }

        var pc10Block = splitPc10BlockBoundaries ? GetPc10BatchBlock(firstDevice) : null;
        var index = startIndex + 1;
        while (index < devices.Count
            && GetBatchGroupKey(devices[index]) == key
            && (!splitPc10BlockBoundaries || GetPc10BatchBlock(devices[index]) == pc10Block))
        {
            index++;
        }

        return index - startIndex;
    }

    public static int GetBatchRunLength(IReadOnlyList<(ResolvedDevice Device, object Value)> items, int startIndex, bool splitPc10BlockBoundaries)
    {
        var firstDevice = items[startIndex].Device;
        var key = GetBatchGroupKey(firstDevice);
        if (key is null)
        {
            return 1;
        }

        var pc10Block = splitPc10BlockBoundaries ? GetPc10BatchBlock(firstDevice) : null;
        var index = startIndex + 1;
        while (index < items.Count
            && GetBatchGroupKey(items[index].Device) == key
            && (!splitPc10BlockBoundaries || GetPc10BatchBlock(items[index].Device) == pc10Block))
        {
            index++;
        }

        return index - startIndex;
    }

    public static string? GetBatchGroupKey(ResolvedDevice device)
    {
        return device.Scheme switch
        {
            "basic-word" => "basic-word",
            "basic-byte" => "basic-byte",
            "ext-bit" or "program-bit" => "ext-bit",
            "ext-word" or "program-word" => "ext-word",
            "ext-byte" or "program-byte" => "ext-byte",
            "pc10-bit" => "pc10-bit",
            "pc10-word" => "pc10-word",
            "pc10-byte" => "pc10-byte",
            _ => null,
        };
    }

    public static int? GetPc10BatchBlock(ResolvedDevice device)
    {
        return device.Scheme switch
        {
            "pc10-bit" or "pc10-word" or "pc10-byte" => Require(device.Address32, "pc10 addr32") >> 16,
            _ => null,
        };
    }

    public static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<ResolvedDevice> devices, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveAddress32Start(devices, step, out start))
        {
            return false;
        }

        var block = Require(devices[0].Address32, "pc10 addr32") >> 16;
        for (var i = 1; i < devices.Count; i++)
        {
            if ((Require(devices[i].Address32, "pc10 addr32") >> 16) != block)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<(ResolvedDevice Device, object Value)> items, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveAddress32Start(items, step, out start))
        {
            return false;
        }

        var block = Require(items[0].Device.Address32, "pc10 addr32") >> 16;
        for (var i = 1; i < items.Count; i++)
        {
            if ((Require(items[i].Device.Address32, "pc10 addr32") >> 16) != block)
            {
                return false;
            }
        }

        return true;
    }

    public static int GetConsecutivePc10WordSegmentLength(IReadOnlyList<ResolvedDevice> devices, int startIndex)
    {
        var startAddress = Require(devices[startIndex].Address32, "pc10 addr32");
        var block = startAddress >> 16;
        var length = 1;
        var expectedAddress = startAddress + 2;
        for (var i = startIndex + 1; i < devices.Count; i++)
        {
            var currentAddress = Require(devices[i].Address32, "pc10 addr32");
            if ((currentAddress >> 16) != block || currentAddress != expectedAddress)
            {
                break;
            }

            length++;
            expectedAddress += 2;
        }

        return length;
    }

    private static bool TryGetConsecutiveAddress32Start(IReadOnlyList<ResolvedDevice> devices, int step, out int start)
    {
        start = default;
        if (devices.Count == 0)
        {
            return false;
        }

        var first = devices[0].Address32;
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            var current = devices[i].Address32;
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetConsecutiveAddress32Start(IReadOnlyList<(ResolvedDevice Device, object Value)> items, int step, out int start)
    {
        start = default;
        if (items.Count == 0)
        {
            return false;
        }

        var first = items[0].Device.Address32;
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < items.Count; i++)
        {
            var current = items[i].Device.Address32;
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static int Require(int? value, string label)
    {
        return value ?? throw new ArgumentException($"Resolved device missing {label}");
    }
}
