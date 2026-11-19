using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

static string Hex(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
static string H(string text) => Hex(Encoding.UTF8.GetBytes(text));
static void Check(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
static string Field(JsonElement e, string key) => e.GetProperty(key).GetString()!;
const string modelHash = "3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
const string kernelHash = "98bda15926941db1f1912f34d4ebc764a467a3e18245a2f0b43755d7c562c8be";
Check(args.Length==5, "Expected mode MODEL ROM CASES OUTPUT");
bool prepare=args[0]=="--prepare-source-first";
Check(prepare || args[0]=="--bonsai-ctg-source-first-probe","Unexpected mode");
Check(Hex(File.ReadAllBytes(typeof(BonsaiCtgCouncilEvaluator).Assembly.Location))==kernelHash,"Kernel checksum");
string file="council.logos.monolith.md";
byte[] bytes=File.ReadAllBytes(Path.Combine(args[2],file));
var rule=BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council.Logos",Path="rom/governance/"+file,ContentHash=Hex(bytes)});
BonsaiCtgFindingPolicy.RequireSupported(rule,CouncilKind.Logos);
if (!prepare)
{
    using var modelFile=File.OpenRead(args[1]);
    Check(Convert.ToHexStringLower(SHA256.HashData(modelFile))==modelHash,"Model checksum");
}
var json = new JsonSerializerOptions{Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
var indented = new JsonSerializerOptions(json){WriteIndented=true};
using var inputs=JsonDocument.Parse(File.ReadAllBytes(args[3]));
var cases=inputs.RootElement.EnumerateArray().ToArray();
Check(cases.Length==6 && cases.Select(c=>Field(c,"id")).Distinct().Count()==6,"Six unique cells required");
using var engine=prepare ? null : new Qwen3CpuModel(args[1],4,2048,"auto");
engine?.ConfigureTrustedPrefixCache(1,1024L<<20);
var rows=new List<object>();
foreach (var cell in cases)
{
    string id=Field(cell,"id"), reading=Field(cell,"reading"), proposal=Field(cell,"proposal");
    var facts=cell.GetProperty("facts").Deserialize<Dictionary<string,string>>()!.ToImmutableSortedDictionary(StringComparer.Ordinal);
    string originalFacts=JsonSerializer.Serialize(facts,json), originalProposal=JsonSerializer.Serialize(proposal,json);
    var reRead=BonsaiCtgFocusedPrompt.Reading(originalFacts,originalProposal);
    Check(H(reRead.System+"\n"+reRead.User)==Field(cell,"readingPromptSha256") && H(reading)==Field(cell,"readingSha256") &&
        Hex(bytes)==Field(cell,"romSha256"),"Saved reading or source mismatch "+id);
    var witness=BonsaiCtgLiteralComparison.TryCreate(proposal,facts)??throw new InvalidDataException("No unique comparison");
    Check(H(witness.ToPromptData())==Field(cell,"witnessSha256"),"Witness mismatch");
    var prompt=BonsaiCtgReasonedFindingPrompt.Build(rule,CouncilKind.Logos,originalFacts,originalProposal,reading,witness.DescribeForCouncil());
    using var evidence=JsonDocument.Parse(witness.ToPromptData());
    var data=evidence.RootElement;
    string key=Field(data,"observationKey"), observed=Field(data.GetProperty("observation"),"literal"),
        candidate=Field(data.GetProperty("candidate"),"literal");
    Check(facts[key].Contains(observed,StringComparison.Ordinal)&&proposal.Contains(candidate,StringComparison.Ordinal),"Source segments");
    string observationData=JsonSerializer.Serialize(new{key,literal=observed},json);
    string proposalData=JsonSerializer.Serialize(new{literal=candidate},json);
    string priorReasonPrompt=BonsaiCtgReasonedFindingPrompt.ForReason(prompt.User);
    string reasonUser=priorReasonPrompt+"\nOriginal observation (source data): "+observationData+
        "\nProposed report (candidate data): "+proposalData+
        "\nIn one sentence compare the original observation, then the proposed report, then what the requester asked for.";
    string reasonPromptSha256=H(prompt.System+"\n"+reasonUser), priorReasonPromptSha256=H(prompt.System+"\n"+priorReasonPrompt);
    string selectionBaseSha256=H(prompt.System+"\n"+prompt.User);
    if(prepare)
    {
        rows.Add(new{id,system=prompt.System,originalReasonUser=priorReasonPrompt,reasonUser,selectionBase=prompt.User,
            reasonPromptSha256,priorReasonPromptSha256,selectionBaseSha256,observationKey=key,observationLiteral=observed,proposalLiteral=candidate});
        continue;
    }
    Check(Field(cell,"reasonPromptSha256")==reasonPromptSha256 && Field(cell,"selectionBaseSha256")==selectionBaseSha256,"Frozen intervention");
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
    CpuGeneration? reason=null, answer=null;
    string? selectionUser=null, finding=null, error=null, errorStage=null;
    int calls=0;
    string stage="reason";
    try
    {
        calls++;
        reason=await engine!.GenerateAsync(prompt.System,reasonUser,192,timeout.Token);
        stage="reason framing";
        selectionUser=BonsaiCtgReasonedFindingPrompt.ForSelection(prompt.User,reason);
        stage="selection";
        calls++;
        answer=await engine.ClassifyAsync(prompt.System,selectionUser,BonsaiCtgReasonedFindingPrompt.Choices(CouncilKind.Logos),timeout.Token);
        stage="selection framing";
        finding=BonsaiCtgReasonedFindingPrompt.Finding(CouncilKind.Logos,answer);
    }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException or ArithmeticException ||
                               ex is OperationCanceledException && timeout.IsCancellationRequested)
    {
        error=ex.GetType().Name+": "+ex.Message;
        errorStage=stage;
    }
    rows.Add(new{id,actualModelCalls=calls,promptSha256=selectionUser==null?null:H(prompt.System+"\n"+selectionUser),
        responseSha256=answer==null?null:H(answer.Text),reasonPromptSha256,priorReasonPromptSha256,
        reasonResponseSha256=reason==null?null:H(reason.Text),selectionBaseSha256,reason,answer,finding,error,errorStage});
    File.WriteAllText(args[4],JsonSerializer.Serialize(new{diagnosticOnly=true,modelHash,kernelHash,planned=cases.Length,rows},indented)+"\n");
}
if(prepare) File.WriteAllText(args[4],JsonSerializer.Serialize(rows,indented)+"\n");
