namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;

public sealed class KernelExecutor : IKernelExecutor
{
    private readonly IPromptGenerator _promptGenerator;
    private readonly IModelPromptCapabilityResolver _capabilityResolver;
    private readonly ITokenizer _tokenizer;
    private readonly IKernelClock _clock;
    private readonly KernelExecutionSuccessResultFactory _successResultFactory = new();
    private readonly KernelExecutionFailureResultFactory _failureResultFactory;
    private long _executionSequence;

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

        var capabilityResult = ResolveCapabilityResult(provider, request);
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
        var promptResult = await GeneratePromptResult(
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
        var outputResult = await GenerateOutputResult(
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

        var outputTokensResult = CountOutputTokensResult(output);
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

    private Result<ModelPromptCapability> ResolveCapabilityResult(
        IModelProvider provider,
        KernelExecutionRequest request)
    {
        try
        {
            return Result<ModelPromptCapability>.Success(
                _capabilityResolver.Resolve(provider, request));
        }
        catch (OperationCanceledException)
        {
            return Result<ModelPromptCapability>.Fail(CanceledError());
        }
        catch (Exception ex)
        {
            return Result<ModelPromptCapability>.Fail(ExecutionFailedError(ex));
        }
    }

    private async Task<Result<GeneratedPrompt>> GeneratePromptResult(
        KernelExecutionRequest request,
        ModelPromptCapability capability,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = await _promptGenerator
                .GenerateAsync(
                    new PromptGenerationRequest(
                        request.ContextSnapshot,
                        request.UserInstruction,
                        capability,
                        request.PromptOptions),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result<GeneratedPrompt>.Success(prompt);
        }
        catch (OperationCanceledException)
        {
            return Result<GeneratedPrompt>.Fail(CanceledError());
        }
        catch (Exception ex)
        {
            return Result<GeneratedPrompt>.Fail(ExecutionFailedError(ex));
        }
    }

    private async Task<Result<string>> GenerateOutputResult(
        IModelProvider provider,
        GeneratedPrompt prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = await provider
                .GenerateAsync(prompt.Messages, cancellationToken)
                .ConfigureAwait(false);

            return Result<string>.Success(output);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Fail(CanceledError());
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ExecutionFailedError(ex));
        }
    }

    private Result<int> CountOutputTokensResult(string output)
    {
        try
        {
            return Result<int>.Success(_tokenizer.CountTokens(output));
        }
        catch (Exception ex)
        {
            return Result<int>.Fail(ExecutionFailedError(ex));
        }
    }

    private static ErrorContext CanceledError()
    {
        return new ErrorContext("Execution was canceled.", "canceled", false);
    }

    private static ErrorContext ExecutionFailedError(Exception exception)
    {
        return new ErrorContext(exception.Message, "execution_failed", false);
    }
}
