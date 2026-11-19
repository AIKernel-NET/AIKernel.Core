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
        => Assert.Null(BonsaiCtgJointReport.Extract(proposal));

    [Theory]
    [InlineData("-12", "+8")]
    [InlineData("07", "7")]
    public async Task JointReportKeepsOperandsOnTheProposalSideAndCouncilsOwnTheirVotes(string first, string second)
    {
        string proposal = $"State that register R holds both value {first} and value {second} simultaneously.";
        var facts = new Dictionary<string,string> { ["evidence"] = "The register holds one signed integer." };
        var original = await RunComparisonTrial(BonsaiCtgResponseMode.ConciseReportRetryVotes, proposal, facts);
        var trial = await RunComparisonTrial(BonsaiCtgResponseMode.JointReportVotes, proposal, facts);
        Assert.Equal(4, trial.Calls.Count);
        Assert.Equal(original.Calls[0], trial.Calls[0]);
        Assert.Equal(original.Calls[3], trial.Calls[3]);
        using var data = JsonDocument.Parse(BonsaiCtgJointReport.Extract(proposal)!);
        Assert.Equal("register R", data.RootElement.GetProperty("subject").GetString());
        Assert.Equal(first, data.RootElement.GetProperty("firstClaim").GetString());
        Assert.Equal(second, data.RootElement.GetProperty("secondClaim").GetString());
        foreach (var (_, user) in trial.Calls.Skip(1).Take(2))
        {
            Assert.Contains(data.RootElement.GetRawText(), user);
            Assert.DoesNotContain("NONBINDING ACTION READING", user);
            Assert.Equal(original.Calls[1].User.Split("ORIGINAL CANDIDATE")[0], user.Split("ORIGINAL CANDIDATE")[0]);
        }
        Assert.True(trial.Result.Evaluation.Succeeded);
        Assert.True(trial.Result.Gate.Accepted); // A contextual prompt cannot replace the model's vote.
    }
}
