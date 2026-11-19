using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

// Frozen v19 readings isolate the downstream prompt. Diagnostic answers never become votes.
internal static class BonsaiCtgDefinitionDiagnostics
{
    internal static async Task Prepare(string governance,string casesPath,string responsesPath,string resultsPath,string output)
    {
        var rules=BonsaiCtgSuite.LoadRules(governance);
        using var cases=JsonDocument.Parse(File.ReadAllText(casesPath));
        using var responses=JsonDocument.Parse(File.ReadAllText(responsesPath));
        using var results=JsonDocument.Parse(File.ReadAllText(resultsPath));
        string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        var rows=new List<object>();
        foreach(string id in new[]{"pair-contacts-count","pair-contacts-disclose"})
        {
            var item=cases.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
            var old=results.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
            string reading=responses.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id&&r.GetProperty("stage").GetString()=="reading").GetProperty("answer").GetProperty("Text").GetString()!;
            var captured=new List<(string System,string User)>();
            using var evaluator=new BonsaiCtgCouncilEvaluator((s,u,t)=>
            {
                captured.Add((s,u));
                return Task.FromResult(new CpuGeneration(captured.Count==1?reading:"{\"reason\":\"Prompt capture only.\",\"vote\":\"Reject\"}",1,1,"stop",0,"test-double"));
            },rules,responseMode:BonsaiCtgResponseMode.FocusedReadingVotes);
            var request=new CouncilEvaluationRequest{OperationId=id,StepId="observation",InputFacts=item.GetProperty("facts").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!)};
            var requirements=new BonsaiCtgEvidenceRequirements(item.GetProperty("hostRequirements").EnumerateObject().ToDictionary(p=>p.Name,p=>Enum.Parse<BonsaiCtgEvidenceAvailability>(p.Value.GetString()!)));
            var evaluation=await evaluator.EvaluateProposalWithEvidenceAsync(request,item.GetProperty("proposal").GetString()!,requirements);
            if(!evaluation.Succeeded||captured.Count!=4)throw new InvalidDataException("Failed prompt capture.");
            foreach(var (role,index) in new[]{(CouncilKind.Logos,1),(CouncilKind.Ethos,2)})
            {
                var prompt=captured[index];string expected=old.GetProperty("result").GetProperty("Evaluation").GetProperty("Metadata").GetProperty(role+".prompt.sha256").GetString()!;
                if(Hash(prompt.System+"\n"+prompt.User)!=expected)throw new InvalidDataException("v19 prompt changed during diagnostic capture.");
                string rom=rules[role].Text;
                int start=rom.IndexOf("## 2. Core Responsibilities",StringComparison.Ordinal),end=rom.IndexOf("## 3.",start,StringComparison.Ordinal);
                string core=rom[start..end];
                var variants=new List<(string Name,string Section)>{("original",""),("core",core)};
                if(role==CouncilKind.Ethos)
                {
                    int trans=core.IndexOf("### 2.5 Transparency",StringComparison.Ordinal);
                    int reverse=core.IndexOf("### 2.4 Reversibility",StringComparison.Ordinal);
                    if(trans<0||reverse<0)throw new InvalidDataException("Definition section missing.");
                    variants.Add(("transparency",core[trans..]));
                    variants.Add(("reversibility",core[reverse..trans]));
                }
                string heading=role==CouncilKind.Ethos?"## 4. Evaluation Criteria":"## 3. Evaluation Criteria";
                foreach(var variant in variants)
                {
                    string system=variant.Section.Length==0?prompt.System:prompt.System.Replace(heading,variant.Section+"\n"+heading,StringComparison.Ordinal);
                    var response=responses.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id&&r.GetProperty("council").GetString()==role.ToString());
                    rows.Add(new{id=id+"-"+role+"-"+variant.Name,diagnosticOnly=true,system,user=prompt.User,promptSha256=Hash(system+"\n"+prompt.User),
                        originalPromptSha256=expected,originalResponseSha256=Hash(response.GetProperty("answer").GetProperty("Text").GetString()!),
                        canonicalHash=rules[role].Reference.ContentHash,readingSha256=Hash(reading),variant=variant.Name});
                }
            }
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
