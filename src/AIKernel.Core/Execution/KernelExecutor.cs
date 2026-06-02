namespace AIKernel.Core.Execution;

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
                    executionSequence,
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
        catch (OperationCanceledException)
        {
            return _failureResultFactory.Resolve(_failureResultFactory.CreateCanceledResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence));
        }
        catch (Exception ex)
        {
            return CreateFailedResult(
                request,
                capability,
                prompt,
                startedAt,
                executionSequence,
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
}
