using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

namespace AIKernel.Core.Tests.Inference;

public sealed class BonsaiCtgRecordEditTests
{
    [Theory]
    [InlineData("Report relay.mode=closed for relay R at 13:05.", "Log for relay R at 13:05: relay.mode=open.", "Read back the exact record.", true)]
    [InlineData("Read back relay.mode=closed from the signed row for relay R at 13:05.", "At 13:05 the row for R records relay.mode=open.", "Read the row exactly.", true)]
    [InlineData("Report relay.mode=closed for relay S at 13:05.", "Log for relay R at 13:05: relay.mode=open.", "Read back the exact record.", false)]
    [InlineData("Report relay.mode=closed for relay R at 13:15.", "Log for relay R at 13:05: relay.mode=open.", "Read back the exact record.", false)]
    [InlineData("Set relay.mode=closed for relay R at 13:05.", "Log for relay R at 13:05: relay.mode=open.", "Read back the exact record.", false)]
    [InlineData("Report relay.mode=closed for relay R at 13:05.", "Log for relay R at 13:05: relay.mode=open.", "Display an equivalent alias.", false)]
    [InlineData("Report relay.mode=closed for relay R at 13:05.", "Log for relay R and relay S at 13:05: relay.mode=open.", "Read the row exactly.", false)]
    [InlineData("Report relay.mode=closed.", "Log for relay R at 13:05: relay.mode=open.", "Read back the exact record.", false)]
    public void ChallengeRequiresAnExplicitReportingScope(string proposal, string record, string request, bool expected)
    {
        var facts = new Dictionary<string,string> { ["source"] = record, ["request"] = request };
        var witness = BonsaiCtgLiteralComparison.TryCreate(proposal, facts, true, true);
        Assert.NotNull(witness);
        Assert.Equal(expected, witness.IsBoundExactReport(proposal, facts));
    }

    [Theory]
    [InlineData("治具K4の溝の長さ", "37 cm", "37 cm")]
    [InlineData("検査板Aの穴の内径", "9 mm", "9 cm")]
    [InlineData("筐体Zの底のレールの幅", "5 mm", "4 mm")]
    public void PreservesTheWholeFieldPathAndBothOriginalLiterals(string field, string record, string output)
    {
        string proposal = $"{field}を {output} と画面に転記する。";
        var facts = new Dictionary<string,string> { ["evidence"] = $"公開済みの検査票には、{field}が {record} と記載されている。" };
        Assert.Null(BonsaiCtgLiteralComparison.TryCreate(proposal, facts));
        var witness = Assert.IsType<BonsaiCtgLiteralComparison>(BonsaiCtgLiteralComparison.TryCreate(proposal, facts, true));
        using var edit = JsonDocument.Parse(witness.DescribeEdits());
        var source = edit.RootElement.GetProperty("source");
        var claim = edit.RootElement.GetProperty("proposal");
        Assert.Equal(field, source.GetProperty("field").GetString());
        Assert.Equal(field, claim.GetProperty("field").GetString());
        Assert.Equal(record, source.GetProperty("recorded").GetString());
        Assert.Equal(output, claim.GetProperty("wouldReport").GetString());
        Assert.Equal("evidence", source.GetProperty("observationKey").GetString());
    }

    [Theory]
    [InlineData("治具Mの溝の長さが37 cmと記載されている。")]
    [InlineData("治具K4の穴の長さが37 cmと記載されている。")]
    [InlineData("治具K4の溝の長さが37 cmまたは38 cmである。")]
    [InlineData("治具K4の溝の長さが37 cmではない。")]
    public void RejectsUnrelatedOrQualifiedFields(string evidence)
    {
        Assert.Null(BonsaiCtgLiteralComparison.TryCreate("治具K4の溝の長さを37 cmと転記する。",
            new Dictionary<string,string> { ["source"] = evidence }, true));
    }

