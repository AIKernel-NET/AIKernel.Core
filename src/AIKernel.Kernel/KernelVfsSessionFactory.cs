namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;
using AIKernel.Kernel;
using AIKernel.Vfs;

/// <summary>EN: Documentation for public API. JA: KernelVfsSessionFactory を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelVfsSessionFactory']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelVfsSessionFactory']/summary" />
public sealed class KernelVfsSessionFactory : IKernelVfsSessionFactory
{
    private readonly IReadOnlyDictionary<string, IVfsProvider> _providers;

    /// <summary>EN: Documentation for public API. JA: KernelVfsSessionFactory を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.#ctor']/summary" />
    public KernelVfsSessionFactory(IEnumerable<IVfsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(
            provider => provider.ProviderId,
            StringComparer.Ordinal);
    }

    /// <summary>EN: Documentation for public API. JA: OpenSessionAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.OpenSessionAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelVfsSessionFactory.OpenSessionAsync']/summary" />
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
        /// <summary>EN: Documentation for public API. JA: Username を取得します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Username']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Username']/summary" />
        public string? Username => credentials.Username;

        /// <summary>EN: Documentation for public API. JA: ApiKey を取得します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.ApiKey']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.ApiKey']/summary" />
        public string? ApiKey => credentials.ApiKey;

        /// <summary>EN: Documentation for public API. JA: Token を取得します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Token']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.Token']/summary" />
        public string? Token => credentials.Token;

        /// <summary>EN: Documentation for public API. JA: Parameters を取得します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.object']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Kernel.KernelVfsSessionFactory.object']/summary" />
        public IReadOnlyDictionary<string, object>? Parameters =>
            credentials.Parameters?.ToDictionary(
                item => item.Key,
                item => (object)item.Value,
                StringComparer.Ordinal);
    }
}
