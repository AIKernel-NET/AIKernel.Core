namespace AIKernel.Core.Governance;

using System.Text;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

/// <summary>
/// EN: Classifies structural CTG rejection outcomes without adding rejection rules. JA: 拒否ルールを追加せず、構造的な CTG 拒否結果を分類します。
/// </summary>
public sealed class CtgRejectReasonClassifier
{
    private readonly IKernelClock _clock;

    /// <summary>
    /// EN: Initializes a classifier using the system kernel clock. JA: システムカーネルクロックを使用する分類器を初期化します。
    /// </summary>
    public CtgRejectReasonClassifier()
        : this(KernelClock.System())
    {
    }

    /// <summary>
    /// EN: Initializes a classifier using the supplied kernel clock. JA: 指定されたカーネルクロックを使用する分類器を初期化します。
    /// </summary>
    /// <param name="clock">EN: The clock used for observation timestamps. JA: 観測時刻に使用するクロックです。</param>
    public CtgRejectReasonClassifier(IKernelClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// EN: Classifies a denied decision gate result from the vote-only input. JA: vote-only 入力から拒否された決定ゲート結果を分類します。
    /// </summary>
    /// <param name="input">EN: The gate input. JA: ゲート入力です。</param>
    /// <param name="canonReferences">EN: Canon references to attach. JA: 付与する正典参照です。</param>
    /// <returns>EN: The rejection reason. JA: 拒否理由を返します。</returns>
    public RejectReasonInfo ClassifyDecisionGateDeny(
        GateInput input,
        IReadOnlyList<CanonReference>? canonReferences = null)
    {
        var kind = input.Ethos == CouncilVoteValue.Reject
            ? RejectReasonKind.EthosVeto
            : HasUnknownVote(input)
                ? RejectReasonKind.FailClosed
                : ApproveCount(input) < 2
                    ? RejectReasonKind.FailClosed
                    : RejectReasonKind.ImplicitDeny;

        return Create(
            reasonId: "ctg.decision_gate." + ToMetadataValue(kind),
            kind: kind,
            message: kind switch
            {
                RejectReasonKind.EthosVeto => "Ethos rejected the step.",
                RejectReasonKind.FailClosed => "Decision gate failed closed.",
                _ => "Decision gate denied the step implicitly."
            },
            canonReferences: canonReferences,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CtgGovernanceMetadataKeys.LogosVote] = input.Logos.ToString(),
                [CtgGovernanceMetadataKeys.EthosVote] = input.Ethos.ToString(),
                [CtgGovernanceMetadataKeys.PathosVote] = input.Pathos.ToString(),
                [CtgGovernanceMetadataKeys.ApproveCount] = ApproveCount(input).ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
    }

    /// <summary>
    /// EN: Classifies an empty trajectory as implicit deny. JA: 空の軌道を implicit deny として分類します。
    /// </summary>
    /// <param name="canonReferences">EN: Canon references to attach. JA: 付与する正典参照です。</param>
    /// <returns>EN: The rejection reason. JA: 拒否理由を返します。</returns>
    public RejectReasonInfo ClassifyEmptyTrajectory(
        IReadOnlyList<CanonReference>? canonReferences = null)
    {
        return Create(
            reasonId: "ctg.trajectory.implicit_deny",
            kind: RejectReasonKind.ImplicitDeny,
            message: "Trajectory gate received no step traces.",
            canonReferences: canonReferences);
    }

    /// <summary>
    /// EN: Classifies a halted trajectory caused by the first denied step. JA: 最初に拒否されたステップにより停止した軌道を分類します。
    /// </summary>
    /// <param name="step">EN: The first denied step. JA: 最初に拒否されたステップです。</param>
    /// <param name="stepIndex">EN: The zero-based step index. JA: 0 始まりのステップ位置です。</param>
    /// <param name="canonReferences">EN: Canon references to attach. JA: 付与する正典参照です。</param>
    /// <returns>EN: The rejection reason. JA: 拒否理由を返します。</returns>
    public RejectReasonInfo ClassifyDeniedStep(
        StepGovernanceTrace step,
        int stepIndex,
        IReadOnlyList<CanonReference>? canonReferences = null)
    {
        ArgumentNullException.ThrowIfNull(step);

        return Create(
            reasonId: "ctg.trajectory.step_denied",
            kind: RejectReasonKind.StepDenied,
            message: "Trajectory gate halted at the first denied step.",
            canonReferences: canonReferences,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CtgGovernanceMetadataKeys.FirstFailingStepIndex] = stepIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [CtgGovernanceMetadataKeys.FirstFailingStepId] = step.StepId
            });
    }

    /// <summary>
    /// EN: Converts a rejection reason kind to canonical uppercase snake case. JA: 拒否理由種別を正規の大文字 snake case に変換します。
    /// </summary>
    /// <param name="kind">EN: The reason kind. JA: 理由種別です。</param>
    /// <returns>EN: The canonical reason code. JA: 正規理由コードを返します。</returns>
    public static string ToReasonCode(RejectReasonKind kind)
    {
        return kind == RejectReasonKind.Unknown
            ? "UNKNOWN"
            : ToUpperSnakeCase(kind.ToString());
    }

    internal static int ApproveCount(GateInput input)
    {
        var count = 0;

        if (input.Logos == CouncilVoteValue.Approve)
        {
            count++;
        }

        if (input.Ethos == CouncilVoteValue.Approve)
        {
            count++;
        }

        if (input.Pathos == CouncilVoteValue.Approve)
        {
            count++;
        }

        return count;
    }

    internal static bool HasUnknownVote(GateInput input)
    {
        return input.Logos == CouncilVoteValue.Unknown ||
               input.Ethos == CouncilVoteValue.Unknown ||
               input.Pathos == CouncilVoteValue.Unknown;
    }

    private RejectReasonInfo Create(
        string reasonId,
        RejectReasonKind kind,
        string message,
        IReadOnlyList<CanonReference>? canonReferences,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new RejectReasonInfo
        {
            ReasonId = reasonId,
            Kind = kind,
            ReasonCode = ToReasonCode(kind),
            Message = message,
            CanonReferences = canonReferences ?? [],
            ObservedAt = _clock.Now,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private static string ToMetadataValue(RejectReasonKind kind)
    {
        return ToReasonCode(kind).ToLowerInvariant();
    }

    private static string ToUpperSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (index > 0 && char.IsUpper(current))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}
