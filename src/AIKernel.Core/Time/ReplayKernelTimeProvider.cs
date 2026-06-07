using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// 決定論的リプレイ用の KernelTimeProvider です。
///
/// 常に固定された UTC 時刻を返します。
/// これにより、VFS Snapshot、Provider Health、Context assembly、起動時検証などに
/// 同一時刻を注入でき、同じ入力から同じ replay 結果を再現できます。
/// </summary>
public sealed class ReplayKernelTimeProvider : KernelTimeProvider
{
    public ReplayKernelTimeProvider(DateTimeOffset fixedUtcNow)
        : this(fixedUtcNow, TimeProvider.System)
    {
    }

    public ReplayKernelTimeProvider(
        DateTimeOffset fixedUtcNow,
        TimeProvider baseProvider)
        : base(
            baseProvider,
            isReplaying: true,
            fixedUtcNow: fixedUtcNow.ToUniversalTime())
    {
        FixedUtcDateTime = fixedUtcNow.ToUniversalTime();
    }

    public DateTimeOffset FixedUtcDateTime { get; }

    public override KernelTimestamp GetLogicalTimestamp()
    {
        return new KernelTimestamp
        {
            UtcDateTime = FixedUtcDateTime,
            SourceId = "replay"
        };
    }
}
