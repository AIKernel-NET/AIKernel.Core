using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class BonsaiCtgQuantityRelationTests
{
    [Theory]
    [InlineData("12", "cm", "12", "mm", "different", "120")]
    [InlineData("1.2", "cm", "12", "mm", "equal", "12")]
    [InlineData("12", "mm", "1.2", "cm", "equal", "1.2")]
    [InlineData("0.0000001", "s", "100", "ns", "equal", "100")]
    [InlineData("-0.00120", "m", "-1200.00", "µm", "equal", "-1200")]
    [InlineData("-1.20", "μm", "-1.2", "um", "equal", "-1.2")]
    [InlineData("+0000.0000", "kg", "-0.0", "mg", "equal", "0")]
    [InlineData("1", "Pa", "1", "mPa", "different", "1000")]
    [InlineData("20", "°C", "20.0", "°C", "equal", "20")]
    [InlineData("1000000000000000000000000000000000000001", "mm", "100000000000000000000000000000000000.0001", "m", "different", "1000000000000000000000000000000000000.001")]
    [InlineData("0.000000000000000000000000000000000000001", "ns", "0", "s", "different", "0.000000000000000000000000000000000000000000000001")]
    public void ComparesBoundedDecimalMeasurementsExactly(string left, string leftUnit, string right, string rightUnit, string relation, string converted)
    {
        var result = Assert.IsType<BonsaiCtgQuantityRelation>(BonsaiCtgQuantityRelation.TryCompare(left, leftUnit, right, rightUnit));
        Assert.Equal(relation, result.Relation);
        Assert.Equal(converted, result.CandidateInObservationUnit);
        Assert.Contains(left + " " + leftUnit, result.Description, StringComparison.Ordinal);
        Assert.Contains(right + " " + rightUnit, result.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3", "mm", "3", "ms")]
    [InlineData("2", "V", "2", "A")]
    [InlineData("0", "°C", "0", "g")]
    public void RecognisedIncompatibleDimensionsCannotBeConverted(string a, string au, string b, string bu)
    {
        var result = Assert.IsType<BonsaiCtgQuantityRelation>(BonsaiCtgQuantityRelation.TryCompare(a, au, b, bu));
        Assert.Equal("incompatible_dimensions", result.Relation);
        Assert.Null(result.CandidateInObservationUnit);
    }

    [Theory]
    [InlineData("1e3","mm")]
    [InlineData("12,000","mm")]
    [InlineData("１２","mm")]
    [InlineData(".12","mm")]
    [InlineData("12.","mm")]
    [InlineData("--1","mm")]
    [InlineData("12345678901234567890123456789012345678901","mm")]
    [InlineData("1","m/s")]
    [InlineData("1","°F")]
    [InlineData("1","K")]
    [InlineData("1","MM")]
    [InlineData("1","m²")]
    [InlineData("1","bogus")]
    public void UnsupportedOrAmbiguousValuesProduceNoPhysicalFinding(string value, string unit)
        => Assert.Null(BonsaiCtgQuantityRelation.TryCompare(value, unit, "1", "m"));
}
