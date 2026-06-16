namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;

internal sealed class KernelExecutionFailureResultFactory
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionIdFactory _executionIdFactory = new();
    /// <summary>
    /// EN: Executes KernelExecutionFailureResultFactory.
    /// [EN] Documents this public package API member. [JA] KernelExecutionFailureResultFactory を実行します。
    /// </summary>

    public KernelExecutionFailureResultFactory(IKernelClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    /// <summary>
    /// EN: Gets CreateFailedResult.
    /// [EN] Documents this public package API member. [JA] CreateFailedResult を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets CreateFailedResult.
    /// [EN] Documents this public package API member. [JA] CreateFailedResult を取得します。
    /// </summary>

    public Result<KernelRequestExecutionResult> CreateFailedResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        ErrorContext error)
    {
        return CreateFailureResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            ExecutionStatus.Failed,
            resultDiscriminator: error.Code.ToLowerInvariant(),
            errorCode: error.Code.ToLowerInvariant(),
            errorMessage: error.Message,
            errorDetail: null,
            error);
    }
    /// <summary>
    /// EN: Gets CreateCanceledResult.
    /// [EN] Documents this public package API member. [JA] CreateCanceledResult を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets CreateCanceledResult.
    /// [EN] Documents this public package API member. [JA] CreateCanceledResult を取得します。
    /// </summary>

    public Result<KernelRequestExecutionResult> CreateCanceledResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        ErrorContext error)
    {
        return CreateFailureResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            ExecutionStatus.Canceled,
            resultDiscriminator: error.Code.ToLowerInvariant(),
            errorCode: error.Code.ToLowerInvariant(),
            errorMessage: error.Message,
            errorDetail: null,
            error);
    }
    /// <summary>
    /// EN: Gets Resolve.
    /// [EN] Documents this public package API member. [JA] Resolve を取得します。
    /// </summary>

    public KernelRequestExecutionResult Resolve(
        Result<KernelRequestExecutionResult> result)
        => result.Match(
            error => new KernelRequestExecutionResult
        {
            ExecutionId = "exec:failed:" + error.Code.ToLowerInvariant(),
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
                Code: error.Code.ToLowerInvariant(),
                Message: error.Message),
            StartedAtUtc = _clock.Now,
            CompletedAtUtc = _clock.Now,
            Metadata = BuildFailureMetadata(error)
        },
            value => value);

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
        string? errorDetail,
        ErrorContext? errorContext = null)
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
            Metadata = BuildFailureMetadata(errorContext)
        });
    }

    private static ImmutableDictionary<string, string> BuildFailureMetadata(
        ErrorContext? errorContext)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (errorContext?.Metadata is not null)
        {
            foreach (var item in errorContext.Metadata)
            {
                builder[item.Key] = item.Value;
            }
        }

        if (errorContext?.FailureKind is { } failureKind)
        {
            builder[ReplayMetadataKeys.FailureKind] = failureKind.ToString();
        }

        if (errorContext?.OriginStep is { } originStep)
        {
            builder[ReplayMetadataKeys.OriginStep] = originStep.ToString();
        }

        if (errorContext?.SemanticSlot is { } semanticSlot)
        {
            builder[ReplayMetadataKeys.SemanticSlot] = semanticSlot.ToString();
        }

        return builder.ToImmutable();
    }

    private static string ResolveExecutionId(Result<string> executionId)
        => executionId.Match(
            error => "exec:failed:" + error.Code.ToLowerInvariant(),
            value => value);

    private static string GetContextSnapshotId(KernelExecutionRequest request)
    {
        return string.IsNullOrWhiteSpace(request.ContextSnapshotId)
            ? "unknown"
            : request.ContextSnapshotId;
    }

    private static string GetContextHash(KernelExecutionRequest request)
    {
        return string.IsNullOrWhiteSpace(request.ContextHash)
            ? "unknown"
            : request.ContextHash;
    }
}
