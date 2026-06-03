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

        var capabilityResult = _stepRunner.ResolveCapability(provider, request);
        if (capabilityResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                capability: null,
                prompt: null,
                startedAt,
                executionSequence,
                capabilityResult.Error!);
        }

        var capability = capabilityResult.Value!;
        var promptResult = await (
            from prompt in _stepRunner.GeneratePromptAsync(
                request,
                capability,
                cancellationToken)
            select new PromptExecutionStep(capability, prompt))
            .ConfigureAwait(false);
        if (promptResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                capability,
                prompt: null,
                startedAt,
                executionSequence,
                promptResult.Error!);
        }

        var promptStep = promptResult.Value!;
        var outputResult = await (
            from output in _stepRunner.GenerateOutputAsync(
                provider,
                promptStep.Prompt,
                cancellationToken)
            from validatedOutput in ValidateOutput(output)
            select new OutputExecutionStep(
                promptStep.Capability,
                promptStep.Prompt,
                validatedOutput))
            .ConfigureAwait(false);
        if (outputResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                promptStep.Capability,
                promptStep.Prompt,
                startedAt,
                executionSequence,
                outputResult.Error!);
        }

        var outputStep = outputResult.Value!;
        var outputTokensResult =
            from outputTokens in _stepRunner.CountOutputTokens(outputStep.Output)
            from validatedOutputTokens in ValidateOutputTokenBudget(
                outputTokens,
                outputStep.Capability)
            select new TokenExecutionStep(
                outputStep.Capability,
                outputStep.Prompt,
                outputStep.Output,
                validatedOutputTokens);
        if (outputTokensResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                outputStep.Capability,
                outputStep.Prompt,
                startedAt,
                executionSequence,
                outputTokensResult.Error!);
        }

        var tokenStep = outputTokensResult.Value!;
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

}
