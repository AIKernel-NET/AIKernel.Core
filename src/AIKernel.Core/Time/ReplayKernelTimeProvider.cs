using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// 決定論的リプレイ用の KernelTimeProvider です。
///
/// 常に固定された UTC 時刻を返します。
/// これにより、VFS Snapshot、Provider Health、Context assembly、起動時検証などに
/// 同一時刻を注入でき、同じ入力から同じ replay 結果を再現できます。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.ReplayKernelTimeProvider']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.ReplayKernelTimeProvider']" />
public sealed class ReplayKernelTimeProvider : KernelTimeProvider
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']" />
    public ReplayKernelTimeProvider(DateTimeOffset fixedUtcNow)
        : this(fixedUtcNow, TimeProvider.System)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.ReplayKernelTimeProvider.FixedUtcDateTime']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.ReplayKernelTimeProvider.FixedUtcDateTime']" />
    public DateTimeOffset FixedUtcDateTime { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.GetLogicalTimestamp']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.GetLogicalTimestamp']" />
    public override KernelTimestamp GetLogicalTimestamp()
    {
        return new KernelTimestamp
        {
            UtcDateTime = FixedUtcDateTime,
            SourceId = "replay"
        };
    }
}
