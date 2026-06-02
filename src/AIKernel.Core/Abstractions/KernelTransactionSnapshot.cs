#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record KernelTransactionSnapshot
{
    public required string TransactionId { get; init; }

    public required string InputHash { get; init; }

    public required string RootRomId { get; init; }

    public required string VfsProviderId { get; init; }

    public required string? RequestedModelId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required ImmutableDictionary<string, string> Metadata { get; init; }
}