namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

public sealed class KernelExecutor : IKernelExecutor
{
    private readonly IPromptGenerator _promptGenerator;
    private readonly IModelPromptCapabilityResolver _capabilityResolver;
    private readonly ITokenizer _tokenizer;
    private readonly IKernelClock _clock;

    public KernelExecutor(
        IPromptGenerator promptGenerator,
        IModelPromptCapabilityResolver capabilityResolver,
        ITokenizer tokenizer,
        IKernelClock? clock = null)
    {
        _promptGenerator = promptGenerator ?? throw new ArgumentNullException(nameof(promptGenerator));
        _capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _clock = clock ?? KernelClock.System();
    }

    public async Task<KernelRequestExecutionResult> ExecuteAsync(
        IModelProvider provider,
        KernelExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = _clock.Now;

        ModelPromptCapability? capability = null;
        GeneratedPrompt? prompt = null;

        try
        {
            capability = _capabilityResolver.Resolve(provider, request);

            prompt = await _promptGenerator
                .GenerateAsync(
                    new PromptGenerationRequest(
                        request.ContextSnapshot,
                        request.UserInstruction,
                        capability,
                        request.PromptOptions),
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var output = await provider
                .GenerateAsync(prompt.Messages, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(output))
            {
                return CreateFailedResult(
                    request,
                    capability,
                    prompt,
                    startedAt,
                    code: "empty_output",
                    message: "Model provider returned empty output.");
            }

            var outputTokens = _tokenizer.CountTokens(output);

            if (outputTokens > capability.MaxOutputTokens)
            {
                return CreateFailedResult(
                    request,
                    capability,
                    prompt,
                    startedAt,
                    code: "output_token_budget_exceeded",
                    message: $"Output token budget exceeded. Actual={outputTokens}, Max={capability.MaxOutputTokens}.");
            }

            var completedAt = _clock.Now;

            return new KernelRequestExecutionResult
            {
                ExecutionId = $"exec:{Guid.NewGuid():N}",
                Status = ExecutionStatus.Succeeded,
                ProviderId = capability.ProviderId,
                ModelId = capability.ModelId,
                ContextSnapshotId = request.ContextSnapshot.SnapshotId,
                ContextHash = request.ContextSnapshot.ContextHash,
                PromptHash = prompt.PromptHash,
                OutputText = output,
                Usage = new ExecutionUsage(
                    InputTokens: prompt.EstimatedInputTokens,
                    OutputTokens: outputTokens,
                    TotalTokens: prompt.EstimatedInputTokens + outputTokens),
                Error = null,
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add("message_format", capability.MessageFormat.ToString())
            };
        }
        catch (OperationCanceledException)
        {
            var completedAt = _clock.Now;

            return new KernelRequestExecutionResult
            {
                ExecutionId = $"exec:{Guid.NewGuid():N}",
                Status = ExecutionStatus.Canceled,
                ProviderId = capability?.ProviderId ?? "unknown",
                ModelId = capability?.ModelId ?? request.RequestedModelId ?? "unknown",
                ContextSnapshotId = request.ContextSnapshot.SnapshotId,
                ContextHash = request.ContextSnapshot.ContextHash,
                PromptHash = prompt?.PromptHash ?? string.Empty,
                OutputText = null,
                Usage = new ExecutionUsage(
                    InputTokens: prompt?.EstimatedInputTokens ?? 0,
                    OutputTokens: 0,
                    TotalTokens: prompt?.EstimatedInputTokens ?? 0),
                Error = new ExecutionError(
                    Code: "canceled",
                    Message: "Execution was canceled."),
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                Metadata = ImmutableDictionary<string, string>.Empty
            };
        }
        catch (Exception ex)
        {
            return CreateFailedResult(
                request,
                capability,
                prompt,
                startedAt,
                code: "execution_failed",
                message: ex.Message,
                detail: ex.GetType().FullName);
        }
    }

    private KernelRequestExecutionResult CreateFailedResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        string code,
        string message,
        string? detail = null)
    {
        var completedAt = _clock.Now;

        return new KernelRequestExecutionResult
        {
            ExecutionId = $"exec:{Guid.NewGuid():N}",
            Status = ExecutionStatus.Failed,
            ProviderId = capability?.ProviderId ?? "unknown",
            ModelId = capability?.ModelId ?? request.RequestedModelId ?? "unknown",
            ContextSnapshotId = request.ContextSnapshot.SnapshotId,
            ContextHash = request.ContextSnapshot.ContextHash,
            PromptHash = prompt?.PromptHash ?? string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: prompt?.EstimatedInputTokens ?? 0,
                OutputTokens: 0,
                TotalTokens: prompt?.EstimatedInputTokens ?? 0),
            Error = new ExecutionError(
                Code: code,
                Message: message,
                Detail: detail),
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Metadata = ImmutableDictionary<string, string>.Empty
        };
    }
}
