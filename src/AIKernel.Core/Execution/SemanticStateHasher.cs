namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;

/// <summary>EN: Documentation for public API. JA: SemanticStateHasher を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateHasher']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateHasher']/summary" />
public sealed class SemanticStateHasher
{
    /// <summary>EN: Documentation for public API. JA: ComputeHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateHasher.ComputeHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateHasher.ComputeHash']/summary" />
    public SemanticStateHash ComputeHash(
        SemanticStateMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);

        var bytes = Encoding.UTF8.GetBytes(material.CanonicalPayload);
        var hash = SHA256.HashData(bytes);

        return new SemanticStateHash(
            Algorithm: "sha256",
            HexDigest: Convert.ToHexString(hash).ToLowerInvariant());
    }
}
