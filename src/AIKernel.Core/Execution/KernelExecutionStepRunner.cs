namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;

internal sealed class KernelExecutionStepRunner
{
    private readonly IPromptGenerator _promptGenerator;
    private readonly IModelPromptCapabilityResolver _capabilityResolver;
    private readonly ITokenizer _tokenizer;

    public KernelExecutionStepRunner(
        IPromptGenerator promptGenerator,
        IModelPromptCapabilityResolver capabilityResolver,
        ITokenizer tokenizer)
    {
        _promptGenerator = promptGenerator ?? throw new ArgumentNullException(nameof(promptGenerator));
        _capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }

    public Result<ModelPromptCapability> ResolveCapability(
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

    public async Task<Result<GeneratedPrompt>> GeneratePromptAsync(
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

    public async Task<Result<string>> GenerateOutputAsync(
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

    public Result<int> CountOutputTokens(string output)
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
