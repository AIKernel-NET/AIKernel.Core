namespace AIKernel.Core.Rom;

/// <summary>EN: Documentation for public API. JA: RomLoadException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoadException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoadException']/summary" />
public class RomLoadException : Exception
{
    /// <summary>EN: Documentation for public API. JA: RomLoadException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    public RomLoadException(string message)
        : base(message)
    {
    }

    /// <summary>EN: Documentation for public API. JA: RomLoadException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoadException.#ctor']/summary" />
    public RomLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}