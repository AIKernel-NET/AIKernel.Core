#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;

public interface IRomSignatureVerifier
{
    Task<RomSignatureVerificationResult> VerifyAsync(
        RomSnapshotCandidate candidate,
        CancellationToken cancellationToken = default);
}