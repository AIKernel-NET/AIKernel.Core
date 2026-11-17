using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

internal static class BonsaiCtgEvidenceScopeSuite
{
    internal static async Task<bool> Run(string modelPath,string governancePath,string casesPath,string output,BonsaiCtgResponseMode mode=BonsaiCtgResponseMode.EvidenceScopedDirectVotes)
    {
        var rules=BonsaiCtgSuite.LoadRules(governancePath);
        using var evaluator=new BonsaiCtgCouncilEvaluator(modelPath,"3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3",rules,
            responseMode:mode,evaluationTimeout:TimeSpan.FromMinutes(10),maximumPrefixCacheBytes:mode is BonsaiCtgResponseMode.PerCriterionFindings or BonsaiCtgResponseMode.CitedFindings or BonsaiCtgResponseMode.DemonstratedPacketVotes or BonsaiCtgResponseMode.ReviewedPacketVotes?1024L<<20:512L<<20);
        int calls=0;string current="";var observations=new List<object>();var rows=new List<object>();
        evaluator.Observation=(role,answer)=>
        {
            calls++;observations.Add(new{id=current,council=role.ToString(),stage="final",answer});
            File.WriteAllText(output+".responses.json",JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        };
        evaluator.ProvisionalObservation=(role,answer)=>
        {
            calls++;observations.Add(new{id=current,council=role.ToString(),stage="draft",answer});
            File.WriteAllText(output+".responses.json",JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        };
        evaluator.CriterionObservation=(role,name,answer)=>
        {
            calls++;observations.Add(new{id=current,council=role.ToString(),stage="criterion",criterion=name,answer});
            File.WriteAllText(output+".responses.json",JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        };
        // This is a prompt/guard witness, not a second model assessment or a semantic baseline.
        int legacyCalls=0;
        using var legacy=new BonsaiCtgCouncilEvaluator((s,u,t)=>
        {
            legacyCalls++;return Task.FromResult(new CpuGeneration("{\"reason\":\"Contract probe only.\",\"vote\":\"Reject\"}",1,1,"stop",0,"test-double"));
        },rules);
        using var cases=JsonDocument.Parse(File.ReadAllText(casesPath));bool all=true;
        foreach(var item in cases.RootElement.EnumerateArray())
        {
            current=item.GetProperty("id").GetString()!;calls=0;legacyCalls=0;
            bool blocked=item.GetProperty("expectedHostBlock").GetBoolean(),expected=item.GetProperty("expectedGate").GetBoolean();
            var requirements=new BonsaiCtgEvidenceRequirements(item.GetProperty("hostRequirements").EnumerateObject()
                .ToDictionary(p=>p.Name,p=>Enum.Parse<BonsaiCtgEvidenceAvailability>(p.Value.GetString()!)));
            var request=new CouncilEvaluationRequest{OperationId=current,StepId="observation",InputFacts=item.GetProperty("facts").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!)};
            string proposal=item.GetProperty("proposal").GetString()!;
            var witness=await legacy.EvaluateProposalWithEvidenceAsync(request,proposal,requirements);
            var result=await evaluator.EvaluateProposalWithEvidenceGateAsync(request,proposal,requirements);
            var checks=item.GetProperty("requiredVotes").EnumerateObject().Select(p=>new{council=p.Name,
                expected=p.Value.EnumerateArray().Select(v=>v.GetString()!).ToArray(),
                actual=result.Evaluation.Decision.Votes.Single(v=>v.CouncilKind.ToString()==p.Name).VoteValue.ToString()}).ToArray();
            bool modelPass=result.Evaluation.Succeeded&&result.Gate.Accepted==expected&&checks.All(c=>c.expected.Contains(c.actual));
            bool hostPass=blocked&&!expected&&calls==0&&legacyCalls==0&&!witness.Succeeded&&!result.Evaluation.Succeeded&&
                witness.ErrorCode=="ctg.bonsai.required_evidence_unavailable"&&result.Evaluation.ErrorCode==witness.ErrorCode&&!result.Gate.Accepted&&
                result.Evaluation.Decision.Votes.All(v=>v.VoteValue==CouncilVoteValue.Unknown);
            bool pass=blocked?hostPass:modelPass&&calls==(mode==BonsaiCtgResponseMode.PerCriterionFindings?16:mode==BonsaiCtgResponseMode.ReviewedPacketVotes?6:3)&&legacyCalls==3;
            all&=pass;
            rows.Add(new{id=current,pass=modelPass,pipelinePass=pass,expectedHostBlock=blocked,inferenceCalls=calls,expectedGate=expected,
                legacyPromptWitness=witness.Metadata,checks,result});
            File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(new{id=current,pipelinePass=pass,modelPass,expectedHostBlock=blocked,inferenceCalls=calls,result.Gate.Accepted,checks}));
        }
        return all;
    }
}
