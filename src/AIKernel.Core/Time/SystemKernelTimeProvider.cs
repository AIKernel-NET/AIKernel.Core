namespace AIKernel.Core.Time;

/// <summary>
/// 通常実行用の KernelTimeProvider です。
///
/// 物理装置としての TimeProvider をそのまま反映します。
/// EN: ただし利用側は TimeProvider を直接参照せず、IKernelClock 経由で時刻を取得します。
/// [EN] Documents this public package API member. [JA] SystemKernelTimeProvider を表します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.SystemKernelTimeProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.SystemKernelTimeProvider']/summary" />
public sealed class SystemKernelTimeProvider : KernelTimeProvider
{
    /// <summary>[EN] Documents this public package API member. [JA] SystemKernelTimeProvider を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']/summary" />
    public SystemKernelTimeProvider()
        : this(TimeProvider.System)
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] SystemKernelTimeProvider を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']/summary" />
    public SystemKernelTimeProvider(TimeProvider baseProvider)
        : base(baseProvider, isReplaying: false)
    {
    }
}
