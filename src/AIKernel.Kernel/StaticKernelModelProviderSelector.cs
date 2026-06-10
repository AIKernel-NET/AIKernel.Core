namespace AIKernel.Kernel;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Kernel;
using AIKernel.Kernel;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.StaticKernelModelProviderSelector']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.StaticKernelModelProviderSelector']/summary" />
public sealed class StaticKernelModelProviderSelector : IKernelModelProviderSelector
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.StaticKernelModelProviderSelector.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.StaticKernelModelProviderSelector.#ctor']/summary" />
    public StaticKernelModelProviderSelector(IEnumerable<IModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = BuildProviderMap(providers);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.StaticKernelModelProviderSelector.SelectAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.StaticKernelModelProviderSelector.SelectAsync']/summary" />
    public Task<IModelProvider> SelectAsync(
        KernelRequest request,
        IContextSnapshot contextSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(contextSnapshot);

        cancellationToken.ThrowIfCancellationRequested();

        var providerId = request.Metadata.TryGetValue(KernelFacadeMetadataKeys.ProviderId, out var value)
            ? value
            : null;

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new KernelRequestValidationException(
                $"{KernelFacadeMetadataKeys.ProviderId} metadata is required for static provider selection.");
        }

        if (!_providers.TryGetValue(providerId, out var provider))
        {
            throw new KernelRequestValidationException(
                $"Model provider was not found. ProviderId='{providerId}'.");
        }

        return Task.FromResult(provider);
    }

    private static IReadOnlyDictionary<string, IModelProvider> BuildProviderMap(
        IEnumerable<IModelProvider> providers)
    {
        var map = new Dictionary<string, IModelProvider>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            ValidateProvider(provider);

            if (!map.TryAdd(provider.ProviderId, provider))
            {
                throw new ArgumentException(
                    $"Duplicate model provider registration. ProviderId='{provider.ProviderId}'.",
                    nameof(providers));
            }
        }

        return map;
    }

    private static void ValidateProvider(IModelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ProviderId))
        {
            throw new ArgumentException(
                "IModelProvider.ProviderId is required.",
                nameof(provider));
        }
    }
}
