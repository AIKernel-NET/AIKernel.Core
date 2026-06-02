#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Providers;
using System.Collections.Immutable;

public sealed record GeneratedPrompt
{
    public required string PromptId { get; init; }

    public required string PromptHash { get; init; }

    public required string ContextSnapshotId { get; init; }

    public required string ContextHash { get; init; }

    public required ModelPromptCapability Capability { get; init; }

    public required ImmutableArray<IModelMessage> Messages { get; init; }

    public required int EstimatedInputTokens { get; init; }

    public ImmutableDictionary<string, string> Metadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
