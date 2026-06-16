using AIKernel.Dtos.Time;

namespace AIKernel.Core.Time;

/// <summary>
/// AIKernel 全域で使用する時間契約です。
///
/// この interface は、物理時間・論理時間・統一 API を意図的に分離します。
///
/// Physical:
///   .NET 標準の TimeProvider。
///   これは「装置」です。OS、テスト、外部環境に依存する物理的な時計です。
///
/// Logical:
///   KernelTimeProvider。
///   これは「法」です。Replay 中か、信頼性はどうか、論理時刻は何か、という
///   AIKernel 固有の時間意味論を扱います。
///
/// Now:
///   Kernel 全域で使う統一 API です。
///   原則として Logical を優先します。
///   これにより、VFS / Provider / Kernel / Hosting が物理装置へ直接触れず、
///   時間の意味論の下で現在時刻を取得できます。
///
/// 設計思想:
///   装置は壊れても、法は揺らがない。
///   TimeProvider がどのような物理時刻を返すかは環境依存ですが、
/// EN:   AIKernel の replay / audit / snapshot における時間の扱いは IKernelClock に集約します。
/// EN: Documentation for public API. JA: IKernelClock contract を定義します。
/// </summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.IKernelClock']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Time.IKernelClock']/summary" />
public interface IKernelClock
{
    /// <summary>EN: Gets the Physical value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Physical 値を取得します。</summary>
    TimeProvider Physical { get; }

    /// <summary>EN: Gets the Logical value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Logical 値を取得します。</summary>
    KernelTimeProvider Logical { get; }

    /// <summary>EN: Gets the Now value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Now 値を取得します。</summary>
    DateTimeOffset Now { get; }

    /// <summary>EN: Gets the IsReplaying value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される IsReplaying 値を取得します。</summary>
    bool IsReplaying { get; }

    /// <summary>EN: Gets the ReliabilityScore value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される ReliabilityScore 値を取得します。</summary>
    double ReliabilityScore { get; }

    /// <summary>EN: Executes the GetLogicalTimestamp operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで GetLogicalTimestamp 操作を実行します。</summary>
    KernelTimestamp GetLogicalTimestamp();
}
