namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

public sealed class KernelExecutor : IKernelExecutor
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionStepRunner _stepRunner;
    private readonly KernelExecutionSuccessResultFactory _successResultFactory = new();
    private readonly KernelExecutionFailureResultFactory _failureResultFactory;
    private long _executionSequence;

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
        _stepRunner = new KernelExecutionStepRunner(
            promptGenerator,
            capabilityResolver,
            tokenizer);
        _failureResultFactory = new KernelExecutionFailureResultFactory(_clock);
    }

    public async Task<KernelRequestExecutionResult> ExecuteAsync(
        IModelProvider provider,
        KernelExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = _clock.Now;
        var executionSequence = Interlocked.Increment(ref _executionSequence);

        var pipelineResult = await ExecutePipelineAsync(
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
                pipelineResult.Error!);
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
            executionSequence);

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
                    ["source_error_code"] = successResult.Error.Code
                }
            });
    }

    private async Task<ResultStep<KernelExecutionPipelineState, TokenExecutionStep>> ExecutePipelineAsync(
        IModelProvider provider,
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence,
        CancellationToken cancellationToken)
    {
        return await (
            from capability in ResolveCapabilityStep(
                provider,
                request,
                startedAt,
                executionSequence)
            from prompt in GeneratePromptStepAsync(
                request,
                startedAt,
                executionSequence,
                capability,
                cancellationToken)
            from output in GenerateOutputStepAsync(
                request,
                startedAt,
                executionSequence,
                provider,
                prompt,
                cancellationToken)
            from tokens in CountOutputTokensStep(
                request,
                startedAt,
                executionSequence,
                output)
            select tokens)
            .ConfigureAwait(false);
    }

    private ResultStep<KernelExecutionPipelineState, ModelPromptCapability> ResolveCapabilityStep(
        IModelProvider provider,
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        var state = CreatePipelineState(
            request,
            startedAt,
            executionSequence);

        return ResultStep<KernelExecutionPipelineState, ModelPromptCapability>
            .FromResult(
                state,
                _stepRunner.ResolveCapability(provider, request))
            .MapState((currentState, capability) => currentState with
            {
                Capability = capability
            });
    }

    private async Task<ResultStep<KernelExecutionPipelineState, PromptExecutionStep>> GeneratePromptStepAsync(
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence,
        ModelPromptCapability capability,
        CancellationToken cancellationToken)
    {
        var state = CreatePipelineState(
            request,
            startedAt,
            executionSequence,
            capability);

        var prompt = await _stepRunner.GeneratePromptAsync(
                request,
                capability,
                cancellationToken)
            .ConfigureAwait(false);

        return ResultStep<KernelExecutionPipelineState, GeneratedPrompt>
            .FromResult(state, prompt)
            .MapState((currentState, generatedPrompt) => currentState with
            {
                Prompt = generatedPrompt
            })
            .Map(generatedPrompt => new PromptExecutionStep(
                capability,
                generatedPrompt));
    }

    private async Task<ResultStep<KernelExecutionPipelineState, OutputExecutionStep>> GenerateOutputStepAsync(
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence,
        IModelProvider provider,
        PromptExecutionStep promptStep,
        CancellationToken cancellationToken)
    {
        var state = CreatePipelineState(
            request,
            startedAt,
            executionSequence,
            promptStep.Capability,
            promptStep.Prompt);

        var output = await _stepRunner.GenerateOutputAsync(
                provider,
                promptStep.Prompt,
                cancellationToken)
            .ConfigureAwait(false);

        return ResultStep<KernelExecutionPipelineState, string>
            .FromResult(state, output)
            .Bind(value => ResultStep<KernelExecutionPipelineState, string>
                .FromResult(state, ValidateOutput(value)))
            .Map(value => new OutputExecutionStep(
                promptStep.Capability,
                promptStep.Prompt,
                value));
    }

    private ResultStep<KernelExecutionPipelineState, TokenExecutionStep> CountOutputTokensStep(
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence,
        OutputExecutionStep outputStep)
    {
        var state = CreatePipelineState(
            request,
            startedAt,
            executionSequence,
            outputStep.Capability,
            outputStep.Prompt);

        return ResultStep<KernelExecutionPipelineState, int>
            .FromResult(
                state,
                _stepRunner.CountOutputTokens(outputStep.Output))
            .Bind(outputTokens => ResultStep<KernelExecutionPipelineState, int>
                .FromResult(
                    state,
                    ValidateOutputTokenBudget(
                        outputTokens,
                        outputStep.Capability)))
            .Map(outputTokens => new TokenExecutionStep(
                outputStep.Capability,
                outputStep.Prompt,
                outputStep.Output,
                outputTokens));
    }

    private static KernelExecutionPipelineState CreatePipelineState(
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence,
        ModelPromptCapability? capability = null,
        GeneratedPrompt? prompt = null)
        => new(
            request,
            startedAt,
            executionSequence,
            capability,
            prompt);

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

    private static Result<string> ValidateOutput(string output)
    {
        return string.IsNullOrWhiteSpace(output)
            ? Result<string>.Fail(new ErrorContext(
                "Model provider returned empty output.",
                "empty_output",
                false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.Provider,
                SemanticSlot = SemanticSlot.T
            })
            : Result<string>.Success(output);
    }

    private static Result<int> ValidateOutputTokenBudget(
        int outputTokens,
        ModelPromptCapability capability)
    {
        return outputTokens > capability.MaxOutputTokens
            ? Result<int>.Fail(new ErrorContext(
                $"Output token budget exceeded. Actual={outputTokens}, Max={capability.MaxOutputTokens}.",
                "output_token_budget_exceeded",
                false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.Tokenizer,
                SemanticSlot = SemanticSlot.T
            })
            : Result<int>.Success(outputTokens);
    }

    private sealed record PromptExecutionStep(
        ModelPromptCapability Capability,
        GeneratedPrompt Prompt);

    private sealed record OutputExecutionStep(
        ModelPromptCapability Capability,
        GeneratedPrompt Prompt,
        string Output);

    private sealed record TokenExecutionStep(
        ModelPromptCapability Capability,
        GeneratedPrompt Prompt,
        string Output,
        int OutputTokens);

    private sealed record KernelExecutionPipelineState(
        KernelExecutionRequest Request,
        DateTimeOffset StartedAt,
        long ExecutionSequence,
        ModelPromptCapability? Capability,
        GeneratedPrompt? Prompt);
}
