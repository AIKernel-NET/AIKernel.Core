#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed record RomSignatureSnapshot(
    string Algorithm,
    string ExpectedHash,
    string ActualHash,
    bool IsVerified);