namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;
using AIKernel.Kernel;
using AIKernel.Vfs;

public sealed class KernelVfsSessionFactory : IKernelVfsSessionFactory
{
    private readonly IReadOnlyDictionary<string, IVfsProvider> _providers;

    public KernelVfsSessionFactory(IEnumerable<IVfsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(
            provider => provider.ProviderId,
            StringComparer.Ordinal);
    }

    public async Task<IVfsSession> OpenSessionAsync(
        KernelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_providers.TryGetValue(request.VfsProviderId, out var provider))
        {
            throw new KernelRequestValidationException(
                $"VFS provider was not found. ProviderId='{request.VfsProviderId}'.");
        }

        var available = await provider
            .IsAvailableAsync()
            .ConfigureAwait(false);

        if (!available)
        {
            throw new KernelRequestValidationException(
                $"VFS provider is not available. ProviderId='{request.VfsProviderId}'.");
        }

        // Side effect boundary:
        // Opens provider-specific VFS session.
        return await provider
            .OpenSessionAsync(new KernelVfsCredentialsAdapter(request.Credentials))
            .ConfigureAwait(false);
    }

    private sealed class KernelVfsCredentialsAdapter(
        AIKernel.Dtos.Vfs.VfsCredentials credentials) : IVfsCredentials
    {
        public string? Username => credentials.Username;

        public string? ApiKey => credentials.ApiKey;

        public string? Token => credentials.Token;

        public IReadOnlyDictionary<string, object>? Parameters =>
            credentials.Parameters?.ToDictionary(
                item => item.Key,
                item => (object)item.Value,
                StringComparer.Ordinal);
    }
}
