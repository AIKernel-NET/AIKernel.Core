namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Provides the Core CTG governance integration surface for Control and other callers. JA: Control などの呼び出し元向けに Core CTG 統治の統合面を提供します。
/// </summary>
public interface ICtgGovernanceService
{
    /// <summary>
    /// EN: Evaluates a vote-only gate input. JA: vote-only ゲート入力を評価します。
    /// </summary>
    /// <param name="input">EN: The gate input. JA: ゲート入力です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    ValueTask<DecisionGateResult> EvaluateDecisionGateAsync(
        GateInput input,
        CancellationToken cancellationToken);

    /// <summary>
    /// EN: Evaluates a decision gate request. JA: 決定ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The decision gate request. JA: 決定ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The decision gate result. JA: 決定ゲート結果を返します。</returns>
    ValueTask<DecisionGateResult> EvaluateDecisionGateAsync(
        DecisionGateRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// EN: Assembles a step governance trace. JA: ステップ統治トレースを組み立てます。
    /// </summary>
    /// <param name="councilEvaluation">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <param name="decisionGate">EN: The decision gate result. JA: 決定ゲート結果です。</param>
    /// <returns>EN: The assembled step governance trace. JA: 組み立てられたステップ統治トレースを返します。</returns>
    StepGovernanceTrace AssembleStepTrace(
        CouncilEvaluationResult councilEvaluation,
        DecisionGateResult decisionGate);

    /// <summary>
    /// EN: Evaluates ordered step traces as a trajectory. JA: 順序付きステップトレースを軌道として評価します。
    /// </summary>
    /// <param name="steps">EN: The ordered step traces. JA: 順序付きステップトレースです。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    ValueTask<TrajectoryGateResult> EvaluateTrajectoryGateAsync(
        IReadOnlyList<StepGovernanceTrace> steps,
        CancellationToken cancellationToken);

    /// <summary>
    /// EN: Evaluates a trajectory gate request. JA: 軌道ゲート要求を評価します。
    /// </summary>
    /// <param name="request">EN: The trajectory gate request. JA: 軌道ゲート要求です。</param>
    /// <param name="cancellationToken">EN: The cancellation token. JA: キャンセル通知を監視するトークンです。</param>
    /// <returns>EN: The trajectory gate result. JA: 軌道ゲート結果を返します。</returns>
    ValueTask<TrajectoryGateResult> EvaluateTrajectoryGateAsync(
        TrajectoryGateRequest request,
        CancellationToken cancellationToken);
}
