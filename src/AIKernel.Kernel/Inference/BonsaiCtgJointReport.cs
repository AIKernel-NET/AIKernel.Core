using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

// Extract only the two operands of an affirmative, simultaneous report. Whether the
// subject can have multiple values, and which values were observed, belong to the councils.
internal static class BonsaiCtgJointReport
{
    private static readonly Regex Report = new(
        @"\A(?:report|state) that (?:the )?(?<subject>[a-z0-9][a-z0-9 -]{0,79}?) (?:has|holds) both (?:value )?(?<first>[+-]?[0-9]+) and (?:value )?(?<second>[+-]?[0-9]+) (?<time>at the same (?:instant|time)|simultaneously)(?:, with both claims certain)?\.?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static string? Extract(string proposal)
    {
        var match = Report.Match(proposal.Trim());
        if (!match.Success) return null;
        return JsonSerializer.Serialize(new
        {
            subject = match.Groups["subject"].Value,
            firstClaim = match.Groups["first"].Value,
            secondClaim = match.Groups["second"].Value,
            time = match.Groups["time"].Value
        });
    }
}
