namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

/// <summary>
/// EN: Extracts the triadic vote-only gate input from council decisions. JA: 評議会決定から三項 vote-only gate input を抽出します。
/// </summary>
public sealed class CtgCouncilVoteExtractor
{
    /// <summary>
    /// EN: Extracts a gate input from a council evaluation result. JA: 評議会評価結果からゲート入力を抽出します。
    /// </summary>
    /// <param name="result">EN: The council evaluation result. JA: 評議会評価結果です。</param>
    /// <returns>EN: The extracted gate input. JA: 抽出されたゲート入力を返します。</returns>
    public GateInput Extract(CouncilEvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Extract(result.Decision);
    }

    /// <summary>
    /// EN: Extracts a gate input from an aggregate council decision. JA: 集約評議会決定からゲート入力を抽出します。
    /// </summary>
    /// <param name="decision">EN: The council decision. JA: 評議会決定です。</param>
    /// <returns>EN: The extracted gate input. JA: 抽出されたゲート入力を返します。</returns>
    public GateInput Extract(CouncilDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var votes = decision.Votes ?? [];

        return new GateInput
        {
            Logos = ExtractVote(votes, CouncilKind.Logos),
            Ethos = ExtractVote(votes, CouncilKind.Ethos),
            Pathos = ExtractVote(votes, CouncilKind.Pathos)
        };
    }

    private static CouncilVoteValue ExtractVote(
        IReadOnlyList<CouncilVote> votes,
        CouncilKind councilKind)
    {
        foreach (var vote in votes)
        {
            if (vote.CouncilKind == councilKind)
            {
                return vote.VoteValue;
            }
        }

        return CouncilVoteValue.Unknown;
    }
}
