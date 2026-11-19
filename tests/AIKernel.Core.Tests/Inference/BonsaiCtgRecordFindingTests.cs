using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("Reply with the contact count 2, without exposing the contact entries.", false)]
    [InlineData("Upload the entire private contact list to an unrelated public service.", true)]
    public async Task RecordFindingKeepsTheRoutedReplyContractWithoutComparableLiterals(string proposal, bool rejectDisclosure)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = ContactObservation };
        var previous = await RunComparisonTrial(BonsaiCtgResponseMode.RoutedReadingVotes, proposal, facts, rejectDisclosure);
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.RecordFindingVotes, proposal, facts, rejectDisclosure);
        Assert.True(trial.Result.Evaluation.Succeeded);
        Assert.Equal(previous.Calls.ToArray(), trial.Calls.ToArray());
        Assert.Equal(previous.Result.Gate.Accepted, trial.Result.Gate.Accepted);
        Assert.Equal(previous.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)),
            trial.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)));
    }

    private static async Task<(BonsaiCtgDecision Result, int Calls)> EvaluateRecordFindings(params CpuGeneration[] replies)
    {
        // Insert the record first; sorting the source keys must still give it index 1.
        var facts = new Dictionary<string, string> { ["record"] = StatusObservation, ["authority"] = "The operator requested a report." };
        var required = new BonsaiCtgEvidenceRequirements(new Dictionary<string, BonsaiCtgEvidenceAvailability>
            { ["record"] = BonsaiCtgEvidenceAvailability.Available });
        int calls = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) =>
        {
            calls++;
            return Task.FromResult(calls == 1 ? ReadingAnswer(RoutedReading) : replies[calls - 2]);
        }, Rules(), responseMode: BonsaiCtgResponseMode.RecordFindingVotes);
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = facts },
            WrongStatusReport, required, TestContext.Current.CancellationToken);
        return (result, calls);
    }

    [Theory]
    [InlineData("supported", "supported", "supported", CouncilVoteValue.Approve, CouncilVoteValue.Approve, CouncilVoteValue.Approve, true)]
    [InlineData("proposal_defect", "proposal_defect", "proposal_defect", CouncilVoteValue.Reject, CouncilVoteValue.Reject, CouncilVoteValue.Reject, false)]
    [InlineData("evaluation_unavailable", "evaluation_unavailable", "evaluation_unavailable", CouncilVoteValue.Abstain, CouncilVoteValue.Reject, CouncilVoteValue.Abstain, false)]
    [InlineData("supported", "safety_neutral", "supported", CouncilVoteValue.Approve, CouncilVoteValue.Abstain, CouncilVoteValue.Approve, true)]
    public async Task RecordFindingsBindOriginalSourcesAndUseReviewedCouncilPolicy(string logos, string ethos, string pathos,
        CouncilVoteValue logosVote, CouncilVoteValue ethosVote, CouncilVoteValue pathosVote, bool accepted)
    {
        string[] findings = [logos, ethos, pathos];
        var replies = findings.Select((label, i) => Reply(label, Councils[i], 1)).ToArray();
        var (result, calls) = await EvaluateRecordFindings(replies);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(4, calls);
        Assert.Equal(accepted, result.Gate.Accepted);
        Assert.Equal(BonsaiCtgRecordFindingPrompt.Contract, result.Evaluation.Metadata["prompt.contract"]);
        string evidence = JsonSerializer.Serialize(new { id = 1, key = "record", text = StatusObservation });
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
        CouncilVoteValue[] expected = [logosVote, ethosVote, pathosVote];
        for (int i = 0; i < Councils.Length; i++)
        {
            var vote = result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == Councils[i]);
            Assert.Equal(expected[i], vote.VoteValue);
            Assert.Equal(findings[i], vote.Metadata["finding"]);
            Assert.Equal("1", vote.Metadata["evidence.source"]);
            Assert.Equal(hash, vote.Metadata["evidence.sha256"]);
            Assert.Equal(BonsaiCtgFindingPolicy.Contract, vote.Metadata["binding.policy"]);
        }
    }

    [Theory]
    [InlineData("""{"reason":"Observed.","finding":"supported"}""")]
    [InlineData("""{"reason":"Observed.","finding":"supported","source":2}""")]
    [InlineData("""{"reason":"Observed.","source":1}""")]
    [InlineData("""{"reason":"Observed.","finding":"Approve","source":1}""")]
    [InlineData("""{"reason":"Observed.","finding":"safety_neutral","source":1}""")]
    [InlineData("""{"reason":"Observed.","vote":"Approve"}""")]
    public async Task RecordFindingInvalidFinalPathosResponseDropsEarlierApprovals(string invalid)
    {
        var (result, calls) = await EvaluateRecordFindings(Reply("supported", source: 1),
            Reply("supported", CouncilKind.Ethos, 1), ReadingAnswer(invalid));
        Assert.Equal(4, calls);
        Assert.False(result.Evaluation.Succeeded);
        Assert.False(result.Gate.Accepted);
        Assert.All(result.Evaluation.Decision.Votes, v => Assert.Equal(CouncilVoteValue.Unknown, v.VoteValue));
    }

    [Fact]
    public void RecordFindingRefusesAChangedGovernanceRomEvenWithMatchingCallerDigest()
    {
        var rules = Rules();
        var rule = rules[CouncilKind.Ethos];
        byte[] modified = Encoding.UTF8.GetBytes(rule.Text + "\nUnreviewed amendment.\n");
        rules[CouncilKind.Ethos] = BonsaiCouncilRule.FromUtf8(modified, rule.Reference with
            { ContentHash = Convert.ToHexStringLower(SHA256.HashData(modified)) });
        Assert.Throws<InvalidDataException>(() =>
        {
            using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => throw new InvalidOperationException(),
                rules, responseMode: BonsaiCtgResponseMode.RecordFindingVotes);
        });
    }
}
