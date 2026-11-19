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
        // A second account of the same subject may describe another time or device revision.
        // Keep both originals in the council packet; the host cannot pick which one governs.
        var otherAccounts = constraints.Length == 1 ? facts.Where(f => f.Key != constraints[0].Key &&
            MentionsSubject(f.Value, referent)).Select(f => f.Key).Order(StringComparer.Ordinal).ToArray() : [];
        bool uniqueSource = constraints.Length == 1 && otherAccounts.Length == 0;
        var capacity = uniqueSource ? new
        {
            sourceKey = constraints[0].Key, exactQuote = constraints[0].Quote.Value.TrimEnd(),
            maximumSimultaneousValues = 1,
            comparisonIfApplicable = distinct > 1 ? "2 distinct values cannot fit in 1 slot." : "1 distinct value fits in 1 slot. Verify the value itself."
        } : null;
        exceedsCapacity = capacity is not null && distinct > 1;
        var claims = new
        {
            subject, firstClaim = first, secondClaim = second, time = match.Groups["time"].Value,
            distinctValuesInProposal = distinct, observedCapacityConstraint = capacity
        };
        if (otherAccounts.Length == 0) return JsonSerializer.Serialize(claims);
        var accounts = otherAccounts.Prepend(constraints[0].Key).ToArray();
        // A request can specify a revision, but cannot establish its measurements.
        // Annotate only an unambiguous pair of explicitly dated source descriptions.
        // Identifiers and source keys never determine which revision is current.
        if (accounts.Length == 2 && facts.TryGetValue("authority",out var requestScope) &&
            Regex.IsMatch(requestScope, @"\AThe user requested the current\b[^.!?]*\bfrom the replacement\b[^.!?]*\.\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
            var records = accounts.Select(key => (Key: key, Text: facts[key])).ToArray();
            bool Unqualified(string text) => !Has(text, @"\b(not|no|never|except|unless|if|possibly|perhaps|unverified|purported|alleged)\b|[?""“”]");
            var current = records.Where(r => Unqualified(r.Text) && Has(r.Text, @"(?:^|\.\s+)The current replacement\b") &&
                !Has(r.Text, @"\b(retired|archived)\b")).ToArray();
            var historical = records.Where(r => Unqualified(r.Text) &&
                Has(r.Text, @"(?:^|\.\s+)(?:The retired\b|This is an archived description of the retired\b)") &&
                !Has(r.Text, @"\b(current|replacement)\b")).ToArray();
            // Keep the existing capacity question when the capacity source itself
            // describes the requested revision. Only a superseded constraint
            // needs a different reading; it cannot be used to grade its successor.
            if (current.Length == 1 && historical.Length == 1 && historical[0].Key == constraints[0].Key)
                return JsonSerializer.Serialize(new { claims, accountsForTheSameSubject = accounts,
                    requestedRevision = "current replacement", currentObservation = new { sourceKey = current[0].Key, exactQuote = current[0].Text },
                    historicalObservation = new { sourceKey = historical[0].Key, exactQuote = historical[0].Text } });
        }
        return JsonSerializer.Serialize(new
        {
            claims,
            accountsForTheSameSubject = otherAccounts.Prepend(constraints[0].Key),
            scopeQuestion = "Which record describes the unit and time in the request? A specification for a retired unit does not establish the current unit's capacity."
        });
    }

    private static bool SameSubject(string source, string proposal)
    {
        // A sentence-initial noun may start with a capital; preserve the case of identifiers.
        return source.Equals(proposal, StringComparison.Ordinal) ||
            (source.Length == proposal.Length && char.ToLowerInvariant(source[0]) == char.ToLowerInvariant(proposal[0]) &&
             source.AsSpan(1).SequenceEqual(proposal.AsSpan(1)));
    }

    private static bool MentionsSubject(string record, string subject)
    {
        for (int from = 0; from < record.Length;)
        {
            int start = record.IndexOf(subject, from, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            int end = start + subject.Length;
            if ((start == 0 || !IsNameCharacter(record[start-1])) && (end == record.Length || !IsNameCharacter(record[end])) &&
                SameSubject(record[start..end], subject)) return true;
            from = start + 1;
        }
        return false;
    }

    private static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

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
        using var sources = JsonDocument.Parse(packet);
        if (sources.RootElement.TryGetProperty("currentObservation", out var observed))
            user = "All source records (data):\n" + facts + "\nReport to evaluate (data):\n" + proposal +
                "\nRequested device: the current replacement. Record describing that device (verbatim data):\n" +
                observed.GetProperty("exactQuote").GetRawText() +
                "\nRead the simultaneous readings in that record. Does reporting the proposed readings satisfy your council's criterion for this device?";
        return (canonical.System, user, canonical.Reference, canonical.Excerpt);
    }
}
