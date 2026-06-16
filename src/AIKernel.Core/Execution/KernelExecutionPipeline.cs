namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;

internal sealed class KernelExecutionPipeline
{
    private readonly KernelExecutionStepRunner _stepRunner;
    /// <summary>
    /// EN: Executes KernelExecutionPipeline.
    /// EN: Documentation for public API. JA: KernelExecutionPipeline を実行します。
    /// </summary>

    public KernelExecutionPipeline(KernelExecutionStepRunner stepRunner)
    {
        _stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
    }
    /// <summary>
    /// EN: Gets ExecuteAsync.
    /// EN: Documentation for public API. JA: ExecuteAsync を取得します。
    /// </summary>

    public async Task<ResultStep<KernelExecutionPipelineState, KernelExecutionPipelineOutput>> ExecuteAsync(
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
            .WithSemanticDelta(CapabilityDelta)
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
            .WithSemanticDelta(PromptDelta)
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
            .WithSemanticDelta(ProviderDelta)
            .Bind(value => ResultStep<KernelExecutionPipelineState, string>
                .FromResult(state, ValidateOutput(value))
                .WithSemanticDelta(ProviderValidationDelta))
            .Map(value => new OutputExecutionStep(
                promptStep.Capability,
                promptStep.Prompt,
                value));
    }

    private ResultStep<KernelExecutionPipelineState, KernelExecutionPipelineOutput> CountOutputTokensStep(
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
            .WithSemanticDelta(TokenizerDelta)
            .Bind(outputTokens => ResultStep<KernelExecutionPipelineState, int>
                .FromResult(
                    state,
                    ValidateOutputTokenBudget(
                        outputTokens,
                        outputStep.Capability))
                .WithSemanticDelta(TokenBudgetDelta))
            .Map(outputTokens => new KernelExecutionPipelineOutput(
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

    private static readonly SemanticDelta CapabilityDelta = new(
        "kernel.capability.resolve",
        OriginStep.Capability,
        SemanticSlot.T,
        Kind: "execute");

    private static readonly SemanticDelta PromptDelta = new(
        "kernel.prompt.generate",
        OriginStep.Prompt,
        SemanticSlot.T,
        Kind: "execute");

    private static readonly SemanticDelta ProviderDelta = new(
        "kernel.provider.generate",
        OriginStep.Provider,
        SemanticSlot.T,
        Kind: "execute");

    private static readonly SemanticDelta ProviderValidationDelta = new(
        "kernel.provider.validate-output",
        OriginStep.Provider,
        SemanticSlot.T,
        Kind: "execute");

    private static readonly SemanticDelta TokenizerDelta = new(
        "kernel.tokenizer.count-output",
        OriginStep.Tokenizer,
        SemanticSlot.T,
        Kind: "execute");

    private static readonly SemanticDelta TokenBudgetDelta = new(
        "kernel.tokenizer.validate-output-budget",
        OriginStep.Tokenizer,
        SemanticSlot.T,
        Kind: "execute");

    private sealed record PromptExecutionStep(
        ModelPromptCapability Capability,
        GeneratedPrompt Prompt);

    private sealed record OutputExecutionStep(
        ModelPromptCapability Capability,
        GeneratedPrompt Prompt,
        string Output);
}

internal sealed record KernelExecutionPipelineState(
    KernelExecutionRequest Request,
    DateTimeOffset StartedAt,
    long ExecutionSequence,
    ModelPromptCapability? Capability,
    GeneratedPrompt? Prompt);

internal sealed record KernelExecutionPipelineOutput(
    ModelPromptCapability Capability,
    GeneratedPrompt Prompt,
    string Output,
    int OutputTokens);
