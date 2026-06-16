using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// 決定論的リプレイ用の KernelTimeProvider です。
///
/// 常に固定された UTC 時刻を返します。
/// これにより、VFS Snapshot、Provider Health、Context assembly、起動時検証などに
/// EN: 同一時刻を注入でき、同じ入力から同じ replay 結果を再現できます。
/// EN: Documentation for public API. JA: ReplayKernelTimeProvider を表します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.ReplayKernelTimeProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.ReplayKernelTimeProvider']/summary" />
public sealed class ReplayKernelTimeProvider : KernelTimeProvider
{
    /// <summary>EN: Documentation for public API. JA: ReplayKernelTimeProvider を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']/summary" />
    public ReplayKernelTimeProvider(DateTimeOffset fixedUtcNow)
        : this(fixedUtcNow, TimeProvider.System)
    {
    }

    /// <summary>EN: Documentation for public API. JA: ReplayKernelTimeProvider を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.#ctor']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: FixedUtcDateTime を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.ReplayKernelTimeProvider.FixedUtcDateTime']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.ReplayKernelTimeProvider.FixedUtcDateTime']/summary" />
    public DateTimeOffset FixedUtcDateTime { get; }

    /// <summary>EN: Documentation for public API. JA: GetLogicalTimestamp を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.GetLogicalTimestamp']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.ReplayKernelTimeProvider.GetLogicalTimestamp']/summary" />
    public override KernelTimestamp GetLogicalTimestamp()
    {
        return new KernelTimestamp
        {
            UtcDateTime = FixedUtcDateTime,
            SourceId = "replay"
        };
    }
}
