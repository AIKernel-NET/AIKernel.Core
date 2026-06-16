namespace AIKernel.Core.Vfs.Memory;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Dtos.Vfs;
using AIKernel.Enums;
using AIKernel.Vfs;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

/// <summary>[EN] Documents this public package API member. [JA] MemoryFileProvider を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProvider']/summary" />
public sealed class MemoryFileProvider(
    MemoryFileProviderOptions? options = null,
    IKernelClock? clock = null) : FileProviderBase(
        options?.ProviderId ?? "memory-file",
        options?.Name ?? "Memory File Provider",
        clock ?? options?.Clock,
        options?.CredentialValidator)
{
    private readonly ConcurrentDictionary<string, MemoryFileState> _files =
        new(StringComparer.Ordinal);

    /// <summary>[EN] Documents this public package API member. [JA] MemoryFileProvider を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.#ctor']/summary" />
    public MemoryFileProvider(
        IOptions<MemoryFileProviderOptions> options,
        IKernelClock? clock = null)
        : this(
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            clock)
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] Seed を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.Seed']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.Seed']/summary" />
    public void Seed(string path, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = VfsPathRules.Normalize(path);
        var now = Clock.Now.UtcDateTime;

        // IKernelClock を使って seed 時刻を固定する。
        // テストでは replay clock を渡すことで、同じ seed 操作から同じ snapshot を再現できる。
        _files[normalized] = new MemoryFileState(
            Content: [.. content],
            CreatedAtUtc: now,
            ModifiedAtUtc: now);
    }

    /// <summary>[EN] Documents this public package API member. [JA] IsAvailableAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.IsAvailableAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.IsAvailableAsync']/summary" />
    public override Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetHealthAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.GetHealthAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.GetHealthAsync']/summary" />
    public override Task<VfsProviderHealth> GetHealthAsync()
    {
        return Task.FromResult(new VfsProviderHealth
        {
            IsHealthy = true,
            Message = "OK",
            CheckedAtUtc = Clock.Now
        });
    }

    /// <summary>EN: Executes the OpenSessionCoreAsync operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenSessionCoreAsync 操作を実行します。</summary>
    protected override Task<IVfsSession> OpenSessionCoreAsync(string sessionId)
    {
        // Provider が保持する Clock を Session へ必ず渡す。
        // Session 側で直接 DateTime.UtcNow を読むことを禁止し、時間の出所を一元化する。
        IVfsSession session = new MemoryFileSession(
            sessionId,
            _files,
            Clock);

        return Task.FromResult(session);
    }

    private sealed record MemoryFileState(
        byte[] Content,
        DateTime CreatedAtUtc,
        DateTime ModifiedAtUtc);

    private sealed class MemoryFileSession(
        string sessionId,
        ConcurrentDictionary<string, MemoryFileProvider.MemoryFileState> files,
        IKernelClock clock) : IVfsSession
    {
        private readonly ConcurrentDictionary<string, MemoryFileState> _files = files ?? throw new ArgumentNullException(nameof(files));
        private readonly IKernelClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        /// <summary>[EN] Documents this public package API member. [JA] SessionId を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.IsNullOrWhiteSpace']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.IsNullOrWhiteSpace']/summary" />
        public string SessionId { get; } = string.IsNullOrWhiteSpace(sessionId)
                ? throw new ArgumentException("SessionId is required.", nameof(sessionId))
                : sessionId;

        /// <summary>[EN] Documents this public package API member. [JA] ReadFileAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.ReadFileAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.ReadFileAsync']/summary" />
        public Task<IVfsFile> ReadFileAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);

            if (!_files.TryGetValue(normalized, out var state))
            {
                throw new FileNotFoundException(
                    $"Memory VFS file was not found. Path='{normalized}'.",
                    normalized);
            }

            return Task.FromResult<IVfsFile>(
                new VfsFileSnapshot(
                    name: VfsPathRules.GetName(normalized),
                    path: normalized,
                    content: state.Content,
                    createdAt: state.CreatedAtUtc,
                    modifiedAt: state.ModifiedAtUtc));
        }

        /// <summary>[EN] Documents this public package API member. [JA] WriteFileAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.WriteFileAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.WriteFileAsync']/summary" />
        public Task WriteFileAsync(string path, byte[] content)
        {
            ArgumentNullException.ThrowIfNull(content);

            var normalized = VfsPathRules.Normalize(path);
            var now = _clock.Now.UtcDateTime;

            // 時間の統治:
            // Memory VFS は物理ファイルシステムを持たないため、
            // 作成時刻・更新時刻は IKernelClock から取得する。
            //
            // DateTime.UtcNow を直接使うと、同じ Write 操作でも実行タイミングにより
            // snapshot の時刻が変わり、Deterministic Replay を阻害する。
            _files.AddOrUpdate(
                normalized,
                _ => new MemoryFileState(
                    Content: [.. content],
                    CreatedAtUtc: now,
                    ModifiedAtUtc: now),
                (_, existing) => existing with
                {
                    Content = [.. content],
                    ModifiedAtUtc = now
                });

            return Task.CompletedTask;
        }

        /// <summary>[EN] Documents this public package API member. [JA] GetDirectoryAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.GetDirectoryAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.GetDirectoryAsync']/summary" />
        public Task<IVfsDirectory> GetDirectoryAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);

            var allFiles = _files
                .Where(x => VfsPathRules.IsUnder(normalized, x.Key))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();

            if (normalized.Length > 0 && allFiles.Length == 0)
            {
                throw new DirectoryNotFoundException(
                    $"Memory VFS directory was not found. Path='{normalized}'.");
            }

            var directFiles = allFiles
                .Where(x => VfsPathRules.IsDirectChild(normalized, x.Key))
                .Select(ToFileSnapshot)
                .ToArray();

            var recursiveFiles = allFiles
                .Select(ToFileSnapshot)
                .ToArray();

            var entries = allFiles
                .Select(x => new VfsEntry
                {
                    Name = VfsPathRules.GetName(x.Key),
                    Path = x.Key,
                    Type = VfsEntryType.File,
                    Size = x.Value.Content.Length,
                    CreatedAt = x.Value.CreatedAtUtc,
                    ModifiedAt = x.Value.ModifiedAtUtc
                })
                .ToArray();

            return Task.FromResult<IVfsDirectory>(
                new VfsDirectorySnapshot(
                    name: VfsPathRules.GetName(normalized),
                    path: normalized,
                    directFiles: directFiles,
                    recursiveFiles: recursiveFiles,
                    directories: [],
                    entries: entries));
        }

        /// <summary>[EN] Documents this public package API member. [JA] ExistsAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.ExistsAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.ExistsAsync']/summary" />
        public Task<bool> ExistsAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);

            var exists =
                _files.ContainsKey(normalized)
                || _files.Keys.Any(x => VfsPathRules.IsUnder(normalized, x));

            return Task.FromResult(exists);
        }

        /// <summary>[EN] Documents this public package API member. [JA] DeleteAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.DeleteAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.DeleteAsync']/summary" />
        public Task DeleteAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);

            _files.TryRemove(normalized, out _);

            return Task.CompletedTask;
        }

        /// <summary>[EN] Documents this public package API member. [JA] QueryAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.QueryAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.QueryAsync']/summary" />
        public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var entries = _files
                .Select(x => new VfsEntry
                {
                    Name = VfsPathRules.GetName(x.Key),
                    Path = x.Key,
                    Type = VfsEntryType.File,
                    Size = x.Value.Content.Length,
                    CreatedAt = x.Value.CreatedAtUtc,
                    ModifiedAt = x.Value.ModifiedAtUtc
                });

            return Task.FromResult(VfsEntryQueryEngine.Execute(entries, query));
        }

        /// <summary>[EN] Documents this public package API member. [JA] DisposeAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.DisposeAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProvider.DisposeAsync']/summary" />
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static IVfsFile ToFileSnapshot(
            KeyValuePair<string, MemoryFileState> pair)
        {
            return new VfsFileSnapshot(
                name: VfsPathRules.GetName(pair.Key),
                path: pair.Key,
                content: pair.Value.Content,
                createdAt: pair.Value.CreatedAtUtc,
                modifiedAt: pair.Value.ModifiedAtUtc);
        }
    }
}
