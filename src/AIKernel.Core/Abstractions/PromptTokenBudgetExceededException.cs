#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed class PromptTokenBudgetExceededException : PromptGenerationException
{
    public PromptTokenBudgetExceededException(string message)
        : base(message)
    {
    }

    public PromptTokenBudgetExceededException(int actualTokens, int maxInputTokens)
        : base($"Prompt token budget exceeded. Actual={actualTokens}, MaxInput={maxInputTokens}.")
    {
        ActualTokens = actualTokens;
        MaxInputTokens = maxInputTokens;
    }

    public int? ActualTokens { get; }

    public int? MaxInputTokens { get; }
}
