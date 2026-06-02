namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

internal sealed class KernelExecutionFailureResultFactory
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionIdFactory _executionIdFactory = new();

    public KernelExecutionFailureResultFactory(IKernelClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Result<KernelRequestExecutionResult> CreateFailedResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        string code,
        string message,
        string? detail = null)
    {
        return CreateFailureResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            ExecutionStatus.Failed,
            resultDiscriminator: code,
            errorCode: code,
            errorMessage: message,
            errorDetail: detail);
    }

    public Result<KernelRequestExecutionResult> CreateCanceledResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        return CreateFailureResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            ExecutionStatus.Canceled,
            resultDiscriminator: "canceled",
            errorCode: "canceled",
            errorMessage: "Execution was canceled.",
            errorDetail: null);
    }

    public KernelRequestExecutionResult Resolve(
        Result<KernelRequestExecutionResult> result)
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        return new KernelRequestExecutionResult
        {
            ExecutionId = "exec:failed:" + result.Error!.Code.ToLowerInvariant(),
            Status = ExecutionStatus.Failed,
            ProviderId = "unknown",
            ModelId = "unknown",
            ContextSnapshotId = "unknown",
            ContextHash = "unknown",
            PromptHash = string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0),
            Error = new ExecutionError(
                Code: result.Error.Code.ToLowerInvariant(),
                Message: result.Error.Message),
            StartedAtUtc = _clock.Now,
            CompletedAtUtc = _clock.Now,
            Metadata = ImmutableDictionary<string, string>.Empty
        };
    }

    private Result<KernelRequestExecutionResult> CreateFailureResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        ExecutionStatus status,
        string resultDiscriminator,
        string errorCode,
        string errorMessage,
        string? errorDetail)
    {
        var executionId = _executionIdFactory.CreateExecutionIdResult(
            request,
            status,
            prompt?.PromptHash ?? string.Empty,
            resultDiscriminator,
            startedAt,
            executionSequence);

        return Result<KernelRequestExecutionResult>.Success(new KernelRequestExecutionResult
        {
            ExecutionId = ResolveExecutionId(executionId),
            Status = status,
            ProviderId = capability?.ProviderId ?? "unknown",
            ModelId = capability?.ModelId ?? request.RequestedModelId ?? "unknown",
            ContextSnapshotId = GetContextSnapshotId(request),
            ContextHash = GetContextHash(request),
            PromptHash = prompt?.PromptHash ?? string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: prompt?.EstimatedInputTokens ?? 0,
                OutputTokens: 0,
                TotalTokens: prompt?.EstimatedInputTokens ?? 0),
            Error = new ExecutionError(
                Code: errorCode,
                Message: errorMessage,
                Detail: errorDetail),
            StartedAtUtc = startedAt,
            CompletedAtUtc = _clock.Now,
            Metadata = ImmutableDictionary<string, string>.Empty
        });
    }

    private static string ResolveExecutionId(Result<string> executionId)
    {
        if (executionId.IsSuccess)
        {
            return executionId.Value!;
        }

        return "exec:failed:" + executionId.Error!.Code.ToLowerInvariant();
    }

    private static string GetContextSnapshotId(KernelExecutionRequest request)
    {
        return request.ContextSnapshot?.SnapshotId ?? "unknown";
    }

    private static string GetContextHash(KernelExecutionRequest request)
    {
        return request.ContextSnapshot?.ContextHash ?? "unknown";
    }
}
