#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed record ContextPromptBlock(
    string Id,
    string Category,
    string Content,
    int Priority,
    IReadOnlyDictionary<string, string> Metadata);
