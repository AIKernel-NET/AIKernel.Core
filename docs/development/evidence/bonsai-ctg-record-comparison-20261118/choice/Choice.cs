using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

static string Hash(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
static void Check(bool v,string message) { if (!v) throw new InvalidDataException(message); }
static string Get(JsonElement data,string name) => data.GetProperty(name).GetString()!;
static Dictionary<CouncilKind,BonsaiCouncilRule> Rules(string path)
{
    var output=new Dictionary<CouncilKind,BonsaiCouncilRule>();
    foreach(var role in new[]{CouncilKind.Logos,CouncilKind.Ethos,CouncilKind.Pathos})
    {
        string name="council."+role.ToString().ToLowerInvariant()+".monolith.md";
        byte[] bytes=File.ReadAllBytes(Path.Combine(path,name));
        var rule=BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council."+role,
            Path="rom/governance/"+name,ContentHash=Convert.ToHexStringLower(SHA256.HashData(bytes))});
        BonsaiCtgFindingPolicy.RequireSupported(rule,role);
        output.Add(role,rule);
    }
    return output;
}
static void Save(string path,object value)
{
    var options=new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    File.WriteAllText(path,JsonSerializer.Serialize(value,options)+"\n");
}
const string modelHash="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
if(args is ["--prepare",var governance,var input,var folder])
{
    var rules=Rules(governance);var source=JsonDocument.Parse(File.ReadAllText(input));
    var format=new JsonSerializerOptions{Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    foreach(string order in new[]{"primary","reverse"})
    {
        var prompts=new List<object>();var key=new List<object>();
        foreach(var cell in source.RootElement.EnumerateArray())
        {
            string id=Get(cell,"id"), reading=Get(cell,"reading");
            var role=Enum.Parse<CouncilKind>(Get(cell,"council"));
            var rule=rules[role];Check(rule.Reference.ContentHash==Get(cell,"romSha256"),"ROM bytes mismatch");
            var facts=cell.GetProperty("facts").Deserialize<Dictionary<string,string>>()!.ToImmutableSortedDictionary(StringComparer.Ordinal);
            string factJson=JsonSerializer.Serialize(facts,format), proposal=Get(cell,"proposal"), proposalJson=JsonSerializer.Serialize(proposal,format);
            var reader=BonsaiCtgFocusedPrompt.Reading(factJson,proposalJson);
            Check(Hash(reader.System+"\n"+reader.User)==Get(cell,"readingPromptSha256")&&Hash(reading)==Get(cell,"readingSha256"),"Previous reading mismatch");
            var witness=BonsaiCtgLiteralComparison.TryCreate(proposal,facts)??throw new InvalidDataException("No comparison");
            var previous=BonsaiCtgAnchoredVotePrompt.Build(rule,role,factJson,proposalJson,reading,literalComparison:witness.ToPromptData());
            Check(Hash(previous.System+"\n"+previous.User)==Get(cell,"v25PromptSha256"),"Original v25 prompt mismatch");
            string comparison=BonsaiCtgRecordFindingPrompt.FormatComparison(witness,facts.Keys);
            var v26=BonsaiCtgRecordFindingPrompt.Build(rule,role,factJson,proposalJson,reading,comparison);
            string[] finding=role==CouncilKind.Ethos?["supported","proposal_defect","evaluation_unavailable","safety_neutral"]:["supported","proposal_defect","evaluation_unavailable"];
            if(order=="reverse")Array.Reverse(finding);
            var mapping=finding.Select((f,i)=>(symbol:((char)('A'+i)).ToString(),finding:f)).ToDictionary(k=>k.symbol,k=>k.finding);
            string choices=string.Join("\n",mapping.Select(k=>k.Key+" = "+k.Value));
            const string old="Return one JSON object with reason, finding, source. Give one short reason about this specific proposal. Choose one canonical finding label from the definitions below. Source is the integer index of the decisive original observation.";
            const string tail="Return only the reason, finding, source JSON assessment.";
            Check(v26.System.Contains(old)&&v26.User.EndsWith(tail,StringComparison.Ordinal),"Unexpected baseline schema");
            string system=v26.System.Replace(old,"Choose the one symbol for the canonical finding that applies to this proposal. Use the fixed mapping below.\n"+choices+"\n",StringComparison.Ordinal);
            string user=v26.User[..^tail.Length]+"Answer with the single symbol of the applicable canonical finding.";
            Check(system.Length<=8192&&user.Length<=4096,"Prompt budget");
            var labels=mapping.Keys.ToArray();
            string promptSha256=Hash(system+"\n"+user);
            prompts.Add(new{id,diagnosticOnly=true,system,user,labels,promptSha256});
            key.Add(new {id,council=role.ToString(),mapping,promptSha256,v26PromptSha256=Hash(v26.System+"\n"+v26.User),romSha256=rule.Reference.ContentHash,readingSha256=Hash(reading)});
        }
        Save(Path.Combine(folder,order+"-cases.json"),prompts);Save(Path.Combine(folder,order+"-mapping.json"),key);
    }
    return;
}
if(args is not ["--bonsai-ctg-choice-pilot",var modelPath,var romPath,var fixture,var outputPath])
    throw new ArgumentException("Expected --prepare or --bonsai-ctg-choice-pilot MODEL ROM INPUT OUTPUT");
_=Rules(romPath);
using(var stream=File.OpenRead(modelPath))
    Check(Convert.ToHexStringLower(SHA256.HashData(stream))==modelHash,"Model digest mismatch");
using var doc=JsonDocument.Parse(File.ReadAllText(fixture));
var cases=doc.RootElement.EnumerateArray().ToArray();
Check(cases.Length==6&&cases.Select(c=>Get(c,"id")).Distinct().Count()==6,"Six unique diagnostics required");
foreach(var row in cases)
{
    Check(row.GetProperty("diagnosticOnly").GetBoolean()&&Hash(Get(row,"system")+"\n"+Get(row,"user"))==Get(row,"promptSha256"),"Prompt mismatch");
    string[] labels=row.GetProperty("labels").Deserialize<string[]>()!;
    Check(labels.SequenceEqual(new[]{"A","B","C"})||labels.SequenceEqual(new[]{"A","B","C","D"}),"Unexpected symbol set");
}
using var engine=new Qwen3CpuModel(modelPath,4,2048,"auto");
engine.ConfigureTrustedPrefixCache(1,1024L<<20);
var rows=new List<object>();
foreach(var row in cases)
{
    string id=Get(row,"id"),promptSha256=Get(row,"promptSha256");
    string[] labels=row.GetProperty("labels").Deserialize<string[]>()!;
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
    var answer=await engine.ClassifyAsync(Get(row,"system"),Get(row,"user"),labels,timeout.Token);
    Check(answer.FinishReason=="classified"&&answer.OutputTokens==1&&labels.Contains(answer.Text),"Invalid classified result");
    rows.Add(new{id,promptSha256,responseSha256=Hash(answer.Text),answer});
    Save(outputPath,new{diagnosticOnly=true,modelHash,planned=cases.Length,rows});
}
