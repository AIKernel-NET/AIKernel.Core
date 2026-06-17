namespace AIKernel.Core.Execution;

/// <summary>[EN] Documents this public package API member. [JA] UnsupportedPromptCapabilityException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']/summary" />
public sealed class UnsupportedPromptCapabilityException : PromptGenerationException
{
    /// <summary>[EN] Documents this public package API member. [JA] UnsupportedPromptCapabilityException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']/summary" />
    public UnsupportedPromptCapabilityException(string message)
        : base(message)
    {
    }
}
