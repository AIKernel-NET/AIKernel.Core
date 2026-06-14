namespace AIKernel.Core.Governance;

using AIKernel.Abstractions.Governance;
using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Coordinates Core CTG evaluators without adding governance rules. JA: 統治ルールを追加せず Core CTG 評価器を連携します。
/// </summary>
public sealed class CtgGovernanceService : ICtgGovernanceService
{
    private readonly IDecisionGate _decisionGate;
    private readonly ITrajectoryGate _trajectoryGate;
    private readonly CtgStepTraceAssembler _stepTraceAssembler;
    private readonly ICtgCanonReferenceSource _canonReferenceSource;

    /// <summary>
    /// EN: Initializes the service. JA: サービスを初期化します。
    /// </summary>
    /// <param name="decisionGate">EN: The decision gate evaluator. JA: 決定ゲート評価器です。</param>
    /// <param name="trajectoryGate">EN: The trajectory gate evaluator. JA: 軌道ゲート評価器です。</param>
    /// <param name="stepTraceAssembler">EN: The step trace assembler. JA: ステップトレースアセンブラーです。</param>
    public CtgGovernanceService(
        IDecisionGate decisionGate,
        ITrajectoryGate trajectoryGate,
        CtgStepTraceAssembler stepTraceAssembler)
        : this(
            decisionGate,
            trajectoryGate,
            stepTraceAssembler,
            new CtgStaticCanonReferenceSource())
    {
    }

    /// <summary>
    /// EN: Initializes the service. JA: サービスを初期化します。
    /// </summary>
    /// <param name="decisionGate">EN: The decision gate evaluator. JA: 決定ゲート評価器です。</param>
    /// <param name="trajectoryGate">EN: The trajectory gate evaluator. JA: 軌道ゲート評価器です。</param>
    /// <param name="stepTraceAssembler">EN: The step trace assembler. JA: ステップトレースアセンブラーです。</param>
    /// <param name="canonReferenceSource">EN: The canon reference source. JA: 正典参照 source です。</param>
    public CtgGovernanceService(
        IDecisionGate decisionGate,
        ITrajectoryGate trajectoryGate,
        CtgStepTraceAssembler stepTraceAssembler,
        ICtgCanonReferenceSource canonReferenceSource)
    {
        _decisionGate = decisionGate ?? throw new ArgumentNullException(nameof(decisionGate));
        _trajectoryGate = trajectoryGate ?? throw new ArgumentNullException(nameof(trajectoryGate));
        _stepTraceAssembler = stepTraceAssembler ?? throw new ArgumentNullException(nameof(stepTraceAssembler));
        _canonReferenceSource = canonReferenceSource ?? throw new ArgumentNullException(nameof(canonReferenceSource));
    }

    /// <summary>
    /// EN: Evaluates a vote-only gate input. JA: vote-only ゲート入力を評価します。
    /// </summary>
    /// <param name="input">EN: The gate input. JA: ゲート入力です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    public ValueTask<DecisionGateResult> EvaluateDecisionGateAsync(
        GateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return EvaluateDecisionGateAsync(
            new DecisionGateRequest
            {
                GateInput = input,
                CanonReferences = _canonReferenceSource.GetDecisionGateReferences()
            },
            cancellationToken);
    }

    /// <summary>
    /// EN: Evaluates a decision gate request. JA: 決定ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The decision gate request. JA: 決定ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    public ValueTask<DecisionGateResult> EvaluateDecisionGateAsync(
        DecisionGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _decisionGate.EvaluateAsync(request, cancellationToken);
    }

    /// <summary>
    /// EN: Assembles a step governance trace. JA: ステップ統治トレースを組み立てます。
    /// </summary>
    /// <param name="councilEvaluation">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <returns>EN: The assembled step governance trace. JA: 組み立てられたステップ統治トレースを返します。</returns>
    public StepGovernanceTrace AssembleStepTrace(
        CouncilEvaluationResult councilEvaluation,
        DecisionGateResult decisionGate)
    {
        ArgumentNullException.ThrowIfNull(councilEvaluation);
        ArgumentNullException.ThrowIfNull(decisionGate);

        return _stepTraceAssembler.Assemble(councilEvaluation, decisionGate);
    }

    /// <summary>
    /// EN: Evaluates ordered step traces as a trajectory. JA: 順序付きステップトレースを軌道として評価します。
    /// </summary>
    /// <param name="steps">EN: The ordered step traces. JA: 順序付きステップトレースです。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    public ValueTask<TrajectoryGateResult> EvaluateTrajectoryGateAsync(
        IReadOnlyList<StepGovernanceTrace> steps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return EvaluateTrajectoryGateAsync(
            new TrajectoryGateRequest
            {
                Steps = steps,
                CanonReferences = _canonReferenceSource.GetTrajectoryGateReferences()
            },
            cancellationToken);
    }

    /// <summary>
    /// EN: Evaluates a trajectory gate request. JA: 軌道ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The trajectory gate request. JA: 軌道ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    public ValueTask<TrajectoryGateResult> EvaluateTrajectoryGateAsync(
        TrajectoryGateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _trajectoryGate.EvaluateAsync(request, cancellationToken);
    }
}
