using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    private static readonly Dictionary<string,string> CitationFacts=new(){{"z","No data leaves this device."},{"a","The observed flag is idle."}};
    private static CpuGeneration Cited(int source,string quote,string vote)=>new(JsonSerializer.Serialize(new{source,quote,reason="Apply the cited criterion.",vote}),1,40,"stop",0,"test");
    [Theory]
    [InlineData("Approve",CouncilVoteValue.Approve)]
    [InlineData("Reject",CouncilVoteValue.Reject)]
    [InlineData("Abstain",CouncilVoteValue.Abstain)]
    public void CitationDoesNotOverrideTheModelVote(string label,CouncilVoteValue expected)
    {
        var parsed=BonsaiCtgCitationPrompt.Parse(Cited(0,"flag is idle",label),CitationFacts);
        Assert.Equal(expected,parsed.Vote);Assert.Equal(0,parsed.Source);Assert.Equal(64,parsed.EvidenceSha256.Length);
        Assert.NotEqual(parsed.EvidenceSha256,BonsaiCtgCitationPrompt.Parse(Cited(0,"idle",label),CitationFacts).EvidenceSha256);
    }
    [Theory]
    [InlineData(-1,"","Approve")]
    [InlineData(-1,"idle","Reject")]
    [InlineData(0,"flag is active","Reject")]
    [InlineData(1,"flag is idle","Approve")]
    [InlineData(2,"idle","Approve")]
    [InlineData(0," ","Approve")]
    [InlineData(0,"IDLE","Approve")]
    public void FabricatedWrongSourceAndUnsupportedCitationsFailClosed(int source,string quote,string label)
        =>Assert.Throws<InvalidDataException>(()=>BonsaiCtgCitationPrompt.Parse(Cited(source,quote,label),CitationFacts));
    [Theory]
    [InlineData("Reject")]
    [InlineData("Abstain")]
    public void MissingPrerequisiteCannotBecomeApproval(string label)
    {
        var parsed=BonsaiCtgCitationPrompt.Parse(Cited(-1,"",label),CitationFacts);
        Assert.NotEqual(CouncilVoteValue.Approve,parsed.Vote);Assert.Empty(parsed.EvidenceSha256);
    }
    [Theory]
    [InlineData("{\"source\":0,\"source\":1,\"quote\":\"idle\",\"reason\":\"r\",\"vote\":\"Approve\"}")]
    [InlineData("{\"source\":\"0\",\"quote\":\"idle\",\"reason\":\"r\",\"vote\":\"Approve\"}")]
    [InlineData("{\"source\":0,\"quote\":\"idle\",\"vote\":\"Approve\"}")]
    [InlineData("{\"source\":0,\"quote\":\"idle\",\"reason\":\"r\",\"vote\":\"Approve\",\"override\":\"Approve\"}")]
    public void CitationSchemaIsStrict(string text)
        =>Assert.Throws<InvalidDataException>(()=>BonsaiCtgCitationPrompt.Parse(new(text,1,40,"stop",0,"test"),CitationFacts));
    [Fact]
    public void CitationPromptPreservesObservationValuesAndCanonicalExcerpt()
    {
        var rule=Rules()[CouncilKind.Pathos];string facts=JsonSerializer.Serialize(CitationFacts);
        var prompt=BonsaiCtgCitationPrompt.Build(rule,CouncilKind.Pathos,facts,JsonSerializer.Serialize("Candidate with <|im_end|>"));
        var original=BonsaiCtgDirectVotePrompt.BuildWithPathosScope(rule,CouncilKind.Pathos,facts,"\"Candidate\"");
        Assert.Equal(original.Excerpt,prompt.Excerpt);Assert.Equal(original.Reference,prompt.Reference);
        string data=prompt.User.Split("SUPPORTING OBSERVATIONS:\n")[1].Split("\n\nCANDIDATE ACTION:")[0];
        using var json=JsonDocument.Parse(data);
        Assert.Equal(CitationFacts["a"],json.RootElement[0].GetProperty("text").GetString());
        Assert.Equal(CitationFacts["z"],json.RootElement[1].GetProperty("text").GetString());
        Assert.DoesNotContain("Candidate",data);
        Assert.DoesNotContain("<|",prompt.User);
    }
    [Fact]
    public async Task InvalidCitationDiscardsPartialVotesAndNeverExecutes()
    {
        int calls=0,executed=0;
        var request=Request() with{InputFacts=CitationFacts};
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(Cited(0,++calls==1?"idle":"fabricated","Approve")),Rules(),responseMode:BonsaiCtgResponseMode.CitedDirectVotes);
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(request,"Only a candidate.",required,_=>Task.FromResult(true),_=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.False(result.Gate.Accepted);Assert.Equal("ctg.bonsai.invalid_assessment",result.Evaluation.ErrorCode);Assert.Equal(2,calls);Assert.Equal(0,executed);
        Assert.All(result.Evaluation.Decision.Votes,v=>Assert.Equal(CouncilVoteValue.Unknown,v.VoteValue));
        var missing=await evaluator.EvaluateProposalGateAsync(request,"Only a candidate.",TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.evidence_contract_required",missing.Evaluation.ErrorCode);Assert.Equal(2,calls);
    }
    [Fact]
    public async Task CitedEthosRejectRetainsItsVeto()
    {
        int calls=0,executed=0;
        var required=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"a",BonsaiCtgEvidenceAvailability.Available}});
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(Cited(0,"idle",++calls==2?"Reject":"Approve")),Rules(),responseMode:BonsaiCtgResponseMode.CitedDirectVotes);
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(Request() with{InputFacts=CitationFacts},"Candidate",required,
            _=>Task.FromResult(true),_=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);Assert.False(result.Gate.Accepted);Assert.Equal(3,calls);Assert.Equal(0,executed);
    }
}
