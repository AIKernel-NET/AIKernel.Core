using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
static string Hash(byte[] input)=>Convert.ToHexStringLower(SHA256.HashData(input));
static string Value(JsonElement e,string name)=>e.GetProperty(name).GetString()!;
if(args is not ["--bonsai-ctg-balanced-probe",var modelPath,var romPath,var casesPath,var resultPath])
    throw new ArgumentException("Expected --bonsai-ctg-balanced-probe MODEL ROM INPUT RESULT");
const string modelHash="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
using(var stream=File.OpenRead(modelPath))
    Check(Convert.ToHexStringLower(SHA256.HashData(stream))==modelHash,"Model bytes differ");
var romHashes=new Dictionary<string,string>(StringComparer.Ordinal);
foreach(var role in new[]{CouncilKind.Logos,CouncilKind.Ethos})
{
    string name="council."+role.ToString().ToLowerInvariant()+".monolith.md";var bytes=File.ReadAllBytes(Path.Combine(romPath,name));
    string hash=Hash(bytes);
    var rom=BonsaiCouncilRule.FromUtf8(bytes,new(){CanonId="Canon.CTG.Monolith.Council."+role,Path="rom/governance/"+name,ContentHash=hash});
    BonsaiCtgFindingPolicy.RequireSupported(rom,role);romHashes.Add(role.ToString(),hash);
}
using var plan=JsonDocument.Parse(File.ReadAllBytes(casesPath));
var cases=plan.RootElement.EnumerateArray().ToArray();
Check(cases.Length==20 && cases.Select(c=>Value(c,"id")).Distinct().Count()==20,"Expected 20 unique runs");
using var model=new Qwen3CpuModel(modelPath,4,2048,"auto");
const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
T Field<T>(string name)=>(T)typeof(Qwen3CpuModel).GetField(name,flags)!.GetValue(model)!;
MethodInfo Method(string name)=>typeof(Qwen3CpuModel).GetMethod(name,flags)!;
var tokenizer=Field<QwenTokenizer>("tokenizer");
var output=Field<GgufWeights.Tensor>("output");
var acquire=Method("AcquireWorkspace"); var forward=Method("Forward");var multiply=Method("Multiply");
string prefill=Field<string>("prefillBackend"),decode=Field<string>("decodeBackend");
int prefillThreads=Field<int>("prefillThreads"),decodeThreads=Field<int>("decodeThreads");
var rows=new List<object>();
foreach(var cell in cases)
{
    var id=Value(cell,"id");var role=Value(cell,"role");
    string system=Value(cell,"system"),user=Value(cell,"user");
    var promptSha256=Hash(Encoding.UTF8.GetBytes(system+"\n"+user));
    Check(cell.GetProperty("diagnosticOnly").GetBoolean() && promptSha256==Value(cell,"promptSha256") && Value(cell,"romSha256")==romHashes[role],"Changed input or ROM");
    var labels=cell.GetProperty("labels").Deserialize<string[]>()!;
    Check(role=="Logos"?labels.SequenceEqual(new[]{"A","B","C"}):labels.SequenceEqual(new[]{"A","B","C","D"}),"Wrong label set");
    var tok=labels.Select(x=>tokenizer.Encode(x).Single()).ToArray();Check(tok.Distinct().Count()==tok.Length,"Duplicate token ID");
    var prompt=tokenizer.Chat(system,user);Check(prompt.Count+1<=2048,"Prompt exceeds model context");
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
    using var state=(Qwen3Workspace)acquire.Invoke(model,[timeout.Token])!;
    state.Options.MaxDegreeOfParallelism=prefillThreads;
    for(int t=0;t<prompt.Count;t++){timeout.Token.ThrowIfCancellationRequested();forward.Invoke(model,[prompt[t],t,state,prefill,null]);}
    state.Options.MaxDegreeOfParallelism=decodeThreads;
    float[] scores=new float[tok.Length];
    var a=new object?[multiply.GetParameters().Length];a[1]=state.Normal;a[2]=state.Logits;a[3]=state.Options;a[4]=decode;
    for(int i=0;i<tok.Length;i++)
    {
        a[0]=output with {Offset=checked(output.Offset+(long)tok[i]*output.RowBytes),Rows=1};
        multiply.Invoke(model,a);scores[i]=state.Logits[0];
    }
    bool finite=scores.All(float.IsFinite);
    var raw=labels.Select((s,i)=>new{symbol=s,tokenId=tok[i],score=finite?(double?)scores[i]:null,bits=BitConverter.SingleToInt32Bits(scores[i])}).ToArray();
    var winners=finite?labels.Where((s,i)=>scores[i]==scores.Max()).ToArray():Array.Empty<string>();
    rows.Add(new{id,role,promptSha256,romSha256=romHashes[role],prefill,decode,prefillThreads,decodeThreads,historyTokens=prompt.Count,allFinite=finite,raw,localMaxima=winners});
    File.WriteAllText(resultPath,JsonSerializer.Serialize(new{diagnosticOnly=true,planned=20,modelHash,rows},new JsonSerializerOptions{WriteIndented=true}));
}

