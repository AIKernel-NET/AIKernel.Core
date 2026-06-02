#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record ModelPromptCapability
{
    public required string ProviderId { get; init; }

    public required string ModelId { get; init; }

    public PromptMessageFormat MessageFormat { get; init; } = PromptMessageFormat.ChatMessages;

    public int MaxInputTokens { get; init; } = 8192;

    public int MaxOutputTokens { get; init; } = 1024;

    public bool SupportsSystemMessages { get; init; } = true;

    public bool SupportsDeveloperMessages { get; init; }

    public bool RequiresAlternatingUserAssistantMessages { get; init; }

    public string SystemInstructionRole { get; init; } = ModelMessageRoles.System;

    public ImmutableArray<string> SupportedRoles { get; init; }
        =
        [
            ModelMessageRoles.System,
            ModelMessageRoles.User,
            ModelMessageRoles.Assistant
        ];

    public ImmutableDictionary<string, string> Metadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
