namespace AIKernel.Core.Vfs.Local;

using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Dtos.Vfs;
using AIKernel.Enums;
using AIKernel.Vfs;
using Microsoft.Extensions.Options;

/// <summary>[EN] Documents this public package API member. [JA] LocalFileProvider を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProvider']/summary" />
public sealed class LocalFileProvider : FileProviderBase
{
    private readonly string _rootPath;
    private readonly bool _allowWrite;

    /// <summary>[EN] Documents this public package API member. [JA] LocalFileProvider を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.#ctor']/summary" />
    public LocalFileProvider(
        IOptions<LocalFileProviderOptions> options,
        IKernelClock? clock = null)
        : this(
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            clock)
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] LocalFileProvider を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.#ctor']/summary" />
    public LocalFileProvider(
        LocalFileProviderOptions options,
        IKernelClock? clock = null)
        : base(
            options.ProviderId,
            options.Name,
            clock ?? options.Clock,
            options.CredentialValidator)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rootPath = Path.GetFullPath(options.RootPath);
        _allowWrite = options.AllowWrite;
    }

    /// <summary>[EN] Documents this public package API member. [JA] IsAvailableAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.IsAvailableAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.IsAvailableAsync']/summary" />
    public override Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(Directory.Exists(_rootPath));
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetHealthAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.GetHealthAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.GetHealthAsync']/summary" />
    public override Task<VfsProviderHealth> GetHealthAsync()
    {
        var exists = Directory.Exists(_rootPath);

        return Task.FromResult(new VfsProviderHealth
        {
            IsHealthy = exists,
            Message = MonadicDecision.SelectText(
                exists,
                $"Root path was not found: {_rootPath}",
                "OK"),
            CheckedAtUtc = Clock.Now
        });
    }

    /// <summary>EN: Executes the OpenSessionCoreAsync operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenSessionCoreAsync 操作を実行します。</summary>
    protected override Task<IVfsSession> OpenSessionCoreAsync(string sessionId)
    {
        // Provider が保持する Clock を LocalFileSession へ渡す。
        //
        // LocalFileProvider は物理ファイルシステムを扱うが、
        // 書き込み時刻を OS クロック任せにすると、同じテスト・同じ入力でも
        // 実行タイミングにより LastWriteTimeUtc が変化する。
        //
        // そのため、Core が能動的に生成する時刻は IKernelClock から取得し、
        // 物理ファイルが既に持つタイムスタンプは「観測された事実」として使用する。
        IVfsSession session = new LocalFileSession(
            sessionId,
            _rootPath,
            _allowWrite,
            Clock);

        return Task.FromResult(session);
    }

    private sealed class LocalFileSession(
        string sessionId,
        string rootPath,
        bool allowWrite,
        IKernelClock clock) : IVfsSession
    {
        private readonly string _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        private readonly IKernelClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        /// <summary>[EN] Documents this public package API member. [JA] SessionId を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.IsNullOrWhiteSpace']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.IsNullOrWhiteSpace']/summary" />
        public string SessionId { get; } = string.IsNullOrWhiteSpace(sessionId)
                ? throw new ArgumentException("SessionId is required.", nameof(sessionId))
                : sessionId;

        /// <summary>[EN] Documents this public package API member. [JA] ReadFileAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.ReadFileAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.ReadFileAsync']/summary" />
        public async Task<IVfsFile> ReadFileAsync(string path)
        {
            var normalized = path == "" ? "" : VfsPathRules.Normalize(path);
            if (normalized.Length == 0) throw new UnauthorizedAccessException("A VFS file path is required.");
            var fullPath = ResolveFilePath(normalized);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Local VFS file was not found. Path='{normalized}'.",
                    normalized);
            }

            RejectReparsePoint(fullPath);

            var info = new FileInfo(fullPath);

            var content = await ReadBoundedAsync(fullPath).ConfigureAwait(false);

            // 読み取り時は物理ファイルが持つ timestamp を使用する。
            // これは OS から現在時刻を読むのではなく、対象リソースに記録された観測事実を読む行為である。
            return new VfsFileSnapshot(
                name: info.Name,
                path: normalized,
                content: content,
                createdAt: info.CreationTimeUtc,
                modifiedAt: info.LastWriteTimeUtc,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ProviderPath"] = ToVirtualPath(fullPath)
                });
        }

        /// <summary>[EN] Documents this public package API member. [JA] WriteFileAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.WriteFileAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.WriteFileAsync']/summary" />
        public async Task WriteFileAsync(string path, byte[] content)
        {
            if (!allowWrite)
            {
                throw new UnauthorizedAccessException("Local VFS provider is read-only.");
            }

            ArgumentNullException.ThrowIfNull(content);

            var normalized = path == "" ? "" : VfsPathRules.Normalize(path);
            if (normalized.Length == 0) throw new UnauthorizedAccessException("A VFS file path is required.");
            var fullPath = ResolveFilePath(normalized);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var existed = File.Exists(fullPath);
            var timestamp = _clock.Now.UtcDateTime;

            await File.WriteAllBytesAsync(fullPath, content)
                .ConfigureAwait(false);

            // 時間の統治:
            // File.WriteAllBytesAsync 後の LastWriteTimeUtc は通常 OS クロックにより設定される。
            // そのままにすると、同じ書き込み操作でも実行タイミングにより snapshot が変わる。
            //
            // AIKernel.Core が能動的に発生させた書き込みについては、
            // IKernelClock から取得した時刻を明示的に反映し、Replay とテストを安定させる。
            if (!existed)
            {
                File.SetCreationTimeUtc(fullPath, timestamp);
            }

            File.SetLastWriteTimeUtc(fullPath, timestamp);
        }

        /// <summary>[EN] Documents this public package API member. [JA] GetDirectoryAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.GetDirectoryAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.GetDirectoryAsync']/summary" />
        public Task<IVfsDirectory> GetDirectoryAsync(string path)
        {
            var normalized = path == "" ? "" : VfsPathRules.Normalize(path);
            var fullPath = ResolveDirectoryPath(normalized);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(
                    $"Local VFS directory was not found. Path='{normalized}'.");
            }

            RejectReparsePoint(fullPath);

            var recursiveFiles = Bounded(Directory.EnumerateFiles(fullPath, "*", SafeEnumeration(true)))
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(ToVfsFileSnapshot).ToArray();
            var directFiles = recursiveFiles.Where(file =>
                StringComparer.Ordinal.Equals(Path.GetDirectoryName(file.Path)?.Replace('\\', '/') ?? "", normalized)).ToArray();

            var directories = Bounded(Directory.EnumerateDirectories(fullPath, "*", SafeEnumeration(false)))
                .Where(IsSafeLocalPath)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => new VfsDirectorySnapshot(
                    name: Path.GetFileName(x),
                    path: ToVirtualPath(x),
                    directFiles: [],
                    recursiveFiles: [],
                    directories: [],
                    entries: []))
                .Cast<IVfsDirectory>()
                .ToArray();

            var entries = recursiveFiles
                .Select(x => new VfsEntry
                {
                    Name = x.Name,
                    Path = x.Path,
                    Type = VfsEntryType.File,
                    Size = x.Size,
                    CreatedAt = x.CreatedAt,
                    ModifiedAt = x.ModifiedAt
                })
                .ToArray();

            return Task.FromResult<IVfsDirectory>(
                new VfsDirectorySnapshot(
                    name: normalized.Length == 0 ? "/" : VfsPathRules.GetName(normalized),
                    path: normalized,
                    directFiles: directFiles,
                    recursiveFiles: recursiveFiles,
                    directories: directories,
                    entries: entries));
        }

        /// <summary>[EN] Documents this public package API member. [JA] ExistsAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.ExistsAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.ExistsAsync']/summary" />
        public Task<bool> ExistsAsync(string path)
        {
            var fullPath = ResolvePath(path);

            return Task.FromResult(
                File.Exists(fullPath)
                || Directory.Exists(fullPath));
        }

        /// <summary>[EN] Documents this public package API member. [JA] DeleteAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.DeleteAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.DeleteAsync']/summary" />
        public Task DeleteAsync(string path)
        {
            if (!allowWrite)
            {
                throw new UnauthorizedAccessException("Local VFS provider is read-only.");
            }

            var fullPath = ResolvePath(path);
            if (Path.GetRelativePath(_rootPath, fullPath) == ".")
                throw new UnauthorizedAccessException("The VFS root cannot be deleted.");

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }

            return Task.CompletedTask;
        }

        /// <summary>[EN] Documents this public package API member. [JA] QueryAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.QueryAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.QueryAsync']/summary" />
        public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var entries = Bounded(Directory.EnumerateFiles(ResolvePath("/"), "*", SafeEnumeration(true)))
                .Where(IsSafeLocalPath)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x =>
                {
                    var info = new FileInfo(x);

                    return new VfsEntry
                    {
                        Name = info.Name,
                        Path = ToVirtualPath(x),
                        Type = VfsEntryType.File,
                        Size = info.Length,
                        CreatedAt = info.CreationTimeUtc,
                        ModifiedAt = info.LastWriteTimeUtc
                    };
                });

            return Task.FromResult(VfsEntryQueryEngine.Execute(entries, query));
        }

        /// <summary>[EN] Documents this public package API member. [JA] DisposeAsync を実行します。</summary>
        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.DisposeAsync']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProvider.DisposeAsync']/summary" />
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private string ResolveFilePath(string path)
        {
            return ResolvePath(path);
        }

        private string ResolveDirectoryPath(string path)
        {
            return ResolvePath(path);
        }

        private string ResolvePath(string path)
        {
            var normalized = path == "" ? "" : VfsPathRules.Normalize(path);

            var combined = Path.Combine(
                _rootPath,
                normalized.Replace('/', Path.DirectorySeparatorChar));

            var fullPath = Path.GetFullPath(combined);

            if (!IsSafeLocalPath(fullPath))
            {
                throw new UnauthorizedAccessException(
                    $"Path escapes local VFS root. Path='{path}'.");
            }

            RejectReparsePoint(_rootPath);
            var current = _rootPath;
            foreach (var segment in Path.GetRelativePath(_rootPath, fullPath).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                RejectReparsePoint(current);
            }
            return fullPath;
        }

        private bool IsSafeLocalPath(string fullPath)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            var normalizedRoot = _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            return fullPath.Equals(normalizedRoot, comparison)
                || fullPath.StartsWith(
                    normalizedRoot + Path.DirectorySeparatorChar,
                    comparison);
        }

        private static void RejectReparsePoint(string fullPath)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(fullPath); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Reparse points are not allowed in LocalFileProvider. Path='{fullPath}'.");
            }
        }

        private static EnumerationOptions SafeEnumeration(bool recursive) => new()
        {
            RecurseSubdirectories = recursive,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };

        private IVfsFile ToVfsFileSnapshot(string fullPath)
        {
            RejectReparsePoint(fullPath);

            var info = new FileInfo(fullPath);
            var virtualPath = ToVirtualPath(fullPath);
            return new VfsFileSnapshot(info.Name, virtualPath, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc,
                () => ReadBoundedAsync(ResolveFilePath(virtualPath)));
        }

        private static IEnumerable<string> Bounded(IEnumerable<string> paths)
        {
            var count = 0;
            foreach (var path in paths)
            {
                if (++count > 65_536) throw new IOException("Local VFS listing limit exceeded.");
                yield return path;
            }
        }

        private static async Task<byte[]> ReadBoundedAsync(string fullPath)
        {
            using var stream = File.OpenRead(fullPath);
            if (stream.Length > 64 * 1024 * 1024) throw new IOException("Local VFS file exceeds 64 MiB.");
            var data = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(data).ConfigureAwait(false);
            return data;
        }

        private string ToVirtualPath(string fullPath)
        {
            var relative = Path.GetRelativePath(_rootPath, fullPath);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
