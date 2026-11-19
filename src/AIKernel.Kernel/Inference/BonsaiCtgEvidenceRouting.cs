using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

// Selects how to present evidence, never a vote. The normal canonical assessment
// reads the source and proposal directly. Narrow record tasks also receive the
// record-comparison questions validated for the corresponding representation.
internal static class BonsaiCtgEvidenceRouting
{
    private static readonly Regex Count = new(@"\b(?:count|counts)\b|件数|個数",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    internal static BonsaiCtgResponseMode Select(string proposal, IReadOnlyDictionary<string,string> facts)
    {
        if(BonsaiCtgCountReport.IsCountStatement(proposal))
            return BonsaiCtgCountReport.SupportedPacket(proposal,facts) is null
                ? BonsaiCtgResponseMode.EvidenceScopedDirectVotes : BonsaiCtgResponseMode.JointReportVotes;
        // A conditional comparison in a scope note can describe two different
        // actions. Use the count question for the proposal actually made. For
        // a different action, require separate authority and effect records.
        bool scopedCount = Count.IsMatch(proposal) || facts.TryGetValue("effects",out var effects) &&
            !string.IsNullOrWhiteSpace(effects) && facts.TryGetValue("authority",out var scope) && Count.IsMatch(scope);
        if (scopedCount || BonsaiCtgEquationReport.IsResultReport(proposal) ||
            BonsaiCtgJointReport.Extract(proposal,facts,out _) is not null ||
            BonsaiCtgLiteralComparison.TryCreate(proposal,facts,includeFieldPaths:true,allowSourceCitation:true) is not null)
            return BonsaiCtgResponseMode.JointReportVotes;
        return BonsaiCtgResponseMode.EvidenceScopedDirectVotes;
    }
}
