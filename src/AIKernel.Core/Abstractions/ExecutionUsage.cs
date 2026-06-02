#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed record ExecutionUsage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens);
