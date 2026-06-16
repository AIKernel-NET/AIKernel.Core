namespace AIKernel.Core.Rom;

/// <summary>[EN] Documents this public package API member. [JA] RomSignatureVerificationException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomSignatureVerificationException']/summary" />
public sealed class RomSignatureVerificationException : RomLoadException
{
    /// <summary>[EN] Documents this public package API member. [JA] RomSignatureVerificationException を取得します。</summary>
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

    /// <summary>[EN] Documents this public package API member. [JA] SourcePath を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.SourcePath']/summary" />
    public string SourcePath { get; }

    /// <summary>[EN] Documents this public package API member. [JA] ExpectedHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ExpectedHash']/summary" />
    public string ExpectedHash { get; }

    /// <summary>[EN] Documents this public package API member. [JA] ActualHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomSignatureVerificationException.ActualHash']/summary" />
    public string ActualHash { get; }
}