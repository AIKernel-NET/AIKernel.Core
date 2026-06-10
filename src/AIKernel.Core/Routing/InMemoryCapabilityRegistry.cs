namespace AIKernel.Core.Routing;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Routing;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Routing;
using AIKernel.Dtos.Rules;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Routing.InMemoryCapabilityRegistry']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Routing.InMemoryCapabilityRegistry']/summary" />
public sealed class InMemoryCapabilityRegistry : ICapabilityRegistry
{
    private readonly ConcurrentDictionary<string, ModelCapacityVector> _capabilities =
        new(StringComparer.Ordinal);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.#ctor']/summary" />
    public InMemoryCapabilityRegistry()
    {
    }

    /// <summary>
    /// [EN] Initializes a registry with pre-registered model prompt capabilities.
    /// [JA] 事前登録された model prompt capabilities を持つ registry を初期化します。
    /// </summary>
    public InMemoryCapabilityRegistry(IEnumerable<ModelPromptCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        foreach (var capability in capabilities)
        {
            RegisterCapability(
                capability.ProviderId,
                CreateCapacityVector(capability));
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.RegisterCapabilityAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.RegisterCapabilityAsync']/summary" />
    public ValueTask RegisterCapabilityAsync(
        string providerId,
        ModelCapacityVector capacityVector,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RegisterCapability(providerId, capacityVector);

        return ValueTask.CompletedTask;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.GetCapabilityAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.GetCapabilityAsync']/summary" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.ResolveCandidatesAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Routing.InMemoryCapabilityRegistry.ResolveCandidatesAsync']/summary" />
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

    private void RegisterCapability(
        string providerId,
        ModelCapacityVector capacityVector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(capacityVector);

        _capabilities[NormalizeProviderId(providerId)] = capacityVector;
    }

    private static string NormalizeProviderId(string providerId)
    {
        return providerId.Trim();
    }

    private static ModelCapacityVector CreateCapacityVector(ModelPromptCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var tokenCapacity = Math.Max(1, capability.MaxInputTokens + capability.MaxOutputTokens);
        var latencyScore = 1f / tokenCapacity;

        return new ModelCapacityVector(
            structuralIntegrity: 1,
            linguisticFluidity: 1,
            reasoningDepth: 1,
            fidelity: 1,
            latencyPerformance: latencyScore);
    }
}
