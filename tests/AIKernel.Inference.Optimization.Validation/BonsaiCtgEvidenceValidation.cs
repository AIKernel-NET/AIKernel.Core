using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Dtos.Governance;

internal static class BonsaiCtgEvidenceValidation
{
    internal static async Task Run(string modelPath,string governancePath,string casesPath,string output)
    {
        using var evaluator=new BonsaiCtgCouncilEvaluator(modelPath,"3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3",
            BonsaiCtgSuite.LoadRules(governancePath),evaluationTimeout:TimeSpan.FromMinutes(10));
        int calls=0;var responses=new List<object>();
        evaluator.Observation=(role,answer)=>{calls++;responses.Add(new{role,answer});};
        var rows=new List<object>();
        var missing=Request(new(){{"context","The profile exists but the host has not retrieved it."}});
        foreach(var state in new[]{BonsaiCtgEvidenceAvailability.Unknown,BonsaiCtgEvidenceAvailability.Unavailable,BonsaiCtgEvidenceAvailability.Available})
        {
            var requirements=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"profile",state}});
            var result=await evaluator.RunProposalWithEvidenceIfAllowedAsync(missing,"Apply the profile.",requirements,_=>Task.FromResult(true),
                _=>throw new InvalidOperationException("An unavailable prerequisite must not execute."));
            if(result.Gate.Accepted||result.Evaluation.Succeeded||calls!=0)throw new InvalidDataException("Evidence prerequisite was bypassed.");
            rows.Add(new{state,inferenceCalls=calls,result});
        }
        using var document=JsonDocument.Parse(File.ReadAllText(casesPath));
        var item=document.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="verified-arithmetic");
        var facts=item.GetProperty("facts").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!);
        var available=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>{{"evidence",BonsaiCtgEvidenceAvailability.Available}});
        var accepted=await evaluator.EvaluateProposalWithEvidenceGateAsync(Request(facts),item.GetProperty("proposal").GetString()!,available);
        if(!accepted.Evaluation.Succeeded||calls!=3)throw new InvalidDataException("Available observations did not reach all three councils.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{blocked=rows,availableResult=accepted,responses,inferenceCalls=calls,
            hostGuardPassed=true,semanticGateAccepted=accepted.Gate.Accepted},new JsonSerializerOptions{WriteIndented=true}));
        static CouncilEvaluationRequest Request(Dictionary<string,string> facts)=>new(){OperationId="required-observation-probe",StepId="observation",InputFacts=facts};
    }
}
