namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

/// <summary>EN: Documentation for public API. JA: RomIdentityMismatchException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']/summary" />
public sealed class RomIdentityMismatchException : ContextAssemblyException
{
    /// <summary>EN: Documentation for public API. JA: RomIdentityMismatchException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']/summary" />
    public RomIdentityMismatchException(RomId requested, RomId actual, string path)
        : base($"Loaded ROM identity did not match requested identity. Requested='{requested.Value}', Actual='{actual.Value}', Path='{path}'.")
    {
        Requested = requested;
        Actual = actual;
        Path = path;
    }

    /// <summary>EN: Documentation for public API. JA: Requested を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']/summary" />
    public RomId Requested { get; }

    /// <summary>EN: Documentation for public API. JA: Actual を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']/summary" />
    public RomId Actual { get; }

    /// <summary>EN: Documentation for public API. JA: Path を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']/summary" />
    public string Path { get; }
}
