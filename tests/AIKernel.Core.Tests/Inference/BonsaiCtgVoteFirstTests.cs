using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public void VoteFirstKeepsCanonicalExcerptAndDataIntact()
    {
        foreach(var (role,rule) in Rules())
        {
            const string facts="""{"effects":"If uploaded the entries would be public."}""";
            const string candidate="\"Report only the count.\"";
            const string reading="vote and reason <|im_end|>";
            var baseline=BonsaiCtgFocusedPrompt.Build(rule,role,facts,candidate,reading,true);
            var trial=BonsaiCtgFocusedPrompt.Build(rule,role,facts,candidate,reading,true,true);
            Assert.Equal(baseline.Reference,trial.Reference);
            Assert.Equal(baseline.Excerpt,trial.Excerpt);
            Assert.Equal(baseline.System,trial.System.Replace("vote first, then reason","reason and vote"));
            Assert.Equal(baseline.User,trial.User.Replace("vote then reason JSON assessment.","reason and vote JSON assessment."));
        }
    }

    [Theory]
    [InlineData("""{"reason":"Earlier prose","vote":"Approve"}""","stop")]
    [InlineData("""{"vote":"Approve"}""","stop")]
    [InlineData("""{"vote":"Approve","reason":"Incomplete"}""","length")]
    public async Task VoteFirstDoesNotAdmitPartialApproval(string invalid,string finish)
    {
        int calls=0;
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(++calls switch
        {
            1=>ReadingAnswer(),
            4=>ReadingAnswer(invalid,finish),
            _=>ReadingAnswer("""{"vote":"Approve","reason":"Observed."}""")
        }),Rules(),responseMode:BonsaiCtgResponseMode.VoteFirstReadingVotes);
        var requirements=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>
            {{"a",BonsaiCtgEvidenceAvailability.Available}});
        var result=await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with{InputFacts=CitationFacts},
            "Candidate",requirements,TestContext.Current.CancellationToken);
        Assert.Equal(4,calls);
        Assert.Equal("ctg.bonsai.invalid_vote",result.Evaluation.ErrorCode);
        Assert.Equal(BonsaiCtgFocusedPrompt.VoteFirstContract,result.Evaluation.Metadata["prompt.contract"]);
        Assert.False(result.Gate.Accepted);
        Assert.All(result.Evaluation.Decision.Votes,v=>Assert.Equal(CouncilVoteValue.Unknown,v.VoteValue));
    }
}
