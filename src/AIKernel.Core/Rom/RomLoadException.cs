namespace AIKernel.Core.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoadException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoadException']/summary" />
public class RomLoadException : Exception
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    public RomLoadException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    public RomLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}