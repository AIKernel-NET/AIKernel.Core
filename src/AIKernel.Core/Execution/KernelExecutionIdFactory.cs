namespace AIKernel.Core.Execution;

using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Enums;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutionIdFactory']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutionIdFactory']" />
public sealed class KernelExecutionIdFactory
{
    private readonly SemanticStateHasher _semanticStateHasher = new();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateExecutionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateExecutionId']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateExecutionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateExecutionId']" />
    public Result<string> TryCreateExecutionId(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        return CreateExecutionIdResult(
            request,
            status,
            promptHash,
            resultDiscriminator,
            startedAt,
            executionSequence);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateFallbackExecutionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateFallbackExecutionId']" />
    public string CreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Unwrap(CreateFallbackExecutionIdResult(request, status));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateFallbackExecutionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateFallbackExecutionId']" />
    public Result<string> TryCreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        return CreateFallbackExecutionIdResult(request, status);
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
