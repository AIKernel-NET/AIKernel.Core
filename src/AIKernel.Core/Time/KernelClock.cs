using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// IKernelClock の標準実装です。
///
/// Physical と Logical を明示的に保持し、利用側には Now を統一 API として公開します。
/// 通常実行では SystemKernelTimeProvider を、Replay 実行では ReplayKernelTimeProvider を
/// Logical として渡します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelClock']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelClock']" />
public sealed class KernelClock :
    IKernelClock,
    AIKernel.Abstractions.Time.IKernelClock
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.#ctor']" />
    public KernelClock(
        TimeProvider physical,
        KernelTimeProvider logical)
    {
        Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        Logical = logical ?? throw new ArgumentNullException(nameof(logical));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Physical']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Physical']" />
    public TimeProvider Physical { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Logical']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelClock.Logical']" />
    public KernelTimeProvider Logical { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.Now']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.Now']" />
    public DateTimeOffset Now
    {
        get
        {
            var logicalNow = Logical.GetUtcNow();
            return logicalNow == default ? Physical.GetUtcNow() : logicalNow;
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.IsReplaying']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.IsReplaying']" />
    public bool IsReplaying => Logical.IsReplaying;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.ReliabilityScore']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelClock.ReliabilityScore']" />
    public double ReliabilityScore => Logical.ReliabilityScore;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.GetLogicalTimestamp']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.GetLogicalTimestamp']" />
    public KernelTimestamp GetLogicalTimestamp()
    {
        return Logical.GetLogicalTimestamp();
    }

    KernelTimestamp AIKernel.Abstractions.Time.IKernelClock.GetCurrentTimestamp()
    {
        return GetLogicalTimestamp();
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.System']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.System']" />
    public static KernelClock System()
    {
        var physical = TimeProvider.System;
        var logical = new SystemKernelTimeProvider(physical);

        return new KernelClock(physical, logical);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.Replay']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelClock.Replay']" />
    public static KernelClock Replay(DateTimeOffset fixedUtcNow)
    {
        var physical = TimeProvider.System;
        var logical = new ReplayKernelTimeProvider(fixedUtcNow, physical);

        return new KernelClock(physical, logical);
    }
}
