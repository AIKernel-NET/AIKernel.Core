using System.Text.Json;

// Validation only. Inputs contain measured next-token logits and permutations.
// No reference answer, expected gate, or model-generated prose participates in ranking.
internal static class BonsaiCtgBalancedChoice
{
    private sealed record Rotation(float[] BySymbol, int[] FindingIndices);
    private sealed record Cell(string Id, string[] Findings, Rotation[] Rotations);
    private sealed record Input(Cell[] Cells);
    private sealed record Ranking(string Id, string[] Findings, double[] CenteredMeanLogits, string? WinningFinding, bool Tied);

    internal static void Run(string inputPath, string outputPath)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, AllowDuplicateProperties = false };
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(inputPath), json);
        if (input?.Cells is not { Length: > 0 } || input.Cells.Any(c => c is null || string.IsNullOrWhiteSpace(c.Id)) ||
            input.Cells.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != input.Cells.Length)
            throw new InvalidDataException("Require uniquely identified measurement cells.");
        var rankings = input.Cells.Select(Rank).ToArray();
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { diagnosticOnly = true, rankings }, json));
    }

    private static Ranking Rank(Cell cell)
    {
        if (cell.Findings is not { Length: 3 or 4 } || cell.Rotations?.Length != cell.Findings.Length)
            throw new InvalidDataException("One complete cycle of three or four canonical findings is required.");
        string[] canonical = ["supported", "proposal_defect", "evaluation_unavailable", "safety_neutral"];
        int count = cell.Findings.Length;
        if (!cell.Findings.SequenceEqual(canonical.Take(count), StringComparer.Ordinal))
            throw new InvalidDataException("Unrecognized finding order or scope.");

        var totals = new double[count];
        var seen = new bool[count];
        foreach (var rotation in cell.Rotations)
        {
            if (rotation?.BySymbol?.Length != count || rotation.FindingIndices?.Length != count ||
                rotation.BySymbol.Any(logit => !float.IsFinite(logit)))
                throw new InvalidDataException("Incomplete or nonfinite logit row.");
            int offset = rotation.FindingIndices[0];
            if ((uint)offset >= count || seen[offset])
                throw new InvalidDataException("Missing or repeated cyclic assignment.");
            seen[offset] = true;
            double center = rotation.BySymbol.Average(value => (double)value);
            for (int symbol = 0; symbol < count; symbol++)
            {
                int finding = (offset + symbol) % count;
                if (rotation.FindingIndices[symbol] != finding)
                    throw new InvalidDataException("Finding assignment is not a cyclic shift.");
                totals[finding] += rotation.BySymbol[symbol] - center;
            }
        }

        var means = totals.Select(sum => sum / count).ToArray();
        double maximum = means.Max();
        bool tied = means.Count(mean => mean == maximum) > 1;
        return new(cell.Id, cell.Findings, means, tied ? null : cell.Findings[Array.IndexOf(means, maximum)], tied);
    }
}
