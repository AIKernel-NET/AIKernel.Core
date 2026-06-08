namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;
using Microsoft.Extensions.Options;
using System.Net;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProvider']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProvider']" />
public sealed class WebGetFileProvider : FileProviderBase
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string? _probePath;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.#ctor']" />
    public WebGetFileProvider(
        IOptions<WebGetFileProviderOptions> options,
        HttpClient httpClient,
        IKernelClock? clock = null)
        : this(
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            httpClient,
            clock)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.#ctor']" />
    public WebGetFileProvider(
        WebGetFileProviderOptions options,
        HttpClient httpClient,
        IKernelClock? clock = null)
        : base(
            options.ProviderId,
            options.Name,
            clock ?? options.Clock,
            options.CredentialValidator)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (options.BaseUri is null)
        {
            throw new ArgumentException(
                "WebGetFileProvider BaseUri is required.",
                nameof(options));
        }

        if (options.BaseUri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException(
                "WebGetFileProvider only supports http and https.",
                nameof(options));
        }

        _baseUri = options.BaseUri;
        _probePath = options.ProbePath;
        _httpClient = httpClient;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.IsAvailableAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.IsAvailableAsync']" />
    public override async Task<bool> IsAvailableAsync()
    {
        if (_probePath is null)
        {
            return true;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Head,
                ResolveUri(_probePath));

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.MethodNotAllowed;
        }
        catch
        {
            return false;
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.GetHealthAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.GetHealthAsync']" />
    public override async Task<VfsProviderHealth> GetHealthAsync()
    {
        var available = await IsAvailableAsync()
            .ConfigureAwait(false);

        return new VfsProviderHealth
        {
            IsHealthy = available,
            Message = available ? "OK" : "Web GET provider probe failed.",
            CheckedAtUtc = Clock.Now
        };
    }

    /// <summary>Executes the OpenSessionCoreAsync operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenSessionCoreAsync 操作を実行します。</summary>
    protected override Task<IVfsSession> OpenSessionCoreAsync(string sessionId)
    {
        IVfsSession session = new WebGetFileSession(
            sessionId,
            _baseUri,
            _httpClient,
            Clock);

        return Task.FromResult(session);
    }

    private Uri ResolveUri(string path)
    {
        var normalized = VfsPathRules.Normalize(path);

        var escaped = string.Join(
            '/',
            normalized
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return new Uri(_baseUri, escaped);
    }

    private sealed class WebGetFileSession(
        string sessionId,
        Uri baseUri,
        HttpClient httpClient,
        IKernelClock clock) : IVfsSession
    {
        private readonly Uri _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IKernelClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.IsNullOrWhiteSpace']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.IsNullOrWhiteSpace']" />
        public string SessionId { get; } = string.IsNullOrWhiteSpace(sessionId)
                ? throw new ArgumentException("SessionId is required.", nameof(sessionId))
                : sessionId;

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.ReadFileAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.ReadFileAsync']" />
        public async Task<IVfsFile> ReadFileAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);
            var uri = ResolveUri(normalized);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException(
                    $"Web VFS file was not found. Path='{normalized}'.",
                    normalized);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new IOException(
                    $"Web VFS GET failed. StatusCode={response.StatusCode}");
            }

            var content = await response.Content
                .ReadAsByteArrayAsync()
                .ConfigureAwait(false);

            // Last-Modified が存在する場合は HTTP レスポンスの観測事実として採用する。
            // 欠落時のみ IKernelClock で補完する。
            //
            // DateTime.UtcNow は使わない。
            // Web リソースに timestamp が無い場合でも、IKernelClock を使えば
            // テストや Replay で同じ時刻を再現できる。
            var observedAtUtc = response.Content.Headers.LastModified?.UtcDateTime
                ?? _clock.Now.UtcDateTime;

            return new VfsFileSnapshot(
                name: VfsPathRules.GetName(normalized),
                path: normalized,
                content: content,
                createdAt: observedAtUtc,
                modifiedAt: observedAtUtc,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Uri"] = uri.ToString(),
                    ["ContentType"] = response.Content.Headers.ContentType?.ToString() ?? string.Empty
                });
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.WriteFileAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.WriteFileAsync']" />
        public Task WriteFileAsync(string path, byte[] content)
        {
            throw new UnauthorizedAccessException(
                "WebGetFileProvider is GET-only and does not support write.");
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.GetDirectoryAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.GetDirectoryAsync']" />
        public Task<IVfsDirectory> GetDirectoryAsync(string path)
        {
            throw new DirectoryNotFoundException(
                "WebGetFileProvider does not expose directory enumeration.");
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.ExistsAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.ExistsAsync']" />
        public async Task<bool> ExistsAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);
            var uri = ResolveUri(normalized);

            using var request = new HttpRequestMessage(HttpMethod.Head, uri);

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                return await ExistsByGetAsync(uri)
                    .ConfigureAwait(false);
            }

            return response.IsSuccessStatusCode;
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.DeleteAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.DeleteAsync']" />
        public Task DeleteAsync(string path)
        {
            throw new UnauthorizedAccessException(
                "WebGetFileProvider is GET-only and does not support delete.");
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.QueryAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.QueryAsync']" />
        public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return Task.FromResult<IVfsQueryResult>(
                VfsQueryResultSnapshot.Failure(
                    "WebGetFileProvider does not support VFS entry queries."));
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.DisposeAsync']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProvider.DisposeAsync']" />
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private async Task<bool> ExistsByGetAsync(Uri uri)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }

        private Uri ResolveUri(string path)
        {
            var escaped = string.Join(
                '/',
                path
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            return new Uri(_baseUri, escaped);
        }
    }
}
