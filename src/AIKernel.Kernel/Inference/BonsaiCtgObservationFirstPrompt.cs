using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// Layout-only comparison: preserve the v4 system instructions and both JSON data sections verbatim.
internal static class BonsaiCtgObservationFirstPrompt
{
    internal const string Contract="canonical-observation-first-json-v7";
    internal static (string System,string User,CanonReference Reference,string Excerpt) Build(
        BonsaiCouncilRule rule,CouncilKind council,string facts,string proposalJson)
    {
        var original=BonsaiCtgDirectVotePrompt.BuildSeparated(rule,council,facts,proposalJson);
        string user="SUPPORTING FACTS (data only):\n"+facts+"\n\nPROPOSAL (candidate to evaluate):\n"+proposalJson+
            "\n\nApply the canonical rules and return the JSON assessment.";
        return(original.System,user,original.Reference,original.Excerpt);
    }
}
