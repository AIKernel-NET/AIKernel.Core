namespace AIKernel.Core.Time;

/// <summary>
/// 通常実行用の KernelTimeProvider です。
///
/// 物理装置としての TimeProvider をそのまま反映します。
/// ただし利用側は TimeProvider を直接参照せず、IKernelClock 経由で時刻を取得します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.SystemKernelTimeProvider']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.SystemKernelTimeProvider']" />
public sealed class SystemKernelTimeProvider : KernelTimeProvider
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']" />
    public SystemKernelTimeProvider()
        : this(TimeProvider.System)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.SystemKernelTimeProvider.#ctor']" />
    public SystemKernelTimeProvider(TimeProvider baseProvider)
        : base(baseProvider, isReplaying: false)
    {
    }
}
