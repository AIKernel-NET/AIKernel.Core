namespace AIKernel.Core.Rom;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

/// <summary>EN: Documentation for public API. JA: Sha256SemanticHasher を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.Sha256SemanticHasher']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.Sha256SemanticHasher']/summary" />
public sealed class Sha256SemanticHasher : ISemanticHasher
{
    /// <summary>EN: Documentation for public API. JA: Algorithm を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Rom.Sha256SemanticHasher.Algorithm']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Rom.Sha256SemanticHasher.Algorithm']/summary" />
    public string Algorithm => "sha256";

    /// <summary>EN: Documentation for public API. JA: ComputeHash を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.ComputeHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.ComputeHash']/summary" />
    public string ComputeHash(CanonicalizedRomDto canonicalized)
    {
        ArgumentNullException.ThrowIfNull(canonicalized);

        var bytes = Encoding.UTF8.GetBytes(canonicalized.CanonicalBody);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>EN: Documentation for public API. JA: ComputeHashAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.ComputeHashAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.ComputeHashAsync']/summary" />
    public Task<string> ComputeHashAsync(
        CanonicalizedRomDto canonicalized,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ComputeHash(canonicalized));
    }

    /// <summary>EN: Documentation for public API. JA: VerifyHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.VerifyHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.VerifyHash']/summary" />
    public bool VerifyHash(
        CanonicalizedRomDto canonicalized,
        string expectedHash)
    {
        var actual = ComputeHash(canonicalized);

        return string.Equals(
            actual,
            expectedHash,
            StringComparison.Ordinal);
    }

    /// <summary>EN: Documentation for public API. JA: VerifyHashAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.VerifyHashAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.Sha256SemanticHasher.VerifyHashAsync']/summary" />
    public async Task<bool> VerifyHashAsync(
        CanonicalizedRomDto canonicalized,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        var actual = await ComputeHashAsync(canonicalized, cancellationToken)
            .ConfigureAwait(false);

        return string.Equals(
            actual,
            expectedHash,
            StringComparison.Ordinal);
    }
}