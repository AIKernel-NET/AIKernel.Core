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
        => Try
            .Run(() => _capabilityResolver.Resolve(provider, request))
            .Match(
                error => Result<ModelPromptCapability>.Fail(ToExecutionError(error, OriginStep.Capability)),
                Result<ModelPromptCapability>.Success);
    /// <summary>
    /// EN: Gets GeneratePromptAsync.
    /// [EN] Documents this public package API member. [JA] GeneratePromptAsync を取得します。
    /// </summary>

    public async Task<Result<GeneratedPrompt>> GeneratePromptAsync(
        KernelExecutionRequest request,
        ModelPromptCapability capability,
        CancellationToken cancellationToken)
    {
        return (await Try.RunAsync(async () => await _promptGenerator
            .GenerateAsync(
                new PromptGenerationRequest(
                    request.ContextSnapshotId,
                    request.ContextHash,
                    request.ContextBlocks,
                    request.UserInstruction,
                    capability,
                    request.PromptOptions),
                cancellationToken)
            .ConfigureAwait(false)).ConfigureAwait(false))
            .Match(
                error => Result<GeneratedPrompt>.Fail(ToExecutionError(error, OriginStep.Prompt)),
                Result<GeneratedPrompt>.Success);
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
        return (await Try.RunAsync(async () => await provider
            .GenerateAsync(ToProviderMessages(prompt.Messages), cancellationToken)
            .ConfigureAwait(false)).ConfigureAwait(false))
            .Match(
                error => Result<string>.Fail(ToExecutionError(error, OriginStep.Provider)),
                Result<string>.Success);
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
        => Try
            .Run(() => _tokenizer.CountTokens(output))
            .Match(
                error => Result<int>.Fail(ToExecutionError(error, OriginStep.Tokenizer)),
                Result<int>.Success);

    private static ErrorContext ToExecutionError(
        ErrorContext error,
        OriginStep originStep)
        => IsOperationCanceled(error)
            ? CanceledError(originStep)
            : ExecutionFailedError(error, originStep);

    private static bool IsOperationCanceled(ErrorContext error)
        => error.Metadata is not null &&
           error.Metadata.TryGetValue(ResultMetadataKeys.ExceptionType, out var exceptionType) &&
           string.Equals(exceptionType, typeof(OperationCanceledException).FullName, StringComparison.Ordinal);

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
