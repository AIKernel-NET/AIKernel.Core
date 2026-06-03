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
        long executionSequence,
        string? finalStepId = null,
        SemanticDelta? finalSemanticDelta = null,
        int? replayLogCount = null,
        string? replayLogHash = null)
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
                Metadata = BuildMetadata(
                    capability,
                    finalStepId,
                    finalSemanticDelta,
                    replayLogCount,
                    replayLogHash)
            };
    }

    private static ImmutableDictionary<string, string> BuildMetadata(
        ModelPromptCapability capability,
        string? finalStepId,
        SemanticDelta? finalSemanticDelta,
        int? replayLogCount,
        string? replayLogHash)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        builder["message_format"] = capability.MessageFormat.ToString();

        if (!string.IsNullOrWhiteSpace(finalStepId))
        {
            builder["step_id"] = finalStepId;
        }

        if (finalSemanticDelta is not null)
        {
            builder["semantic_delta"] = finalSemanticDelta.Label;

            if (finalSemanticDelta.OriginStep is { } originStep)
            {
                builder["origin_step"] = originStep.ToString();
            }

            if (finalSemanticDelta.SemanticSlot is { } semanticSlot)
            {
                builder["semantic_slot"] = semanticSlot.ToString();
            }
        }

        if (replayLogCount is { } count)
        {
            builder["replay_log_count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(replayLogHash))
        {
            builder["replay_log_hash"] = replayLogHash;
        }

        return builder.ToImmutable();
    }
}
