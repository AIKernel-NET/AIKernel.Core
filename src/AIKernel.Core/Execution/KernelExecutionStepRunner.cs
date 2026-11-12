namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using ExecutionModelMessage = AIKernel.Dtos.Execution.ModelMessage;

internal sealed class KernelExecutionStepRunner
{
    private readonly IPromptGenerator _promptGenerator;
    private readonly IModelPromptCapabilityResolver _capabilityResolver;
    private readonly ITokenizer _tokenizer;
    /// <summary>
    /// EN: Gets KernelExecutionStepRunner.
    /// [EN] Documents this public package API member. [JA] KernelExecutionStepRunner を取得します。
    /// </summary>

    public KernelExecutionStepRunner(
        IPromptGenerator promptGenerator,
        IModelPromptCapabilityResolver capabilityResolver,
        ITokenizer tokenizer)
    {
        _promptGenerator = promptGenerator ?? throw new ArgumentNullException(nameof(promptGenerator));
        _capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }
    /// <summary>
    /// EN: Gets ResolveCapability.
    /// [EN] Documents this public package API member. [JA] ResolveCapability を取得します。
    /// </summary>

    public Result<ModelPromptCapability> ResolveCapability(
        IModelProvider provider,
        KernelExecutionRequest request)
        => Capture(() => _capabilityResolver.Resolve(provider, request), OriginStep.Capability);
    /// <summary>
    /// EN: Gets GeneratePromptAsync.
    /// [EN] Documents this public package API member. [JA] GeneratePromptAsync を取得します。
    /// </summary>

    public async Task<Result<GeneratedPrompt>> GeneratePromptAsync(
        KernelExecutionRequest request,
        ModelPromptCapability capability,
        CancellationToken cancellationToken)
    {
        return await CaptureAsync(() => _promptGenerator
            .GenerateAsync(
                new PromptGenerationRequest(
                    request.ContextSnapshotId,
                    request.ContextHash,
                    request.ContextBlocks,
                    request.UserInstruction,
                    capability,
                    request.PromptOptions),
                cancellationToken), OriginStep.Prompt).ConfigureAwait(false);
    }
    /// <summary>
    /// EN: Gets GenerateOutputAsync.
    /// [EN] Documents this public package API member. [JA] GenerateOutputAsync を取得します。
    /// </summary>

    public async Task<Result<string>> GenerateOutputAsync(
        IModelProvider provider,
        GeneratedPrompt prompt,
        CancellationToken cancellationToken)
    {
        return await CaptureAsync(() => provider.GenerateAsync(
            ToProviderMessages(prompt.Messages), cancellationToken), OriginStep.Provider).ConfigureAwait(false);
    }

    private static IReadOnlyList<IModelMessage> ToProviderMessages(
        IReadOnlyList<ExecutionModelMessage> messages)
        => messages
            .Select(message => new ProviderModelMessage(message.Role, message.Content))
            .ToArray();
    /// <summary>
    /// EN: Executes CountOutputTokens.
    /// [EN] Documents this public package API member. [JA] CountOutputTokens を実行します。
    /// </summary>

    public Result<int> CountOutputTokens(string output)
        => Capture(() => _tokenizer.CountTokens(output), OriginStep.Tokenizer);

    // Classify before projecting to ErrorContext: exception_type is diagnostic
    // text and cannot retain an arbitrary derived exception's inheritance.
    private static Result<T> Capture<T>(Func<T> operation, OriginStep originStep)
    {
        try
        {
            return Result<T>.Success(operation());
        }
        catch (Exception error)
        {
            return Result<T>.Fail(ToExecutionError(error, originStep));
        }
    }

    private static async Task<Result<T>> CaptureAsync<T>(Func<Task<T>> operation, OriginStep originStep)
    {
        try
        {
            return Result<T>.Success(await operation().ConfigureAwait(false));
        }
        catch (Exception error)
        {
            return Result<T>.Fail(ToExecutionError(error, originStep));
        }
    }

    private static ErrorContext ToExecutionError(
        Exception error,
        OriginStep originStep)
        => CancellationExceptionClassifier.IsCancellation(error)
            ? CanceledError(originStep)
            : ExecutionFailedError(error, originStep);

    private static ErrorContext CanceledError(OriginStep originStep)
    {
        return new ErrorContext("Execution was canceled.", "canceled", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = originStep,
            SemanticSlot = SemanticSlot.T
        };
    }

    private static ErrorContext ExecutionFailedError(
        Exception exception,
        OriginStep originStep)
        => ExecutionFailedError(ErrorContext.FromException(exception), originStep);

    private static ErrorContext ExecutionFailedError(
        ErrorContext source,
        OriginStep originStep)
    {
        return source with
        {
            Code = "execution_failed",
            FailureKind = FailureKind.FailClosed,
            OriginStep = originStep,
            SemanticSlot = SemanticSlot.T
        };
    }

    private sealed record ProviderModelMessage(
        string Role,
        string Content) : IModelMessage;
}
