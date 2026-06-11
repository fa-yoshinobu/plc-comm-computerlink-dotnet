namespace PlcComm.Toyopuc.Tests;

public class DeviceRunPlannerTests
{
    [Fact]
    public void CompileRunPlan_GroupsSameBatchClassAndPreservesKeyFormat()
    {
        var devices = new[]
        {
            Resolve("P1-D0100"),
            Resolve("P1-D0101"),
            Resolve("P1-M0100"),
        };

        Assert.Equal(new[] { 2, 1 }, DeviceRunPlanner.CompileRunPlan(devices, splitPc10BlockBoundaries: true));
        Assert.Equal(2, DeviceRunPlanner.GetBatchRunLength(devices, startIndex: 0, splitPc10BlockBoundaries: true));
        Assert.Equal("1\u001FP1-D0100\u001FP1-D0101\u001FP1-M0100", DeviceRunPlanner.BuildRunPlanKey(devices, splitPc10BlockBoundaries: true));
    }

    [Fact]
    public void CompileRunPlan_SplitsPc10WordsAtCurrentBlockBoundaryWhenRequested()
    {
        var devices = new[]
        {
            Resolve("FR007FFE"),
            Resolve("FR007FFF"),
            Resolve("FR008000"),
        };

        Assert.Equal(new[] { 2, 1 }, DeviceRunPlanner.CompileRunPlan(devices, splitPc10BlockBoundaries: true));
        Assert.Equal(new[] { 3 }, DeviceRunPlanner.CompileRunPlan(devices, splitPc10BlockBoundaries: false));
        Assert.Equal(2, DeviceRunPlanner.GetConsecutivePc10WordSegmentLength(devices, startIndex: 0));
    }

    [Fact]
    public void CompileRunPlan_KeepsCurrentPc10BlockPolicyForMixedAreas()
    {
        var devices = new[]
        {
            Resolve("U08000"),
            Resolve("EB00000"),
        };

        Assert.Equal(new[] { 1, 1 }, DeviceRunPlanner.CompileRunPlan(devices, splitPc10BlockBoundaries: true));
        Assert.Equal(new[] { 2 }, DeviceRunPlanner.CompileRunPlan(devices, splitPc10BlockBoundaries: false));
    }

    [Fact]
    public void CompileRunPlan_ForWriteItemsPreservesCurrentGroupingRules()
    {
        var items = new[]
        {
            (Device: Resolve("P1-D0100"), Value: (object)0x1111),
            (Device: Resolve("P1-D0101"), Value: (object)0x2222),
            (Device: Resolve("P1-M0100"), Value: (object)1),
        };
        var pc10Items = new[]
        {
            (Device: Resolve("FR007FFE"), Value: (object)0x1111),
            (Device: Resolve("FR007FFF"), Value: (object)0x2222),
            (Device: Resolve("FR008000"), Value: (object)0x3333),
        };

        Assert.Equal(new[] { 2, 1 }, DeviceRunPlanner.CompileRunPlan(items, splitPc10BlockBoundaries: true));
        Assert.Equal(new[] { 2, 1 }, DeviceRunPlanner.CompileRunPlan(pc10Items, splitPc10BlockBoundaries: true));
        Assert.Equal(new[] { 3 }, DeviceRunPlanner.CompileRunPlan(pc10Items, splitPc10BlockBoundaries: false));
    }

    [Fact]
    public void TryGetConsecutivePc10BlockStart_RejectsCurrentCrossBlockRuns()
    {
        var sameBlock = new[]
        {
            Resolve("FR007FFE"),
            Resolve("FR007FFF"),
        };
        var crossBlock = new[]
        {
            Resolve("FR007FFF"),
            Resolve("FR008000"),
        };

        Assert.True(DeviceRunPlanner.TryGetConsecutivePc10BlockStart(sameBlock, step: 2, out var start));
        Assert.Equal(ToyopucAddress.EncodeFrWordAddr32(0x007FFE), start);
        Assert.False(DeviceRunPlanner.TryGetConsecutivePc10BlockStart(crossBlock, step: 2, out _));
    }

    private static ResolvedDevice Resolve(string device)
    {
        return ToyopucDeviceResolver.ResolveDevice(device, ToyopucAddressingOptions.Nano10GxCompatible);
    }
}
