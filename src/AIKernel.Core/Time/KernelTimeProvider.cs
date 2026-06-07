using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// AIKernel.NET における「時間の法」を表す TimeProvider です。
///
/// TimeProvider は .NET 標準の物理的な時計装置です。
/// 一方、KernelTimeProvider は AIKernel がその時刻に与える意味を扱います。
///
/// 設計意図:
/// - 物理装置としての TimeProvider は壊れる、差し替わる、環境により揺らぐ。
/// - しかし Kernel の replay / snapshot / audit における時間の意味は揺らいではならない。
/// - そのため、TimeProvider を直接 Core 全域に配るのではなく、
///   KernelTimeProvider で意味論を付与し、IKernelClock で統一的に参照します。
///
/// v0.1.0 では標準 TimeProvider への委譲と固定時刻 replay を提供します。
/// v0.2.0 以降で HLC、署名付き時刻、外部時刻証明などを追加する席をここに確保します。
/// </summary>
public abstract class KernelTimeProvider : TimeProvider
{
    protected KernelTimeProvider(
        TimeProvider baseProvider,
        bool isReplaying = false,
        DateTimeOffset? fixedUtcNow = null)
    {
        BaseProvider = baseProvider ?? throw new ArgumentNullException(nameof(baseProvider));
        IsReplaying = isReplaying;
        FixedUtcNow = fixedUtcNow?.ToUniversalTime();
    }

    /// <summary>
    /// 物理的な時計装置です。
    /// KernelTimeProvider はこの装置へ標準 TimeProvider 機能を委譲します。
    /// </summary>
    protected TimeProvider BaseProvider { get; }

    protected DateTimeOffset? FixedUtcNow { get; }

    /// <summary>
    /// 決定論的リプレイ中かどうかを表します。
    /// </summary>
    public virtual bool IsReplaying { get; }

    /// <summary>
    /// 時刻ソースの信頼度です。
    ///
    /// v0.1.0 では 1.0 を返します。
    /// 将来的に NTP、署名付き時刻、外部監査時刻などを導入した場合、
    /// その信頼性をここで表現します。
    /// </summary>
    public virtual double ReliabilityScore => 1.0;

    public override DateTimeOffset GetUtcNow()
    {
        if (FixedUtcNow is not null)
        {
            return FixedUtcNow.Value;
        }

        return BaseProvider.GetUtcNow();
    }

    public override TimeZoneInfo LocalTimeZone => BaseProvider.LocalTimeZone;

    public override long TimestampFrequency => BaseProvider.TimestampFrequency;

    public override long GetTimestamp()
    {
        return BaseProvider.GetTimestamp();
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        return BaseProvider.CreateTimer(callback, state, dueTime, period);
    }

    /// <summary>
    /// Kernel の論理時刻を取得します。
    ///
    /// v0.1.0 では GetUtcNow() を単純にラップします。
    /// 将来的に HLC や署名付き時刻を導入しても、利用側はこの API を使い続けられます。
    /// </summary>
    public virtual KernelTimestamp GetLogicalTimestamp()
    {
        return new KernelTimestamp
        {
            UtcDateTime = GetUtcNow().ToUniversalTime(),
            SourceId = IsReplaying ? "replay" : "system"
        };
    }
}
