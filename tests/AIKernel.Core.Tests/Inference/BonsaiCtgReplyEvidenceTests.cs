using AIKernel.Enums.Governance;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed partial class BonsaiCtgTests
{
    [Theory]
    [InlineData("Give a summary 'Snow is blue.'", "summary", "Snow is blue.")]
    [InlineData("原文の『Hello』の日本語訳として『こんにちは』と回答する。", "translation", "こんにちは")]
    [InlineData("State that the meter reading at noon is 9.", "measurement report", "9")]
    public void ReplyFieldsRemainPartOfTheCandidate(string proposal, string operation, string output)
    {
        var facts = new Dictionary<string, string> {
            ["evidence"] = "The original record is disputed.", ["authority"] = "The owner denied this request.",
            ["effects"] = "A third party will see the reply."
        };
        var fields = Assert.IsType<BonsaiCtgReplyEvidence>(BonsaiCtgReplyEvidence.Read(proposal, facts));
        Assert.Equal(operation, fields.Operation);
        Assert.Equal(output, fields.Output);
        Assert.False(fields.AssessedBy(CouncilKind.Pathos));
        var prompt = fields.Build(Rules()[CouncilKind.Ethos], CouncilKind.Ethos, facts, proposal);
        Assert.Contains(facts["authority"], prompt.User);
        Assert.Contains(facts["effects"], prompt.User);
        Assert.DoesNotContain(proposal, prompt.System);
        Assert.Contains("Fail-Closed", prompt.Excerpt);
        Assert.Null(BonsaiCtgReplyEvidence.Read(proposal + " Then publish the source.", facts));
        facts.Remove("evidence");
        Assert.Null(BonsaiCtgReplyEvidence.Read(proposal, facts));
    }

    [Theory]
    [InlineData("The gauge was measured as 4 at timestamp T.", true)]
    [InlineData("The gauge was measured as 4 at timestamp U.", false)]
    [InlineData("The meter was measured as 4 at timestamp T.", false)]
    [InlineData("The gauge was measured as 5 at timestamp T.", false)]
    public void MeasurementComparisonRequiresTheSameFieldAndTime(string evidence, bool different)
    {
        var facts = new Dictionary<string, string> {
            ["evidence"] = evidence, ["authority"] = "Display the requested reading.", ["effects"] = "Local answer."
        };
        var reply = Assert.IsType<BonsaiCtgReplyEvidence>(BonsaiCtgReplyEvidence.Read("Report that the gauge value at T is 5.", facts));
        Assert.False(reply.AssessedBy(CouncilKind.Logos));
        Assert.Equal(different ? "4" : null, reply.DifferentRecordedValue);
    }

    [Theory]
    [InlineData("2030/03/29", true)]
    [InlineData("2030/03/28", false)]
    [InlineData("2030-03-29", false)]
    public void DateFormattingRequiresTheRequestedLiteralReplacement(string output, bool eligible)
    {
        var facts = new Dictionary<string, string> {
            ["evidence"] = "The input is 2030-03-29.", ["authority"] = "Use slashes.", ["effects"] = "Local reply."
        };
        var action = $"Display {output} by replacing the hyphens in the date 2030-03-29 with slashes.";
        Assert.Equal(eligible, BonsaiCtgReplyEvidence.Read(action, facts) is not null);
        facts["evidence"] += " Another possible input is 2030-03-28.";
        Assert.Null(BonsaiCtgReplyEvidence.Read(action, facts));
    }

    [Fact]
    public void BackTranslationIsBlindToTheExpectedSourceAndCannotCastAVote()
    {
        var facts = new Dictionary<string,string> { ["evidence"] = "The source is Good evening.",
            ["authority"] = "The user requested a translation.", ["effects"] = "Screen only." };
        const string proposal = "原文の『Good evening』の日本語訳として『こんばんは』と回答する。";
        var reply = Assert.IsType<BonsaiCtgReplyEvidence>(BonsaiCtgReplyEvidence.Read(proposal, facts));
        var reading = reply.BackTranslation(facts)!.Value;
        Assert.Contains("こんばんは", reading.User);
        Assert.DoesNotContain("Good evening", reading.User);
        Assert.DoesNotContain("Good evening", reading.System);
        var rule = Rules()[CouncilKind.Logos];
        var matching = reply.WithBackTranslation(ReadingAnswer("Good evening."));
        Assert.NotEqual(reply.Build(rule, CouncilKind.Logos, facts, proposal).User, matching.Build(rule, CouncilKind.Logos, facts, proposal).User);
        var quoted = reply.WithBackTranslation(ReadingAnswer("\"Good evening\""));
        Assert.Equal(matching.Build(rule, CouncilKind.Logos, facts, proposal), quoted.Build(rule, CouncilKind.Logos, facts, proposal));
        var qualified = reply.WithBackTranslation(ReadingAnswer("\"Good evening\" is incorrect."));
        Assert.Equal(reply.Build(rule, CouncilKind.Logos, facts, proposal), qualified.Build(rule, CouncilKind.Logos, facts, proposal));
        var different = reply.WithBackTranslation(ReadingAnswer("It is not a good evening."));
        Assert.Equal(reply.Build(rule, CouncilKind.Logos, facts, proposal), different.Build(rule, CouncilKind.Logos, facts, proposal));
        facts["evidence"] = "The source is missing.";
        Assert.Null(reply.BackTranslation(facts));
    }

    [Theory]
    [InlineData(" That statement has been withdrawn.", true)]
    [InlineData(" That statement has not been withdrawn.", false)]
    [InlineData(" Unrelated notice: the sale has been withdrawn.", false)]
    public void AWithdrawalMustReferToTheQuotedStatement(string next, bool withdrawn)
    {
        var facts = new Dictionary<string,string> {
            ["evidence"] = "The notice read: The pool is open." + next,
            ["authority"] = "Give the current status.", ["effects"] = "Screen only."
        };
        var reply = Assert.IsType<BonsaiCtgReplyEvidence>(BonsaiCtgReplyEvidence.Read("Give a summary 'The pool is open.'", facts));
        Assert.Equal(withdrawn, reply.Withdrawal is not null);
        Assert.Equal(!withdrawn, reply.AssessedBy(CouncilKind.Logos));
    }
}
