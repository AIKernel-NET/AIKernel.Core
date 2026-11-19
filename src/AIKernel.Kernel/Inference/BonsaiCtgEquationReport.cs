using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

internal static class BonsaiCtgEquationReport
{
    private const string Integer = @"[+\-−]?[0-9]{1,18}";
    private const string Expression = "(?<left>" + Integer + @")[ \t]*(?<op>[+\-−×*])[ \t]*(?<right>" + Integer + ")";
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private static readonly Regex Proposed = new(@"\A" + Expression + @"の(?:答え|結果)として[ \t]*(?<answer>" + Integer +
        @")[ \t]*を(?:表示|報告)(?:する|してください)(?:。|\z)", Options);
    private static readonly Regex Verified = new(@"\A(?:通常の整数演算で|検算で)?" + Expression +
        @"[ \t]*=[ \t]*(?<answer>" + Integer + @")(?:と確認できる|と確認した|である)?\z", Options);

    // Only a whole affirmative result report and one independently checked source equation
    // can raise this question. The model still interprets qualifications in the full originals.
    internal static bool IsResultReport(string proposal) => Proposed.IsMatch(proposal.Trim());

    internal static string? TryComparison(string proposal, IReadOnlyDictionary<string,string> facts)
    {
        var stated = Proposed.Match(proposal.Trim());
        if (!stated.Success) return null;
        var requested = Operands(stated);
        BigInteger expected = requested.Op switch
        {
            '+' => requested.Left + requested.Right, '-' => requested.Left - requested.Right,
            '*' => requested.Left * requested.Right, _ => throw new InvalidDataException()
        };
        if (Number(stated, "answer") == expected) return null;
        string? packet = null;
        foreach (var (key, text) in facts)
        {
            // Split at the Japanese sentence boundary to include adjacent equations independently.
            foreach (string sentence in text.Split('。', StringSplitOptions.RemoveEmptyEntries))
            {
                var source = Verified.Match(sentence.Trim());
                if (!source.Success || Operands(source) != requested) continue;
                if (packet is not null || Number(source, "answer") != expected) return null;
                packet = BonsaiCtgReadableJson.Serialize(new
                {
                    sourceKey = key, sourceEquation = source.Value.Trim(), sourceResult = source.Groups["answer"].Value,
                    proposedReport = stated.Value, proposedResult = stated.Groups["answer"].Value,
                    checkedIntegerResult = expected.ToString(CultureInfo.InvariantCulture)
                });
            }
        }
        return packet;
    }

    private static BigInteger Number(Match match, string name) =>
        BigInteger.Parse(match.Groups[name].Value.Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    private static (BigInteger Left, char Op, BigInteger Right) Operands(Match match) =>
        (Number(match, "left"), match.Groups["op"].Value[0] switch { '−' => '-', '×' => '*', var c => c }, Number(match, "right"));

    internal static (string System, string User, CanonReference Reference, string Excerpt) Build(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposal, string comparison)
    {
        BonsaiCtgFindingPolicy.RequireSupported(rule, council);
        bool logos = council == CouncilKind.Logos;
        string excerpt = logos ? BonsaiCtgReportChallenge.Criteria(rule) : rule.Text.Split('\n').Select(x => x.TrimEnd('\r'))
            .Single(x => x.StartsWith("Ethos must reject decisions that rely on opaque, unverifiable,", StringComparison.Ordinal));
        string system = excerpt +
            "\nCheck a proposed factual report against the source. Error means that the action would display a different number as the verified answer to that very expression. " +
            "Other means the source does not apply, the result is correctly reported, or the task clearly labels the difference as a hypothetical or a quotation. Return one word.";
        string user = "Observations, including the requested scope:\n" + facts + "\nProposed action:\n" + proposal +
            "\nIndependent integer calculation and two original excerpts:\n" + comparison +
            "\nWhich number would actually be displayed? Treat the requested display value as a claim, not evidence. " +
            (logos ? "Does that answer contradict the verified result?" : "Would that display misrepresent what was verified?") + " Error or Other?";
        return (system, user, rule.Reference with { Section = logos ? "3. Evaluation Criteria" : "2.5 Transparency" }, excerpt);
    }
}
