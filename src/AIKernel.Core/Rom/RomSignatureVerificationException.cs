namespace AIKernel.Core.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']" />
public sealed class RomSignatureVerificationException : RomLoadException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomSignatureVerificationException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomSignatureVerificationException.#ctor']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']" />
    public string SourcePath { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']" />
    public string ExpectedHash { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']" />
    public string ActualHash { get; }
}