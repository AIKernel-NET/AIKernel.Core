namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Adapts council decision carriers into CTG vote-only gate input carriers. JA: 評議会決定 carrier を CTG の vote-only gate input carrier に変換します。
/// </summary>
public sealed class CtgCouncilDecisionToGateInputAdapter
{
    private readonly CtgCouncilVoteExtractor _extractor;

    /// <summary>
    /// EN: Initializes the adapter. JA: アダプターを初期化します。
    /// </summary>
    /// <param name="extractor">EN: The council vote extractor. JA: 評議会投票抽出器です。</param>
    public CtgCouncilDecisionToGateInputAdapter(CtgCouncilVoteExtractor extractor)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
    }

    /// <summary>
    /// EN: Converts a council evaluation result into gate input. JA: 評議会評価結果をゲート入力へ変換します。
    /// </summary>
    /// <param name="result">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <returns>EN: The normalized gate input. JA: 正規化されたゲート入力を返します。</returns>
    public GateInput Convert(CouncilEvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return _extractor.Extract(result);
    }

    /// <summary>
    /// EN: Converts a council decision into gate input. JA: 評議会決定をゲート入力へ変換します。
    /// </summary>
    /// <param name="decision">EN: The council decision. JA: 評議会決定です。</param>
    /// <returns>EN: The normalized gate input. JA: 正規化されたゲート入力を返します。</returns>
    public GateInput Convert(CouncilDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return _extractor.Extract(decision);
    }
}
