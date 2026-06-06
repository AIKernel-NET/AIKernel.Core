namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

public sealed class RomSignatureVerifier : IRomSignatureVerifier
{
    private readonly IRomCanonicalizer _canonicalizer;
    private readonly ISemanticHasher _semanticHasher;

    public RomSignatureVerifier(
        IRomCanonicalizer canonicalizer,
        ISemanticHasher semanticHasher)
    {
        _canonicalizer = canonicalizer
            ?? throw new ArgumentNullException(nameof(canonicalizer));

        _semanticHasher = semanticHasher
            ?? throw new ArgumentNullException(nameof(semanticHasher));
    }

    public async Task<RomSignatureVerificationResult> VerifyAsync(
        RomSnapshotCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        cancellationToken.ThrowIfCancellationRequested();

        var document = new CandidateRomDocument(candidate, _canonicalizer, _semanticHasher);

        // Pure boundary:
        // Canonicalization must be deterministic for identical candidate state.
        var canonicalized = await _canonicalizer
            .CanonicalizeAsync(document, cancellationToken)
            .ConfigureAwait(false);

        // Pure boundary:
        // Hashing must be deterministic for identical canonicalized ROM.
        var actualHash = await _semanticHasher
            .ComputeHashAsync(canonicalized, cancellationToken)
            .ConfigureAwait(false);

        var verified = string.Equals(
            candidate.ExpectedHash,
            actualHash,
            StringComparison.Ordinal);

        return new RomSignatureVerificationResult(
            IsVerified: verified,
            Algorithm: _semanticHasher.Algorithm,
            ExpectedHash: candidate.ExpectedHash,
            ActualHash: actualHash);
    }
}
