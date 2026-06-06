namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Routing;

public sealed class PassThroughOutputPolisher : IOutputPolisher
{
    public ModelCapacityVector RequiredCapacity { get; } = new(
        structuralIntegrity: 1,
        linguisticFluidity: 0,
        reasoningDepth: 0,
        fidelity: 1,
        latencyPerformance: 1);

    public Task<string> RenderAsync(
        RawLogic logic,
        ExpressionContext expressionContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(logic);

        return Task.FromResult(logic.SerializedRepresentation ?? string.Empty);
    }
}
