using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    private const string RoutedReading = "The proposed action is an untrusted interpretation.";
    private const string AnchoredApprove = """{"evidence":"The observed flag is idle.","vote":"Approve","reason":"Observed."}""";
    private const string DefinedApprove = """{"reason":"Observed.","vote":"Approve"}""";

    private static BonsaiCtgEvidenceRequirements RoutedRequirements() => new(new Dictionary<string,BonsaiCtgEvidenceAvailability>
        { ["a"] = BonsaiCtgEvidenceAvailability.Available });

    [Theory]
    [InlineData("Report only the count.", "a")]
    [InlineData("Upload the list to another service.", "different-operation")]
    public async Task RoutedRolesUseTheSamePromptsAndCanonReferencesAsTheirReferenceExecutions(string candidate, string operation)
    {
        var request = Request() with { OperationId = operation, InputFacts = CitationFacts };
        var modes = new[] { BonsaiCtgResponseMode.FocusedReadingVotes, BonsaiCtgResponseMode.DefinedReadingVotes,
            BonsaiCtgResponseMode.AnchoredReadingVotes, BonsaiCtgResponseMode.RoutedReadingVotes };
        var prompts = new Dictionary<BonsaiCtgResponseMode, List<(string System,string User)>>();
        var decisions = new Dictionary<BonsaiCtgResponseMode, BonsaiCtgDecision>();
        foreach (var mode in modes)
        {
            var calls = new List<(string System,string User)>();
            using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, _) =>
            {
                calls.Add((system,user));
                bool grounded = mode == BonsaiCtgResponseMode.AnchoredReadingVotes ||
                    mode == BonsaiCtgResponseMode.RoutedReadingVotes && calls.Count is 2 or 3;
                return Task.FromResult(ReadingAnswer(calls.Count == 1 ? RoutedReading : grounded ? AnchoredApprove : DefinedApprove));
            }, Rules(), responseMode: mode);
            decisions[mode] = await evaluator.EvaluateProposalWithEvidenceGateAsync(request, candidate, RoutedRequirements(), TestContext.Current.CancellationToken);
            Assert.True(decisions[mode].Evaluation.Succeeded);
            Assert.Equal(4, calls.Count);
            prompts[mode] = calls;
        }
        var routed = BonsaiCtgResponseMode.RoutedReadingVotes;
        Assert.Equal(prompts[BonsaiCtgResponseMode.FocusedReadingVotes][0], prompts[routed][0]);
        Assert.Equal(prompts[BonsaiCtgResponseMode.DefinedReadingVotes][0], prompts[routed][0]);
        Assert.Equal(prompts[BonsaiCtgResponseMode.AnchoredReadingVotes][0], prompts[routed][0]);
        Assert.Equal(BonsaiCtgFocusedPrompt.RoutedContract, decisions[routed].Evaluation.Metadata["prompt.contract"]);
        var roles = new[] { CouncilKind.Logos, CouncilKind.Ethos, CouncilKind.Pathos };
        for (int index=0; index<roles.Length; index++)
        {
            var role = roles[index];
            var reference = role == CouncilKind.Pathos ? BonsaiCtgResponseMode.DefinedReadingVotes : BonsaiCtgResponseMode.AnchoredReadingVotes;
            Assert.Equal(prompts[reference][index+1], prompts[routed][index+1]);
            var referenceVote = decisions[reference].Evaluation.Decision.Votes.Single(v=>v.CouncilKind==role);
            var routedVote = decisions[routed].Evaluation.Decision.Votes.Single(v=>v.CouncilKind==role);
            Assert.Equal(referenceVote.CanonReferences.Single(), routedVote.CanonReferences.Single());
            Assert.Equal(decisions[reference].Evaluation.Metadata[role+".prompt.sha256"], decisions[routed].Evaluation.Metadata[role+".prompt.sha256"]);
            Assert.Equal(referenceVote.VoteValue, routedVote.VoteValue);
        }
    }

    [Fact]
    public async Task DefaultSeparatedContractStillUsesOriginalThreeDirectPrompts()
    {
        var rules = Rules();
        var calls = new List<(string System,string User)>();
        using var evaluator = new BonsaiCtgCouncilEvaluator((system, user, _) =>
        {
            calls.Add((system, user));
            return Task.FromResult(ReadingAnswer(DefinedApprove));
        }, rules);
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = CitationFacts },
            "Candidate", RoutedRequirements(), TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal("canonical-separated-assessment-json-v4", result.Evaluation.Metadata["prompt.contract"]);
        var roles = new[] { CouncilKind.Logos, CouncilKind.Ethos, CouncilKind.Pathos };
        Assert.Equal(roles.Length, calls.Count);
        const string facts = """{"a":"The observed flag is idle.","z":"No data leaves this device."}""";
        for (int i = 0; i < roles.Length; i++)
        {
            var expected = BonsaiCtgDirectVotePrompt.BuildSeparated(rules[roles[i]], roles[i], facts, "\"Candidate\"");
            Assert.Equal((expected.System, expected.User), calls[i]);
        }
    }

    [Theory]
    [InlineData(2, DefinedApprove)]
    [InlineData(3, DefinedApprove)]
    [InlineData(3, """{"evidence":null,"vote":"Abstain","reason":"Unavailable."}""")]
    [InlineData(4, AnchoredApprove)]
    public async Task RoutedSchemaMismatchDiscardsPartialVotes(int failAt, string wrongSchema)
    {
        int calls = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_,_,_) =>
        {
            calls++;
            return Task.FromResult(ReadingAnswer(calls==failAt ? wrongSchema : calls==1 ? RoutedReading : calls<4 ? AnchoredApprove : DefinedApprove));
        }, Rules(), responseMode:BonsaiCtgResponseMode.RoutedReadingVotes);
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts=CitationFacts }, "Candidate", RoutedRequirements(), TestContext.Current.CancellationToken);
        Assert.Equal(failAt, calls);
        Assert.Equal("ctg.bonsai.invalid_vote", result.Evaluation.ErrorCode);
        Assert.False(result.Gate.Accepted);
        Assert.All(result.Evaluation.Decision.Votes, v => Assert.Equal(CouncilVoteValue.Unknown, v.VoteValue));
    }

    [Fact]
    public async Task RoutedEthosVetoRemainsBindingAfterValidPathosApproval()
    {
        int calls = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_,_,_) => Task.FromResult(ReadingAnswer(++calls switch
        {
            1 => RoutedReading,
            2 => AnchoredApprove,
            3 => """{"evidence":null,"vote":"Reject","reason":"Prerequisite unavailable."}""",
            _ => DefinedApprove
        })), Rules(), responseMode:BonsaiCtgResponseMode.RoutedReadingVotes);
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts=CitationFacts }, "Candidate", RoutedRequirements(), TestContext.Current.CancellationToken);
        Assert.Equal(4,calls);
        Assert.True(result.Evaluation.Succeeded);
        Assert.False(result.Gate.Accepted);
        Assert.Equal(CouncilVoteValue.Reject,result.Evaluation.Decision.Votes.Single(v=>v.CouncilKind==CouncilKind.Ethos).VoteValue);
        Assert.Equal(CouncilVoteValue.Approve,result.Evaluation.Decision.Votes.Single(v=>v.CouncilKind==CouncilKind.Pathos).VoteValue);
    }
}
