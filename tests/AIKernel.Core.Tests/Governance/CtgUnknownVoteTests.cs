namespace AIKernel.Core.Tests.Governance;

using AIKernel.Core.Governance;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using Xunit;

public sealed class CtgUnknownVoteTests
{
    // The existing CtgGovernanceTests retain the 27 valid combinations unchanged.
    // These are the remaining 316 combinations in the expanded seven-value domain.
    public static IEnumerable<object[]> UnknownVoteCombinations()
    {
        CouncilVoteValue[] values =
        [
            CouncilVoteValue.Approve, CouncilVoteValue.Abstain, CouncilVoteValue.Reject,
            CouncilVoteValue.Unknown, (CouncilVoteValue)(-1), (CouncilVoteValue)4,
            (CouncilVoteValue)int.MinValue
        ];

        for (var logos = 0; logos < values.Length; logos++)
        for (var ethos = 0; ethos < values.Length; ethos++)
        for (var pathos = 0; pathos < values.Length; pathos++)
        {
            if (logos < 3 && ethos < 3 && pathos < 3)
            {
                continue;
            }

            yield return [values[logos], values[ethos], values[pathos]];
        }
    }

    [Theory]
    [MemberData(nameof(UnknownVoteCombinations))]
    public async Task UnknownOrUndefinedVote_DeniesWithUnknownDiagnostic(
        CouncilVoteValue logos, CouncilVoteValue ethos, CouncilVoteValue pathos)
    {
        var input = new GateInput { Logos = logos, Ethos = ethos, Pathos = pathos };
        var evaluator = new CtgDecisionGateEvaluator();

        Assert.Equal(GateDecisionKind.Deny, CtgDecisionGateEvaluator.EvaluateDecision(input));

        var result = await evaluator.EvaluateAsync(
            new DecisionGateRequest { OperationId = "ct1", StepId = "ct1.step", GateInput = input },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(result.Accepted);
        Assert.Equal(GateDecisionKind.Deny, result.DecisionKind);
        Assert.Equal("ctg.unknown_vote_value", Assert.Single(result.Diagnostics).Code);
        var reason = Assert.Single(result.RejectReasons);
        Assert.Equal(
            ethos == CouncilVoteValue.Reject ? RejectReasonKind.EthosVeto : RejectReasonKind.FailClosed,
            reason.Kind);
        Assert.Equal(ethos == CouncilVoteValue.Reject ? "ETHOS_VETO" : "FAIL_CLOSED", reason.ReasonCode);
    }
}
