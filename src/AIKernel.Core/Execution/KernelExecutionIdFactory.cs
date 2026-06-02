namespace AIKernel.Core.Execution;

using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

public sealed class KernelExecutionIdFactory
{
    private readonly SemanticStateHasher _semanticStateHasher = new();

    public string CreateExecutionId(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        ArgumentNullException.ThrowIfNull(request);

        var material = SemanticStateMaterial.FromKernelExecution(
            request,
            status,
            promptHash,
            resultDiscriminator,
            startedAt,
            executionSequence);

        return _semanticStateHasher
            .ComputeHash(material)
            .ToExecutionId();
    }

    public string CreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);

        var material = SemanticStateMaterial.FromKernelFallback(request, status);

        return _semanticStateHasher
            .ComputeHash(material)
            .ToExecutionId();
    }
}
