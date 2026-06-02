namespace AIKernel.Kernel;

using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;

internal sealed class FailClosedProviderRouter : IProviderRouter
{
    private static readonly DateTime StableRetrievedAt = DateTime.UnixEpoch;

    public static FailClosedProviderRouter Instance { get; } = new();

    private FailClosedProviderRouter()
    {
    }

    public Task<MaterialContextDto> RetrieveAsync(
        string source,
        string query)
    {
        return Task.FromResult(new MaterialContextDto
        {
            Source = source,
            RawData = string.Empty,
            NormalizedData = string.Empty,
            RelevanceScore = 0.0,
            RetrievedAt = StableRetrievedAt
        });
    }

    public Task<IReadOnlyList<MaterialContextDto>> RetrieveMultipleAsync(
        IReadOnlyList<string> sources,
        string query)
    {
        ArgumentNullException.ThrowIfNull(sources);

        IReadOnlyList<MaterialContextDto> materials = sources
            .Select(source => new MaterialContextDto
            {
                Source = source,
                RawData = string.Empty,
                NormalizedData = string.Empty,
                RelevanceScore = 0.0,
                RetrievedAt = StableRetrievedAt
            })
            .ToArray();

        return Task.FromResult(materials);
    }

    public Task<MaterialContextDto?> GetFromCacheAsync(
        string cacheKey)
    {
        return Task.FromResult<MaterialContextDto?>(null);
    }

    public Task CacheMaterialAsync(
        string cacheKey,
        MaterialContextDto data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Task.CompletedTask;
    }

    public void RegisterProvider(
        string name,
        IProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);
    }

    public bool UnregisterProvider(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return false;
    }

    public IReadOnlyList<string> GetRegisteredProviders()
    {
        return [];
    }
}
