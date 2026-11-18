using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

// Eligibility for an extra exact-copy challenge, never a source of votes. Return
// false when the original inputs do not identify one supported reporting operation.
internal static class BonsaiCtgReportScope
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private static readonly Regex Report = new(
        @"^\s*(?:report|read\s+back|tell|display|transcribe|copy|quote|show)\b|(?:転記|回答|報告)(?:する|し)", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Fidelity = new(
        @"\b(?:exact(?:ly)?|verbatim|faithful(?:ly)?)\b|\bas (?:written|recorded)\b|転記|そのまま|正確に", Options | RegexOptions.IgnoreCase);
    private static readonly Regex ReportingRequest = new(
        @"\b(?:report|read|readout|transcri(?:be|ption)|copy|quote|print|display|show)\b|報告|回答|転記", Options | RegexOptions.IgnoreCase);
    private static readonly Regex RecordReference = new(
        @"\b(?:record|row|log|table|measurement|reading|sheet)\b|検査票|記録|測定票|表", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Alternative = new(
        @"\b(?:alias|translate|equivalent|conversion|convert|unless|except|hypothetical|fictional|illustrative|fixture|example|forecast|possibly|incorrect|invalid|disputed|rejected|whether|tomorrow|yesterday)\b|同義|換算|翻訳|仮定|誤り", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Time = new(
        @"\b(?:[0-2]?[0-9]:[0-5][0-9](?::[0-5][0-9])?|[0-9]{4}-[0-9]{2}-[0-9]{2}|noon|midnight)\b|\bat\s+T\b", Options);
    private static readonly Regex NamedObject = new(
        @"\b([a-z][a-z_-]+)\s+([A-Z][A-Z0-9]*(?:[-_][A-Z0-9]+)*|[0-9]+)\b", Options);

    internal static bool IsBound(string proposal, IReadOnlyDictionary<string,string> observations,
        string sourceKey, string field, bool measurement)
    {
        if (!Report.IsMatch(proposal) || Alternative.IsMatch(proposal)) return false;
        var requests = observations.Where(p => p.Key != sourceKey && ReportingRequest.IsMatch(p.Value)).Select(p => p.Value).ToArray();
        if (!requests.Any(text => Fidelity.IsMatch(text) && (RecordReference.IsMatch(text) || text.Contains(field, StringComparison.Ordinal))) ||
            requests.Any(Alternative.IsMatch)) return false;
        string record = observations[sourceKey];
        var claimedTimes = Time.Matches(proposal).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var recordedTimes = Time.Matches(record).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        if (claimedTimes.Count > 1 || !claimedTimes.SetEquals(recordedTimes)) return false;
        var mentions = NamedObject.Matches(record).Where(m => m.Groups[1].Value != "at").ToArray();
        if (mentions.GroupBy(m => m.Groups[1].Value).Any(g => g.Select(m => m.Groups[2].Value).Distinct().Skip(1).Any())) return false;
        var sourceObjects = mentions.Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        foreach (Match objectName in NamedObject.Matches(proposal))
        {
            string kind = objectName.Groups[1].Value;
            if (kind == "at" || sourceObjects.Contains(objectName.Value)) continue;
            // "row for N" can identify "fan N" only when the literal field also names
            // that type, e.g. fan.level; a shared number alone is insufficient.
            if (!field.StartsWith(kind + ".", StringComparison.Ordinal) ||
                !sourceObjects.Contains("for " + objectName.Groups[2].Value)) return false;
        }
        // The full Japanese object/part/property path is a subject anchor in its own
        // right. Bare state keys require an explicit, matching time in both statements.
        return claimedTimes.Count == 1 || measurement && field.Contains('の');
    }
}