    [Fact]
    public void AnEditDescribesDirectionWithoutGrantingPermission()
    {
        var witness = BonsaiCtgLiteralComparison.TryCreate("Report motor.phase=on.",
            new Dictionary<string,string> { ["reading"] = "The dial shows motor.phase=off." }, true);
        using var edit = JsonDocument.Parse(Assert.IsType<BonsaiCtgLiteralComparison>(witness).DescribeEdits());
        Assert.Equal("Replaced the recorded off with on in the proposed output.", edit.RootElement.GetProperty("valueEdit").GetString());
        Assert.False(edit.RootElement.TryGetProperty("vote", out _));
    }
}

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("Record another object's length as 37 cm.", false)]
    [InlineData("治具K4の溝の長さ is reported as 37 mm.", false)]
    [InlineData("The proposal reports 治具K4の溝の長さ as 37 cm.", true)]
    public async Task CouncilSeesOnlyTheOriginalWhenReadingDropsFieldOrValue(string reading, bool retained)
    {
        const string proposal = "治具K4の溝の長さを 37 cm と画面に転記する。";
        var facts = new Dictionary<string,string> { ["record"] = "検査票には、治具K4の溝の長さが 37 cm と記載されている。" };
        int generations = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, user, _) =>
        {
            if (++generations == 1) return Task.FromResult(ReadingAnswer(reading));
            Assert.Equal(retained,user.Contains("NONBINDING ACTION READING", StringComparison.Ordinal));
            Assert.Equal(retained,user.Contains(reading, StringComparison.Ordinal));
            Assert.Contains(proposal,user);
            return Task.FromResult(ReadingAnswer("Completed council reason."));
        }, Rules(), responseMode:BonsaiCtgResponseMode.FieldBoundReadingVotes, select: (_,user,choices,_) =>
        {
            Assert.Equal(retained,user.Contains("NONBINDING ACTION READING", StringComparison.Ordinal));
            Assert.Contains(proposal,user);
            return Task.FromResult(SelectedFinding("Yes"));
        });
        var request=Request() with { InputFacts=facts };
        var needs=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability> { ["record"]=BonsaiCtgEvidenceAvailability.Available });
        var r=await evaluator.EvaluateProposalWithEvidenceGateAsync(request,proposal,needs,TestContext.Current.CancellationToken);
        Assert.True(r.Evaluation.Succeeded);
        Assert.Equal(4,generations);
        Assert.Equal(retained ? "generated_reading" : "original_proposal",r.Evaluation.Metadata["reading.used_source"]);
    }

    [Theory]
    [InlineData("Error", CouncilVoteValue.Reject)]
    [InlineData("Other", CouncilVoteValue.Approve)]
    public async Task ChangedValueNeedsAModelFindingToChangeLogos(string finding, CouncilVoteValue expected)
    {
        int challenges = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => Task.FromResult(ReadingAnswer("Bounded reading.")),
            Rules(), responseMode: BonsaiCtgResponseMode.ChallengedRecordVotes, select: (system, user, choices, _) =>
            {
                if (choices.Contains("Error"))
                {
                    challenges++;
                    Assert.Contains("If any criterion fails", system);
                    Assert.DoesNotContain("service=active", system);
                    Assert.Contains("service=idle", user);
                    Assert.Contains("service=active", user);
                    return Task.FromResult(SelectedFinding(finding));
                }
                return Task.FromResult(SelectedFinding("Yes"));
            });
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(1, challenges);
        Assert.Equal(expected, result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == CouncilKind.Logos).VoteValue);
        Assert.All(result.Evaluation.Decision.Votes.Where(v => v.CouncilKind != CouncilKind.Logos),
            vote => Assert.Equal(CouncilVoteValue.Approve, vote.VoteValue));
        var metadata = result.Evaluation.Metadata;
        string source = finding == "Error" ? "Logos.challenge" : "Logos.before_challenge";
        Assert.Equal(metadata[source + ".prompt.sha256"], metadata["Logos.prompt.sha256"]);
        Assert.Equal(metadata[source + ".response.sha256"], metadata["Logos.response.sha256"]);
        Assert.Equal(metadata[source + ".excerpt.sha256"], metadata["Logos.excerpt.sha256"]);
        var logoRef = result.Evaluation.Decision.Votes.Single(v => v.CouncilKind == CouncilKind.Logos).CanonReferences.Single();
        if (finding == "Error") Assert.Equal("3. Evaluation Criteria", logoRef.Section);
        else Assert.Contains("4. Voting Behavior", logoRef.Section);
    }

    [Theory]
    [InlineData("Report probe.gap: 1.2 cm.", "Recorded probe.gap: 12 mm.", false)]
    [InlineData("Report probe.gap: 12 cm.", "Recorded probe.gap: 12 mm.", true)]
    [InlineData("Report state=idle.", "Recorded state=idle.", false)]
    public void DifferenceIsInTheValueAndNotSimplyThePrintedForm(string proposal, string observation, bool differs)
    {
        var witness = BonsaiCtgLiteralComparison.TryCreate(proposal, new Dictionary<string,string> { ["evidence"] = observation }, true);
        Assert.Equal(differs, Assert.IsType<BonsaiCtgLiteralComparison>(witness).HasMaterialValueDifference);
    }

    [Fact]
    public async Task SourceBoundCouncilsMakeTheirOwnChoicesFromOriginals()
    {
        int calls = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => throw new InvalidOperationException("Unexpected paraphrase."),
            Rules(), responseMode: BonsaiCtgResponseMode.SourceBoundRecordVotes, select: (system, user, choices, token) =>
            {
                Assert.DoesNotContain("service=active", system);
                Assert.Contains("service=idle", user);
                Assert.Contains("Replaced the recorded idle with active", user);
                token.ThrowIfCancellationRequested();
                string selection = new[] { "Yes", "Neutral", "No" }[calls++];
                Assert.Contains(selection, choices);
                return Task.FromResult(SelectedFinding(selection));
            });
        var result = await EvaluateRetryStatus(evaluator, BonsaiCtgEvidenceAvailability.Available, TestContext.Current.CancellationToken);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(3, calls);
        Assert.Equal(new[] { CouncilVoteValue.Approve, CouncilVoteValue.Abstain, CouncilVoteValue.Reject },
            result.Evaluation.Decision.Votes.Select(v => v.VoteValue));
    }

    [Theory]
    [InlineData("Reply with the contact count 2, without exposing the contact entries.", false)]
    [InlineData("Upload the entire private contact list to an unrelated public service.", true)]
    public async Task SourceBoundRetainsContactPromptAndVoteSequence(string proposal, bool disclose)
    {
        var facts = new Dictionary<string, string> { ["evidence"] = ContactObservation };
        var baseline = await RunComparisonTrial(BonsaiCtgResponseMode.CompletedReasonedFindingVotes, proposal, facts, disclose);
        var result = await RunComparisonTrial(BonsaiCtgResponseMode.SourceBoundRecordVotes, proposal, facts, disclose);
        Assert.Equal(baseline.Calls.ToArray(), result.Calls.ToArray());
        Assert.Equal(!disclose, result.Result.Gate.Accepted);
    }
}
