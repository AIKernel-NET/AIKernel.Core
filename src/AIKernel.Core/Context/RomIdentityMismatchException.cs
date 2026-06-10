namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']/summary" />
public sealed class RomIdentityMismatchException : ContextAssemblyException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']/summary" />
    public RomIdentityMismatchException(RomId requested, RomId actual, string path)
        : base($"Loaded ROM identity did not match requested identity. Requested='{requested.Value}', Actual='{actual.Value}', Path='{path}'.")
    {
        Requested = requested;
        Actual = actual;
        Path = path;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']/summary" />
    public RomId Requested { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']/summary" />
    public RomId Actual { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']/summary" />
    public string Path { get; }
}
