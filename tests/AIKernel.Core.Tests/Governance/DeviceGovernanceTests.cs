using AIKernel.Common.Results;
using AIKernel.Core.Governance;
using AIKernel.Kernel.Governance;

namespace AIKernel.Core.Tests.Governance;

public sealed class DeviceGovernanceTests
{
    [Fact]
    public void AdmissionBoundsTierCapacityAndPcieTraffic()
    {
        var policy = new ResourceAdmissibilityPolicy();
        var request = new InferenceResourceRequest(65_536, 4, 262_144, 4, 64, 8, 24);
        var budget = new InferenceResourceBudget(4096, 4096, 8192, 8192, long.MaxValue, true, 4096, 4096);
        var allowed = policy.Evaluate(request, budget);
        Assert.True(allowed.IsSuccess);
        Assert.Equal(32_768, allowed.Value.EncodedWeightBytes);
        Assert.Equal(4, allowed.Value.KvBlocks);
        Assert.Equal(2, allowed.Value.MinimumVramBlocks);
        Assert.Equal("resource_budget_exceeded", policy.Evaluate(request, budget with { AvailableVramBytes = 4096 }).Error?.Code);
        Assert.Equal("resource_pcie_budget_exceeded", policy.Evaluate(request,
            budget with { MaxPcieBytes = allowed.Value.EstimatedPcieBytes - 1 }).Error?.Code);
        var cpu = budget with { UseGpu = false, AvailableVramBytes = 0, WeightVramBytes = 0, KvVramBytes = 0, MaxPcieBytes = 0 };
        Assert.Equal(0, policy.Evaluate(request, cpu).Value.EstimatedPcieBytes);
    }

    [Fact]
    public async Task EmergencyGetsTheNextLayerAndTheBatchResumes()
    {
        var scheduler = new LayerQosScheduler();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var spilled = 0;
        using var batch = scheduler.Enter(InferencePriority.BackgroundBatch, () =>
        {
            Interlocked.Increment(ref spilled);
            return Result<int>.Success(2);
        }, timeout.Token).Value!;
        var emergency = Task.Run(() =>
        {
            var entry = scheduler.Enter(InferencePriority.Emergency, static () => Result<int>.Success(0), timeout.Token);
            Assert.True(entry.IsSuccess);
            using var lease = entry.Value!;
            Assert.Equal(1, Volatile.Read(ref spilled));
            Assert.False(lease.Checkpoint().Value);
        }, timeout.Token);

        Assert.True(SpinWait.SpinUntil(() => scheduler.WaitingCount == 1, TimeSpan.FromSeconds(10)));
        var result = batch.Checkpoint();
        Assert.True(result.IsSuccess && result.Value);
        Assert.Equal(1, batch.Preemptions);
        await emergency;
    }

    [Fact]
    public async Task FailedSpillClosesTheQueueBeforeAnotherRequestRuns()
    {
        var scheduler = new LayerQosScheduler();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var batch = scheduler.Enter(InferencePriority.BackgroundBatch, static () => default, timeout.Token).Value!;
        var urgent = Task.Run(() => scheduler.Enter(InferencePriority.Emergency,
            static () => Result<int>.Success(0), timeout.Token), timeout.Token);

        Assert.True(SpinWait.SpinUntil(() => scheduler.WaitingCount == 1, TimeSpan.FromSeconds(10)));
        Assert.Equal("device_preemption_failed", batch.Checkpoint().Error?.Code);
        var blocked = await urgent;
        Assert.Equal("device_preemption_failed", blocked.Error?.Code);
    }

    [Fact]
    public void UncontendedLayerCheckpointsAllocateNoManagedBytes()
    {
        var scheduler = new LayerQosScheduler();
        using var lease = scheduler.Enter(InferencePriority.Normal, static () => Result<int>.Success(0), TestContext.Current.CancellationToken).Value!;
        for (var i = 0; i < 100; i++) _ = lease.Checkpoint();
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) _ = lease.Checkpoint();
        var end = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(start, end);
    }

    [Fact]
    public void HealthLimitsRejectHotDevicesAndThrottleOnlyBatch()
    {
        var sample = Result<DeviceHealthSnapshot>.Success(new(81, 90_000, 100_000,
            100, 200, 100, null, null, null, null));
        Assert.Equal("device_temperature_exceeded",
            DeviceHealthPolicy.Evaluate(sample, new(80, 90), InferencePriority.Emergency).Error?.Code);
        Assert.Equal("device_power_throttled",
            DeviceHealthPolicy.Evaluate(sample, new(85, 90), InferencePriority.BackgroundBatch).Error?.Code);
        Assert.True(DeviceHealthPolicy.Evaluate(sample, new(85, 90), InferencePriority.Normal).IsSuccess);
    }

    [Fact]
    public void MissingNvmlOrAnInvalidDeviceReturnsAStatus()
    {
        var open = NvmlDeviceTelemetry.Open();
        if (open.IsSuccess)
        {
            using var telemetry = open.Value!;
            Assert.Equal("device_telemetry_unavailable", telemetry.Read("ffff:ff:ff.0").Error?.Code);
        }
        else
        {
            Assert.Equal("device_telemetry_unavailable", open.Error?.Code);
        }
    }
}
