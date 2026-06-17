namespace AIKernel.Kernel;

using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;

internal sealed class FailClosedProviderRouter : IProviderRouter
{
    private static readonly DateTime StableRetrievedAt = DateTime.UnixEpoch;
    /// <summary>
    /// EN: Executes Instance.
    /// [EN] Documents this public package API member. [JA] Instance を実行します。
    /// </summary>

    public static FailClosedProviderRouter Instance { get; } = new();

    private FailClosedProviderRouter()
    {
    }
    /// <summary>
    /// EN: Gets RetrieveAsync.
    /// [EN] Documents this public package API member. [JA] RetrieveAsync を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets RetrieveMultipleAsync.
    /// [EN] Documents this public package API member. [JA] RetrieveMultipleAsync を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets GetFromCacheAsync.
    /// [EN] Documents this public package API member. [JA] GetFromCacheAsync を取得します。
    /// </summary>

    public Task<MaterialContextDto?> GetFromCacheAsync(
        string cacheKey)
    {
        return Task.FromResult<MaterialContextDto?>(null);
    }
    /// <summary>
    /// EN: Gets CacheMaterialAsync.
    /// [EN] Documents this public package API member. [JA] CacheMaterialAsync を取得します。
    /// </summary>

    public Task CacheMaterialAsync(
        string cacheKey,
        MaterialContextDto data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Task.CompletedTask;
    }
    /// <summary>
    /// EN: Gets RegisterProvider.
    /// [EN] Documents this public package API member. [JA] RegisterProvider を取得します。
    /// </summary>

    public void RegisterProvider(
        string name,
        IProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);
    }
    /// <summary>
    /// EN: Gets UnregisterProvider.
    /// [EN] Documents this public package API member. [JA] UnregisterProvider を取得します。
    /// </summary>

    public bool UnregisterProvider(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return false;
    }
    /// <summary>
    /// EN: Executes GetRegisteredProviders.
    /// [EN] Documents this public package API member. [JA] GetRegisteredProviders を実行します。
    /// </summary>

    public IReadOnlyList<string> GetRegisteredProviders()
    {
        return [];
    }
}
