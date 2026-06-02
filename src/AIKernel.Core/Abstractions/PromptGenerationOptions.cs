#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed record PromptGenerationOptions
{
    public static PromptGenerationOptions Default { get; } = new()
    {
        OverflowPolicy = PromptOverflowPolicy.FailClosed,
        IncludeContextHash = true,
        IncludeSourceMetadata = true
    };

    public required PromptOverflowPolicy OverflowPolicy { get; init; }

    public required bool IncludeContextHash { get; init; }

    public required bool IncludeSourceMetadata { get; init; }
}
