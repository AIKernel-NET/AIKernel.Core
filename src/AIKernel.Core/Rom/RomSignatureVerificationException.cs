namespace AIKernel.Core.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']/summary" />
public sealed class RomSignatureVerificationException : RomLoadException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomSignatureVerificationException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomSignatureVerificationException.#ctor']/summary" />
    public RomSignatureVerificationException(
        string sourcePath,
        string expectedHash,
        string actualHash)
        : base(
            $"ROM signature verification failed. SourcePath='{sourcePath}', Expected='{expectedHash}', Actual='{actualHash}'.")
    {
        SourcePath = sourcePath;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']/summary" />
    public string SourcePath { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']/summary" />
    public string ExpectedHash { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']/summary" />
    public string ActualHash { get; }
}