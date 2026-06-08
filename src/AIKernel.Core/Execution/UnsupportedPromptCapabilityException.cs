namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.UnsupportedPromptCapabilityException']" />
public sealed class UnsupportedPromptCapabilityException : PromptGenerationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.UnsupportedPromptCapabilityException.#ctor']" />
    public UnsupportedPromptCapabilityException(string message)
        : base(message)
    {
    }
}
