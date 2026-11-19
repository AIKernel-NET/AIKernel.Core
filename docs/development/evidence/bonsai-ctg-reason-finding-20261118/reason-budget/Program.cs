using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;
static void Check(bool ok,string detail){if(!ok)throw new InvalidDataException(detail);}
static string SHA(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string H(string text)=>SHA(Encoding.UTF8.GetBytes(text));
static string Str(JsonElement e,string name)=>e.GetProperty(name).GetString()!;
const string modelHash="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
const string kernelHash="98bda15926941db1f1912f34d4ebc764a467a3e18245a2f0b43755d7c562c8be";
Check(args is ["--bonsai-ctg-reason-budget-probe",_,_,_,_],"Expected MODE MODEL ROM CASES OUTPUT");
using var totalTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
Check(SHA(File.ReadAllBytes(typeof(BonsaiCtgCouncilEvaluator).Assembly.Location))==kernelHash,"Kernel mismatch");
using(var f=File.OpenRead(args[1]))Check(Convert.ToHexStringLower(SHA256.HashData(f))==modelHash,"Model mismatch");
var cases=JsonDocument.Parse(File.ReadAllBytes(args[3])).RootElement;
Check(cases.GetArrayLength()==1,"One diagnostic case");
var c=cases[0];const CouncilKind role=CouncilKind.Pathos;
const string name="council.pathos.monolith.md";byte[] bytes=File.ReadAllBytes(Path.Combine(args[2],name));
var rule=BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council.Pathos",Path="rom/governance/"+name,ContentHash=SHA(bytes)});
BonsaiCtgFindingPolicy.RequireSupported(rule,role);
Check(rule.Reference.ContentHash==Str(c,"romSha256"),"ROM mismatch");
var json=new JsonSerializerOptions{Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
var input=c.GetProperty("facts").Deserialize<Dictionary<string,string>>()!.ToImmutableSortedDictionary(StringComparer.Ordinal);
string facts=JsonSerializer.Serialize(input,json),prop=Str(c,"proposal"),proposal=JsonSerializer.Serialize(prop,json),reading=Str(c,"reading");
var reader=BonsaiCtgFocusedPrompt.Reading(facts,proposal);
Check(H(reader.System+"\n"+reader.User)==Str(c,"readingPromptSha256") && H(reading)==Str(c,"readingSha256"),"Reading mismatch");
var w=BonsaiCtgLiteralComparison.TryCreate(prop,input)??throw new InvalidDataException("Original witness unavailable");
Check(H(w.ToPromptData())==Str(c,"comparisonSha256"),"Witness mismatch");
var prompt=BonsaiCtgReasonedFindingPrompt.Build(rule,role,facts,proposal,reading,w.DescribeForCouncil());
string user=BonsaiCtgReasonedFindingPrompt.ForReason(prompt.User);
string prior=Str(c,"recordedDraft192");
Check(H(prompt.System+"\n"+user)==Str(c,"draftPromptSha256") && H(prior)==Str(c,"draftResponseSha256") &&
    c.GetProperty("draftOutputTokens").GetInt32()==192 && Str(c,"draftFinishReason")=="length","Original draft differs");
Check(prior.Length>0 && prior==prior.Trim(),"Visible previous answer expected");
const string shortInstruction="\nQuote only the two values briefly. Give your conclusion in one English sentence of at most 25 words. Do not quote the full proposal.";
using var model=new Qwen3CpuModel(args[1],4,2048,"auto");model.ConfigureTrustedPrefixCache(1,1024L<<20);
int calls=0;var rows=new List<object>();var watch=Stopwatch.StartNew();
void Save() => File.WriteAllText(args[4],JsonSerializer.Serialize(new{diagnosticOnly=true,modelHash,kernelHash,planned=1,
    id=Str(c,"id"),maximumAttempts=2,elapsedSeconds=watch.Elapsed.TotalSeconds,actualModelCalls=calls,rows},new JsonSerializerOptions(json){WriteIndented=true})+"\n");
for (int run=0; run<2; run++)
{
    if(totalTimeout.IsCancellationRequested)break;
    var reasonUser=run==0?user:user+shortInstruction;
    using var attempt=CancellationTokenSource.CreateLinkedTokenSource(totalTimeout.Token);attempt.CancelAfter(TimeSpan.FromMinutes(5));
    CpuGeneration? reason=null, answer=null;
    string? finding=null, failure=null, selectionDigest=null;bool? prefixMatch=null;
    bool fallbackEligible=false;
    try
    {
        calls++;reason=await model.GenerateAsync(prompt.System,reasonUser,256,attempt.Token);
        if(run==0)
        {
            prefixMatch=reason.OutputTokens>=192 && reason.Text.StartsWith(prior.TrimEnd(),StringComparison.Ordinal);
            if(prefixMatch!=true)failure="original-visible-prefix-mismatch";
        }
        if(failure==null)
        {
            string selection=BonsaiCtgReasonedFindingPrompt.ForSelection(prompt.User,reason);
            selectionDigest=H(prompt.System+"\n"+selection);calls++;
            answer=await model.ClassifyAsync(prompt.System,selection,BonsaiCtgReasonedFindingPrompt.Choices(role),attempt.Token);
            finding=BonsaiCtgReasonedFindingPrompt.Finding(role,answer);
        }
    }
    catch (InvalidDataException ex)
    {
        failure="invalid-output: "+ex.Message;
        fallbackEligible=true;
    }
    catch (OperationCanceledException) when(attempt.IsCancellationRequested)
    {
        failure="deadline";fallbackEligible=!totalTimeout.IsCancellationRequested;
    }
    rows.Add(new{id=Str(c,"id"),variant=run==0?"original-256":"short-256",systemSha256=H(prompt.System),reasonUser,
        reasonPromptSha256=H(prompt.System+"\n"+reasonUser),reasonResponseSha256=reason==null?null:H(reason.Text),reason,
        originalVisiblePrefixMatches=prefixMatch,visibleCharacters=reason?.Text.Length,originalLength=prior.Length,
        selectionPromptSha256=selectionDigest,selectionResponseSha256=answer==null?null:H(answer.Text),answer,finding,failure,fallbackEligible});
    Save();
    // A valid but wrong choice is a semantic result. A missing prefix invalidates comparability.
    if(!fallbackEligible || totalTimeout.IsCancellationRequested)break;
}
Save();
