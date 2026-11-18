using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Kernel.Inference;

internal static class BonsaiCtgRecordReplay
{
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static JsonElement Read(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        return doc.RootElement.Clone();
    }

    internal static async Task Run(string modeName, string governancePath, string directory, string output)
    {
        var mode = modeName switch
        {
            "v30" => BonsaiCtgResponseMode.SourceBoundRecordVotes,
            "v31" => BonsaiCtgResponseMode.ChallengedRecordVotes,
            "v32" => BonsaiCtgResponseMode.FieldBoundReadingVotes,
            _ => throw new ArgumentException("Expected v30, v31 or v32.")
        };
        var cases = Read(Path.Combine(directory,"cases.json")).EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);
        var responses = Read(Path.Combine(directory,"result.json.responses.json")).EnumerateArray().ToLookup(x => x.GetProperty("id").GetString()!);
        var rules = BonsaiCtgSuite.LoadRules(governancePath);
        var rows = new List<object>();
        foreach (var row in Read(Path.Combine(directory,"result.json")).EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            var item = cases[id];
            var actual = row.GetProperty("result");
            var evaluation = actual.GetProperty("Evaluation");
            var meta = evaluation.GetProperty("Metadata").Deserialize<Dictionary<string,string>>()!;
            if (meta["prompt.contract"] != BonsaiCtgCouncilEvaluator.ReadingContract(mode) ||
                meta["model.sha256"] != "3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3")
                throw new InvalidDataException("Unexpected source model or contract.");
            if(evaluation.GetProperty("ErrorCode").GetString() == "ctg.bonsai.timeout")
            {
                rows.Add(new { id, outcome = "original-timeout", calls = 0 });
                continue;
            }
            var saved = new Dictionary<(string, string),CpuGeneration>();
            foreach (var r in responses[id])
            {
                string role = r.GetProperty("council").GetString()!;
                string key = r.GetProperty("stage").GetString() switch
                {
                    "reading" => "reading", "draft" => role + ".draft", "final" => role,
                    "criterion" => r.GetProperty("criterion").GetString() switch
                    {
                        "truncated_reason" => role + ".draft.initial", "before_challenge" => role + ".before_challenge",
                        "challenge_deferred" => role + ".challenge", _ => throw new InvalidDataException()
                    },
                    _ => throw new InvalidDataException()
                };
                var a = r.GetProperty("answer").Deserialize<CpuGeneration>()!;
                string kind = a.FinishReason == "classified" ? "selection" :
                    key == role + ".draft" && meta.ContainsKey(role + ".draft.initial.response.sha256") ? "retry" : "generation";
                if (Hash(a.Text) != meta[key + ".response.sha256"]) throw new InvalidDataException("Changed response in " + id);
                saved.Add((meta[key + ".prompt.sha256"],kind),a);
            }
            int count = saved.Count;
            Task<CpuGeneration> Reply(string system, string user, string kind, IReadOnlyList<string>? choices = null)
            {
                if (!saved.Remove((Hash(system + "\n" + user), kind), out var a) || choices != null && !choices.Contains(a.Text))
                    throw new KeyNotFoundException("No matching original model call in " + id);
                return Task.FromResult(a);
            }
            using var evaluator = new BonsaiCtgCouncilEvaluator((s,u,t) => Reply(s,u,"generation"),rules,responseMode:mode,
                select:(s,u,c,t) => Reply(s,u,"selection",c), retryReason:(s,u,t) => Reply(s,u,"retry"));
            var request = new CouncilEvaluationRequest { OperationId=id, StepId="observation",
                InputFacts=item.GetProperty("facts").Deserialize<Dictionary<string,string>>()! };
            var requirements = new BonsaiCtgEvidenceRequirements(item.GetProperty("hostRequirements").EnumerateObject()
                .ToDictionary(x => x.Name, x => Enum.Parse<BonsaiCtgEvidenceAvailability>(x.Value.GetString()!)));
            var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(request,item.GetProperty("proposal").GetString()!,requirements);
            var votes = evaluation.GetProperty("Decision").GetProperty("Votes").EnumerateArray()
                .ToDictionary(v => v.GetProperty("CouncilKind").GetInt32(),v => v.GetProperty("VoteValue").GetInt32());
            if (saved.Count != 0 || count != row.GetProperty("inferenceCalls").GetInt32() ||
                result.Evaluation.Succeeded != evaluation.GetProperty("Succeeded").GetBoolean() ||
                result.Evaluation.ErrorCode != evaluation.GetProperty("ErrorCode").GetString() ||
                result.Gate.Accepted != actual.GetProperty("Gate").GetProperty("Accepted").GetBoolean() ||
                !result.Evaluation.Decision.Votes.All(v => votes[(int)v.CouncilKind] == (int)v.VoteValue))
                throw new InvalidDataException("Recorded decisions did not replay for " + id);
            rows.Add(new { id, outcome="matched", calls=count });
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new { replayOnly=true, newModelCalls=0, rows },new JsonSerializerOptions{WriteIndented=true}));
    }
}
