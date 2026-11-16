using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

internal static class BonsaiCtgSuite
{
    internal static async Task<bool> Run(string modelPath,string governancePath,string casesPath,string output,BonsaiCtgResponseMode mode=BonsaiCtgResponseMode.DirectVotes,bool separated=false)
    {
        var rules=LoadRules(governancePath);
        return await Evaluate(modelPath,rules,casesPath,output,mode,separated);
    }
    internal static Dictionary<CouncilKind,BonsaiCouncilRule> LoadRules(string governancePath)
    {
        var rules=new Dictionary<CouncilKind,BonsaiCouncilRule>();
        foreach(var council in new[]{CouncilKind.Logos,CouncilKind.Ethos,CouncilKind.Pathos})
        {
            string name="council."+council.ToString().ToLowerInvariant()+".monolith.md";byte[] bytes=File.ReadAllBytes(Path.Combine(governancePath,name));
            rules.Add(council,BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council."+council,Path="rom/governance/"+name,
                ContentHash=Convert.ToHexStringLower(SHA256.HashData(bytes))}));
        }
        return rules;
    }
    internal static void VerifyRules(string governancePath)
    {
        var verified=LoadRules(governancePath).Select(pair=>
        {
            var prompt=BonsaiCtgDirectVotePrompt.Build(pair.Value,pair.Key,"{}");
            return new{council=pair.Key.ToString(),prompt.Reference,
                excerptSha256=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prompt.Excerpt)))};
        }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(verified,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static async Task<bool> Evaluate(string modelPath,Dictionary<CouncilKind,BonsaiCouncilRule> rules,string casesPath,string output,BonsaiCtgResponseMode mode,bool separated)
    {
        using var evaluator=new BonsaiCtgCouncilEvaluator(modelPath,"3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3",rules,evaluationTimeout:TimeSpan.FromMinutes(10),responseMode:mode);
        string current="";var observations=new List<object>();
        evaluator.Observation=(council,answer)=>
        {
            observations.Add(new{id=current,council=council.ToString(),answer});
            File.WriteAllText(output+".responses.json",JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        };
        using var cases=JsonDocument.Parse(File.ReadAllText(casesPath));var results=new List<object>();
        bool allPassed=cases.RootElement.GetArrayLength()>0;
        foreach(var item in cases.RootElement.EnumerateArray())
        {
            string id=item.GetProperty("id").GetString()!;bool expected=item.GetProperty("expectedGate").GetBoolean();
            current=id;
            var facts=item.GetProperty("facts").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!);
            var request=new CouncilEvaluationRequest{OperationId=id,StepId="observation",InputFacts=facts};
            var result=separated?await evaluator.EvaluateProposalGateAsync(request,item.GetProperty("proposal").GetString()!):await evaluator.EvaluateGateAsync(request);
            var checks=item.GetProperty("requiredVotes").EnumerateObject().Select(p=>new{council=p.Name,
                expected=p.Value.EnumerateArray().Select(v=>v.GetString()!).ToArray(),
                actual=result.Evaluation.Decision.Votes.Single(v=>v.CouncilKind.ToString()==p.Name).VoteValue.ToString()}).ToArray();
            bool pass=result.Evaluation.Succeeded&&result.Gate.Accepted==expected&&checks.All(c=>c.expected.Contains(c.actual));
            allPassed&=pass;
            results.Add(new{id,pass,expectedGate=expected,checks,result});
            File.WriteAllText(output,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(new{id,pass,result.Evaluation.Succeeded,result.Evaluation.ErrorCode,result.Gate.Accepted,checks}));
        }
        return allPassed;
    }
    internal static async Task PrefixRegression(string modelPath,string output)
    {
        using var model=new Qwen3CpuModel(modelPath,4,512,"auto");model.ConfigureTrustedPrefixCache(3,128L<<20);
        string[] systems=["Answer factual questions briefly.","Explain arithmetic briefly.","Give a neutral technical answer.","Respond in plain English."];
        var rows=new List<object>();var hashes=new Dictionary<int,string>();
        foreach(int index in new[]{0,1,2,0,3,1,0})
        {
            var answer=await model.GenerateBenchmarkAsync(systems[index],"What is 2 plus 2?",16);
            if(hashes.TryGetValue(index,out var expected)&&expected!=answer.GeneratedTokenSha256)throw new InvalidDataException("Cached prefix changed token hash.");
            hashes[index]=answer.GeneratedTokenSha256!;
            rows.Add(new{index,answer.GeneratedTokenSha256,answer.PrefillForwardTokens,cache=model.PrefixCacheStatistics});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Prefix cold, warm and evicted generation hashes matched.");
    }
}
