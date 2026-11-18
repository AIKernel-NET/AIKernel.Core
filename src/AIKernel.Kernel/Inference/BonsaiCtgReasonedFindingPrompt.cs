using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// Validation contract. The model makes a separate semantic choice after seeing its
// own explanation. Neither the comparison nor the explanation casts a vote.
internal static class BonsaiCtgReasonedFindingPrompt
{
    internal const string Contract = "canonical-reasoned-finding-v28";
    private static readonly ImmutableArray<string> Standard = ["Yes", "No", "Unknown"];
    private static readonly ImmutableArray<string> Ethical = ["Yes", "No", "Unknown", "Neutral"];
    private static readonly JsonSerializerOptions DataJson = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    internal static IReadOnlyList<string> Choices(CouncilKind council)
        => council == CouncilKind.Ethos ? Ethical : Standard;

    internal static (string System, string User, CanonReference Reference, string Excerpt) Build(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposalJson, string? reading, string comparison)
    {
        string schema = "Use the following reviewed findings for your council. Assess the original proposal against the source, " +
            "including what the requester authorized. A source can be available even when it contradicts the proposal.\n" +
            BonsaiCtgFindingPolicy.Describe(council) +
            "\nWhen asked whether this council's criteria are satisfied, answer Yes for supported, No for proposal_defect, " +
            "or Unknown for evaluation_unavailable." +
            (council == CouncilKind.Ethos ? " Neutral means safety_neutral in its defined scope." : "");
        return BonsaiCtgFocusedPrompt.Build(rule, council, facts, proposalJson, reading, includeResponsibilities: true,
            assessmentSchema: schema, assessmentSuffix: "", literalComparison: comparison);
    }

    internal static string ForReason(string original) => original +
        "\nExplain in one short sentence which original fact is decisive and what it establishes under this council's criteria.";

    internal static (string System, string User, CanonReference Reference, string Excerpt) BuildFromRecord(
        BonsaiCouncilRule rule, CouncilKind council, string facts, string proposalJson, string edits)
    {
        var canonical = BonsaiCtgDirectVotePrompt.BuildWithResponsibilities(rule, council, facts, proposalJson,
            assessmentSchema: BonsaiCtgFindingPolicy.Describe(council) +
                "\nSelect Yes for supported, No for proposal_defect, Unknown for evaluation_unavailable." +
                (council == CouncilKind.Ethos ? " Neutral means safety_neutral." : ""));
        string user = "Original observations, including the request and its scope:\n" + facts +
            "\nProposed action to evaluate:\n" + proposalJson +
            "\nRecord-to-proposal edit (quoted data from the two original inputs):\n" + edits +
            "\nRead the edit from the recorded field to the proposed output. An exact readout preserves the recorded value and unit. " +
            "An authorized conversion may change the writing while preserving the quantity. A requested change of state is a separate action. " +
            "Compare the subject, time and requested operation in the originals. Would carrying out this proposal satisfy your council's criteria?";
        return (canonical.System, user, canonical.Reference, canonical.Excerpt);
    }

    internal static string ForSelection(string original, CpuGeneration reason)
    {
        if (reason.FinishReason != "stop" || string.IsNullOrWhiteSpace(reason.Text) || reason.Text.Length > 1024)
            throw new InvalidDataException("Incomplete or oversized council explanation.");
        // Serialize the draft as untrusted data. Never put request or generated text in the cached system prefix.
        return original + "\nTHIS COUNCIL'S PRELIMINARY EXPLANATION (generated interpretation, not evidence):\n" +
            JsonSerializer.Serialize(reason.Text, DataJson) +
            "\nCheck this explanation against the original proposal and observations. Are this council's criteria satisfied? Answer with one word.";
    }

    internal static string Finding(CouncilKind council, CpuGeneration answer)
    {
        if (answer.FinishReason != "classified" || answer.OutputTokens != 1)
            throw new InvalidDataException("Expected a single classified token.");
        return answer.Text switch
        {
            "Yes" => "supported",
            "No" => "proposal_defect",
            "Unknown" => "evaluation_unavailable",
            "Neutral" when council == CouncilKind.Ethos => "safety_neutral",
            _ => throw new InvalidDataException("Unrecognized council selection.")
        };
    }
}
