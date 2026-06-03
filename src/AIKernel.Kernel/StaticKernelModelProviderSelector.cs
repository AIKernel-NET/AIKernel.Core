namespace AIKernel.Kernel;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Kernel;
using AIKernel.Kernel;

public sealed class StaticKernelModelProviderSelector : IKernelModelProviderSelector
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers;

    public StaticKernelModelProviderSelector(IEnumerable<IModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(
            provider => provider.ProviderId,
            StringComparer.Ordinal);
    }

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
}
