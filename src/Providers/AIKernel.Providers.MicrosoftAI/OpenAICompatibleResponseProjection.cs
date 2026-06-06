namespace AIKernel.Providers.MicrosoftAI;

using System.Collections.Immutable;

public sealed record OpenAICompatibleResponseProjection
{
    public required string ModelId { get; init; }

    public required string RawResponse { get; init; }

    public required string PrimaryText { get; init; }

    public required bool IsTruncated { get; init; }

    public required DateTimeOffset ObservedAtUtc { get; init; }

    public required ImmutableDictionary<string, string> Metadata { get; init; }
}
