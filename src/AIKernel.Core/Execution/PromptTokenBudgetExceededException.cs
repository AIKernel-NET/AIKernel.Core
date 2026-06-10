namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']/summary" />
public sealed class PromptTokenBudgetExceededException : PromptGenerationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    public PromptTokenBudgetExceededException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    public PromptTokenBudgetExceededException(int actualTokens, int maxInputTokens)
        : base($"Prompt token budget exceeded. ActualTokens='{actualTokens}', MaxInputTokens='{maxInputTokens}'.")
    {
        ActualTokens = actualTokens;
        MaxInputTokens = maxInputTokens;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']/summary" />
    public int? ActualTokens { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']/summary" />
    public int? MaxInputTokens { get; }
}
