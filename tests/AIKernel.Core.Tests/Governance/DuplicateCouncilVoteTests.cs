using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Governance;

public sealed class DuplicateCouncilVoteTests
{
    [Theory]
    [InlineData(CouncilVoteValue.Reject, CouncilVoteValue.Unknown, CouncilVoteValue.Reject)]
    [InlineData(CouncilVoteValue.Abstain, CouncilVoteValue.Reject, CouncilVoteValue.Reject)]
    [InlineData(CouncilVoteValue.Unknown, CouncilVoteValue.Approve, CouncilVoteValue.Unknown)]
    [InlineData(CouncilVoteValue.Approve, CouncilVoteValue.Abstain, CouncilVoteValue.Abstain)]
    public void SafetyPrecedenceIsIndependentOfProviderAndArrivalOrder(CouncilVoteValue first, CouncilVoteValue second, CouncilVoteValue expected)
    {
        var a = new CouncilVote { VoteId = "a", CouncilKind = CouncilKind.Ethos, VoteValue = first };
        var z = new CouncilVote { VoteId = "z", CouncilKind = CouncilKind.Ethos, VoteValue = second };
        foreach (var votes in new CouncilVote[][] { [a, z], [z, a], [a with { VoteValue = second }, z with { VoteValue = first }] })
        {
            var decision = new CouncilDecision { Votes = votes };
            var gate = new AIKernel.Core.Governance.CtgCouncilVoteExtractor().Extract(decision);
            Assert.Equal(expected, gate.Ethos);
        }
    }
}
