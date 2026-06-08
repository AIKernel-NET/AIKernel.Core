namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Routing;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PassThroughOutputPolisher']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PassThroughOutputPolisher']" />
public sealed class PassThroughOutputPolisher : IOutputPolisher
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PassThroughOutputPolisher.new']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PassThroughOutputPolisher.new']" />
    public ModelCapacityVector RequiredCapacity { get; } = new(
        structuralIntegrity: 1,
        linguisticFluidity: 0,
        reasoningDepth: 0,
        fidelity: 1,
        latencyPerformance: 1);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PassThroughOutputPolisher.RenderAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PassThroughOutputPolisher.RenderAsync']" />
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
