namespace AIKernel.Core.Governance;

using AIKernel.Abstractions.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

/// <summary>
/// EN: Evaluates the CTG trajectory gate by aggregating ordered step gate results. JA: 順序付きステップゲート結果を集約して CTG 軌道ゲートを評価します。
/// </summary>
public sealed class CtgTrajectoryGateEvaluator : ITrajectoryGate
{
    private readonly CtgRejectReasonClassifier _reasonClassifier;
    private readonly CtgGovernanceTraceBuilder _traceBuilder;
    private readonly IKernelClock _clock;

    /// <summary>
    /// EN: Initializes an evaluator using the system kernel clock. JA: システムカーネルクロックを使用する評価器を初期化します。
    /// </summary>
    public CtgTrajectoryGateEvaluator()
        : this(KernelClock.System())
    {
    }

    /// <summary>
    /// EN: Initializes an evaluator using the supplied kernel clock. JA: 指定されたカーネルクロックを使用する評価器を初期化します。
    /// </summary>
    /// <param name="clock">EN: The clock used for timestamps. JA: タイムスタンプに使用するクロックです。</param>
    public CtgTrajectoryGateEvaluator(IKernelClock clock)
        : this(new CtgRejectReasonClassifier(clock), new CtgGovernanceTraceBuilder(clock), clock)
    {
    }

    /// <summary>
    /// EN: Initializes an evaluator using supplied collaborators. JA: 指定された協調オブジェクトを使用する評価器を初期化します。
    /// </summary>
    /// <param name="reasonClassifier">EN: The structural reason classifier. JA: 構造的理由分類器です。</param>
    /// <param name="traceBuilder">EN: The trace builder. JA: トレースビルダーです。</param>
    /// <param name="clock">EN: The clock used for timestamps. JA: タイムスタンプに使用するクロックです。</param>
    public CtgTrajectoryGateEvaluator(
        CtgRejectReasonClassifier reasonClassifier,
        CtgGovernanceTraceBuilder traceBuilder,
        IKernelClock clock)
    {
        _reasonClassifier = reasonClassifier ?? throw new ArgumentNullException(nameof(reasonClassifier));
        _traceBuilder = traceBuilder ?? throw new ArgumentNullException(nameof(traceBuilder));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// EN: Evaluates a trajectory gate request. JA: 軌道ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The trajectory gate request. JA: 軌道ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    public ValueTask<TrajectoryGateResult> EvaluateAsync(
        TrajectoryGateRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Evaluate(request));
    }

    /// <summary>
    /// EN: Evaluates a trajectory gate request synchronously. JA: 軌道ゲート要求を同期的に評価します。
    /// </summary>
    /// <param name="request">EN: The trajectory gate request. JA: 軌道ゲート要求です。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    public TrajectoryGateResult Evaluate(TrajectoryGateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var steps = request.Steps ?? [];
        var decisionKind = EvaluateDecision(steps);
        var accepted = decisionKind == TrajectoryGateDecisionKind.Continue;
        var metadata = CreateMetadata(request.Metadata);
        var rejectReasons = CreateRejectReasons(steps, request.CanonReferences, metadata);

        var trace = _traceBuilder.BuildGovernanceTrace(
            steps,
            request.Trajectory ?? new TrajectoryGateInput(),
            decisionKind,
            accepted,
            rejectReasons,
            request.CanonReferences ?? [],
            request.TraceId,
            metadata);

        return new TrajectoryGateResult
        {
            OperationId = request.OperationId,
            Succeeded = true,
            DecisionKind = decisionKind,
            Accepted = accepted,
            RejectReasons = rejectReasons,
            Trace = trace,
            ObservedAt = _clock.Now,
            CorrelationId = request.CorrelationId,
            TraceId = request.TraceId,
            Metadata = metadata
        };
    }

    /// <summary>
    /// EN: Evaluates ordered step traces as a trajectory decision. JA: 順序付きステップトレースを軌道決定として評価します。
    /// </summary>
    /// <param name="steps">EN: The ordered step traces. JA: 順序付きステップトレースです。</param>
    /// <returns>EN: The trajectory decision kind. JA: 軌道決定種別を返します。</returns>
    public static TrajectoryGateDecisionKind EvaluateDecision(
        IReadOnlyList<StepGovernanceTrace> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            return TrajectoryGateDecisionKind.Halt;
        }

        foreach (var step in steps)
        {
            if (step.DecisionGate.DecisionKind == GateDecisionKind.Deny)
            {
                return TrajectoryGateDecisionKind.Halt;
            }
        }

        return TrajectoryGateDecisionKind.Continue;
    }

    private IReadOnlyList<RejectReasonInfo> CreateRejectReasons(
        IReadOnlyList<StepGovernanceTrace> steps,
        IReadOnlyList<CanonReference>? canonReferences,
        Dictionary<string, string> metadata)
    {
        if (steps.Count == 0)
        {
            return [_reasonClassifier.ClassifyEmptyTrajectory(canonReferences)];
        }

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];

            if (step.DecisionGate.DecisionKind != GateDecisionKind.Deny)
            {
                continue;
            }

            metadata[CtgGovernanceMetadataKeys.FirstFailingStepIndex] =
                index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata[CtgGovernanceMetadataKeys.FirstFailingStepId] = step.StepId;

            return [_reasonClassifier.ClassifyDeniedStep(step, index, canonReferences)];
        }

        return [];
    }

    private static Dictionary<string, string> CreateMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        return new Dictionary<string, string>(
            metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
