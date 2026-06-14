namespace AIKernel.Core.Governance;

using AIKernel.Abstractions.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Diagnostics;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Diagnostics;
using AIKernel.Enums.Governance;

/// <summary>
/// EN: Evaluates the CTG decision gate as a pure discrete vote function. JA: CTG 決定ゲートを純粋な離散投票関数として評価します。
/// </summary>
public sealed class CtgDecisionGateEvaluator : IDecisionGate
{
    private readonly CtgRejectReasonClassifier _reasonClassifier;
    private readonly IKernelClock _clock;

    /// <summary>
    /// EN: Initializes an evaluator using the system kernel clock. JA: システムカーネルクロックを使用する評価器を初期化します。
    /// </summary>
    public CtgDecisionGateEvaluator()
        : this(KernelClock.System())
    {
    }

    /// <summary>
    /// EN: Initializes an evaluator using the supplied kernel clock. JA: 指定されたカーネルクロックを使用する評価器を初期化します。
    /// </summary>
    /// <param name="clock">EN: The clock used for timestamps. JA: タイムスタンプに使用するクロックです。</param>
    public CtgDecisionGateEvaluator(IKernelClock clock)
        : this(new CtgRejectReasonClassifier(clock), clock)
    {
    }

    /// <summary>
    /// EN: Initializes an evaluator using the supplied classifier and clock. JA: 指定された分類器とクロックを使用する評価器を初期化します。
    /// </summary>
    /// <param name="reasonClassifier">EN: The structural reason classifier. JA: 構造的理由分類器です。</param>
    /// <param name="clock">EN: The clock used for timestamps. JA: タイムスタンプに使用するクロックです。</param>
    public CtgDecisionGateEvaluator(
        CtgRejectReasonClassifier reasonClassifier,
        IKernelClock clock)
    {
        _reasonClassifier = reasonClassifier ?? throw new ArgumentNullException(nameof(reasonClassifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// EN: Evaluates a decision gate request. JA: 決定ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The decision gate request. JA: 決定ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    public ValueTask<DecisionGateResult> EvaluateAsync(
        DecisionGateRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Evaluate(request));
    }

    /// <summary>
    /// EN: Evaluates a decision gate request synchronously. JA: 決定ゲート要求を同期的に評価します。
    /// </summary>
    /// <param name="request">EN: The decision gate request. JA: 決定ゲート要求です。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    public DecisionGateResult Evaluate(DecisionGateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = request.GateInput ?? new GateInput();
        var decisionKind = EvaluateDecision(input);
        var accepted = decisionKind == GateDecisionKind.Allow;
        var rejectReasons = accepted
            ? Array.Empty<RejectReasonInfo>()
            : [_reasonClassifier.ClassifyDecisionGateDeny(input, request.CanonReferences)];

        return new DecisionGateResult
        {
            OperationId = request.OperationId,
            Succeeded = true,
            DecisionKind = decisionKind,
            Accepted = accepted,
            RejectReasons = rejectReasons,
            CanonReferences = request.CanonReferences ?? [],
            Diagnostics = CreateDiagnostics(input, request),
            ObservedAt = _clock.Now,
            CorrelationId = request.CorrelationId,
            TraceId = request.TraceId,
            Metadata = CreateMetadata(input, request, decisionKind)
        };
    }

    /// <summary>
    /// EN: Evaluates a vote-only gate input. JA: vote-only ゲート入力を評価します。
    /// </summary>
    /// <param name="input">EN: The gate input. JA: ゲート入力です。</param>
    /// <returns>EN: The gate decision kind. JA: ゲート決定種別を返します。</returns>
    public static GateDecisionKind EvaluateDecision(GateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Ethos == CouncilVoteValue.Reject)
        {
            return GateDecisionKind.Deny;
        }

        if (CtgRejectReasonClassifier.HasUnknownVote(input))
        {
            return GateDecisionKind.Deny;
        }

        return CtgRejectReasonClassifier.ApproveCount(input) >= 2
            ? GateDecisionKind.Allow
            : GateDecisionKind.Deny;
    }

    private IReadOnlyList<DiagnosticEntry> CreateDiagnostics(
        GateInput input,
        DecisionGateRequest request)
    {
        if (!CtgRejectReasonClassifier.HasUnknownVote(input))
        {
            return [];
        }

        return
        [
            new DiagnosticEntry
            {
                DiagnosticId = "ctg.decision_gate.unknown_vote",
                Code = "ctg.unknown_vote_value",
                Message = "Decision gate received at least one Unknown council vote.",
                Severity = DiagnosticSeverity.Warning,
                Scope = DiagnosticScope.Governance,
                ObservedAt = _clock.Now,
                CorrelationId = request.CorrelationId,
                TraceId = request.TraceId,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CtgGovernanceMetadataKeys.LogosVote] = input.Logos.ToString(),
                    [CtgGovernanceMetadataKeys.EthosVote] = input.Ethos.ToString(),
                    [CtgGovernanceMetadataKeys.PathosVote] = input.Pathos.ToString()
                }
            }
        ];
    }

    private static IReadOnlyDictionary<string, string> CreateMetadata(
        GateInput input,
        DecisionGateRequest request,
        GateDecisionKind decisionKind)
    {
        var metadata = new Dictionary<string, string>(
            request.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        metadata[CtgGovernanceMetadataKeys.OperationId] = request.OperationId;
        metadata[CtgGovernanceMetadataKeys.StepId] = request.StepId;
        metadata[CtgGovernanceMetadataKeys.LogosVote] = input.Logos.ToString();
        metadata[CtgGovernanceMetadataKeys.EthosVote] = input.Ethos.ToString();
        metadata[CtgGovernanceMetadataKeys.PathosVote] = input.Pathos.ToString();
        metadata[CtgGovernanceMetadataKeys.ApproveCount] =
            CtgRejectReasonClassifier.ApproveCount(input).ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata[CtgGovernanceMetadataKeys.DecisionKind] = decisionKind.ToString();

        return metadata;
    }
}
