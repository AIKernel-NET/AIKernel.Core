using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    private static CpuGeneration SelectedFinding(string text, string finish = "classified", int tokens = 1)
        => new(text, 42, tokens, finish, 1, "test");

    private static async Task<BonsaiCtgDecision> RunReasonedTrial(
        Func<string, string, CancellationToken, Task<CpuGeneration>> generate,
        Func<string, string, IReadOnlyList<string>, CancellationToken, Task<CpuGeneration>> select,
        BonsaiCtgEvidenceAvailability availability = BonsaiCtgEvidenceAvailability.Available)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = StatusObservation };
        var required = new BonsaiCtgEvidenceRequirements(new Dictionary<string, BonsaiCtgEvidenceAvailability>
            { ["evidence"] = availability });
        using var evaluator = new BonsaiCtgCouncilEvaluator(generate, Rules(),
            responseMode: BonsaiCtgResponseMode.ReasonedFindingVotes, select: select);
        return await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = facts },
            WrongStatusReport, required, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Yes", "Yes", "Yes", CouncilVoteValue.Approve, CouncilVoteValue.Approve, CouncilVoteValue.Approve, true)]
    [InlineData("No", "No", "No", CouncilVoteValue.Reject, CouncilVoteValue.Reject, CouncilVoteValue.Reject, false)]
    [InlineData("Unknown", "Unknown", "Unknown", CouncilVoteValue.Abstain, CouncilVoteValue.Reject, CouncilVoteValue.Abstain, false)]
    [InlineData("Yes", "Neutral", "Yes", CouncilVoteValue.Approve, CouncilVoteValue.Abstain, CouncilVoteValue.Approve, true)]
    public async Task ReasonedFindingsUseOnlyTheCurrentCouncilsReasonAndBindOnlyItsModelChoice(
        string logos, string ethos, string pathos, CouncilVoteValue l, CouncilVoteValue e, CouncilVoteValue p, bool accepted)
    {
        var labels = new[] { logos, ethos, pathos };
        var reasons = Councils.Select(role => $"{role} reports an unverified \"finding\" for this record.").ToArray();
        var stages = new List<string>();
        var generationPrompts = new List<(string System, string User)>();
        var selectionPrompts = new List<(string System, string User, string[] Choices)>();
        var result = await RunReasonedTrial((system, user, _) =>
        {
            int i = generationPrompts.Count;
            generationPrompts.Add((system, user));
            stages.Add(i == 0 ? "read" : "reason");
            return Task.FromResult(ReadingAnswer(i == 0 ? RoutedReading : reasons[i - 1]));
        }, (system, user, choices, _) =>
        {
            int i = selectionPrompts.Count;
            selectionPrompts.Add((system, user, choices.ToArray()));
            stages.Add("select");
            return Task.FromResult(SelectedFinding(labels[i]));
        });

        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(accepted, result.Gate.Accepted);
        Assert.Equal(new[] { "read", "reason", "select", "reason", "select", "reason", "select" }, stages);
        CouncilVoteValue[] expected = [l, e, p];
        for (int i = 0; i < Councils.Length; i++)
        {
            var role = Councils[i];
            var final = selectionPrompts[i];
            string[] choices = role == CouncilKind.Ethos ? ["Neutral", "No", "Unknown", "Yes"] : ["No", "Unknown", "Yes"];
            Assert.Equal(choices, final.Choices.Order(StringComparer.Ordinal));
            Assert.Equal(expected[i], result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == role).VoteValue);
            Assert.Contains(JsonSerializer.Serialize(reasons[i]), final.User);
            Assert.DoesNotContain(reasons[i], final.User);
            for (int j = 0; j < reasons.Length; j++)
            {
                string marker = $"{Councils[j]} reports an unverified";
                Assert.DoesNotContain(marker, generationPrompts[i + 1].User);
                Assert.DoesNotContain(marker, generationPrompts[i + 1].System);
                Assert.DoesNotContain(marker, final.System);
                if (i != j) Assert.DoesNotContain(marker, final.User);
            }
        }
        // No writes to observations, including generated rationale and forced-choice data.
        var legacy = await RunComparisonTrial(BonsaiCtgResponseMode.RoutedReadingVotes, WrongStatusReport,
            new Dictionary<string, string> { ["evidence"] = StatusObservation });
        Assert.Equal(legacy.Calls[0], generationPrompts[0]);
        Assert.Equal(legacy.Result.Evaluation.Metadata["facts.sha256"], result.Evaluation.Metadata["facts.sha256"]);
    }

    [Theory]
    [InlineData("Yes ", "classified", 1)]
    [InlineData("yes", "classified", 1)]
    [InlineData("supported", "classified", 1)]
    [InlineData("Yes", "stop", 1)]
    [InlineData("Yes", "classified", 2)]
    [InlineData("Neutral", "classified", 1)]
    public async Task ReasonedFindingInvalidFinalChoiceDropsEarlierApprovals(string token, string finish, int count)
    {
        int choices = 0;
        var result = await RunReasonedTrial((_, _, _) => Task.FromResult(ReadingAnswer("The source contains the state.")),
            (_, _, _, _) => Task.FromResult(++choices < 3 ? SelectedFinding("Yes") : SelectedFinding(token, finish, count)));
        Assert.Equal(3, choices);
        AssertUnknownReasonedDecision(result);
    }

    [Theory]
    [InlineData("", "stop")]
    [InlineData("Incomplete reason", "length")]
    public async Task ReasonedFindingInvalidFinalReasonStopsBeforeSelection(string text, string finish)
    {
        int generated = 0, choices = 0;
        var result = await RunReasonedTrial((_, _, _) => Task.FromResult(++generated == 4
            ? ReadingAnswer(text, finish) : ReadingAnswer("The recorded value differs.")), (_, _, _, _) =>
        {
            choices++;
            return Task.FromResult(SelectedFinding("Yes"));
        });
        Assert.Equal(4, generated);
        Assert.Equal(2, choices);
        AssertUnknownReasonedDecision(result);
    }

    private static void AssertUnknownReasonedDecision(BonsaiCtgDecision result)
    {
        Assert.False(result.Evaluation.Succeeded);
        Assert.False(result.Gate.Accepted);
        Assert.Equal(3, result.Evaluation.Decision.Votes.Count);
        Assert.All(result.Evaluation.Decision.Votes, vote => Assert.Equal(CouncilVoteValue.Unknown, vote.VoteValue));
    }

    [Fact]
    public async Task ReasonedFindingRequiresHostEvidenceBeforeEitherModelStage()
    {
        var result = await RunReasonedTrial((_, _, _) => throw new InvalidOperationException("Unexpected generation"),
            (_, _, _, _) => throw new InvalidOperationException("Unexpected classification"),
            BonsaiCtgEvidenceAvailability.Unavailable);
        AssertUnknownReasonedDecision(result);
        Assert.Equal("ctg.bonsai.required_evidence_unavailable", result.Evaluation.ErrorCode);
    }

    [Theory]
    [InlineData("Reply with the contact count 2, without exposing the contact entries.", false)]
    [InlineData("Upload the entire private contact list to an unrelated public service.", true)]
    public async Task ReasonedFindingPreservesLegacyContactPromptsAndVotes(string proposal, bool reject)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = ContactObservation };
        var baseline = await RunComparisonTrial(BonsaiCtgResponseMode.RoutedReadingVotes, proposal, facts, reject);
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.ReasonedFindingVotes, proposal, facts, reject);
        Assert.True(trial.Result.Evaluation.Succeeded);
        Assert.Equal(baseline.Calls.ToArray(), trial.Calls.ToArray());
        Assert.Equal(!reject, trial.Result.Gate.Accepted);
        Assert.Equal(baseline.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)),
            trial.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)));
    }

    [Fact]
    public void ReasonedFindingRejectsUnreviewedRomEvenWithValidDigest()
    {
        var rules = Rules();
        var ethos = rules[CouncilKind.Ethos];
        byte[] amended = Encoding.UTF8.GetBytes(ethos.Text + "\nUnreviewed change.\n");
        rules[CouncilKind.Ethos] = BonsaiCouncilRule.FromUtf8(amended,
            ethos.Reference with { ContentHash = Convert.ToHexStringLower(SHA256.HashData(amended)) });
        Assert.Throws<InvalidDataException>(() =>
        {
            using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => throw new InvalidOperationException(),
                rules, responseMode: BonsaiCtgResponseMode.ReasonedFindingVotes,
                select: (_, _, _, _) => throw new InvalidOperationException());
        });
    }
}
