namespace AIKernel.Core.Context;

/// <summary>EN: Documentation for public API. JA: ContextAssemblyException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyException']/summary" />
public class ContextAssemblyException : InvalidOperationException
{
    /// <summary>EN: Documentation for public API. JA: ContextAssemblyException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']/summary" />
    public ContextAssemblyException(string message)
        : base(message)
    {
    }

    /// <summary>EN: Documentation for public API. JA: ContextAssemblyException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyException.#ctor']/summary" />
    public ContextAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
