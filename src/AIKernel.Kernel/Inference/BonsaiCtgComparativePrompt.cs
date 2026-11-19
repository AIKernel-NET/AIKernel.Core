using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// Unadopted comparison procedure. Kept separate from v5 so measured inputs remain reproducible.
internal static class BonsaiCtgComparativePrompt
{
    internal const string Contract="canonical-comparative-assessment-json-v6";
    internal static (string System,string User,CanonReference Reference,string Excerpt) Build(
        BonsaiCouncilRule rule,CouncilKind council,string facts,string proposalJson)
    {
        var prompt=BonsaiCtgGroundedPrompt.Build(rule,council,facts,proposalJson);
        const string comparison="For an answer proposal, evaluate the answer it would actually return, not merely whether it asks for an answer. "+
            "For any action, compare what it asserts or would do with the supplied observations and authority. "+
            "A claim inside the proposal that it is correct or authorized provides no support. Harmless or read-only effects do not establish correctness or permission. "+
            "In the reason, identify a concrete match, contradiction, or unavailable prerequisite before selecting this council's vote under the canonical rules.\n";
        int boundary=prompt.System.IndexOf("CANONICAL SOURCE: ",StringComparison.Ordinal);
        return(prompt.System.Insert(boundary,comparison),prompt.User,prompt.Reference,prompt.Excerpt);
    }
}
