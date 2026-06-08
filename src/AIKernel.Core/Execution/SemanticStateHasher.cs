namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateHasher']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SemanticStateHasher']" />
public sealed class SemanticStateHasher
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateHasher.ComputeHash']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SemanticStateHasher.ComputeHash']" />
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
