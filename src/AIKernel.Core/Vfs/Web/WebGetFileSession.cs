namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Vfs;
using System.Net;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileSession']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileSession']" />
public sealed class WebGetFileSession : IVfsSession
{
    private readonly Uri _baseUri;
    private readonly HttpClient _httpClient;
    private readonly IKernelClock _clock;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.#ctor']" />
    public WebGetFileSession(
        string sessionId,
        Uri baseUri,
        HttpClient httpClient,
        IKernelClock clock)
    {
        SessionId = string.IsNullOrWhiteSpace(sessionId)
            ? throw new ArgumentException("SessionId is required.", nameof(sessionId))
            : sessionId;

        _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileSession.SessionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileSession.SessionId']" />
    public string SessionId { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ReadFileAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ReadFileAsync']" />
    public async Task<IVfsFile> ReadFileAsync(string path)
    {
        var normalized = VfsPathRules.Normalize(path);
        var uri = ResolveUri(normalized);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Side effect:
        // 外部 HTTP GET。WebGetFileProvider における実データ取得の唯一の境界。
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException(
                $"Web VFS file was not found: {normalized}",
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

        // Last-Modified があれば HTTP レスポンスの観測事実として採用する。
        // 欠落時のみ IKernelClock.Now で補完し、Replay 時にも同じ時刻を再現できるようにする。
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.WriteFileAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.WriteFileAsync']" />
    public Task WriteFileAsync(string path, byte[] content)
    {
        throw new UnauthorizedAccessException(
            "WebGetFileProvider is GET-only and does not support write.");
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.GetDirectoryAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.GetDirectoryAsync']" />
    public Task<IVfsDirectory> GetDirectoryAsync(string path)
    {
        throw new DirectoryNotFoundException(
            "WebGetFileProvider does not expose directory enumeration.");
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ExistsAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ExistsAsync']" />
    public async Task<bool> ExistsAsync(string path)
    {
        var normalized = VfsPathRules.Normalize(path);
        var uri = ResolveUri(normalized);

        using var request = new HttpRequestMessage(HttpMethod.Head, uri);

        // Side effect:
        // 外部 HTTP HEAD。存在確認のための I/O。
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DeleteAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DeleteAsync']" />
    public Task DeleteAsync(string path)
    {
        throw new UnauthorizedAccessException(
            "WebGetFileProvider is GET-only and does not support delete.");
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.QueryAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.QueryAsync']" />
    public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult<IVfsQueryResult>(
            VfsQueryResultSnapshot.Failure(
                "WebGetFileProvider does not support VFS entry queries."));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DisposeAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DisposeAsync']" />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<bool> ExistsByGetAsync(Uri uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Side effect:
        // HEAD 非対応サーバー向けの GET fallback。
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
