namespace AIKernel.Core.Execution;

/// <summary>[EN] Documents this public package API member. [JA] PromptTokenBudgetExceededException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptTokenBudgetExceededException']/summary" />
public sealed class PromptTokenBudgetExceededException : PromptGenerationException
{
    /// <summary>[EN] Documents this public package API member. [JA] PromptTokenBudgetExceededException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    public PromptTokenBudgetExceededException(string message)
        : base(message)
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] PromptTokenBudgetExceededException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptTokenBudgetExceededException.#ctor']/summary" />
    public PromptTokenBudgetExceededException(int actualTokens, int maxInputTokens)
        : base($"Prompt token budget exceeded. ActualTokens='{actualTokens}', MaxInputTokens='{maxInputTokens}'.")
    {
        ActualTokens = actualTokens;
        MaxInputTokens = maxInputTokens;
    }

    /// <summary>[EN] Documents this public package API member. [JA] ActualTokens を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.ActualTokens']/summary" />
    public int? ActualTokens { get; }

    /// <summary>[EN] Documents this public package API member. [JA] MaxInputTokens を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Execution.PromptTokenBudgetExceededException.MaxInputTokens']/summary" />
    public int? MaxInputTokens { get; }
}
