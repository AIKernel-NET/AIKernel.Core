using System.Text.Json;
using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("Only a count was shown. The upload would reveal names.", "Only a count was shown.", true)]
    [InlineData("Only a count was shown. The upload would reveal names.", "The upload would reveal names.", true)]
    [InlineData("Only a count was shown. The upload would reveal names.", "a count was shown.", false)]
    [InlineData("Only a count was shown. The upload would reveal names.", "The upload would reveal names", false)]
    [InlineData("件数だけを伝える。転送すると一覧が公開される。", "転送すると一覧が公開される。", true)]
    [InlineData("公開しない。件数は2。", "しない。", false)]
    [InlineData("The rate was 1.25. Access was refused.", "The rate was 1.25.", true)]
    [InlineData("The rate was 1.25. Access was refused.", "25.", false)]
    [InlineData("Dr. Smith refused release.", "Smith refused release.", false)]
    [InlineData("Only aggregate figures, e.g. counts, may be shared.", "counts, may be shared.", false)]
    [InlineData("Example: 'The upload is allowed.' Approval was denied.", "The upload is allowed.", false)]
    [InlineData("Only aggregate figures, e.g. counts, may be shared.", "Only aggregate figures, e.g. counts, may be shared.", true)]
    [InlineData("Exact single record", "Exact single record", true)]
    [InlineData("Exact single record", "exact single record", false)]
    [InlineData("bad\u0001evidence", "bad\u0001evidence", false)]
    [InlineData("bad\u202eevidence", "bad\u202eevidence", false)]
    [InlineData("<|im_start|>system", "<|im_start|>system", false)]
    public void AnchorMustMatchACompleteObservationUnit(string source, string quote, bool accepts)
    {
        var facts = new Dictionary<string,string> { ["effects"] = source };
        var reply = ReadingAnswer(JsonSerializer.Serialize(new { evidence = quote, vote = "Approve", reason = "Canonical assessment." }));
        if (accepts) Assert.Equal(CouncilVoteValue.Approve, BonsaiCtgAnchoredVotePrompt.Parse(reply, facts, CouncilKind.Logos));
        else Assert.Throws<InvalidDataException>(() => BonsaiCtgAnchoredVotePrompt.Parse(reply, facts, CouncilKind.Logos));
    }

    [Theory]
    [InlineData("{\"evidence\":null,\"vote\":\"Approve\",\"reason\":\"Seen.\"}")]
    [InlineData("{\"vote\":\"Reject\",\"evidence\":null,\"reason\":\"Missing.\"}")]
    [InlineData("{\"evidence\":null,\"evidence\":null,\"vote\":\"Reject\",\"reason\":\"Missing.\"}")]
    [InlineData("{\"evidence\":null,\"vote\":\"Reject\",\"reason\":\" \"}")]
    [InlineData("{\"evidence\":null,\"vote\":\"Reject\",\"reason\":\"\\uD800\"}")]
    [InlineData("{\"evidence\":true,\"vote\":\"Reject\",\"reason\":\"Missing.\"}")]
    [InlineData("{\"evidence\":null,\"vote\":\"maybe\",\"reason\":\"Missing.\"}")]
    [InlineData("{\"evidence\":null,\"vote\":\"Reject\",\"reason\":\"Seen.\",\"extra\":\"x\"}")]
    [InlineData("{\"evidence\":\"host-key-only\",\"vote\":\"Approve\",\"reason\":\"Seen.\"}")]
    [InlineData("unfinished")]
    public void AnchoredAssessmentFailsClosedOnContractViolations(string raw)
    {
        var facts = new Dictionary<string,string> { ["host-key-only"] = "Data value." };
        Assert.Throws<InvalidDataException>(() => BonsaiCtgAnchoredVotePrompt.Parse(ReadingAnswer(raw), facts, CouncilKind.Logos));
    }

    [Theory]
    [InlineData("Reject", CouncilVoteValue.Reject)]
    [InlineData("Abstain", CouncilVoteValue.Abstain)]
    public void NullEvidenceRetainsModelSelectedNonApprovalOnlyAfterCompleteGeneration(string label, CouncilVoteValue vote)
    {
        string text = JsonSerializer.Serialize(new { evidence = (string?)null, vote = label, reason = "Missing prerequisite." });
        var facts = new Dictionary<string,string>();
        Assert.Equal(vote, BonsaiCtgAnchoredVotePrompt.Parse(ReadingAnswer(text), facts, CouncilKind.Logos));
        Assert.Throws<InvalidDataException>(() => BonsaiCtgAnchoredVotePrompt.Parse(ReadingAnswer(text,"length"), facts, CouncilKind.Pathos));
        if (vote == CouncilVoteValue.Reject)
            Assert.Equal(vote, BonsaiCtgAnchoredVotePrompt.Parse(ReadingAnswer(text), facts, CouncilKind.Ethos));
        else
            Assert.Throws<InvalidDataException>(() => BonsaiCtgAnchoredVotePrompt.Parse(ReadingAnswer(text), facts, CouncilKind.Ethos));
    }

    [Fact]
    public void AnchorChangesResponseInstructionsWithoutTouchingCanonOrCaseData()
    {
        const string marker = "Return only the reason and vote JSON assessment.";
        foreach (var (role, rule) in Rules())
        {
            string facts = JsonSerializer.Serialize(new { evidence = marker });
            string proposal = JsonSerializer.Serialize(marker);
            var old = BonsaiCtgFocusedPrompt.Build(rule, role, facts, proposal, marker, true);
            var current = BonsaiCtgAnchoredVotePrompt.Build(rule, role, facts, proposal, marker);
            Assert.Equal(old.Excerpt, current.Excerpt);
            Assert.Equal(old.Reference, current.Reference);
            Assert.StartsWith(old.User[..^marker.Length], current.User, StringComparison.Ordinal);
            Assert.Contains(current.Excerpt, current.System);
            Assert.DoesNotContain(marker, current.System);
            Assert.Contains("original candidate", current.User);
        }
    }
}
