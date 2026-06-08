namespace AIKernel.Core.Providers;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Providers;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Providers.InMemoryProviderRegistry']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Providers.InMemoryProviderRegistry']" />
public sealed class InMemoryProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, IProvider> _providers =
        new(StringComparer.Ordinal);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.#ctor']" />
    public InMemoryProviderRegistry()
    {
    }

    /// <summary>Initializes a new instance for the InMemoryProviderRegistry AIKernel contract surface. JA: InMemoryProviderRegistry AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    public InMemoryProviderRegistry(IEnumerable<IProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            RegisterProvider(provider.ProviderId, provider);
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.RegisterProvider']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.RegisterProvider']" />
    public void RegisterProvider(string name, IProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);

        _providers[NormalizeName(name)] = provider;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.UnregisterProvider']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.UnregisterProvider']" />
    public bool UnregisterProvider(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _providers.TryRemove(NormalizeName(name), out _);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.GetRegisteredProviders']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Providers.InMemoryProviderRegistry.GetRegisteredProviders']" />
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
