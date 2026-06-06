namespace AIKernel.Core.Time;

/// <summary>
/// 通常実行用の KernelTimeProvider です。
///
/// 物理装置としての TimeProvider をそのまま反映します。
/// ただし利用側は TimeProvider を直接参照せず、IKernelClock 経由で時刻を取得します。
/// </summary>
public sealed class SystemKernelTimeProvider : KernelTimeProvider
{
    public SystemKernelTimeProvider()
        : this(TimeProvider.System)
    {
    }

    public SystemKernelTimeProvider(TimeProvider baseProvider)
        : base(baseProvider, isReplaying: false)
    {
    }
}
