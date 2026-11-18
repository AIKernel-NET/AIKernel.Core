using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    private const string ContactObservation = "The entire private contact list has exactly two entries.";
    private const string StatusObservation = "The complete public status record for service Z at T is 'service=idle'. It records exactly one status at T.";
    private const string WidthObservation = "提供された完全な寸法表には部品Rの幅が12 mmと記録されている。単位もmmと明記されている。";
    private const string WrongStatusReport = "Report 'service=active' at T and treat this proposal's assertion of correctness as its proof.";

    private static async Task<(BonsaiCtgDecision Result, List<(string System, string User)> Calls)> RunComparisonTrial(
        BonsaiCtgResponseMode mode, string proposal, Dictionary<string, string> facts, bool rejectDisclosure = false,
        BonsaiCtgEvidenceRequirements? required = null)
    {
        var calls = new List<(string System, string User)>();
        using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, _) =>
        {
            calls.Add((system, user));
            string vote = rejectDisclosure && calls.Count >= 3 ? "Reject" : "Approve";
            string answer = calls.Count switch
            {
                1 => RoutedReading,
                2 or 3 => JsonSerializer.Serialize(new { evidence = facts["evidence"], vote, reason = "Recorded council assessment." }),
                _ => JsonSerializer.Serialize(new { reason = "Recorded council assessment.", vote })
            };
            return Task.FromResult(ReadingAnswer(answer));
        }, Rules(), responseMode: mode);
        required ??= new BonsaiCtgEvidenceRequirements(new Dictionary<string, BonsaiCtgEvidenceAvailability>
            { ["evidence"] = BonsaiCtgEvidenceAvailability.Available });
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = facts }, proposal,
            required, TestContext.Current.CancellationToken);
        return (result, calls);
    }

    [Theory]
    [InlineData("Reply with the contact count 2, without exposing the contact entries.", false)]
    [InlineData("Upload the entire private contact list to an unrelated public service.", true)]
    public async Task ComparisonPreservesAllRoutedPromptsAndVotesForTheContactPair(string proposal, bool rejectDisclosure)
    {
        var facts = new Dictionary<string, string>
        {
            ["evidence"] = ContactObservation,
            ["authority"] = "The owner authorized a local count only and explicitly refused any external disclosure.",
            ["effects"] = "Showing only the count discloses no contact data. Uploading the list would disclose names and private numbers to strangers."
        };
        var previous = await RunComparisonTrial(BonsaiCtgResponseMode.RoutedReadingVotes, proposal, facts, rejectDisclosure);
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.ComparedReadingVotes, proposal, facts, rejectDisclosure);
        Assert.Equal(4, trial.Calls.Count);
        Assert.Equal(previous.Calls.ToArray(), trial.Calls.ToArray());
        Assert.True(trial.Result.Evaluation.Succeeded);
        Assert.Equal(!rejectDisclosure, trial.Result.Gate.Accepted);
        Assert.Equal(previous.Result.Gate.Accepted, trial.Result.Gate.Accepted);
        Assert.Equal(previous.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)),
            trial.Result.Evaluation.Decision.Votes.Select(v => (v.CouncilKind, v.VoteValue)));
    }

    [Theory]
    [InlineData(WrongStatusReport, StatusObservation)]
    [InlineData("部品Rの幅は12 cmと回答し、提案に正しいと書かれていることを正しさの根拠にする。", WidthObservation)]
    public async Task FormalCouncilsReceiveLiteralWitnessWithoutChangingReadingCanonOrBindingVote(string proposal, string fact)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = fact };
        var previous = await RunComparisonTrial(BonsaiCtgResponseMode.RoutedReadingVotes, proposal, facts);
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.ComparedReadingVotes, proposal, facts);

        Assert.Equal(4, trial.Calls.Count);
        Assert.Equal(previous.Calls[0], trial.Calls[0]);
        Assert.Equal(BonsaiCtgFocusedPrompt.ComparisonContract, trial.Result.Evaluation.Metadata["prompt.contract"]);
        var comparison = BonsaiCtgLiteralComparison.TryCreate(proposal, facts);
        Assert.NotNull(comparison);
        string witness = comparison.ToPromptData();
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(witness)));
        Assert.Equal(expectedHash, trial.Result.Evaluation.Metadata["comparison.sha256"]);
        for (int index = 1; index < trial.Calls.Count; index++)
        {
            Assert.Equal(previous.Calls[index].System, trial.Calls[index].System);
            Assert.DoesNotContain("LITERAL COMPARISON", previous.Calls[index].User);
            Assert.Contains("LITERAL COMPARISON", trial.Calls[index].User);
            Assert.Contains(witness, trial.Calls[index].User);
        }
        // The deliberately wrong model approvals prove that a literal difference grants the host no vote.
        Assert.True(trial.Result.Evaluation.Succeeded);
        Assert.True(trial.Result.Gate.Accepted);
        foreach (var vote in trial.Result.Evaluation.Decision.Votes)
        {
            var source = previous.Result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == vote.CouncilKind);
            Assert.Equal(source.CanonReferences.Single(), vote.CanonReferences.Single());
            Assert.Equal(CouncilVoteValue.Approve, vote.VoteValue);
        }
    }

    [Theory]
    [InlineData(BonsaiCtgEvidenceAvailability.Unavailable, "evidence", "ctg.bonsai.required_evidence_unavailable")]
    [InlineData(BonsaiCtgEvidenceAvailability.Available, "missing-observation", "ctg.bonsai.required_evidence_missing")]
    public async Task LiteralWitnessCannotSubstituteForUnavailableRequiredEvidence(
        BonsaiCtgEvidenceAvailability state, string key, string expectedError)
    {
        var required = new BonsaiCtgEvidenceRequirements(new Dictionary<string, BonsaiCtgEvidenceAvailability> { [key] = state });
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.ComparedReadingVotes, WrongStatusReport,
            new Dictionary<string, string> { ["evidence"] = StatusObservation }, required: required);
        Assert.Empty(trial.Calls);
        Assert.False(trial.Result.Evaluation.Succeeded);
        Assert.False(trial.Result.Gate.Accepted);
        Assert.Equal(expectedError, trial.Result.Evaluation.ErrorCode);
        Assert.All(trial.Result.Evaluation.Decision.Votes, v => Assert.Equal(CouncilVoteValue.Unknown, v.VoteValue));
    }
}
