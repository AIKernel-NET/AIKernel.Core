namespace AIKernel.Core.Time;

using AIKernel.Common.Results;
using AIKernel.Dtos.Time;

/// <summary>
/// IKernelClock の標準実装です。
///
/// Physical と Logical を明示的に保持し、利用側には Now を統一 API として公開します。
/// 通常実行では SystemKernelTimeProvider を、Replay 実行では ReplayKernelTimeProvider を
/// Logical として渡します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelClock']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelClock']/summary" />
public sealed class KernelClock :
    IKernelClock,
    AIKernel.Abstractions.Time.IKernelClock
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.#ctor']/summary" />
    public KernelClock(
        TimeProvider physical,
        KernelTimeProvider logical)
    {
        Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        Logical = logical ?? throw new ArgumentNullException(nameof(logical));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Physical']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Physical']/summary" />
    public TimeProvider Physical { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Logical']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Logical']/summary" />
    public KernelTimeProvider Logical { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.Now']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.Now']/summary" />
    public DateTimeOffset Now
    {
        get
        {
            var logicalNow = Logical.GetUtcNow();
            return LogicalTime(logicalNow).Match(() => Physical.GetUtcNow(), value => value);
        }
    }

    private static Option<DateTimeOffset> LogicalTime(DateTimeOffset logicalNow)
        => logicalNow == default
            ? Option<DateTimeOffset>.None()
            : Option<DateTimeOffset>.Some(logicalNow);

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.IsReplaying']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.IsReplaying']/summary" />
    public bool IsReplaying => Logical.IsReplaying;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.ReliabilityScore']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.ReliabilityScore']/summary" />
    public double ReliabilityScore => Logical.ReliabilityScore;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.GetLogicalTimestamp']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.GetLogicalTimestamp']/summary" />
    public KernelTimestamp GetLogicalTimestamp()
    {
        return Logical.GetLogicalTimestamp();
    }

    KernelTimestamp AIKernel.Abstractions.Time.IKernelClock.GetCurrentTimestamp()
    {
        return GetLogicalTimestamp();
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.System']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.System']/summary" />
    public static KernelClock System()
    {
        var physical = TimeProvider.System;
        var logical = new SystemKernelTimeProvider(physical);

        return new KernelClock(physical, logical);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.Replay']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.Replay']/summary" />
    public static KernelClock Replay(DateTimeOffset fixedUtcNow)
    {
        var physical = TimeProvider.System;
        var logical = new ReplayKernelTimeProvider(fixedUtcNow, physical);

        return new KernelClock(physical, logical);
    }
}
