namespace AIKernel.Core.Tests.Time;

using AIKernel.Core.Time;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class KernelClockTests
{
    [Fact]
    public void SystemClock_UsesSystemSemanticTime()
    {
        var clock = KernelClock.System();

        Assert.False(clock.IsReplaying);
        Assert.Equal(1.0, clock.ReliabilityScore);
        Assert.NotNull(clock.Physical);
        Assert.NotNull(clock.Logical);
    }

    [Fact]
    public void ReplayClock_ReturnsFixedNow()
    {
        var fixedUtcNow = new DateTimeOffset(
            2026,
            5,
            12,
            0,
            0,
            0,
            TimeSpan.Zero);

        var clock = KernelClock.Replay(fixedUtcNow);

        Assert.True(clock.IsReplaying);
        Assert.Equal(fixedUtcNow, clock.Now);
        Assert.Equal(fixedUtcNow, clock.Logical.GetUtcNow());
    }

    [Fact]
    public void ReplayClock_ReturnsReplayLogicalTimestamp()
    {
        var fixedUtcNow = new DateTimeOffset(
            2026,
            5,
            12,
            0,
            0,
            0,
            TimeSpan.Zero);

        var clock = KernelClock.Replay(fixedUtcNow);

        var timestamp = clock.GetLogicalTimestamp();

        Assert.Equal(fixedUtcNow, timestamp.UtcDateTime);
        Assert.Equal("replay", timestamp.SourceId);
    }

    [Fact]
    public void AddAIKernelCore_RegistersProvidedReplayClock()
    {
        var fixedUtcNow = new DateTimeOffset(
            2026,
            5,
            12,
            0,
            0,
            0,
            TimeSpan.Zero);

        var clock = KernelClock.Replay(fixedUtcNow);
        var services = new ServiceCollection();

        services.AddAIKernelCore(clock);

        using var provider = services.BuildServiceProvider();

        var resolvedClock = provider.GetRequiredService<IKernelClock>();
        var resolvedPhysical = provider.GetRequiredService<TimeProvider>();
        var resolvedLogical = provider.GetRequiredService<KernelTimeProvider>();

        Assert.Same(clock, resolvedClock);
        Assert.Same(clock.Physical, resolvedPhysical);
        Assert.Same(clock.Logical, resolvedLogical);
        Assert.Equal(fixedUtcNow, resolvedClock.Now);
    }
}
