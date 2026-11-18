using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

internal static class BonsaiCtgReportChallengeProbe
{
    // Diagnostic only. No gate or council votes are inferred from this probe.
    internal static async Task Run(string modelPath, string governancePath, string casesPath, string output, bool requireBoundReport = false)
    {
        using (var file = File.OpenRead(modelPath))
            if (Convert.ToHexStringLower(SHA256.HashData(file)) != "3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3")
                throw new InvalidDataException("Unexpected model.");
        var rule = BonsaiCtgSuite.LoadRules(governancePath)[CouncilKind.Logos];
        using var model = new Qwen3CpuModel(modelPath, 4, 2048, "auto");
        model.ConfigureTrustedPrefixCache(1, 128L << 20);
        var rows = new List<object>();
        using var cases = JsonDocument.Parse(File.ReadAllBytes(casesPath));
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            string id = item.GetProperty("id").GetString()!;
            string proposal = item.GetProperty("proposal").GetString()!;
            var facts = item.GetProperty("facts").Deserialize<Dictionary<string,string>>()!.ToImmutableSortedDictionary(StringComparer.Ordinal);
            var witness = BonsaiCtgLiteralComparison.TryCreate(proposal, facts, includeFieldPaths: true, allowSourceCitation: requireBoundReport);
            var edits = witness?.HasMaterialValueDifference == true && (!requireBoundReport || witness.IsBoundExactReport(proposal, facts))
                ? witness.DescribeEdits() : null;
            string? promptSha256 = null;
            CpuGeneration? answer = null;
            if (edits is { Length: <= 1024 })
            {
                var check = BonsaiCtgReportChallenge.Build(rule, BonsaiCtgReadableJson.Serialize(facts),
                    BonsaiCtgReadableJson.Serialize(proposal), edits);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                promptSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(check.System + "\n" + check.User)));
                answer = await model.ClassifyAsync(check.System, check.User, BonsaiCtgReportChallenge.Choices, timeout.Token);
                if (answer.FinishReason != "classified" || answer.OutputTokens != 1 || !BonsaiCtgReportChallenge.Choices.Contains(answer.Text))
                    throw new InvalidDataException("Incomplete model classification.");
            }
            rows.Add(new { id, promptSha256, answer });
            File.WriteAllText(output, JsonSerializer.Serialize(new { diagnosticOnly = true, rows }));
        }
    }
}
