namespace AIKernel.Core.Execution;

/// <summary>[EN] Documents this public package API member. [JA] PromptGenerationException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']/summary" />
public class PromptGenerationException : InvalidOperationException
{
    /// <summary>[EN] Documents this public package API member. [JA] PromptGenerationException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']/summary" />
    public PromptGenerationException(string message)
        : base(message)
    {
    }
}
