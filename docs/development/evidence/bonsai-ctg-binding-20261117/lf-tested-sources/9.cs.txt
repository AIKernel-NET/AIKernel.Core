using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    private static CpuGeneration CriterionReply(string finding)=>new(JsonSerializer.Serialize(new{reason="Criterion checked against the observations.",finding}),1,24,"stop",0,"test");
    [Fact]
    public void DecompositionSelectsEveryVerbatimCriterionWithoutDuplicatingUserFactsIntoTheSystem()
    {
        foreach(var pair in Rules())
        {
            var parts=BonsaiCtgCriteriaPrompt.Build(pair.Value,pair.Key,"{\"a\":\"private-fact-marker\"}","\"candidate-marker\"");
            Assert.Equal(pair.Key==CouncilKind.Ethos?6:5,parts.Count);
            Assert.Single(parts.Select(p=>p.System).Distinct());
            Assert.Equal(parts.Count,parts.Select(p=>p.Name).Distinct().Count());
            foreach(var part in parts)
            {
                Assert.Contains(part.Text,pair.Value.Text);Assert.Contains(JsonSerializer.Serialize(part.Text),part.User);
                Assert.DoesNotContain("private-fact-marker",part.System);Assert.DoesNotContain("candidate-marker",part.System);
                Assert.Equal(pair.Value.Reference.ContentHash,part.Reference.ContentHash);
                Assert.Contains("Voting Behavior",part.System);Assert.Contains("Interaction with Other Councils",part.System);
            }
        }
    }
    [Theory]
    [InlineData(CouncilKind.Logos,5,CouncilVoteValue.Abstain)]
    [InlineData(CouncilKind.Ethos,6,CouncilVoteValue.Reject)]
    [InlineData(CouncilKind.Pathos,5,CouncilVoteValue.Abstain)]
    public void AggregationRequiresAllCriteriaAndPreservesCanonicalUnavailableAndDefectRules(CouncilKind council,int count,CouncilVoteValue unavailable)
    {
        var values=Enumerable.Repeat("supported",count).ToArray();
        Assert.Equal(CouncilVoteValue.Approve,BonsaiCtgCriteriaPrompt.Resolve(council,values));
        Assert.Throws<InvalidDataException>(()=>BonsaiCtgCriteriaPrompt.Resolve(council,values[..^1]));
        values[0]="evaluation_unavailable";Assert.Equal(unavailable,BonsaiCtgCriteriaPrompt.Resolve(council,values));
        values[^1]="proposal_defect";Assert.Equal(CouncilVoteValue.Reject,BonsaiCtgCriteriaPrompt.Resolve(council,values));
        values[1]="safety_neutral";Assert.Throws<InvalidDataException>(()=>BonsaiCtgCriteriaPrompt.Resolve(council,values));
    }
    [Theory]
    [InlineData("{\"reason\":\"r\",\"finding\":\"supported\",\"vote\":\"Approve\"}")]
    [InlineData("{\"reason\":\"r\",\"finding\":\"supported\",\"finding\":\"proposal_defect\"}")]
    [InlineData("{\"reason\":\"r\",\"finding\":\"safety_neutral\"}")]
    [InlineData("{\"reason\":\"\",\"finding\":\"supported\"}")]
    [InlineData("{\"reason\":\"r\",\"finding\":true}")]
    public void InvalidCriterionCannotInjectVotesOrSkipCriteria(string text)
        =>Assert.Throws<InvalidDataException>(()=>BonsaiCtgCriteriaPrompt.Parse(new(text,1,30,"stop",0,"test")));
    [Theory]
    [InlineData(0,true)]
    [InlineData(6,false)]
    [InlineData(11,false)]
    public async Task AllSixteenAssessmentsPrecedeTheGateAndAnEthosCriterionCanVeto(int rejectAt,bool accepted)
    {
        int calls=0,executed=0,observed=0;
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>{Assert.Equal(0,executed);return Task.FromResult(CriterionReply(++calls==rejectAt?"proposal_defect":"supported"));},Rules(),responseMode:BonsaiCtgResponseMode.PerCriterionFindings);
        evaluator.CriterionObservation=(_,_,_)=>observed++;
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(Request() with{InputFacts=CitationFacts},"Candidate",required,
            _=>Task.FromResult(true),_=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);Assert.Equal(accepted,result.Gate.Accepted);Assert.Equal(16,calls);Assert.Equal(16,observed);Assert.Equal(accepted?1:0,executed);
        Assert.Equal(3,result.Evaluation.Decision.Votes.Count);Assert.Equal(BonsaiCtgCriteriaPrompt.Binding,result.Evaluation.Metadata["binding.policy"]);
    }
    [Fact]
    public async Task AnInvalidLateCriterionDiscardsEarlierApprovalsAndMissingEvidenceSkipsAllInference()
    {
        int calls=0;
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(++calls==10?new CpuGeneration("bad",1,1,"stop",0,"test"):CriterionReply("supported")),Rules(),responseMode:BonsaiCtgResponseMode.PerCriterionFindings);
        var request=Request() with{InputFacts=CitationFacts};
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(request,"Candidate",required,_=>Task.FromResult(true),
            _=>throw new Exception("must not execute"),TestContext.Current.CancellationToken);
        Assert.Equal(10,calls);Assert.False(result.Gate.Accepted);Assert.Equal("ctg.bonsai.invalid_criterion",result.Evaluation.ErrorCode);
        Assert.All(result.Evaluation.Decision.Votes,v=>Assert.Equal(CouncilVoteValue.Unknown,v.VoteValue));
        var missing=await evaluator.EvaluateProposalAsync(request,"Candidate",TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.evidence_contract_required",missing.ErrorCode);Assert.Equal(10,calls);
    }
    [Fact]
    public async Task CancellationDuringCriteriaPropagatesWithoutExecuting()
    {
        int calls=0;using var cancel=new CancellationTokenSource();
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>
        {
            if(++calls==7){cancel.Cancel();return Task.FromCanceled<CpuGeneration>(t);}
            return Task.FromResult(CriterionReply("supported"));
        },Rules(),responseMode:BonsaiCtgResponseMode.PerCriterionFindings);
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>evaluator.RunProposalWithEvidenceIfAllowedAsync(Request() with{InputFacts=CitationFacts},"Candidate",required,
            _=>Task.FromResult(true),_=>throw new Exception("must not execute"),cancel.Token));
        Assert.Equal(7,calls);
    }
}
