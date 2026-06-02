#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record RomSnapshotCandidate
{
    public required RomId RomId { get; init; }

    public required string SourcePath { get; init; }

    public required string Body { get; init; }

    public required ImmutableArray<string> SecurityTags { get; init; }

    public required ImmutableArray<RomRelationSnapshot> Relations { get; init; }

    public required string ExpectedHash { get; init; }

    public required ImmutableDictionary<string, string> AdditionalMetadata { get; init; }
}