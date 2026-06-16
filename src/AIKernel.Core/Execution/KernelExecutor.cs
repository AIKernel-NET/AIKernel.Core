namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

/// <summary>EN: Documentation for public API. JA: KernelExecutor を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutor']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelExecutor']/summary" />
public sealed class KernelExecutor : IKernelExecutor
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionPipeline _pipeline;
    private readonly KernelExecutionSuccessResultFactory _successResultFactory = new();
    private readonly KernelExecutionFailureResultFactory _failureResultFactory;

    /// <summary>EN: Documentation for public API. JA: KernelExecutor を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.#ctor']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: ExecuteAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.ExecuteAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelExecutor.ExecuteAsync']/summary" />
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
        var failedPipeline = StopWhenPipelineFailed(
            request,
            pipelineResult)
            .Match<KernelRequestExecutionResult?>(
                () => null,
                result => result);
        if (failedPipeline is { } failureResult)
        {
            return failureResult;
        }

        var tokenStep = pipelineResult.Match(
            (_, error) => throw new InvalidOperationException(error.Message),
            (_, value) => value);
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

        return successResult.Match(
            error => CreateFailedResult(
                request,
                tokenStep.Capability,
                tokenStep.Prompt,
                startedAt,
                executionSequence,
                new ErrorContext(
                    error.Message,
                    "execution_id_generation_failed",
                    false)
                {
                    FailureKind = FailureKind.FailClosed,
                    OriginStep = OriginStep.SemanticHash,
                    SemanticSlot = SemanticSlot.B,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ResultMetadataKeys.SourceErrorCode] = error.Code
                    }
                }),
            value => value);
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

    private Option<KernelRequestExecutionResult> StopWhenPipelineFailed(
        KernelExecutionRequest request,
        ResultStep<KernelExecutionPipelineState, KernelExecutionPipelineOutput> pipelineResult)
        => pipelineResult.Match(
            (state, error) => Option<KernelRequestExecutionResult>.Some(CreateStepFailureResult(
                request,
                state.Capability,
                state.Prompt,
                state.StartedAt,
                state.ExecutionSequence,
                WithStepMetadata(
                    error,
                    pipelineResult.StepId,
                    pipelineResult.SemanticDelta,
                    pipelineResult.ReplayLog.Count,
                    pipelineResult.ReplayLogHash))),
            (_, _) => Option<KernelRequestExecutionResult>.None());

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
