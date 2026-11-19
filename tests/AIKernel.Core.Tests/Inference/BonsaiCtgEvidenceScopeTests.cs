using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public async Task EvidenceScopeCannotBeCalledWithoutTheHostContract()
    {
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>throw new Exception("must not infer"),Rules(),
            responseMode:BonsaiCtgResponseMode.EvidenceScopedDirectVotes);
        var result=await evaluator.EvaluateProposalGateAsync(Request(),"Candidate",TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.evidence_contract_required",result.Evaluation.ErrorCode);
        Assert.False(result.Gate.Accepted);Assert.All(result.Evaluation.Decision.Votes,v=>Assert.Equal(CouncilVoteValue.Unknown,v.VoteValue));
    }
    [Theory]
    [InlineData(BonsaiCtgEvidenceAvailability.Unknown,"ctg.bonsai.required_evidence_unavailable")]
    [InlineData(BonsaiCtgEvidenceAvailability.Unavailable,"ctg.bonsai.required_evidence_unavailable")]
    [InlineData(BonsaiCtgEvidenceAvailability.Available,"ctg.bonsai.required_evidence_missing")]
    public async Task EvidenceScopeFailsBeforeInferenceOnUnestablishedContext(BonsaiCtgEvidenceAvailability state,string error)
    {
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>throw new Exception("must not infer"),Rules(),
            responseMode:BonsaiCtgResponseMode.EvidenceScopedDirectVotes);
        var requirements=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"configuration-body",state}});
        int executed=0;
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(Request(),"The proposal asserts all context is available.",requirements,
            _=>Task.FromResult(true),_=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.Equal(error,result.Evaluation.ErrorCode);Assert.False(result.Gate.Accepted);Assert.Equal(0,executed);
        Assert.All(result.Evaluation.Decision.Votes,v=>Assert.Equal(CouncilVoteValue.Unknown,v.VoteValue));
    }
    [Theory]
    [InlineData(false,"Approve",false)]
    [InlineData(true,"Reject",false)]
    [InlineData(true,"Approve",true)]
    public async Task EvidenceScopePreservesTheScopePromptHostAuthorityAndEthosVeto(bool admitted,string ethos,bool allowed)
    {
        var rules=Rules();int calls=0,executed=0;string proposal="Candidate";
        var request=Request() with{InputFacts=new Dictionary<string,string>{{"evidence","A complete observation."}}};
        var requirements=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"evidence",BonsaiCtgEvidenceAvailability.Available}});
        using var evaluator=new BonsaiCtgCouncilEvaluator((system,user,token)=>
        {
            Assert.True(admitted);var role=Councils[calls++];
            var expected=BonsaiCtgDirectVotePrompt.BuildWithPathosScope(rules[role],role,"{\"evidence\":\"A complete observation.\"}","\"Candidate\"");
            Assert.Equal(expected.System,system);Assert.Equal(expected.User,user);
            return Task.FromResult(Vote(role==CouncilKind.Ethos?ethos:"Approve"));
        },rules,responseMode:BonsaiCtgResponseMode.EvidenceScopedDirectVotes);
        var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(request,proposal,requirements,_=>Task.FromResult(admitted),
            _=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.Equal(allowed,result.Gate.Accepted);Assert.Equal(admitted?3:0,calls);Assert.Equal(allowed?1:0,executed);
    }
}
