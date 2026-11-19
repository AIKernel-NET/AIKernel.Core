using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

if (args is not ["--bonsai-ctg-report-criterion-probe",var modelPath,var governance,var casesPath,var resultPath])
    throw new ArgumentException("Expected mode MODEL ROM CASES OUTPUT");
static void Check(bool ok,string detail) { if (!ok) throw new InvalidDataException(detail); }
static string FileHash(string path) { using var s=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(s)); }
static string Digest(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
static string Str(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
const string modelHash="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3";
const string kernelHash="25e8a79ffc56fae14fb38a6572319926055a4b567cbdfd0bb141af9d0fd84cb0";
Check(FileHash(modelPath)==modelHash && FileHash(typeof(Qwen3CpuModel).Assembly.Location)==kernelHash,"Unexpected model or kernel");
string romFile=Path.Combine(governance,"council.logos.monolith.md"),romHash=FileHash(romFile);
var rom=BonsaiCouncilRule.FromUtf8(File.ReadAllBytes(romFile),new(){CanonId="Canon.CTG.Monolith.Council.Logos",Path="rom/governance/council.logos.monolith.md",ContentHash=romHash});
BonsaiCtgFindingPolicy.RequireSupported(rom,CouncilKind.Logos);
var lines=File.ReadAllLines(romFile).Where(t=>t.StartsWith("- **Justification**") || t.StartsWith("- **Non-contradiction**")).ToArray();
Check(lines.Length==2,"Missing primary criteria");
using var doc=JsonDocument.Parse(File.ReadAllBytes(casesPath));
var cells=doc.RootElement.EnumerateArray().ToArray();
Check(cells.Length==16 && cells.Select(x=>Str(x,"id")).Distinct().Count()==16,"Expected 16 unique cells");
using var model=new Qwen3CpuModel(modelPath,4,2048,"auto");
model.ConfigureTrustedPrefixCache(1,1024L<<20);
var tokenizer=(QwenTokenizer)typeof(Qwen3CpuModel).GetField("tokenizer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(model)!;
var labels=new[]{"Good","Bad","Other"};
var tokenIds=labels.Select(l=>tokenizer.Encode(l).Single()).ToArray();
Check(tokenIds.Distinct().Count()==3,"Distinct one-token labels required");
var rows=new List<object>();
foreach (var c in cells) {
    string id=Str(c,"id"),system=Str(c,"system"),user=Str(c,"user"),promptSha256=Digest(system+"\n"+user);
    Check(c.GetProperty("diagnosticOnly").GetBoolean() && Str(c,"romSha256")==romHash && Str(c,"promptSha256")==promptSha256,"Changed case");
    Check(system.Contains(string.Join("\n",lines),StringComparison.Ordinal) && labels.SequenceEqual(c.GetProperty("labels").Deserialize<string[]>()!),"Changed question");
    using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10));
    var answer=await model.ClassifyAsync(system,user,labels,deadline.Token);
    Check(answer.FinishReason=="classified" && answer.OutputTokens==1 && labels.Contains(answer.Text),"Incomplete answer");
    rows.Add(new{id,promptSha256,romSha256=romHash,responseSha256=Digest(answer.Text),answer});
    File.WriteAllText(resultPath,JsonSerializer.Serialize(new{diagnosticOnly=true,planned=16,modelHash,kernelHash,tokenIds,rows},new JsonSerializerOptions{WriteIndented=true}));
}
