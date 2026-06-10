namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PromptGenerationException']/summary" />
public class PromptGenerationException : InvalidOperationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PromptGenerationException.#ctor']/summary" />
    public PromptGenerationException(string message)
        : base(message)
    {
    }
}
