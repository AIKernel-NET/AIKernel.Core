namespace AIKernel.Core.Time;

using AIKernel.Common.Results;
using AIKernel.Dtos.Time;

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
/// EN: v0.2.0 以降で HLC、署名付き時刻、外部時刻証明などを追加する席をここに確保します。
/// EN: Documentation for public API. JA: KernelTimeProvider を表します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelTimeProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.KernelTimeProvider']/summary" />
public abstract class KernelTimeProvider : TimeProvider
{
    /// <summary>EN: Initializes a new instance for the KernelTimeProvider AIKernel contract surface. JA: KernelTimeProvider AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
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

    /// <summary>EN: Gets the FixedUtcNow value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される FixedUtcNow 値を取得します。</summary>
    protected DateTimeOffset? FixedUtcNow { get; }

    /// <summary>
    /// EN: 決定論的リプレイ中かどうかを表します。
    /// EN: Documentation for public API. JA: IsReplaying を取得します。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelTimeProvider.IsReplaying']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Time.KernelTimeProvider.IsReplaying']/summary" />
    public virtual bool IsReplaying { get; }

    /// <summary>
    /// 時刻ソースの信頼度です。
    ///
    /// v0.1.0 では 1.0 を返します。
    /// 将来的に NTP、署名付き時刻、外部監査時刻などを導入した場合、
    /// EN: その信頼性をここで表現します。
    /// EN: Documentation for public API. JA: ReliabilityScore を取得します。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.ReliabilityScore']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.ReliabilityScore']/summary" />
    public virtual double ReliabilityScore => 1.0;

    /// <summary>EN: Documentation for public API. JA: GetUtcNow を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetUtcNow']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetUtcNow']/summary" />
    public override DateTimeOffset GetUtcNow()
    {
        if (FixedUtcNow is not null)
        {
            return FixedUtcNow.Value;
        }

        return BaseProvider.GetUtcNow();
    }

    /// <summary>EN: Documentation for public API. JA: LocalTimeZone を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.LocalTimeZone']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.LocalTimeZone']/summary" />
    public override TimeZoneInfo LocalTimeZone => BaseProvider.LocalTimeZone;

    /// <summary>EN: Documentation for public API. JA: TimestampFrequency を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.TimestampFrequency']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Time.KernelTimeProvider.TimestampFrequency']/summary" />
    public override long TimestampFrequency => BaseProvider.TimestampFrequency;

    /// <summary>EN: Documentation for public API. JA: GetTimestamp を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetTimestamp']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetTimestamp']/summary" />
    public override long GetTimestamp()
    {
        return BaseProvider.GetTimestamp();
    }

    /// <summary>EN: Documentation for public API. JA: CreateTimer を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.CreateTimer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.CreateTimer']/summary" />
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
    /// EN: 将来的に HLC や署名付き時刻を導入しても、利用側はこの API を使い続けられます。
    /// EN: Documentation for public API. JA: GetLogicalTimestamp を実行します。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetLogicalTimestamp']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Time.KernelTimeProvider.GetLogicalTimestamp']/summary" />
    public virtual KernelTimestamp GetLogicalTimestamp()
    {
        return new KernelTimestamp
        {
            UtcDateTime = GetUtcNow().ToUniversalTime(),
            SourceId = SourceId(IsReplaying)
        };
    }

    private static string SourceId(bool isReplaying)
        => MonadicDecision.SelectText(isReplaying, "system", "replay");
}
