namespace AIKernel.Core.Execution;

using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Enums;

/// <summary>[EN] Documents this public package API member. [JA] KernelExecutionIdFactory を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutionIdFactory']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutionIdFactory']/summary" />
public sealed class KernelExecutionIdFactory
{
    private readonly SemanticStateHasher _semanticStateHasher = new();

    /// <summary>[EN] Documents this public package API member. [JA] CreateExecutionId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateExecutionId']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] TryCreateExecutionId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateExecutionId']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] CreateFallbackExecutionId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateFallbackExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.CreateFallbackExecutionId']/summary" />
    public string CreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Unwrap(CreateFallbackExecutionIdResult(request, status));
    }

    /// <summary>[EN] Documents this public package API member. [JA] TryCreateFallbackExecutionId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateFallbackExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutionIdFactory.TryCreateFallbackExecutionId']/summary" />
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
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}
