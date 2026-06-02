namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;

public sealed class SemanticStateHasher
{
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
