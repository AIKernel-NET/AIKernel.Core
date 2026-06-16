namespace AIKernel.Kernel;

/// <summary>EN: Documentation for public API. JA: KernelRequestValidationException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestValidationException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestValidationException']/summary" />
public sealed class KernelRequestValidationException : Exception
{
    /// <summary>EN: Documentation for public API. JA: KernelRequestValidationException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestValidationException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestValidationException.#ctor']/summary" />
    public KernelRequestValidationException(string message)
        : base(message)
    {
    }
}
