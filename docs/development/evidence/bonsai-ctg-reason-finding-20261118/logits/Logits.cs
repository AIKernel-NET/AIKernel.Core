using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
static void Check(bool ok,string message) { if(!ok) throw new InvalidDataException(message); }
static string Text(JsonElement obj,string key) => obj.GetProperty(key).GetString()!;
if(args is not ["--bonsai-ctg-logit-probe",var modelPath,var governancePath,var casesPath,var destination])
    throw new ArgumentException("Expected --bonsai-ctg-logit-probe MODEL ROM INPUT OUTPUT");
const string modelHash="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
using(var modelStream=File.OpenRead(modelPath))
    Check(Convert.ToHexStringLower(SHA256.HashData(modelStream))==modelHash,"Model bytes differ");
var kind=CouncilKind.Logos;
const string romName="council.logos.monolith.md";
byte[] bytes=File.ReadAllBytes(Path.Combine(governancePath,romName));
var rom=BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council.Logos",Path="rom/governance/"+romName,ContentHash=Hash(bytes)});
BonsaiCtgFindingPolicy.RequireSupported(rom,kind);

using var fixtures=JsonDocument.Parse(File.ReadAllBytes(casesPath));
var cases=fixtures.RootElement.EnumerateArray().ToArray();
Check(cases.Length==3 && cases.Select(c=>Text(c,"id")).Distinct(StringComparer.Ordinal).Count()==3,"Expected three distinct cells");
using var model=new Qwen3CpuModel(modelPath,4,2048,"auto");
model.ConfigureTrustedPrefixCache(1,1024L<<20);
const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
T Field<T>(string field) => (T)typeof(Qwen3CpuModel).GetField(field,flags)!.GetValue(model)!;
MethodInfo Hidden(string method)=>typeof(Qwen3CpuModel).GetMethod(method,flags)!;
var tokenizer=Field<QwenTokenizer>("tokenizer");
var output=Field<GgufWeights.Tensor>("output");
var forward=Hidden("Forward");var multiply=Hidden("Multiply");var acquire=Hidden("AcquireWorkspace");
var rows=new List<object>();
foreach(var cell in cases)
{
    var id=Text(cell,"id");var system=Text(cell,"system");var user=Text(cell,"user");
    string promptSha256=Hash(Encoding.UTF8.GetBytes(system+"\n"+user));
    Check(cell.GetProperty("diagnosticOnly").GetBoolean() && Text(cell,"promptSha256")==promptSha256 && Text(cell,"romSha256")==rom.Reference.ContentHash,"Wrong prompt/ROM");
    string[] labels=cell.GetProperty("labels").Deserialize<string[]>()!;
    Check(labels.SequenceEqual(new[]{"A","B","C"}),"Label set differs");
    var tokenIds=labels.Select(label=>tokenizer.Encode(label).Single()).ToArray();
    Check(tokenIds.Distinct().Count()==labels.Length,"Duplicate label token IDs");
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));var ct=timeout.Token;
    var answer=await model.ClassifyAsync(system,user,labels,ct);
    Check(answer.FinishReason=="classified" && answer.OutputTokens==1 && labels.Contains(answer.Text),"Public classifier failed");
    string prefill=Field<string>("prefillBackend"),decode=Field<string>("decodeBackend");
    int prefillThreads=Field<int>("prefillThreads"),decodeThreads=Field<int>("decodeThreads");
    var tokens=tokenizer.Chat(system,user);
    Check(answer.InputTokens==tokens.Count,"History length mismatch");
    using var state=(Qwen3Workspace)acquire.Invoke(model,[ct])!;
    state.Options.MaxDegreeOfParallelism=prefillThreads;
    for(int i=0;i<tokens.Count;i++){ct.ThrowIfCancellationRequested();forward.Invoke(model,[tokens[i],i,state,prefill,null]);}
    state.Options.MaxDegreeOfParallelism=decodeThreads;
    void Project(GgufWeights.Tensor tensor)
    {
        var a=new object?[multiply.GetParameters().Length];
        a[0]=tensor;a[1]=state.Normal;a[2]=state.Logits;a[3]=state.Options;a[4]=decode;
        multiply.Invoke(model,a);
    }
    Project(output);
    int vocabulary=state.Logits.Length;
    int nonfinite=state.Logits.Count(v=>!float.IsFinite(v));Check(nonfinite==0,"Nonfinite full logits");
    float[] full=tokenIds.Select(token=>state.Logits[token]).ToArray();
    int allBest=Array.IndexOf(state.Logits,state.Logits.Max());
    float allBestScore=state.Logits[allBest];
    var fullRank=full.Select(s=>1+state.Logits.Count(v=>v>s)).ToArray();
    string logitsSha256=Hash(MemoryMarshal.AsBytes(state.Logits.AsSpan()).ToArray());
    var comparison=new List<object>();float[] singles=new float[labels.Length];
    for(int i=0;i<labels.Length;i++)
    {
        Project(output with {Offset=checked(output.Offset+(long)tokenIds[i]*output.RowBytes),Rows=1});
        singles[i]=state.Logits[0];Check(float.IsFinite(singles[i]),"Nonfinite sliced logit");
        comparison.Add(new{label=labels[i],tokenId=tokenIds[i],full=full[i],single=singles[i],fullVocabularyRank=fullRank[i],fullBits=BitConverter.SingleToInt32Bits(full[i]),singleBits=BitConverter.SingleToInt32Bits(singles[i])});
    }
    string fullPick=labels[Array.IndexOf(full,full.Max())],singlePick=labels[Array.IndexOf(singles,singles.Max())];
    int fullTieCount=full.Count(v=>v==full.Max()),singleTieCount=singles.Count(v=>v==singles.Max());
    double Margin(IEnumerable<float> values){var s=values.OrderDescending().ToArray();return (double)s[0]-s[1];}
    // Isolate a response-position intervention after the published classifier's first answer step.
    const string answerStart="My answer is:\n";
    var additional=tokenizer.Encode(answerStart);
    Check(additional.Count>0 && tokens.Count+additional.Count<2048,"Prefix exceeds context");
    state.Options.MaxDegreeOfParallelism=decodeThreads;
    for(int j=0;j<additional.Count;j++){ct.ThrowIfCancellationRequested();forward.Invoke(model,[additional[j],tokens.Count+j,state,decode,null]);}
    Project(output);
    int prefixNonfinite=state.Logits.Count(v=>!float.IsFinite(v));Check(prefixNonfinite==0,"Nonfinite full logits after prefix");
    var prefixScores=tokenIds.Select(token=>state.Logits[token]).ToArray();
    var prefixRanks=prefixScores.Select(s=>1+state.Logits.Count(v=>v>s)).ToArray();
    var prefixHash=Hash(MemoryMarshal.AsBytes(state.Logits.AsSpan()).ToArray());
    int prefixBest=Array.IndexOf(state.Logits,state.Logits.Max());float prefixBestScore=state.Logits[prefixBest];
    var isolated=new float[labels.Length];
    for(int j=0;j<labels.Length;j++)
    {
        Project(output with {Offset=checked(output.Offset+(long)tokenIds[j]*output.RowBytes),Rows=1});
        isolated[j]=state.Logits[0];Check(float.IsFinite(isolated[j]),"Nonfinite sliced prefix logit");
    }
    string prefixPick=labels[Array.IndexOf(prefixScores,prefixScores.Max())];
    var afterPrefix=new{literal=answerStart,tokenIds=additional,historyTokens=tokens.Count+additional.Count,
        decode,prefixPick,singlePick=labels[Array.IndexOf(isolated,isolated.Max())],margin=Margin(prefixScores),
        tieCount=prefixScores.Count(v=>v==prefixScores.Max()),singleTieCount=isolated.Count(v=>v==isolated.Max()),singleMargin=Margin(isolated),
        logitsSha256=prefixHash,fullVocabArgmax=prefixBest,fullVocabMax=prefixBestScore,fullNonfinite=prefixNonfinite,singleNonfinite=0,
        allLabelScoresBitwiseIdentical=prefixScores.Select(BitConverter.SingleToInt32Bits).SequenceEqual(isolated.Select(BitConverter.SingleToInt32Bits)),
        byLabel=labels.Select((s,j)=>new{label=s,tokenId=tokenIds[j],full=prefixScores[j],single=isolated[j],fullBits=BitConverter.SingleToInt32Bits(prefixScores[j]),singleBits=BitConverter.SingleToInt32Bits(isolated[j]),fullVocabularyRank=prefixRanks[j]}).ToArray()};
    rows.Add(new{id,promptSha256,afterPrefix,responseSha256=Hash(Encoding.UTF8.GetBytes(answer.Text)),answer,romSha256=rom.Reference.ContentHash,prefill,decode,prefillThreads,decodeThreads,historyTokens=tokens.Count,vocabulary,fullNonfinite=nonfinite,singleNonfinite=0,fullVocabArgmax=allBest,fullVocabMax=allBestScore,logitsSha256,labels=comparison,fullPick,singlePick,publicPick=answer.Text,fullTieCount,singleTieCount,fullMargin=Margin(full),singleMargin=Margin(singles),publicMatchesFull=answer.Text==fullPick,publicMatchesSingle=answer.Text==singlePick,allLabelScoresBitwiseIdentical=full.Select(BitConverter.SingleToInt32Bits).SequenceEqual(singles.Select(BitConverter.SingleToInt32Bits))});
    File.WriteAllText(destination,JsonSerializer.Serialize(new{diagnosticOnly=true,planned=cases.Length,modelHash,rows},new JsonSerializerOptions{WriteIndented=true}));
}

