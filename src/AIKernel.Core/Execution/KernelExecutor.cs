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
        var capabilityStep = await CreatePipelineStart(
                provider,
                request,
                startedAt,
                executionSequence)
            .BindAsync(step => Task.FromResult(ResolveCapabilityStep(step)))
            .ConfigureAwait(false);
        var promptStep = await capabilityStep
            .BindAsync(step => GeneratePromptStepAsync(step, cancellationToken))
            .ConfigureAwait(false);
        var outputStep = await promptStep
            .BindAsync(step => GenerateOutputStepAsync(step, provider, cancellationToken))
            .ConfigureAwait(false);

        return await outputStep
            .BindAsync(step => Task.FromResult(CountOutputTokensStep(step)))
            .ConfigureAwait(false);
    }

    private static ResultStep<KernelExecutionPipelineState, IModelProvider> CreatePipelineStart(
        IModelProvider provider,
        KernelExecutionRequest request,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        return ResultStep<KernelExecutionPipelineState, IModelProvider>.Success(
            new KernelExecutionPipelineState(
                request,
                startedAt,
                executionSequence,
                Capability: null,
                Prompt: null),
            provider);
    }

    private ResultStep<KernelExecutionPipelineState, ModelPromptCapability> ResolveCapabilityStep(
        ResultStep<KernelExecutionPipelineState, IModelProvider> step)
    {
        return ResultStep<KernelExecutionPipelineState, ModelPromptCapability>
            .FromResult(
                step.State,
                _stepRunner.ResolveCapability(step.Value!, step.State.Request))
            .MapState(current => current.State with { Capability = current.Value });
    }

    private async Task<ResultStep<KernelExecutionPipelineState, PromptExecutionStep>> GeneratePromptStepAsync(
        ResultStep<KernelExecutionPipelineState, ModelPromptCapability> step,
        CancellationToken cancellationToken)
    {
        var prompt = await _stepRunner.GeneratePromptAsync(
                step.State.Request,
                step.Value!,
                cancellationToken)
            .ConfigureAwait(false);

        return ResultStep<KernelExecutionPipelineState, GeneratedPrompt>
            .FromResult(step.State, prompt)
            .MapState(current => current.State with { Prompt = current.Value })
            .Map(current => new PromptExecutionStep(
                current.State.Capability!,
                current.Value!));
    }

    private async Task<ResultStep<KernelExecutionPipelineState, OutputExecutionStep>> GenerateOutputStepAsync(
        ResultStep<KernelExecutionPipelineState, PromptExecutionStep> step,
        IModelProvider provider,
        CancellationToken cancellationToken)
    {
        var output = await _stepRunner.GenerateOutputAsync(
                provider,
                step.Value!.Prompt,
                cancellationToken)
            .ConfigureAwait(false);

        return ResultStep<KernelExecutionPipelineState, string>
            .FromResult(step.State, output)
            .Bind(current => ResultStep<KernelExecutionPipelineState, string>
                .FromResult(current.State, ValidateOutput(current.Value!)))
            .Map(current => new OutputExecutionStep(
                current.State.Capability!,
                current.State.Prompt!,
                current.Value!));
    }

    private ResultStep<KernelExecutionPipelineState, TokenExecutionStep> CountOutputTokensStep(
        ResultStep<KernelExecutionPipelineState, OutputExecutionStep> step)
    {
        return ResultStep<KernelExecutionPipelineState, int>
            .FromResult(
                step.State,
                _stepRunner.CountOutputTokens(step.Value!.Output))
            .Bind(current => ResultStep<KernelExecutionPipelineState, int>
                .FromResult(
                    current.State,
                    ValidateOutputTokenBudget(
                        current.Value,
                        step.Value!.Capability)))
            .Map(current => new TokenExecutionStep(
                step.Value!.Capability,
                step.Value.Prompt,
                step.Value.Output,
                current.Value));
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
