namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']" />
public sealed class PromptTokenBudgetExceededException : PromptGenerationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']" />
    public PromptTokenBudgetExceededException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']" />
    public PromptTokenBudgetExceededException(int actualTokens, int maxInputTokens)
        : base($"Prompt token budget exceeded. ActualTokens='{actualTokens}', MaxInputTokens='{maxInputTokens}'.")
    {
        ActualTokens = actualTokens;
        MaxInputTokens = maxInputTokens;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']" />
    public int? ActualTokens { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']" />
    public int? MaxInputTokens { get; }
}
