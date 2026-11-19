using System.Text.Json;
using System.Text.RegularExpressions;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// Extract only the two operands of an affirmative, simultaneous report. Whether the
// subject can have multiple values, and which values were observed, belong to the councils.
internal static class BonsaiCtgJointReport
{
    private static readonly Regex Report = new(
        @"\A(?:report|state) that (?:the )?(?<subject>[a-z0-9][a-z0-9 -]{0,79}?) (?:has|holds) both (?:value )?(?<first>[+-]?[0-9]+) and (?:value )?(?<second>[+-]?[0-9]+) (?<time>at the same (?:instant|time)|simultaneously)(?:, with both claims certain)?\.?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // A capacity quote must start a source record. It remains conditional on that
    // record's scope. A different subject, a quoted claim or competing records cannot bind it.
    private static readonly Regex ScalarRecord = new(
        @"\A(?:the )?(?<subject>[a-z0-9][a-z0-9 -]{0,79}?) (?:holds|stores) (?:exactly )?(?:one|a single|1) (?:signed )?integer(?: per timestamp| at a time)?\.(?:\s|\z)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static string? Extract(string proposal, IReadOnlyDictionary<string,string> facts, out bool exceedsCapacity)
    {
        exceedsCapacity = false;
        var match = Report.Match(proposal.Trim());
        if (!match.Success) return null;
        string subject = match.Groups["subject"].Value;
        string first = match.Groups["first"].Value;
        string second = match.Groups["second"].Value;
        int distinct = NormalizeInteger(first) == NormalizeInteger(second) ? 1 : 2;
        string referent = Regex.Replace(subject, @"\A(?:same exact |same )", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var constraints = facts.Select(f => (f.Key, Quote: ScalarRecord.Match(f.Value)))
            .Where(f => f.Quote.Success && SameSubject(f.Quote.Groups["subject"].Value, referent))
            .Take(2).ToArray();
        var capacity = constraints.Length == 1 ? new
        {
            sourceKey = constraints[0].Key, exactQuote = constraints[0].Quote.Value.TrimEnd(),
            maximumSimultaneousValues = 1,
            comparisonIfApplicable = distinct > 1 ? "2 distinct values cannot fit in 1 slot." : "1 distinct value fits in 1 slot. Verify the value itself."
        } : null;
        exceedsCapacity = capacity is not null && distinct > 1;
        return JsonSerializer.Serialize(new
        {
            subject, firstClaim = first, secondClaim = second, time = match.Groups["time"].Value,
            distinctValuesInProposal = distinct, observedCapacityConstraint = capacity
        });
    }

    private static bool SameSubject(string source, string proposal)
    {
        // A sentence-initial noun may start with a capital; preserve the case of identifiers.
        return source.Equals(proposal, StringComparison.Ordinal) ||
            (source.Length == proposal.Length && char.ToLowerInvariant(source[0]) == char.ToLowerInvariant(proposal[0]) &&
             source.AsSpan(1).SequenceEqual(proposal.AsSpan(1)));
    }

    // Decimal strings need no fixed-width conversion. Retain the original spelling in the packet.
    private static string NormalizeInteger(string value)
    {
        ReadOnlySpan<char> digits = value.AsSpan(value[0] is '-' or '+' ? 1 : 0).TrimStart('0');
        return digits.IsEmpty ? "0" : value[0] == '-' ? "-" + digits.ToString() : digits.ToString();
    }

    internal static (string System, string User, CanonReference Reference, string Excerpt) Build(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposal, string packet)
    {
        var canonical = BonsaiCtgDirectVotePrompt.BuildWithResponsibilities(rule, council, facts, proposal,
            assessmentSchema: BonsaiCtgFindingPolicy.Describe(council) +
            "\nChoose Yes for supported, No for proposal_defect, Unknown for evaluation_unavailable." +
            (council == CouncilKind.Ethos ? " Neutral means safety_neutral under the canonical criteria." : ""));
        string user = "Source observations and authority (data):\n" + facts + "\nProposed report (data):\n" + proposal +
            "\nOperands from the proposed report and any single-value constraint quoted from a source:\n" + packet +
            "\nCheck the subject, time, qualifications and all source observations. A missing capacity annotation adds no fact. " +
            "Repeating one number twice asserts one value. Distinct simultaneous values need a source that can hold and verify them together. " +
            (council == CouncilKind.Logos ? "Is the entire report justified and logically possible?" :
                "Can the entire report be presented as verified under the requested truthful reporting scope?") + " Answer using your canonical finding.";
        return (canonical.System, user, canonical.Reference, canonical.Excerpt);
    }
}
