using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Fact]
    public void PathosScopeUsesExactGovernanceAndPreservesOtherRoles()
    {
        foreach(var pair in Rules())
        {
            var original=BonsaiCtgDirectVotePrompt.BuildSeparated(pair.Value,pair.Key,"{}","\"proposal\"");
            var candidate=BonsaiCtgDirectVotePrompt.BuildWithPathosScope(pair.Value,pair.Key,"{}","\"proposal\"");
            Assert.Equal(original.User,candidate.User);
            if(pair.Key!=CouncilKind.Pathos){Assert.Equal(original,candidate);continue;}
            int start=pair.Value.Text.IndexOf("## 5. Interaction with Other Councils",StringComparison.Ordinal);
            int end=pair.Value.Text.IndexOf("## 6. Abstain Semantics",StringComparison.Ordinal);
            string section=pair.Value.Text[start..end];
            Assert.Equal(original.Excerpt,candidate.Excerpt.Replace(section+"\n","",StringComparison.Ordinal));
            Assert.Equal(original.System,candidate.System.Replace(section+"\n","",StringComparison.Ordinal));
            Assert.Equal(original.Reference.ContentHash,candidate.Reference.ContentHash);
            Assert.Contains("5. Interaction with Other Councils",candidate.Reference.Section!);
        }
    }
    [Fact]
    public async Task PathosScopeCannotOverrideEthosVetoOrChangeLegacyContract()
    {
        int calls=0,executed=0;
        using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>Task.FromResult(Vote(++calls==2?"Reject":"Approve")),Rules(),
            responseMode:BonsaiCtgResponseMode.PathosScopeDirectVotes);
        var result=await evaluator.RunProposalIfAllowedAsync(Request(),"A bounded proposed reply.",_=>Task.FromResult(true),
            _=>{executed++;return Task.CompletedTask;},TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);Assert.False(result.Gate.Accepted);Assert.Equal(0,executed);Assert.Equal(3,calls);
        Assert.Equal("canonical-pathos-scope-json-v9",result.Evaluation.Metadata["prompt.contract"]);
        var legacy=await evaluator.EvaluateGateAsync(Request(),TestContext.Current.CancellationToken);
        Assert.Equal("ctg.bonsai.unsupported_contract",legacy.Evaluation.ErrorCode);Assert.Equal(3,calls);
    }
}
