namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutor']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutor']" />
public sealed class KernelExecutor : IKernelExecutor
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionPipeline _pipeline;
    private readonly KernelExecutionSuccessResultFactory _successResultFactory = new();
    private readonly KernelExecutionFailureResultFactory _failureResultFactory;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.#ctor']" />
    public KernelExecutor(
        IPromptGenerator promptGenerator,
        IModelPromptCapabilityResolver capabilityResolver,
        ITokenizer tokenizer,
        IKernelClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(promptGenerator);
        ArgumentNullException.ThrowIfNull(capabilityResolver);
        ArgumentNullException.ThrowIfNull(tokenizer);

        _clock = clock ?? KernelClock.System();
        _pipeline = new KernelExecutionPipeline(
            new KernelExecutionStepRunner(
                promptGenerator,
                capabilityResolver,
                tokenizer));
        _failureResultFactory = new KernelExecutionFailureResultFactory(_clock);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.ExecuteAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.ExecuteAsync']" />
    public async Task<KernelRequestExecutionResult> ExecuteAsync(
        IModelProvider provider,
        KernelExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = _clock.Now;
        const long executionSequence = 0;

        var pipelineResult = await _pipeline.ExecuteAsync(
            provider,
            request,
            startedAt,
            executionSequence,
            cancellationToken).ConfigureAwait(false);
        if (pipelineResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                pipelineResult.State.Capability,
                pipelineResult.State.Prompt,
                pipelineResult.State.StartedAt,
                pipelineResult.State.ExecutionSequence,
                WithStepMetadata(
                    pipelineResult.Error!,
                    pipelineResult.StepId,
                    pipelineResult.SemanticDelta,
                    pipelineResult.ReplayLog.Count,
                    pipelineResult.ReplayLogHash));
        }

        var tokenStep = pipelineResult.Value!;
        var completedAt = _clock.Now;

        var successResult = _successResultFactory.CreateSucceededResult(
            request,
            tokenStep.Capability,
            tokenStep.Prompt,
            tokenStep.Output,
            tokenStep.OutputTokens,
            startedAt,
            completedAt,
            executionSequence,
            pipelineResult.StepId,
            pipelineResult.SemanticDelta,
            pipelineResult.ReplayLog.Count,
            pipelineResult.ReplayLogHash);

        if (successResult.IsSuccess)
        {
            return successResult.Value!;
        }

        return CreateFailedResult(
            request,
            tokenStep.Capability,
            tokenStep.Prompt,
            startedAt,
            executionSequence,
            new ErrorContext(
                successResult.Error!.Message,
                "execution_id_generation_failed",
                false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.SemanticHash,
                SemanticSlot = SemanticSlot.B,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ResultMetadataKeys.SourceErrorCode] = successResult.Error.Code
                }
            });
    }

    private KernelRequestExecutionResult CreateFailedResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        ErrorContext error)
    {
        return _failureResultFactory.Resolve(_failureResultFactory.CreateFailedResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            error));
    }

    private KernelRequestExecutionResult CreateStepFailureResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        ErrorContext error)
    {
        if (string.Equals(error.Code, "canceled", StringComparison.Ordinal))
        {
            return _failureResultFactory.Resolve(_failureResultFactory.CreateCanceledResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
                error));
        }

        return _failureResultFactory.Resolve(_failureResultFactory.CreateFailedResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            error));
    }

    private static ErrorContext WithStepMetadata(
        ErrorContext error,
        string stepId,
        SemanticDelta semanticDelta,
        int replayLogCount,
        string replayLogHash)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (error.Metadata is not null)
        {
            foreach (var item in error.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        metadata[ReplayMetadataKeys.StepId] = stepId;
        metadata[ReplayMetadataKeys.SemanticDelta] = semanticDelta.Label;
        if (!string.IsNullOrWhiteSpace(semanticDelta.Kind))
        {
            metadata[PipelineStepMetadataKeys.DeltaKind] = semanticDelta.Kind;
        }

        metadata[ReplayMetadataKeys.ReplayLogCount] = replayLogCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata[ReplayMetadataKeys.ReplayLogHash] = replayLogHash;

        return error with
        {
            Metadata = metadata,
            OriginStep = error.OriginStep ?? semanticDelta.OriginStep,
            SemanticSlot = error.SemanticSlot ?? semanticDelta.SemanticSlot
        };
    }
}
