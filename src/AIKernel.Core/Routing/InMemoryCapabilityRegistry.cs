namespace AIKernel.Core.Routing;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Routing;
using AIKernel.Dtos.Routing;
using AIKernel.Dtos.Rules;

public sealed class InMemoryCapabilityRegistry : ICapabilityRegistry
{
    private readonly ConcurrentDictionary<string, ModelCapacityVector> _capabilities =
        new(StringComparer.Ordinal);

    public ValueTask RegisterCapabilityAsync(
        string providerId,
        ModelCapacityVector capacityVector,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(capacityVector);

        _capabilities[NormalizeProviderId(providerId)] = capacityVector;

        return ValueTask.CompletedTask;
    }

    public ValueTask<ModelCapacityVector?> GetCapabilityAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new KeyNotFoundException("Provider id is required.");
        }

        var normalized = NormalizeProviderId(providerId);

        _capabilities.TryGetValue(normalized, out var capacityVector);

        return ValueTask.FromResult(capacityVector);
    }

    public ValueTask<IReadOnlyList<string>> ResolveCandidatesAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<string> candidates = _capabilities.Keys
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return ValueTask.FromResult(candidates);
    }

    private static string NormalizeProviderId(string providerId)
    {
        return providerId.Trim();
    }
}
