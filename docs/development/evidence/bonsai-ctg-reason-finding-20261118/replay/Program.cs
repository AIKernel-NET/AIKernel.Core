using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

static void Check(bool ok, string detail)
{
    if (!ok) throw new InvalidDataException(detail);
}
static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static string FileDigest(string path) => Digest(File.ReadAllBytes(path));
static JsonElement JsonFile(string path)
{
    using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
    return doc.RootElement.Clone();
}
static string Id(JsonElement e) => e.GetProperty("id").GetString()!;
static string Hash(string text) => Digest(Encoding.UTF8.GetBytes(text));

if (args.Length != 3) throw new ArgumentException("Replay SCRATCH GOVERNANCE OUTPUT");
string root = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[2]);
var tested = JsonFile(Path.Combine(root, "tested-build.json"));
var kernelHash = FileDigest(typeof(BonsaiCtgCouncilEvaluator).Assembly.Location);
Check(kernelHash == tested.GetProperty("kernelSha256").GetString() &&
    kernelHash == FileDigest(Path.Combine(root, "app-v28", "AIKernel.Kernel.dll")), "Mismatch in actual kernel bytes");
var roles = new[] { CouncilKind.Logos, CouncilKind.Ethos, CouncilKind.Pathos };
var rules = new Dictionary<CouncilKind, BonsaiCouncilRule>();
foreach (var role in roles)
{
    string name = $"council.{role.ToString().ToLowerInvariant()}.monolith.md";
    byte[] bytes = File.ReadAllBytes(Path.Combine(args[1], name));
    rules[role] = BonsaiCouncilRule.FromUtf8(bytes, new CanonReference {
        CanonId = "Canon.CTG.Monolith.Council." + role, Path = "rom/governance/" + name, ContentHash = Digest(bytes) });
}
var phases = new List<object>();
int requests = 0, capturedCalls = 0, checkedSelections = 0;
foreach (var phase in new[] { "pilot", "paired", "independent", "semantic", "host", "full" })
{
    string dir = Path.Combine(root, "server", "server-" + phase);
    string FileIn(string name) => Path.Combine(dir, name);
    if (!File.Exists(FileIn("completion.json"))) continue;
    var transferred = JsonFile(FileIn("transferred-files.json"));
    var caseSha = FileDigest(FileIn("cases.json"));
    Check(caseSha == transferred.GetProperty("cases.json").GetString() &&
        kernelHash == transferred.GetProperty("app/AIKernel.Kernel.dll").GetString() &&
        tested.GetProperty("validationHarnessSha256").GetString() == transferred.GetProperty("app/AIKernel.Core.Tests.dll").GetString(),
        "Transferred case or assembly not identical: " + phase);
    foreach (var (role, rule) in rules)
    {
        string source = $"governance/council.{role.ToString().ToLowerInvariant()}.monolith.md";
        Check(transferred.GetProperty(source).GetString() == rule.Reference.ContentHash, "Transferred governance mismatch " + role);
    }
    string inputName = phase switch { "pilot"=>"pilot-cases", "paired"=>"paired-cases", "independent"=>"independent-cases",
        "semantic"=>"purpose-cases", "host"=>"host-guard-cases", "full"=>"guarded-cases", _=>throw new InvalidDataException() };
    Check(caseSha == FileDigest(Path.Combine(root, "server", inputName + ".json")), "Frozen input mismatch: " + phase);
    var cases = JsonFile(FileIn("cases.json")).EnumerateArray().ToDictionary(Id);
    var actualRows = JsonFile(FileIn("result.json")).EnumerateArray().ToArray();
    bool hasResponses = File.Exists(FileIn("result.json.responses.json"));
    var answered = hasResponses ? JsonFile(FileIn("result.json.responses.json")).EnumerateArray().ToArray() : [];
    var rows = new List<object>();
    var included = new HashSet<string>();
    foreach (var original in actualRows)
    {
        string id = Id(original);
        Check(included.Add(id), "Duplicate result " + id);
        var item = cases[id];
        // Input projection deliberately omits expectedGate, requiredVotes and all other scoring data.
        var request = new CouncilEvaluationRequest { OperationId = id, StepId = "observation",
            InputFacts = item.GetProperty("facts").EnumerateObject().ToDictionary(x=>x.Name, x=>x.Value.GetString()!) };
        var requirements = new BonsaiCtgEvidenceRequirements(item.GetProperty("hostRequirements").EnumerateObject()
            .ToDictionary(x => x.Name, x => Enum.Parse<BonsaiCtgEvidenceAvailability>(x.Value.GetString()!)));
        var recordedResult = original.GetProperty("result");
        var evaluation = recordedResult.GetProperty("Evaluation");
        var serverData = evaluation.GetProperty("Metadata").Deserialize<Dictionary<string,string>>()!;
        Check(serverData["prompt.contract"] == BonsaiCtgReasonedFindingPrompt.Contract, "Wrong response contract");
        Check(serverData["model.sha256"] == "3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3",
            "Wrong model in traces");
        var replies = answered.Where(r => Id(r) == id).ToArray();
        var evidence = new List<object>();
        var drafts = new Dictionary<string,string>();
        bool usingComparison = serverData.ContainsKey("comparison.sha256");
        int offset = 0, observed = 0;
        Task<CpuGeneration> Read(string system, string user, IReadOnlyList<string>? choices)
        {
            var row = replies[offset++];
            string role = row.GetProperty("council").GetString()!, stage = row.GetProperty("stage").GetString()!;
            var answer = row.GetProperty("answer").Deserialize<CpuGeneration>()!;
            string key = stage switch { "reading" => "reading", "draft" => role + ".draft", "final" => role, _ => throw new InvalidDataException(stage) };
            string promptHash = Hash(system + "\n" + user), answerHash = Hash(answer.Text);
            Check(promptHash == serverData[key+".prompt.sha256"] && answerHash == serverData[key+".response.sha256"],
                $"Original prompt/reply digest mismatch at {phase}/{id}/{key}");
            Check((choices is not null) == (stage == "final" && usingComparison), "Wrong generation/selection callback " + key);
            string? ownReasonSha256 = null;
            if (stage == "draft") drafts.Add(role, answer.Text);
            if (choices is not null)
            {
                const string marker = "\nTHIS COUNCIL'S PRELIMINARY EXPLANATION (generated interpretation, not evidence):\n";
                int start = user.LastIndexOf(marker, StringComparison.Ordinal);
                Check(start >= 0 && user.IndexOf(marker, StringComparison.Ordinal) == start, "Reason field count");
                start += marker.Length;
                int end = user.IndexOf('\n', start);
                string attached = JsonSerializer.Deserialize<string>(user[start..end])!;
                Check(attached == drafts[role], "Attached reason came from another role or input " + role);
                ownReasonSha256 = Hash(attached);
                Check(ownReasonSha256 == serverData[role+".draft.response.sha256"], "Reason record hash " + role);
                foreach ((var otherRole, var other) in drafts)
                {
                    if (role != otherRole && attached != other)
                        Check(!user.Contains(JsonSerializer.Serialize(other), StringComparison.Ordinal), "Another role's draft appeared");
                }
                var allowed = role == "Ethos" ? new[] { "Yes", "No", "Unknown", "Neutral" } : new[] { "Yes", "No", "Unknown" };
                Check(choices.SequenceEqual(allowed), "Choice labels/order changed " + role);
                checkedSelections++;
            }
            evidence.Add(new { role, stage, promptSha256 = promptHash, responseSha256 = answerHash, attachedReasonSha256 = ownReasonSha256 });
            capturedCalls++;
            return Task.FromResult(answer);
        }
        void Observe(string stage, string role, CpuGeneration answer)
        {
            var raw = replies[observed++];
            Check(Id(raw)==id && raw.GetProperty("stage").GetString()==stage && raw.GetProperty("council").GetString()==role &&
                raw.GetProperty("answer").GetProperty("Text").GetString()==answer.Text, $"Recorded callback mismatch {id}/{stage}/{role}");
        }
        using var evaluator = new BonsaiCtgCouncilEvaluator((s,u,t) => Read(s,u,null), rules,
            responseMode: BonsaiCtgResponseMode.ReasonedFindingVotes, select: (s,u,c,t) => Read(s,u,c));
        evaluator.ReadingObservation = ans => Observe("reading","none",ans);
        evaluator.ProvisionalObservation = (c,ans) => Observe("draft", c.ToString(), ans);
        evaluator.Observation = (c,ans) => Observe("final", c.ToString(), ans);
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(request, item.GetProperty("proposal").GetString()!, requirements);
        Check(offset==observed && offset==replies.Length && offset==original.GetProperty("inferenceCalls").GetInt32(), "Recorded calls mismatch " + id);
        // No model is loaded. The replay has no live-model prefix cache and identifies its input source as test-double.
        var reproduced = result.Evaluation.Metadata;
        var sharedKeys = serverData.Keys.Where(k=>k!="model.sha256" && !k.StartsWith("prefix_cache.", StringComparison.Ordinal)).ToArray();
        Check(sharedKeys.All(k=>reproduced.TryGetValue(k, out var value) && serverData[k]==value) &&
            reproduced.Keys.Where(k=>k!="model.sha256").All(sharedKeys.Contains), "Metadata mismatch " + id);
        var gate = recordedResult.GetProperty("Gate");
        Check(result.Gate.Accepted==gate.GetProperty("Accepted").GetBoolean() && result.Evaluation.Succeeded==evaluation.GetProperty("Succeeded").GetBoolean()
            && result.Evaluation.ErrorCode==evaluation.GetProperty("ErrorCode").GetString(), "Gate/evaluation mismatch " + id);
        var votes = new Dictionary<string,string>();
        foreach (var vote in result.Evaluation.Decision.Votes)
        {
            var real = evaluation.GetProperty("Decision").GetProperty("Votes").EnumerateArray().Single(v=>v.GetProperty("CouncilKind").GetInt32()==(int)vote.CouncilKind);
            Check(real.GetProperty("VoteValue").GetInt32()==(int)vote.VoteValue,"Vote mismatch " + id);
            var voteData = real.GetProperty("Metadata").ValueKind == JsonValueKind.Null ? new() :
                real.GetProperty("Metadata").Deserialize<Dictionary<string,string>>()!;
            Check(voteData.All(k=>vote.Metadata is not null && vote.Metadata.TryGetValue(k.Key,out var v) && v==k.Value) && voteData.Count==(vote.Metadata?.Count??0),"Vote metadata mismatch " + id);
            votes[vote.CouncilKind.ToString()] = vote.VoteValue.ToString();
        }
        rows.Add(new { id, reconstructedCalls=offset, attachedReasons=drafts.Count, verifiedSelectionCalls=replies.Count(a=>usingComparison && a.GetProperty("stage").GetString()=="final"), formalGate=result.Gate.Accepted, succeeded=result.Evaluation.Succeeded, votes, captured=evidence });
        requests++;
    }
    // The external pilot monitor can stop between cases or in the next reading. An unfinished row never has a formal decision.
    var discarded = answered.Where(a=>!included.Contains(Id(a))).Select(Id).Distinct().ToArray();
    Check(discarded.All(id=>cases.ContainsKey(id)), "Response for a non-fixture case");
    phases.Add(new { phase, casesSha256=caseSha, observationsSha256=hasResponses?FileDigest(FileIn("result.json.responses.json")):null,
        recordedResultsSha256=FileDigest(FileIn("result.json")), uncommittedPartialCaseIds=discarded, rows });
}
Check(phases.Count>0, "No completed v28 phases collected");
var summary = new { replayOnly=true, newModelCalls=0, kernelSha256=kernelHash, verifierSha256=FileDigest(typeof(Program).Assembly.Location),
    completedCases=requests, capturedStages=capturedCalls, ownReasonSelections=checkedSelections, phases };
string temp=output+".new";
File.WriteAllText(temp,JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true})+"\n");
File.Move(temp,output,true);
Console.WriteLine($"Checked {requests} completed cases, {capturedCalls} recorded calls, {checkedSelections} selection inputs; no inference.");
