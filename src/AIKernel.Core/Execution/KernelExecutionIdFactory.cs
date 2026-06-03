namespace AIKernel.Core.Execution;

using AIKernel.Common.Results;
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

        return Unwrap(
            CreateExecutionIdResult(
                request,
                status,
                promptHash,
                resultDiscriminator,
                startedAt,
                executionSequence));
    }

    public string CreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Unwrap(CreateFallbackExecutionIdResult(request, status));
    }

    internal Result<string> CreateExecutionIdResult(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        return
            from material in SemanticStateMaterial.CreateKernelExecutionResult(
                request,
                status,
                promptHash,
                resultDiscriminator,
                startedAt,
                executionSequence)
            let hash = _semanticStateHasher.ComputeHash(material)
            select hash.ToExecutionId();
    }

    internal Result<string> CreateFallbackExecutionIdResult(
        KernelRequest request,
        ExecutionStatus status)
    {
        return
            from material in SemanticStateMaterial.CreateKernelFallbackResult(request, status)
            let hash = _semanticStateHasher.ComputeHash(material)
            select hash.ToExecutionId();
    }

    private static string Unwrap(Result<string> result)
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw new InvalidOperationException(result.Error!.Message);
    }
}
