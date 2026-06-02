#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;
using System.Collections.Immutable;

public sealed record ContextAssemblyRequest(
    RomId RootRomId,
    string? ParentSnapshotId,
    ContextAssemblyScope Scope)
{
    public int MaxDepth { get; init; } = 16;

    public ImmutableHashSet<string> RelationKindsToFollow { get; init; }
        = ImmutableHashSet<string>.Empty;
}
