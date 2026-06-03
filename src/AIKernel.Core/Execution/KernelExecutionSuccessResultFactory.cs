namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;

internal sealed class KernelExecutionSuccessResultFactory
{
    private readonly KernelExecutionIdFactory _executionIdFactory = new();

    public Result<KernelRequestExecutionResult> CreateSucceededResult(
        KernelExecutionRequest request,
        ModelPromptCapability capability,
        GeneratedPrompt prompt,
        string output,
        int outputTokens,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        long executionSequence)
    {
        return
            from executionId in _executionIdFactory.CreateExecutionIdResult(
                request,
                ExecutionStatus.Succeeded,
                prompt.PromptHash,
                output,
                startedAt,
                executionSequence)
            select new KernelRequestExecutionResult
            {
                ExecutionId = executionId,
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
}
