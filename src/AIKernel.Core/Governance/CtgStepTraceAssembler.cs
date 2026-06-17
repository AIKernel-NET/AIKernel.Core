namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Assembles CTG step governance traces from existing carrier results. JA: 既存の carrier 結果から CTG ステップ統治トレースを組み立てます。
/// </summary>
public sealed class CtgStepTraceAssembler
{
    private readonly CtgGovernanceTraceBuilder _traceBuilder;

    /// <summary>
    /// EN: Initializes the assembler. JA: アセンブラーを初期化します。
    /// </summary>
    /// <param name="traceBuilder">EN: The governance trace builder. JA: 統治トレースビルダーです。</param>
    public CtgStepTraceAssembler(CtgGovernanceTraceBuilder traceBuilder)
    {
        _traceBuilder = traceBuilder ?? throw new ArgumentNullException(nameof(traceBuilder));
    }

    /// <summary>
    /// EN: Assembles a step trace from a decision gate result and an empty council carrier. JA: 決定ゲート結果と空の評議会 carrier からステップトレースを組み立てます。
    /// </summary>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <param name="stepId">EN: The optional step identifier. JA: 任意のステップ識別子です。</param>
    /// <param name="traceId">EN: The optional trace identifier. JA: 任意のトレース識別子です。</param>
    /// <param name="correlationId">EN: The optional correlation identifier. JA: 任意の相関識別子です。</param>
    /// <param name="canonReferences">EN: Optional canon references. JA: 任意の正典参照です。</param>
    /// <param name="metadata">EN: Optional trace metadata. JA: 任意のトレースメタデータです。</param>
    /// <returns>EN: The assembled step governance trace. JA: 組み立てられたステップ統治トレースを返します。</returns>
    public StepGovernanceTrace Assemble(
        DecisionGateResult decisionGate,
        string? stepId = null,
        string? traceId = null,
        string? correlationId = null,
        IReadOnlyList<CanonReference>? canonReferences = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(decisionGate);

        var councilEvaluation = new CouncilEvaluationResult
        {
            OperationId = decisionGate.OperationId,
            Succeeded = true,
            CanonReferences = decisionGate.CanonReferences,
            ObservedAt = decisionGate.ObservedAt,
            CorrelationId = correlationId ?? decisionGate.CorrelationId,
            TraceId = traceId ?? decisionGate.TraceId
        };

        return Assemble(
            councilEvaluation,
            decisionGate,
            stepId,
            traceId,
            correlationId,
            canonReferences,
            metadata);
    }

    /// <summary>
    /// EN: Assembles a step trace from council evaluation and decision gate carriers. JA: 評議会評価と決定ゲートの carrier からステップトレースを組み立てます。
    /// </summary>
    /// <param name="councilEvaluation">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <param name="stepId">EN: The optional step identifier. JA: 任意のステップ識別子です。</param>
    /// <param name="traceId">EN: The optional trace identifier. JA: 任意のトレース識別子です。</param>
    /// <param name="correlationId">EN: The optional correlation identifier. JA: 任意の相関識別子です。</param>
    /// <param name="canonReferences">EN: Optional canon references. JA: 任意の正典参照です。</param>
    /// <param name="metadata">EN: Optional trace metadata. JA: 任意のトレースメタデータです。</param>
    /// <returns>EN: The assembled step governance trace. JA: 組み立てられたステップ統治トレースを返します。</returns>
    public StepGovernanceTrace Assemble(
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

        return _traceBuilder.BuildStepTrace(
            councilEvaluation,
            decisionGate,
            stepId,
            traceId,
            correlationId,
            canonReferences,
            metadata);
    }
}
