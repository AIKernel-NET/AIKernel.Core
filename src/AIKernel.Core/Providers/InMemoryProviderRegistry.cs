namespace AIKernel.Core.Providers;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Providers;

public sealed class InMemoryProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, IProvider> _providers =
        new(StringComparer.Ordinal);

    public InMemoryProviderRegistry()
    {
    }

    public InMemoryProviderRegistry(IEnumerable<IProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            RegisterProvider(provider.ProviderId, provider);
        }
    }

    public void RegisterProvider(string name, IProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);

        _providers[NormalizeName(name)] = provider;
    }

    public bool UnregisterProvider(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _providers.TryRemove(NormalizeName(name), out _);
    }

    public IReadOnlyList<string> GetRegisteredProviders()
    {
        return _providers.Keys
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeName(string name)
    {
        return name.Trim();
    }
}
