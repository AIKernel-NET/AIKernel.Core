namespace AIKernel.Core.Time;

/// <summary>
/// IKernelClock の標準実装です。
///
/// Physical と Logical を明示的に保持し、利用側には Now を統一 API として公開します。
/// 通常実行では SystemKernelTimeProvider を、Replay 実行では ReplayKernelTimeProvider を
/// Logical として渡します。
/// </summary>
public sealed class KernelClock : IKernelClock
{
    public KernelClock(
        TimeProvider physical,
        KernelTimeProvider logical)
    {
        Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        Logical = logical ?? throw new ArgumentNullException(nameof(logical));
    }

    public TimeProvider Physical { get; }

    public KernelTimeProvider Logical { get; }

    public DateTimeOffset Now
    {
        get
        {
            var logicalNow = Logical.GetUtcNow();
            return logicalNow == default ? Physical.GetUtcNow() : logicalNow;
        }
    }

    public bool IsReplaying => Logical.IsReplaying;

    public double ReliabilityScore => Logical.ReliabilityScore;

    public KernelTimestamp GetLogicalTimestamp()
    {
        return Logical.GetLogicalTimestamp();
    }

    public static KernelClock System()
    {
        var physical = TimeProvider.System;
        var logical = new SystemKernelTimeProvider(physical);

        return new KernelClock(physical, logical);
    }

    public static KernelClock Replay(DateTimeOffset fixedUtcNow)
    {
        var physical = TimeProvider.System;
        var logical = new ReplayKernelTimeProvider(fixedUtcNow, physical);

        return new KernelClock(physical, logical);
    }
}
