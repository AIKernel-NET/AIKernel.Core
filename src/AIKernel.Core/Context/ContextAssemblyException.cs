namespace AIKernel.Core.Context;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyException']" />
public class ContextAssemblyException : InvalidOperationException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']" />
    public ContextAssemblyException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']" />
    public ContextAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
