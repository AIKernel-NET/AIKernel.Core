namespace AIKernel.Kernel;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestValidationException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestValidationException']" />
public sealed class KernelRequestValidationException : Exception
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestValidationException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestValidationException.#ctor']" />
    public KernelRequestValidationException(string message)
        : base(message)
    {
    }
}
