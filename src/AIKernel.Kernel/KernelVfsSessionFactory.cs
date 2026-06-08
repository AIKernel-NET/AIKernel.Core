namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;
using AIKernel.Kernel;
using AIKernel.Vfs;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelVfsSessionFactory']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelVfsSessionFactory']" />
public sealed class KernelVfsSessionFactory : IKernelVfsSessionFactory
{
    private readonly IReadOnlyDictionary<string, IVfsProvider> _providers;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.#ctor']" />
    public KernelVfsSessionFactory(IEnumerable<IVfsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(
            provider => provider.ProviderId,
            StringComparer.Ordinal);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.OpenSessionAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.OpenSessionAsync']" />
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
        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Username']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Username']" />
        public string? Username => credentials.Username;

        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.ApiKey']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.ApiKey']" />
        public string? ApiKey => credentials.ApiKey;

        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Token']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Token']" />
        public string? Token => credentials.Token;

        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.object']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.object']" />
        public IReadOnlyDictionary<string, object>? Parameters =>
            credentials.Parameters?.ToDictionary(
                item => item.Key,
                item => (object)item.Value,
                StringComparer.Ordinal);
    }
}
