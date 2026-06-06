namespace AIKernel.Core.Rom;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

public sealed class Sha256SemanticHasher : ISemanticHasher
{
    public string Algorithm => "sha256";

    public string ComputeHash(CanonicalizedRomDto canonicalized)
    {
        ArgumentNullException.ThrowIfNull(canonicalized);

        var bytes = Encoding.UTF8.GetBytes(canonicalized.CanonicalBody);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public Task<string> ComputeHashAsync(
        CanonicalizedRomDto canonicalized,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ComputeHash(canonicalized));
    }

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