using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public void ApplicationExamplesNeverBecomeLiveObservationsOrCanonicalRules()
    {
        foreach(var pair in Rules())
        {
            var compact=BonsaiCtgPacketPrompt.Build(pair.Value,pair.Key,"{\"value\":\"live-fact-marker\"}","\"live-proposal-marker\"");
            var taught=BonsaiCtgDemonstrationPrompt.Build(pair.Value,pair.Key,"{\"value\":\"live-fact-marker\"}","\"live-proposal-marker\"");
            Assert.Equal(compact.User,taught.User);Assert.Equal(compact.Excerpt,taught.Excerpt);Assert.Equal(compact.Reference,taught.Reference);
            Assert.StartsWith(compact.System,taught.System);Assert.DoesNotContain("LQ7",taught.User);Assert.DoesNotContain("LQ9",taught.User);
            Assert.DoesNotContain("live-fact-marker",taught.System);Assert.DoesNotContain("live-proposal-marker",taught.System);
            Assert.Contains("\"vote\":\"Reject\"",taught.System);Assert.Contains("\"vote\":\"Approve\"",taught.System);
        }
    }
    [Fact]
    public async Task ExamplesCannotOverrideEthosVetoOrHostEvidenceRequirement()
    {
        int calls=0,executed=0;
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(Vote(++calls==2?"Reject":"Approve")),Rules(),responseMode:BonsaiCtgResponseMode.DemonstratedPacketVotes);
        var request=Request() with{InputFacts=CitationFacts};
        var missing=await evaluator.EvaluateProposalGateAsync(request,"Candidate",TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.evidence_contract_required",missing.Evaluation.ErrorCode);Assert.Equal(0,calls);
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(request,"Candidate",required,_=>Task.FromResult(true),
            _=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);Assert.False(result.Gate.Accepted);Assert.Equal(3,calls);Assert.Equal(0,executed);
    }
}
