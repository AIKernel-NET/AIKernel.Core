namespace AIKernel.Core.Execution;

using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

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

    public string Domain { get; }

    public string CanonicalPayload { get; }

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

        if (request.ContextSnapshot is null)
        {
            return Result<SemanticStateMaterial>.Fail(SemanticHashError("ContextSnapshot is required."));
        }

        // Phase-2 SemanticStateHash boundary:
        // ContextSnapshot and prompt material are canonicalized here before hashing.
        // ProviderInput, PromptRules, and VFS Snapshot can be appended here without
        // changing KernelExecutor or the public execution contract.
        var payload = string.Join(
            '\n',
            request.ContextSnapshot.ContextHash,
            request.ContextSnapshot.SnapshotId,
            request.RequestedModelId ?? string.Empty,
            promptHash,
            status.ToString(),
            resultDiscriminator,
            startedAt.Ticks.ToString("D20"),
            executionSequence.ToString("D16"));

        return Result<SemanticStateMaterial>.Success(new SemanticStateMaterial(
            "kernel.execution",
            payload));
    }

    public static SemanticStateMaterial FromKernelFallback(
        KernelRequest request,
        ExecutionStatus status)
    {
        return Unwrap(CreateKernelFallbackResult(request, status));
    }

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
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw new InvalidOperationException(result.Error!.Message);
    }

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
