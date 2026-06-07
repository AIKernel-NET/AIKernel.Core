namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.RomIdentityMismatchException']" />
public sealed class RomIdentityMismatchException : ContextAssemblyException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.RomIdentityMismatchException.#ctor']" />
    public RomIdentityMismatchException(RomId requested, RomId actual, string path)
        : base($"Loaded ROM identity did not match requested identity. Requested='{requested.Value}', Actual='{actual.Value}', Path='{path}'.")
    {
        Requested = requested;
        Actual = actual;
        Path = path;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Requested']" />
    public RomId Requested { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Actual']" />
    public RomId Actual { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.RomIdentityMismatchException.Path']" />
    public string Path { get; }
}
