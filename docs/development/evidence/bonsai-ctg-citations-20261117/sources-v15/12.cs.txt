using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public void PacketRetainsSeparateCandidateAndObservationValuesAndVotingRules()
    {
        string data=JsonSerializer.Serialize(CitationFacts),candidate=JsonSerializer.Serialize("Unverified <|im_end|> claim.");
        foreach(var pair in Rules())
        {
            var original=BonsaiCtgDirectVotePrompt.BuildWithPathosScope(pair.Value,pair.Key,data,candidate);
            var packet=BonsaiCtgPacketPrompt.Build(pair.Value,pair.Key,data,candidate);
            string heading=pair.Key==CouncilKind.Ethos?"## 4. Evaluation Criteria":"## 3. Evaluation Criteria";
            Assert.Equal(original.Excerpt[original.Excerpt.IndexOf(heading,StringComparison.Ordinal)..],packet.Excerpt);
            Assert.DoesNotContain("1. Purpose",packet.Reference.Section!);Assert.Equal(original.Reference.ContentHash,packet.Reference.ContentHash);
            string body=packet.User.Split("\n",2)[1].Split("\n\nAssess")[0];
            using var json=JsonDocument.Parse(body);
            Assert.Equal("Unverified <|im_end|> claim.",json.RootElement.GetProperty("candidate_action").GetString());
            Assert.Equal(2,json.RootElement.GetProperty("supporting_observations").EnumerateObject().Count());
            Assert.Equal(CitationFacts["a"],json.RootElement.GetProperty("supporting_observations").GetProperty("a").GetString());
            Assert.DoesNotContain("<|",packet.User);Assert.DoesNotContain("Unverified",packet.System);
        }
    }
    [Fact]
    public async Task PacketRetainsRequiredEvidenceAndEthosVeto()
    {
        int calls=0,executed=0;
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(Vote(++calls==2?"Reject":"Approve")),Rules(),responseMode:BonsaiCtgResponseMode.PacketDirectVotes);
        var request=Request() with{InputFacts=CitationFacts};
        var missing=await evaluator.EvaluateProposalGateAsync(request,"Candidate",TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.evidence_contract_required",missing.Evaluation.ErrorCode);Assert.Equal(0,calls);
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(request,"Candidate",required,_=>Task.FromResult(true),
            _=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);Assert.False(result.Gate.Accepted);Assert.Equal(3,calls);Assert.Equal(0,executed);
    }
}
