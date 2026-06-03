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
        var promptResult = await _stepRunner.GeneratePromptAsync(
                request,
                capability,
                cancellationToken)
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

        var prompt = promptResult.Value!;
        var outputResult = await _stepRunner.GenerateOutputAsync(
                provider,
                prompt,
                cancellationToken)
            .ConfigureAwait(false);
        if (outputResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
                outputResult.Error!);
        }

        var output = outputResult.Value!;
        if (string.IsNullOrWhiteSpace(output))
        {
            return CreateFailedResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
                code: "empty_output",
                message: "Model provider returned empty output.");
        }

        var outputTokensResult = _stepRunner.CountOutputTokens(output);
        if (outputTokensResult.IsFailure)
        {
            return CreateStepFailureResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
                outputTokensResult.Error!);
        }

        var outputTokens = outputTokensResult.Value!;
        if (outputTokens > capability.MaxOutputTokens)
        {
            return CreateFailedResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
                code: "output_token_budget_exceeded",
                message: $"Output token budget exceeded. Actual={outputTokens}, Max={capability.MaxOutputTokens}.");
        }

        var completedAt = _clock.Now;

        var successResult = _successResultFactory.CreateSucceededResult(
            request,
            capability,
            prompt,
            output,
            outputTokens,
            startedAt,
            completedAt,
            executionSequence);

        if (successResult.IsSuccess)
        {
            return successResult.Value!;
        }

        return CreateFailedResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            code: "execution_id_generation_failed",
            message: successResult.Error!.Message,
            detail: successResult.Error.Code);
    }

    private KernelRequestExecutionResult CreateFailedResult(
        KernelExecutionRequest request,
        ModelPromptCapability? capability,
        GeneratedPrompt? prompt,
        DateTimeOffset startedAt,
        long executionSequence,
        string code,
        string message,
        string? detail = null)
    {
        return _failureResultFactory.Resolve(_failureResultFactory.CreateFailedResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            code,
            message,
            detail));
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
                executionSequence));
        }

        return CreateFailedResult(
            request,
            capability,
            prompt,
            startedAt,
            executionSequence,
            code: error.Code.ToLowerInvariant(),
            message: error.Message);
    }

}
