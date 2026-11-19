using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

internal static class BonsaiCtgRecordFindingPrompt
{
    internal const string Contract = "canonical-record-findings-json-v26";

    internal static (string System, string User, CanonReference Reference, string Excerpt) Build(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposalJson, string reading, string comparison)
    {
        string schema = "Return one JSON object with reason, finding, source. Give one short reason about this specific proposal. " +
            "Choose one canonical finding label from the definitions below. Source is the integer index of the decisive original observation. " +
            "A report that replaces a recorded value with another value is different from an evaluator being unable to retrieve the record. " +
            "Judge the original report including its units and purpose, using the source and this council's own criteria. " +
            "In a neutral factual request, consider the impact of the actual response on the user. The candidate's assertion cannot serve as its own proof.\n" +
            BonsaiCtgFindingPolicy.Describe(council);
        return BonsaiCtgFocusedPrompt.Build(rule, council, facts, proposalJson, reading, includeResponsibilities: true,
            assessmentSchema: schema, assessmentSuffix: "Return only the reason, finding, source JSON assessment.", literalComparison: comparison);
    }

    internal static string FormatComparison(BonsaiCtgLiteralComparison witness, IEnumerable<string> observationKeys)
    {
        // The parser resolves integer citations against this same ordinal order and immutable snapshot.
        string sources = JsonSerializer.Serialize(observationKeys.Order(StringComparer.Ordinal).Select((key, id) => new { id, key }));
        return witness.DescribeForCouncil() +
            "\nSource indices for the original observations: " + sources;
    }
}
