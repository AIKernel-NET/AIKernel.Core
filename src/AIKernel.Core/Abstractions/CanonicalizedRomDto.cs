#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

/// <summary>
/// CanonicalizedRomDto の契約を定義します。
/// </summary>
public sealed record CanonicalizedRomDto
{
    public required string CanonicalBody { get; init; }

    public required string CanonicalizationVersion { get; init; }

    public IReadOnlyList<RomEntityMetadataDto> Entities { get; init; } = new List<RomEntityMetadataDto>();

    public IReadOnlyList<ResolvedRomRelationDto> Relations { get; init; } = new List<ResolvedRomRelationDto>();
}
