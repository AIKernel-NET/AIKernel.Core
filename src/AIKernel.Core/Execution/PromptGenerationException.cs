namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']" />
public class PromptGenerationException : InvalidOperationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']" />
    public PromptGenerationException(string message)
        : base(message)
    {
    }
}
