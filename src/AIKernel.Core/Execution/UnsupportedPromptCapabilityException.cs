namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']/summary" />
public sealed class UnsupportedPromptCapabilityException : PromptGenerationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']/summary" />
    public UnsupportedPromptCapabilityException(string message)
        : base(message)
    {
    }
}
