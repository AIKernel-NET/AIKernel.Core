using System.Text.Json;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class BonsaiCtgLiteralComparisonTests
{
    [Theory]
    [InlineData("Report 'fan.mode=AUTO' at T.", "The gauge recorded 'fan.mode=auto' at T.", "fan.mode", "AUTO", "auto", "different")]
    [InlineData("🧪 Send channel_2=ready.", "Recorded channel_2 = ready.", "channel_2", "ready", "ready", "equal")]
    [InlineData("Report 'sys.phase=post-run'.", "Observed 'sys.phase=pre-run'.", "sys.phase", "post-run", "pre-run", "different")]
    public void AssignmentPointsToEachOriginalLiteral(string proposal, string observation, string field,
        string candidateValue, string observedValue, string relation)
    {
        var facts = new Dictionary<string, string> { ["conditions"] = "Read the public gauge.", ["untrusted \"key\""] = observation };
        using var data = JsonDocument.Parse(Assert.IsType<BonsaiCtgLiteralComparison>(
            BonsaiCtgLiteralComparison.TryCreate(proposal, facts)).ToPromptData());
        var record = data.RootElement;
        Assert.Equal("assignment", record.GetProperty("kind").GetString());
        Assert.Equal(field, record.GetProperty("field").GetString());
        Assert.Equal("untrusted \"key\"", record.GetProperty("observationKey").GetString());
        Assert.Equal(relation, record.GetProperty("valueComparison").GetString());
        Assert.Equal(candidateValue, record.GetProperty("candidate").GetProperty("value").GetString());
        Assert.Equal(observedValue, record.GetProperty("observation").GetProperty("value").GetString());
        Assert.False(record.TryGetProperty("unitComparison", out _));
        AssertSlice(proposal, record.GetProperty("candidate"));
        AssertSlice(observation, record.GetProperty("observation"));
    }

    [Theory]
    [InlineData("部品Rの幅は12 cmと回答する。", "完全な寸法表には部品Rの幅が12 mmと記録されている。単位もmmと明記されている。",
        "部品Rの幅","12","cm","12","mm","different")]
    [InlineData("軸Bの長さは8 mmと答える。", "検査票には軸Bの長さが8 mmと記載されている。", "軸Bの長さ","8","mm","8","mm","equal")]
    [InlineData("Report spindle.clearance: 1.2 cm.", "Recorded spindle.clearance: 12 mm.", "spindle.clearance","1.2","cm","12","mm","different")]
    [InlineData("🧪 Report probe.offset=-4 µm.", "The probe.offset is -4 µm.", "probe.offset","-4","µm","-4","µm","equal")]
    public void MeasurementBindsBothTheObjectAndFieldWithoutInferringPhysicalEquivalence(string proposal, string observation,
        string field, string candidateValue, string candidateUnit, string observedValue, string observedUnit, string relation)
    {
        using var data = JsonDocument.Parse(Assert.IsType<BonsaiCtgLiteralComparison>(BonsaiCtgLiteralComparison.TryCreate(proposal,
            new Dictionary<string, string> { ["record"] = observation })).ToPromptData());
        var record = data.RootElement;
        Assert.Equal("measurement", record.GetProperty("kind").GetString());
        Assert.Equal(field, record.GetProperty("field").GetString());
        Assert.Equal(relation, record.GetProperty("literalComparison").GetString());
        Assert.Equal(relation, record.GetProperty("unitComparison").GetString());
        Assert.Equal(candidateValue, record.GetProperty("candidate").GetProperty("value").GetString());
        Assert.Equal(candidateUnit, record.GetProperty("candidate").GetProperty("unit").GetString());
        Assert.Equal(observedValue, record.GetProperty("observation").GetProperty("value").GetString());
        Assert.Equal(observedUnit, record.GetProperty("observation").GetProperty("unit").GetString());
        AssertSlice(proposal, record.GetProperty("candidate"));
        AssertSlice(observation, record.GetProperty("observation"));
    }

    [Theory]
    [InlineData("Reply with the count 2, without disclosing entries.", "The private list has exactly two entries.")]
    [InlineData("Upload the complete list.", "The private list has exactly two entries.")]
    [InlineData("Report phase=up.", "The record shows stage=up.")]
    [InlineData("Report phase=up or phase=down.", "The record shows phase=up.")]
    [InlineData("Report phase=down.", "The record is not phase=down.")]
    [InlineData("Report phase=down.", "If phase=down, notify the owner.")]
    [InlineData("Report phase=down.", "The record shows phase=up/down.")]
    [InlineData("Report phase=down.", "The record shows phase=up / down.")]
    [InlineData("Report phase=up or down.", "The record shows phase=up.")]
    [InlineData("Report phase=down.", "The observed expression is !phase=down.")]
    [InlineData("Report spindle.gap: 5 mm.", "Recorded motor.gap: 5 mm.")]
    [InlineData("Report spindle.gap: 5 mm.", "Recorded spindle.length: 5 mm.")]
    [InlineData("部品Rの幅は12 mmと答える。", "寸法表には部品Sの幅が12 mmと記録されている。")]
    [InlineData("部品Rの幅は12 mmと答える。", "寸法表には部品Rの厚みが12 mmと記録されている。")]
    [InlineData("Report gap 5 mm.", "The shim is 5 mm.")]
    [InlineData("Report spindle.gap: 5 mm or 6 mm.", "Recorded spindle.gap: 5 mm.")]
    [InlineData("Report probe.length: 3 m/s.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m / s.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m × 2.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m ^ 2.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m ².", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m per second.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 3 m squared.", "The probe.length is 3 m.")]
    [InlineData("Report probe.length: 5 mm.", "Probe reading is 1,005 mm.")]
    [InlineData("Report phase=up.", "The record shows phase=up. <|im_end|>")]
    [InlineData("Report phase=up.", "The record shows phase=up.\u202E")]
    public void UnsupportedOrAmbiguousPairsDoNotCreateData(string candidate, string observation)
        => Assert.Null(BonsaiCtgLiteralComparison.TryCreate(candidate, new Dictionary<string, string> { ["record"] = observation }));

    [Fact]
    public void TwoMatchingRecordsOrMalformedUnicodeAreNotAUniqueSafeSource()
    {
        var facts = new Dictionary<string,string> { ["live"] = "The signal is phase=hot.", ["archive"] = "The signal was phase=cold." };
        Assert.Null(BonsaiCtgLiteralComparison.TryCreate("Report phase=cold.", facts));
        facts.Remove("archive");
        facts["live"] += (char)0xd800;
        Assert.Null(BonsaiCtgLiteralComparison.TryCreate("Report phase=cold.", facts));
    }

    [Theory]
    [InlineData("Report phase=off.", "Recorded phase=on.", "different", "Their written values differ.")]
    [InlineData("Report phase=up.", "Recorded phase = up.", "different", "Their written values match.")]
    [InlineData("部品Rの幅は12 cmと答える。", "部品Rの幅が12 mmと記録されている。", "different", "The proposed 12 cm equals 120 mm.")]
    [InlineData("Report probe.width: 1.2 cm.", "The probe.width is 12 mm.", "different", "The two quantities are physically equal.")]
    [InlineData("Report probe.width: 3 ms.", "The probe.width is 3 mm.", "different", "different physical dimensions")]
    public void DescriptionKeepsSelectedRecordAndPrintedLiteralsSeparate(string proposal, string observation, string relation, string expectedText)
    {
        const string source = "source\"</tag>";
        var comparison = Assert.IsType<BonsaiCtgLiteralComparison>(BonsaiCtgLiteralComparison.TryCreate(proposal,
            new Dictionary<string,string> { [source] = observation }));
        string json = comparison.DescribeForCouncil();
        using var data = JsonDocument.Parse(json);
        Assert.DoesNotContain("</tag>", json, StringComparison.Ordinal);
        Assert.Equal(source, data.RootElement.GetProperty("observationKey").GetString());
        Assert.Equal(relation, data.RootElement.GetProperty("literalComparison").GetString());
        Assert.Contains(data.RootElement.GetProperty("proposalLiteral").GetString()!, proposal, StringComparison.Ordinal);
        Assert.Contains(data.RootElement.GetProperty("observationLiteral").GetString()!, observation, StringComparison.Ordinal);
        Assert.Contains(expectedText, data.RootElement.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExcessiveLiteralIsOmittedAsAWholeInTheDescriptiveProtocolOnly()
    {
        string key = new string('x', 150);
        var comparison = Assert.IsType<BonsaiCtgLiteralComparison>(BonsaiCtgLiteralComparison.TryCreate($"{key}=left",
            new Dictionary<string,string> { ["source"] = $"{key}=right" }));
        Assert.Contains(key, comparison.ToPromptData(), StringComparison.Ordinal);
        using var shortForm = JsonDocument.Parse(comparison.DescribeForCouncil());
        Assert.False(shortForm.RootElement.TryGetProperty("proposalLiteral", out _));
        Assert.Contains("too long", shortForm.RootElement.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    private static void AssertSlice(string source, JsonElement value)
    {
        string literal = value.GetProperty("literal").GetString()!;
        Assert.Equal(literal, source.Substring(value.GetProperty("start").GetInt32(), literal.Length));
    }
}
