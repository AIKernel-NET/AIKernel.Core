namespace AIKernel.Core.Governance;

using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

/// <summary>
/// EN: Builds CTG governance trace carriers without performing decision logic. JA: 判定ロジックを持たずに CTG 統治トレース carrier を組み立てます。
/// </summary>
public sealed class CtgGovernanceTraceBuilder
{
    private readonly IKernelClock _clock;

    /// <summary>
    /// EN: Initializes a trace builder using the system kernel clock. JA: システムカーネルクロックを使用するトレースビルダーを初期化します。
    /// </summary>
    public CtgGovernanceTraceBuilder()
        : this(KernelClock.System())
    {
    }

    /// <summary>
    /// EN: Initializes a trace builder using the supplied kernel clock. JA: 指定されたカーネルクロックを使用するトレースビルダーを初期化します。
    /// </summary>
    /// <param name="clock">EN: The clock used for trace timestamps. JA: トレース時刻に使用するクロックです。</param>
    public CtgGovernanceTraceBuilder(IKernelClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// EN: Builds a step trace from a council decision and a decision gate result. JA: 評議会決定と決定ゲート結果からステップトレースを組み立てます。
    /// </summary>
    /// <param name="decision">EN: The council decision. JA: 評議会決定です。</param>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <param name="stepId">EN: The step identifier. JA: ステップ識別子です。</param>
    /// <param name="traceId">EN: The trace identifier. JA: トレース識別子です。</param>
    /// <param name="correlationId">EN: The correlation identifier. JA: 相関識別子です。</param>
    /// <param name="canonReferences">EN: Canon references to attach. JA: 付与する正典参照です。</param>
    /// <param name="metadata">EN: Trace metadata. JA: トレースメタデータです。</param>
    /// <returns>EN: The step governance trace. JA: ステップ統治トレースを返します。</returns>
    public StepGovernanceTrace BuildStepTrace(
        CouncilDecision decision,
        DecisionGateResult decisionGate,
        string? stepId = null,
        string? traceId = null,
        string? correlationId = null,
        IReadOnlyList<CanonReference>? canonReferences = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(decisionGate);

        var councilEvaluation = new CouncilEvaluationResult
        {
            OperationId = decisionGate.OperationId,
            Succeeded = true,
            Decision = decision,
            RejectReasons = decision.RejectReasons,
            CanonReferences = decision.CanonReferences,
            ObservedAt = decision.ObservedAt,
            CorrelationId = correlationId ?? decision.CorrelationId ?? decisionGate.CorrelationId,
            TraceId = traceId ?? decision.TraceId ?? decisionGate.TraceId,
            Metadata = decision.Metadata
        };

        return BuildStepTrace(
            councilEvaluation,
            decisionGate,
            stepId,
            traceId,
            correlationId,
            canonReferences,
            metadata);
    }

    /// <summary>
    /// EN: Builds a step trace from a council evaluation result and a decision gate result. JA: 評議会評価結果と決定ゲート結果からステップトレースを組み立てます。
    /// </summary>
    /// <param name="councilEvaluation">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <param name="stepId">EN: The step identifier. JA: ステップ識別子です。</param>
    /// <param name="traceId">EN: The trace identifier. JA: トレース識別子です。</param>
    /// <param name="correlationId">EN: The correlation identifier. JA: 相関識別子です。</param>
    /// <param name="canonReferences">EN: Canon references to attach. JA: 付与する正典参照です。</param>
    /// <param name="metadata">EN: Trace metadata. JA: トレースメタデータです。</param>
    /// <returns>EN: The step governance trace. JA: ステップ統治トレースを返します。</returns>
    public StepGovernanceTrace BuildStepTrace(
        CouncilEvaluationResult councilEvaluation,
        DecisionGateResult decisionGate,
        string? stepId = null,
        string? traceId = null,
        string? correlationId = null,
        IReadOnlyList<CanonReference>? canonReferences = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(councilEvaluation);
        ArgumentNullException.ThrowIfNull(decisionGate);

        return new StepGovernanceTrace
        {
            TraceId = traceId ?? decisionGate.TraceId ?? councilEvaluation.TraceId ?? string.Empty,
            StepId = stepId ?? GetMetadataValue(decisionGate.Metadata, CtgGovernanceMetadataKeys.StepId),
            CouncilEvaluation = councilEvaluation,
            DecisionGate = decisionGate,
            CanonReferences = MergeCanonReferences(
                canonReferences,
                councilEvaluation.CanonReferences,
                councilEvaluation.Decision.CanonReferences,
                decisionGate.CanonReferences),
            RejectReasons = MergeRejectReasons(
                councilEvaluation.RejectReasons,
                councilEvaluation.Decision.RejectReasons,
                decisionGate.RejectReasons),
            ObservedAt = _clock.Now,
            CorrelationId = correlationId ?? decisionGate.CorrelationId ?? councilEvaluation.CorrelationId,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// EN: Builds a trajectory governance trace from supplied carrier values. JA: 指定された carrier 値から軌道統治トレースを組み立てます。
    /// </summary>
    /// <param name="steps">EN: The step traces. JA: ステップトレースです。</param>
    /// <param name="trajectoryInput">EN: The trajectory input carrier. JA: 軌道入力 carrier です。</param>
    /// <param name="decisionKind">EN: The trajectory decision kind. JA: 軌道決定種別です。</param>
    /// <param name="accepted">EN: Whether the trajectory was accepted. JA: 軌道が受理されたかどうかです。</param>
    /// <param name="rejectReasons">EN: Rejection reasons. JA: 拒否理由です。</param>
    /// <param name="canonReferences">EN: Canon references. JA: 正典参照です。</param>
    /// <param name="traceId">EN: The trace identifier. JA: トレース識別子です。</param>
    /// <param name="metadata">EN: Trace metadata. JA: トレースメタデータです。</param>
    /// <returns>EN: The governance trace. JA: 統治トレースを返します。</returns>
    public GovernanceTrace BuildGovernanceTrace(
        IReadOnlyList<StepGovernanceTrace> steps,
        TrajectoryGateInput trajectoryInput,
        TrajectoryGateDecisionKind decisionKind,
        bool accepted,
        IReadOnlyList<RejectReasonInfo>? rejectReasons = null,
        IReadOnlyList<CanonReference>? canonReferences = null,
        string? traceId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(trajectoryInput);

        var now = _clock.Now;

        return new GovernanceTrace
        {
            TraceId = traceId ?? string.Empty,
            Steps = steps,
            TrajectoryInput = trajectoryInput,
            DecisionKind = decisionKind,
            Accepted = accepted,
            RejectReasons = rejectReasons ?? [],
            CanonReferences = canonReferences ?? [],
            CreatedAt = now,
            CompletedAt = now,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private static string GetMetadataValue(
        IReadOnlyDictionary<string, string>? metadata,
        string key)
    {
        return metadata is not null && metadata.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    private static IReadOnlyList<CanonReference> MergeCanonReferences(
        params IReadOnlyList<CanonReference>?[] sources)
    {
        var merged = new List<CanonReference>();

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            merged.AddRange(source);
        }

        return merged;
    }

    private static IReadOnlyList<RejectReasonInfo> MergeRejectReasons(
        params IReadOnlyList<RejectReasonInfo>?[] sources)
    {
        var merged = new List<RejectReasonInfo>();

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            merged.AddRange(source);
        }

        return merged;
    }
}
