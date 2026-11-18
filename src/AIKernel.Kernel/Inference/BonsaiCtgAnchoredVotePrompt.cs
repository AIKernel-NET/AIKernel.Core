using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

internal static class BonsaiCtgAnchoredVotePrompt
{
    internal const string Contract = "canonical-anchored-vote-json-v23";
    private const string Schema = "Return one JSON object with exactly three fields, in order: evidence, vote, reason. " +
        "For evidence, copy one complete sentence verbatim from a supporting observation about the proposed action, keeping its conditions and negation. " +
        "You may copy a whole observation value. If the necessary evidence is absent, use JSON null. " +
        "A quote alone does not establish that the evidence applies to this action. Compare the action and its scope with the observations. " +
        "For vote, choose Approve, Reject, or Abstain under the canonical rules. Approve requires supporting evidence. Ethos must Reject if necessary evidence is absent. " +
        "For reason, explain the decisive criterion in one short sentence. No Markdown or other text.";

    internal static (string System, string User, CanonReference Reference, string Excerpt) Build(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposalJson, string reading)
        => BonsaiCtgFocusedPrompt.Build(rule, council, facts, proposalJson, reading, includeResponsibilities: true,
            assessmentSchema: Schema, assessmentSuffix: "Return only the evidence, vote, reason JSON assessment in that order.");

    internal static CouncilVoteValue Parse(CpuGeneration answer, IReadOnlyDictionary<string, string> facts, CouncilKind council)
    {
        if (answer.FinishReason != "stop" || answer.Text.Length > 8192)
            throw new InvalidDataException("Incomplete or oversized anchored assessment.");
        try
        {
            using var doc = JsonDocument.Parse(answer.Text, new() { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Expected an anchored assessment object.");
            var fields = doc.RootElement.EnumerateObject();
            var evidence = Next(ref fields, "evidence");
            var label = Next(ref fields, "vote");
            var reason = Next(ref fields, "reason");
            if (fields.MoveNext() || label.ValueKind != JsonValueKind.String || reason.ValueKind != JsonValueKind.String || !ValidText(reason.GetString()))
                throw new InvalidDataException("Invalid anchored assessment fields.");
            var vote = label.GetString() switch
            {
                "Approve" => CouncilVoteValue.Approve,
                "Abstain" => CouncilVoteValue.Abstain,
                "Reject" => CouncilVoteValue.Reject,
                _ => throw new InvalidDataException("Invalid vote.")
            };
            if (evidence.ValueKind == JsonValueKind.Null)
            {
                if (vote == CouncilVoteValue.Approve || council == CouncilKind.Ethos && vote != CouncilVoteValue.Reject)
                    throw new InvalidDataException("Vote is incompatible with an absent prerequisite.");
                return vote;
            }
            if (evidence.ValueKind != JsonValueKind.String || !ValidText(evidence.GetString()))
                throw new InvalidDataException("Expected a bounded evidence sentence.");
            string quote = evidence.GetString()!;
            if (!facts.Values.Any(observation => ContainsEvidenceUnit(observation, quote)))
                throw new InvalidDataException("Evidence is not a complete observation or sentence.");
            return vote;
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed anchored assessment.", error); }
        catch (InvalidOperationException error) { throw new InvalidDataException("Invalid assessment string encoding.", error); }
    }

    private static JsonElement Next(ref JsonElement.ObjectEnumerator fields, string name)
    {
        if (!fields.MoveNext() || !fields.Current.NameEquals(name))
            throw new InvalidDataException("Anchored assessment fields are missing or out of order.");
        return fields.Current.Value;
    }

    private static bool ValidText(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 2048 &&
        !text.Contains("<|", StringComparison.Ordinal) && !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t') ||
            char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format);

    // Only literal source membership is established here. Never infer applicability or consent from a match.
    private static bool ContainsEvidenceUnit(string observation, string quote)
    {
        if (observation == quote) return true;
        // Do not fragment quotes, brackets, multiline records, or ellipses. The full value remains available.
        if (observation.AsSpan().IndexOfAny("\"'()[]{}「」『』“”‘’\r\n…") >= 0 || observation.Contains("..", StringComparison.Ordinal)) return false;
        int start = 0;
        bool matched = false;
        for (int i = 0; i < observation.Length; i++)
        {
            char ch = observation[i];
            if (ch is not ('.' or '!' or '?' or '。' or '！' or '？')) continue;
            if (i + 1 < observation.Length && ch is '.' or '!' or '?' && !char.IsWhiteSpace(observation[i + 1])) continue;
            if (ch == '.' && i + 1 < observation.Length && IsAmbiguousPeriod(observation, i)) return false;
            if (observation.AsSpan(start, i + 1 - start).Trim().SequenceEqual(quote)) matched = true;
            start = i + 1;
        }
        return matched;
    }

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    { "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "no", "fig", "inc", "ltd", "corp", "etc", "vs", "approx", "dept", "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec" };

    private static bool IsAmbiguousPeriod(string text, int index)
    {
        int start = index;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        var word = text[start..index];
        return word.Length == 1 || Abbreviations.Contains(word) || start > 0 && text[start - 1] == '.';
    }
}
