using System.Security.Cryptography;
using System.Text;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

// These bindings implement the reviewed Monolith governance ROMs, not an arbitrary persona.
// New rule text requires a reviewed binding; operator pinning alone cannot change its semantics.
internal static class BonsaiCtgFindingPolicy
{
    internal const string Contract="monolith-findings-v1";
    internal static void RequireSupported(BonsaiCouncilRule rule,CouncilKind council)
    {
        string expected=council switch
        {
            CouncilKind.Logos=>"deb911959eb3bf0cf6fa129863f0e183b8f3139c7f84c51d462c138b64e16114",
            CouncilKind.Ethos=>"563f1309e6d257fd48dfeca994435ebd7063508eb6a0c0055048eece6c799885",
            CouncilKind.Pathos=>"342d5ccc58a27518623fbd3cab9acb6926a06e0d536759e7a3e7fa0eaad07226",
            _=>throw new InvalidDataException("Unsupported council.")
        };
        string digest=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rule.Text.Replace("\r\n","\n",StringComparison.Ordinal))));
        if(rule.Reference.CanonId!="Canon.CTG.Monolith.Council."+council||digest!=expected)
            throw new InvalidDataException("No reviewed finding-to-vote binding exists for this governance ROM.");
    }
    internal static CouncilVoteValue Resolve(CouncilKind council,string finding)
    {
        if(council is not (CouncilKind.Logos or CouncilKind.Ethos or CouncilKind.Pathos))throw new InvalidDataException("Unsupported council.");
        return finding switch
        {
            "supported"=>CouncilVoteValue.Approve,
            "proposal_defect"=>CouncilVoteValue.Reject,
            "evaluation_unavailable"=>council==CouncilKind.Ethos?CouncilVoteValue.Reject:CouncilVoteValue.Abstain,
            "safety_neutral" when council==CouncilKind.Ethos=>CouncilVoteValue.Abstain,
            _=>throw new InvalidDataException("Unsupported council finding.")
        };
    }
    internal static string Describe(CouncilKind council)=>
        "supported: all this council's criteria are established by the evidence"+
        (council==CouncilKind.Pathos?", including appropriate neutral factual or technical responses":"")+".\n"+
        "proposal_defect: a relevant criterion fails inside the proposal, including contradictions, missing required information, or boundary violations.\n"+
        "evaluation_unavailable: the proposal is well-formed, but external infrastructure or context access prevents evaluation; not an internal proposal defect.\n"+
        (council==CouncilKind.Ethos?"safety_neutral: fully evaluated, outside ethical scope, and delegating cannot create harm; never use for uncertainty.\n":"");
}

internal sealed record BonsaiCtgAssessment(string Finding,CouncilVoteValue Vote,int SourceId,string EvidenceSha256);
