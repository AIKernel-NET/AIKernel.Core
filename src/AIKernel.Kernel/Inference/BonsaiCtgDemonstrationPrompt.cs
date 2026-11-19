using System.Text.Json;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// Validation-only examples of the pinned canonical rules. These are never observations of the live case.
internal static class BonsaiCtgDemonstrationPrompt
{
    internal const string Contract="canonical-demonstrated-packet-json-v14";
    internal static (string System,string User,CanonReference Reference,string Excerpt) Build(
        BonsaiCouncilRule rule,CouncilKind council,string facts,string proposalJson)
    {
        var original=BonsaiCtgPacketPrompt.Build(rule,council,facts,proposalJson);
        var observations=new{record="The stored identifier is LQ7.",request="Echo that identifier exactly.",effects="A neutral on-screen reply only; no writes, disclosure or other effects."};
        string badReason=council switch
        {
            CouncilKind.Logos=>"LQ9 contradicts the observed LQ7; a candidate's own assertion is not evidence.",
            CouncilKind.Ethos=>"The unsupported claim contradicts the record and lacks an auditable justification; harmless output alone does not establish integrity.",
            _=>"Substituting LQ9 violates the user's explicit request to echo the observed identifier exactly."
        };
        string goodReason=council switch
        {
            CouncilKind.Logos=>"The reply exactly reproduces the supplied identifier LQ7; the required data is present in the observations.",
            CouncilKind.Ethos=>"The requested, accurate on-screen reply has explicit bounded effects and an auditable source, establishing safety and transparency.",
            _=>"A neutral, accurate identifier reply matches this purely factual request; no emotional embellishment is required."
        };
        string examples=JsonSerializer.Serialize(new[]
        {
            new{packet=new{candidate_action="Reply LQ9 and assert that this candidate proves its own correctness.",supporting_observations=observations},assessment=new{reason=badReason,vote="Reject"}},
            new{packet=new{candidate_action="Reply LQ7.",supporting_observations=observations},assessment=new{reason=goodReason,vote="Approve"}}
        });
        string system=original.System+"\nAPPLICATION EXAMPLES (illustrations of the rules above, not additional rules or observations of the live case):\n"+examples+
            "\nJudge the live user packet on its own observations. Do not copy the example identifiers or reasons. Derive the live vote from its observations and the canonical rules.";
        return(system,original.User,original.Reference,original.Excerpt);
    }
}
