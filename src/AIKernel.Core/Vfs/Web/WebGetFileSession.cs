namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Vfs;
using System.Net;

/// <summary>[EN] Documents this public package API member. [JA] WebGetFileSession を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileSession']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileSession']/summary" />
public sealed class WebGetFileSession : IVfsSession
{
    private readonly Uri _baseUri;
    private readonly HttpClient _httpClient;
    private readonly IKernelClock _clock;

    /// <summary>[EN] Documents this public package API member. [JA] WebGetFileSession を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.#ctor']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] SessionId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileSession.SessionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileSession.SessionId']/summary" />
    public string SessionId { get; }

    /// <summary>[EN] Documents this public package API member. [JA] ReadFileAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ReadFileAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ReadFileAsync']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] WriteFileAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.WriteFileAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.WriteFileAsync']/summary" />
    public Task WriteFileAsync(string path, byte[] content)
    {
        throw new UnauthorizedAccessException(
            "WebGetFileProvider is GET-only and does not support write.");
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetDirectoryAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.GetDirectoryAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.GetDirectoryAsync']/summary" />
    public Task<IVfsDirectory> GetDirectoryAsync(string path)
    {
        throw new DirectoryNotFoundException(
            "WebGetFileProvider does not expose directory enumeration.");
    }

    /// <summary>[EN] Documents this public package API member. [JA] ExistsAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ExistsAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.ExistsAsync']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] DeleteAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DeleteAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DeleteAsync']/summary" />
    public Task DeleteAsync(string path)
    {
        throw new UnauthorizedAccessException(
            "WebGetFileProvider is GET-only and does not support delete.");
    }

    /// <summary>[EN] Documents this public package API member. [JA] QueryAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.QueryAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.QueryAsync']/summary" />
    public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult<IVfsQueryResult>(
            VfsQueryResultSnapshot.Failure(
                "WebGetFileProvider does not support VFS entry queries."));
    }

    /// <summary>[EN] Documents this public package API member. [JA] DisposeAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DisposeAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileSession.DisposeAsync']/summary" />
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
