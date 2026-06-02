#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record KernelRequestExecutionResult
{
    public required string ExecutionId { get; init; }

    public required ExecutionStatus Status { get; init; }

    public required string ProviderId { get; init; }

    public required string ModelId { get; init; }

    public required string ContextSnapshotId { get; init; }

    public required string ContextHash { get; init; }

    public required string PromptHash { get; init; }

    public required string? OutputText { get; init; }

    public required ExecutionUsage Usage { get; init; }

    public required ExecutionError? Error { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }

    public ImmutableDictionary<string, string> Metadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
