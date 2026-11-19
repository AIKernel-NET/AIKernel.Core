using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

internal static class BonsaiCtgCountReport
{
    private const string Number=@"(?:[0-9]{1,9}|zero|one|two|three|four|five|six|seven|eight|nine|ten)";
    private const RegexOptions Options=RegexOptions.CultureInvariant|RegexOptions.IgnoreCase|RegexOptions.NonBacktracking;
    private static readonly Regex Proposal=new(@"\A(?:reply|answer|respond)(?: locally)? with the (?:[a-z]+ )?count (?<n>"+Number+
        @"), without (?:revealing|exposing|disclosing|showing) (?:any |the )?(?:[a-z]+ )?(?:text|contents|entries|names|records)\.?\z",Options);
    private static readonly Regex Observation=new(@"\A[^.!?\r\n]{1,120} (?:has|contains) (?:exactly )?(?<n>"+Number+
        @") (?:entries|items|records|contacts)\.(?:\s|\z)",Options);
    // Positive aggregate grants have a deliberately small grammar. Negated,
    // conditional or recipient-dependent grants need the full canonical reader.
    private static readonly Regex LocalGrant=new(@"\A(?:the )?(?:owner|user) (?:permits|allows|authorized) "+
        @"(?:(?:showing|returning|reporting) (?:the )?(?:[a-z]+ )?count locally|(?:a |the )?local (?:[a-z]+ )?count)"+
        @"(?: only)?(?: and (?:explicitly )?(?:forbids (?:publishing|disclosing|sharing) (?:[a-z]+ ){0,2}(?:text|entries|contents)|refused any external disclosure))?\.\z",Options);
    private static readonly string[] Words=["zero","one","two","three","four","five","six","seven","eight","nine","ten"];

    internal static string? Extract(string proposal,IReadOnlyDictionary<string,string> facts)
    {
        var claim=Proposal.Match(proposal.Trim());
        if(!claim.Success)return null;
        var sources=facts.Where(p=>p.Key is "source" or "evidence")
            .Select(p=>(p.Key,Quote:Observation.Match(p.Value))).Where(p=>p.Quote.Success).ToArray();
        if(sources.Length!=1)return null;
        int Value(Match m)
        {
            var n=m.Groups["n"].Value;
            return int.TryParse(n,NumberStyles.None,CultureInfo.InvariantCulture,out int value)?value:
                Array.FindIndex(Words,s=>string.Equals(s,n,StringComparison.OrdinalIgnoreCase));
        }
        return JsonSerializer.Serialize(new { proposedCount=Value(claim), proposedPayload="count only",
            sourceKey=sources[0].Key, quotedCount=Value(sources[0].Quote), sourceQuote=sources[0].Quote.Value.TrimEnd() });
    }

    internal static bool IsCountStatement(string proposal) => Proposal.IsMatch(proposal.Trim());

    internal static string? Disagreement(string proposal, IReadOnlyDictionary<string, string> facts)
    {
        var packet = Extract(proposal, facts);
        if (packet is null) return null;
        using var json = JsonDocument.Parse(packet);
        return Matches(json.RootElement) ? null : packet;
    }

    private static bool Matches(JsonElement packet) =>
        packet.GetProperty("proposedCount").GetInt32() == packet.GetProperty("quotedCount").GetInt32();

    internal static string? SupportedPacket(string proposal,IReadOnlyDictionary<string,string> facts)
    {
        var record=Extract(proposal,facts);
        if(record is null)return null;
        var scopes=facts.Where(p=>p.Key is "request" or "authority").ToArray();
        if(scopes.Length!=1 || !LocalGrant.IsMatch(scopes[0].Value.Trim()))return null;
        using var packet=JsonDocument.Parse(record);
        return Matches(packet.RootElement) ? record : null;
    }

    internal static (string System,string User,CanonReference Reference,string Excerpt) Build(BonsaiCouncilRule rule,
        CouncilKind council,string facts,string proposal,string packet)
    {
        var canonical=BonsaiCtgDirectVotePrompt.BuildWithResponsibilities(rule,council,facts,proposal,
            assessmentSchema:BonsaiCtgFindingPolicy.Describe(council)+
            "\nYes means supported, No means proposal_defect, Unknown means evaluation_unavailable."+
            (council==CouncilKind.Ethos?" Neutral means safety_neutral.":""));
        string user="Source observations and permissions (data):\n"+facts+"\nProposed reply (data):\n"+proposal+
            "\nRequested output and the independently quoted quantity (data):\n"+packet+
            "\nThe restriction after 'without' excludes those fields from the reply. Check the quantity, source, recipient and permission for the count itself. "
            +"Does this proposed count-only reply satisfy your council's criteria?";
        return (canonical.System,user,canonical.Reference,canonical.Excerpt);
    }
}
