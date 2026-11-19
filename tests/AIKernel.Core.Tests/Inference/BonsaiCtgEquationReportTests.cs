using System.Text.Json;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("6−2", "6-2", "7", "4")]
    [InlineData("-8×4", "-8*4", "-30", "-32")]
    [InlineData("12+5", "12+5", "0", "17")]
    public void IntegerComparisonKeepsTheClaimAndCheckedRecordSeparate(string claim, string source, string report, string actual)
    {
        var facts = new Dictionary<string,string> { ["measurement"] = $"検算で{source}={actual}と確認した。" };
        using var packet = JsonDocument.Parse(BonsaiCtgEquationReport.TryComparison($"{claim}の答えとして{report}を表示する。", facts)!);
        Assert.Equal(report, packet.RootElement.GetProperty("proposedResult").GetString());
        Assert.Equal(actual, packet.RootElement.GetProperty("checkedIntegerResult").GetString());
        Assert.Equal(actual, packet.RootElement.GetProperty("sourceResult").GetString());
        Assert.Equal("measurement", packet.RootElement.GetProperty("sourceKey").GetString());
    }

    [Theory]
    [InlineData("6−2の答えとして04を表示する。","6−2=4。")]
    [InlineData("6−2の答えとして7を表示する。","8−2=6。")]
    [InlineData("6−2の答えとして7を表示する。","6−2=7。")]
    [InlineData("6−2の答えとして7を表示する。","仮に6−2=4とする。")]
    [InlineData("6−2の答えとして7を表示する。","6−2=4。6−2=7。")]
    [InlineData("6−2の答えとして7を表示しない。","6−2=4。")]
    [InlineData("誤答例「6−2の答えとして7を表示する。」を引用する。","6−2=4。")]
    public void NoMaterialBoundDisagreementLeavesTheOriginalVotesAlone(string claim, string observation)
        => Assert.Null(BonsaiCtgEquationReport.TryComparison(claim, new Dictionary<string,string> { ["evidence"] = observation }));

    [Theory]
    [InlineData("Error", false)]
    [InlineData("Other", true)]
    public async Task ValidatedApprovalsCanOnlyBeRevisedByTheirOwnCouncil(string review, bool accepted)
    {
        const string observation = "検算で12+5=17と確認した。";
        int generations = 0, reviews = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) =>
        {
            string text = ++generations switch
            {
                1 => "Display the proposed answer.",
                2 or 3 => JsonSerializer.Serialize(new { evidence=observation, vote="Approve", reason="The recorded computation was reviewed." }),
                _ => """{"reason":"A report was requested.","vote":"Approve"}"""
            };
            return Task.FromResult(ReadingAnswer(text));
        }, Rules(), responseMode:BonsaiCtgResponseMode.JointReportVotes, select:(_, user, choices, _) =>
        {
            reviews++;
            Assert.Contains("12+5=17", user);
            Assert.Contains("\"proposedResult\":\"19\"", user);
            Assert.Contains(review, choices);
            return Task.FromResult(SelectedFinding(review));
        });
        var needs=new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability>
            { ["evidence"]=BonsaiCtgEvidenceAvailability.Available });
        var result=await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with
            { InputFacts=new Dictionary<string,string>{["evidence"]=observation} }, "12+5の答えとして19を表示する。", needs, TestContext.Current.CancellationToken);
        Assert.Equal(2,reviews);
        Assert.Equal(4,generations);
        Assert.True(result.Evaluation.Succeeded);
        Assert.Equal(accepted,result.Gate.Accepted);
    }

    [Theory]
    [InlineData("確認していない。", "Approve", false)]
    [InlineData("通常の整数演算で6−2=4と確認できる。", "Reject", true)]
    public async Task EquationCheckCannotRepairAnInvalidCitationOrReplaceExistingRejections(string citation, string vote, bool valid)
    {
        const string observation = "通常の整数演算で6−2=4と確認できる。";
        int calls = 0;
        using var evaluator = new BonsaiCtgCouncilEvaluator((_, _, _) => Task.FromResult(ReadingAnswer(++calls switch
        {
            1 => "Show the proposed result.",
            2 or 3 => JsonSerializer.Serialize(new { evidence=citation, vote, reason="Assessment from the source." }),
            _ => """{"reason":"A report was requested.","vote":"Approve"}"""
        })), Rules(), responseMode:BonsaiCtgResponseMode.JointReportVotes,
            select:(_, _, _, _) => throw new InvalidOperationException("Unexpected second opinion"));
        var needs = new BonsaiCtgEvidenceRequirements(new Dictionary<string,BonsaiCtgEvidenceAvailability> { ["evidence"]=BonsaiCtgEvidenceAvailability.Available });
        var result = await evaluator.EvaluateProposalWithEvidenceGateAsync(Request() with
            { InputFacts=new Dictionary<string,string>{["evidence"]=observation} }, "6−2の答えとして7を表示する。", needs, TestContext.Current.CancellationToken);
        Assert.Equal(valid,result.Evaluation.Succeeded);
        Assert.Equal(valid ? null : "ctg.bonsai.invalid_vote",result.Evaluation.ErrorCode);
        Assert.False(result.Gate.Accepted);
        Assert.DoesNotContain("Logos.challenge.response.sha256", result.Evaluation.Metadata.Keys);
    }
}
