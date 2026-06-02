namespace AIKernel.Providers.MicrosoftAI;

public sealed record OpenAICompatibleProviderHealthContext
{
    public required string ProviderId { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string ModelId { get; init; }

    public required bool IsInitialized { get; init; }

    public required DateTimeOffset CheckedAtUtc { get; init; }
}