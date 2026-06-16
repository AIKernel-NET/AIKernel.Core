namespace AIKernel.Core.Execution;

using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Enums;

/// <summary>EN: Documentation for public API. JA: SemanticStateMaterial を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateMaterial']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateMaterial']/summary" />
public sealed record SemanticStateMaterial
{
    private SemanticStateMaterial(
        string domain,
        string canonicalPayload)
    {
        Domain = string.IsNullOrWhiteSpace(domain)
            ? throw new ArgumentException("Domain is required.", nameof(domain))
            : domain;

        CanonicalPayload = canonicalPayload
            ?? throw new ArgumentNullException(nameof(canonicalPayload));
    }

    /// <summary>EN: Documentation for public API. JA: Domain を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.SemanticStateMaterial.Domain']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.SemanticStateMaterial.Domain']/summary" />
    public string Domain { get; }

    /// <summary>EN: Documentation for public API. JA: CanonicalPayload を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.SemanticStateMaterial.CanonicalPayload']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.SemanticStateMaterial.CanonicalPayload']/summary" />
    public string CanonicalPayload { get; }

    /// <summary>EN: Documentation for public API. JA: FromKernelExecution を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.FromKernelExecution']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.FromKernelExecution']/summary" />
    public static SemanticStateMaterial FromKernelExecution(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        return Unwrap(
            CreateKernelExecutionResult(
                request,
                status,
                promptHash,
                resultDiscriminator,
                startedAt,
                executionSequence));
    }

    /// <summary>EN: Documentation for public API. JA: CreateKernelExecutionResult を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.CreateKernelExecutionResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.CreateKernelExecutionResult']/summary" />
    public static Result<SemanticStateMaterial> CreateKernelExecutionResult(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        if (request is null)
        {
            return Result<SemanticStateMaterial>.Fail(SemanticHashError("KernelExecutionRequest is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ContextSnapshotId))
        {
            return Result<SemanticStateMaterial>.Fail(SemanticHashError("ContextSnapshotId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ContextHash))
        {
            return Result<SemanticStateMaterial>.Fail(SemanticHashError("ContextHash is required."));
        }

        // Phase-2 SemanticStateHash boundary:
        // ContextSnapshot and prompt material are canonicalized here before hashing.
        // ProviderInput, PromptRules, and VFS Snapshot can be appended here without
        // changing KernelExecutor or the public execution contract.
        var payload = string.Join(
            '\n',
            request.ContextHash,
            request.ContextSnapshotId,
            request.RequestedModelId ?? string.Empty,
            promptHash,
            status.ToString(),
            resultDiscriminator);

        return Result<SemanticStateMaterial>.Success(new SemanticStateMaterial(
            "kernel.execution",
            payload));
    }

    /// <summary>EN: Documentation for public API. JA: FromKernelFallback を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.FromKernelFallback']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.FromKernelFallback']/summary" />
    public static SemanticStateMaterial FromKernelFallback(
        KernelRequest request,
        ExecutionStatus status)
    {
        return Unwrap(CreateKernelFallbackResult(request, status));
    }

    /// <summary>EN: Documentation for public API. JA: CreateKernelFallbackResult を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.CreateKernelFallbackResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateMaterial.CreateKernelFallbackResult']/summary" />
    public static Result<SemanticStateMaterial> CreateKernelFallbackResult(
        KernelRequest request,
        ExecutionStatus status)
    {
        if (request is null)
        {
            return Result<SemanticStateMaterial>.Fail(SemanticHashError("KernelRequest is required."));
        }

        // Phase-2 SemanticStateHash boundary:
        // This path runs before ContextSnapshot exists, so the KernelRequest
        // contract is the canonical fallback state.
        var payload = string.Join(
            '\n',
            request.Input ?? string.Empty,
            request.RootRomId?.Value ?? string.Empty,
            request.VfsProviderId ?? string.Empty,
            request.ParentSnapshotId ?? string.Empty,
            request.RequestedModelId ?? string.Empty,
            status.ToString());

        return Result<SemanticStateMaterial>.Success(new SemanticStateMaterial(
            "kernel.fallback",
            payload));
    }

    private static SemanticStateMaterial Unwrap(
        Result<SemanticStateMaterial> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);

    private static ErrorContext SemanticHashError(string message)
    {
        return new ErrorContext(message, "ERROR", IsRetryable: false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.SemanticHash,
            SemanticSlot = SemanticSlot.B
        };
    }
}
