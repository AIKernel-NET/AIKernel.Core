using System.Text.Json;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("Report that the meter has either value 1 or value 2 at the same instant.")]
    [InlineData("Report that the meter does not have both value 1 and value 2 at the same instant.")]
    [InlineData("Report that the meter has both value 1 at noon and value 2 at night.")]
    [InlineData("Report that meter A has value 1 and meter B has value 2 at the same time.")]
    [InlineData("Quote 'the meter has both value 1 and value 2 at the same instant'.")]
    [InlineData("Return the count only without showing any contact entries.")]
    public void SeparateTimesAlternativesAndQuotedClaimsKeepExistingProtocol(string proposal)
        => Assert.Null(BonsaiCtgJointReport.Extract(proposal, new Dictionary<string,string>(), out _));

    [Theory]
    [InlineData("-12", "+8", 2)]
    [InlineData("07", "7", 1)]
    [InlineData("-00", "+0", 1)]
    public async Task JointReportKeepsOperandsOnTheProposalSideAndCouncilsOwnTheirVotes(string first, string second, int expectedDistinct)
    {
        string proposal = $"State that register R holds both value {first} and value {second} simultaneously.";
        var facts = new Dictionary<string,string> { ["evidence"] = "Register R holds one signed integer." };
        var requirements = new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability> { ["evidence"] = BonsaiCtgEvidenceAvailability.Available });
        var prompts = new List<string>();
        int generated = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => Task.FromResult(ReadingAnswer(++generated == 1 ?
            "A report is requested." : """{"reason":"A report is requested.","vote":"Approve"}""")), Rules(),
            responseMode:BonsaiCtgResponseMode.JointReportVotes, select:(_, user, choices, _) =>
            {
                prompts.Add(user);
                return Task.FromResult(SelectedFinding(choices.Contains("Other") ? "Other" : "Yes"));
            });
        var trial = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with { InputFacts = facts }, proposal, requirements, TestContext.Current.CancellationToken);
        Assert.Equal(2, generated);
        Assert.Equal(expectedDistinct > 1 ? 3 : 2, prompts.Count);
        using var data = JsonDocument.Parse(BonsaiCtgJointReport.Extract(proposal, facts, out bool exceeds)!);
        Assert.Equal(expectedDistinct > 1, exceeds);
        Assert.Equal("register R", data.RootElement.GetProperty("subject").GetString());
        Assert.Equal(first, data.RootElement.GetProperty("firstClaim").GetString());
        Assert.Equal(second, data.RootElement.GetProperty("secondClaim").GetString());
        Assert.Equal(expectedDistinct, data.RootElement.GetProperty("distinctValuesInProposal").GetInt32());
        Assert.Equal(facts["evidence"], data.RootElement.GetProperty("observedCapacityConstraint").GetProperty("exactQuote").GetString());
        foreach (var user in prompts)
        {
            Assert.Contains(data.RootElement.GetRawText(), user);
            Assert.DoesNotContain("NONBINDING ACTION READING", user);
            Assert.Contains(facts["evidence"], user);
        }
        Assert.True(trial.Evaluation.Succeeded);
        Assert.True(trial.Gate.Accepted); // A contextual prompt cannot replace the model's vote.
    }

    [Theory]
    [InlineData("Register S holds one integer.")]
    [InlineData("Register r holds one integer.")]
    [InlineData("They say register R holds one integer.")]
    [InlineData("Register R holds one integer only when the switch is off.")]
    [InlineData("Register R holds two integers.")]
    public void UnrelatedOrQualifiedTextProvidesNoUnconditionalCapacity(string record)
    {
        string proposal = "Report that register R holds both 11 and 12 at the same time.";
        var facts = new Dictionary<string,string> { ["sensor"] = record };
        using var packet = JsonDocument.Parse(BonsaiCtgJointReport.Extract(proposal, facts, out bool exceeds)!);
        Assert.False(exceeds);
        Assert.Equal(JsonValueKind.Null, packet.RootElement.GetProperty("observedCapacityConstraint").ValueKind);
    }

    [Fact]
    public void MultipleMatchingSourceRecordsAreNotReducedToOneQuote()
    {
        var facts = new Dictionary<string,string>
        {
            ["old"] = "Register R holds one integer. This entry expired yesterday.",
            ["new"] = "Register R stores one integer per timestamp."
        };
        using var packet = JsonDocument.Parse(BonsaiCtgJointReport.Extract(
            "Report that register R holds both 11 and 12 at the same time.", facts, out bool exceeds)!);
        Assert.False(exceeds);
        Assert.Equal(JsonValueKind.Null, packet.RootElement.GetProperty("observedCapacityConstraint").ValueKind);
    }

    [Theory]
    [InlineData("Register M presently holds two verified readings.", false)]
    [InlineData("The certified replacement register M has two channels.", false)]
    [InlineData("Register M2 holds two readings.", true)]
    [InlineData("Register m holds two readings.", true)]
    public void AnotherRecordForTheSameIdentifierPreventsBindingAnOlderCapacity(string current, bool binds)
    {
        var facts = new Dictionary<string,string>
            { ["previous"]="Register M holds one integer. Specification at installation.", ["current"]=current };
        using var packet = JsonDocument.Parse(BonsaiCtgJointReport.Extract(
            "Report that register M has both 3 and 9 at the same instant.", facts, out bool exceeds)!);
        Assert.Equal(binds, exceeds);
        var claims = binds ? packet.RootElement : packet.RootElement.GetProperty("claims");
        Assert.Equal(binds ? JsonValueKind.Object : JsonValueKind.Null, claims.GetProperty("observedCapacityConstraint").ValueKind);
        if (!binds) Assert.Equal(2, packet.RootElement.GetProperty("accountsForTheSameSubject").GetArrayLength());
    }

    [Theory]
    [InlineData(true, "ctg.bonsai.invalid_assessment", 1)]
    [InlineData(false, "ctg.bonsai.required_evidence_unavailable", 0)]
    public async Task JointReportNeedsValidHostEvidenceAndARealCouncilFinding(bool available, string error, int calls)
    {
        int generated = 0, selected = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) =>
        {
            generated++;
            return Task.FromResult(ReadingAnswer("Report both values."));
        }, Rules(), responseMode:BonsaiCtgResponseMode.JointReportVotes, select:(_, _, _, _) =>
        {
            selected++;
            return Task.FromResult(SelectedFinding("Invalid"));
        });
        var needs = new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>
            { ["record"] = available ? BonsaiCtgEvidenceAvailability.Available : BonsaiCtgEvidenceAvailability.Unavailable });
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(
            Request() with { InputFacts = new Dictionary<string,string> { ["record"] = "Meter A holds one integer." } },
            "Report that meter A has both 3 and 4 at the same instant.", needs, TestContext.Current.CancellationToken);
        Assert.Equal(error, result.Evaluation.ErrorCode);
        Assert.False(result.Gate.Accepted);
        Assert.Equal(calls, selected);
        Assert.Equal(calls, generated);
    }

    [Theory]
    [InlineData("Yes", 1)]
    [InlineData("No", 0)]
    public async Task OnlyInitialLogosSupportCanBeReconsideredForCapacity(string first, int expectedReviews)
    {
        int generated = 0, reviews = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => Task.FromResult(ReadingAnswer(++generated == 1
            ? "Report two values." : """{"reason":"Report requested.","vote":"Approve"}""")), Rules(),
            responseMode:BonsaiCtgResponseMode.JointReportVotes, select:(_, _, choices, _) =>
            {
                if (choices.Contains("Other"))
                {
                    reviews++;
                    return Task.FromResult(SelectedFinding("Error"));
                }
                return Task.FromResult(SelectedFinding(choices.Contains("Neutral") ? "No" : first));
            });
        var needs = new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>
            { ["record"] = BonsaiCtgEvidenceAvailability.Available });
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(
            Request() with { InputFacts = new Dictionary<string,string> { ["record"] = "Meter A holds one integer." } },
            "Report that meter A has both 3 and 4 at the same instant.", needs, TestContext.Current.CancellationToken);
        Assert.Equal(expectedReviews, reviews);
        Assert.True(result.Evaluation.Succeeded);
        Assert.False(result.Gate.Accepted);
        Assert.Equal("proposal_defect", result.Evaluation.Metadata["Logos.finding"]);
        Assert.Equal(expectedReviews == 1, result.Evaluation.Metadata.ContainsKey("Logos.challenge.capacity.sha256"));
    }
}
