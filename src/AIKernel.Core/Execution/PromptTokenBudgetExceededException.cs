namespace AIKernel.Core.Execution;

public sealed class PromptTokenBudgetExceededException : PromptGenerationException
{
    public PromptTokenBudgetExceededException(string message)
        : base(message)
    {
    }

    public PromptTokenBudgetExceededException(int actualTokens, int maxInputTokens)
        : base($"Prompt token budget exceeded. ActualTokens='{actualTokens}', MaxInputTokens='{maxInputTokens}'.")
    {
        ActualTokens = actualTokens;
        MaxInputTokens = maxInputTokens;
    }

    public int? ActualTokens { get; }

    public int? MaxInputTokens { get; }
}
