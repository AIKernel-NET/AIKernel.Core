namespace AIKernel.Core.Tests.Execution;

using AIKernel.Core.Execution;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using Xunit;

public sealed class OutputPolisherTests
{
    [Fact]
    public async Task PassThroughOutputPolisher_RendersSerializedLogic()
    {
        var polisher = new PassThroughOutputPolisher();

        var output = await polisher.RenderAsync(
            new RawLogic("logic"),
            new ExpressionContext(new ExpressionBuffer()),
            TestContext.Current.CancellationToken);

        Assert.Equal("logic", output);
        Assert.Equal(1, polisher.RequiredCapacity.StructuralIntegrity);
        Assert.Equal(1, polisher.RequiredCapacity.Fidelity);
    }

    [Fact]
    public async Task DefaultPolisherValidator_ValidatesPreservedLogic()
    {
        var validator = new DefaultPolisherValidator();

        var result = await validator.ValidateLogicPreservationAsync(
            new RawLogic("logic\r\n"),
            "logic",
            TestContext.Current.CancellationToken);
        var divergence = await validator.AnalyzeDivergenceAsync(
            new RawLogic("logic"),
            "logic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.LogicIntegrityScore);
        Assert.Empty(result.Violations);
        Assert.False(divergence.DivergenceDetected);
        Assert.Equal("none", divergence.Severity);
    }

    [Fact]
    public async Task DefaultPolisherValidator_FailsClosedForDivergentOutput()
    {
        var validator = new DefaultPolisherValidator();

        var result = await validator.ValidateLogicPreservationAsync(
            new RawLogic("logic"),
            "changed",
            TestContext.Current.CancellationToken);
        var divergence = await validator.AnalyzeDivergenceAsync(
            new RawLogic("logic"),
            "changed",
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.LogicIntegrityScore);
        Assert.Equal(["logic_divergence"], result.Violations);
        Assert.True(divergence.DivergenceDetected);
        Assert.Equal("fail_closed", divergence.Severity);
        Assert.Equal(["serialized_representation"], divergence.AlteredSegments);
    }
}
