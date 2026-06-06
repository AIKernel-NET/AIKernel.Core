namespace AIKernel.Core.Vfs.Local;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Dtos.Vfs;
using AIKernel.Enums;
using AIKernel.Vfs;
using Microsoft.Extensions.Options;

public sealed class LocalFileProvider : FileProviderBase
{
    private readonly string _rootPath;
    private readonly bool _allowWrite;

    public LocalFileProvider(
        IOptions<LocalFileProviderOptions> options,
        IKernelClock? clock = null)
        : this(
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            clock)
    {
    }

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

    public override Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(Directory.Exists(_rootPath));
    }

    public override Task<VfsProviderHealth> GetHealthAsync()
    {
        var exists = Directory.Exists(_rootPath);

        return Task.FromResult(new VfsProviderHealth
        {
            IsHealthy = exists,
            Message = exists ? "OK" : $"Root path was not found: {_rootPath}",
            CheckedAtUtc = Clock.Now
        });
    }

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

        public string SessionId { get; } = string.IsNullOrWhiteSpace(sessionId)
                ? throw new ArgumentException("SessionId is required.", nameof(sessionId))
                : sessionId;

        public async Task<IVfsFile> ReadFileAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);
            var fullPath = ResolveFilePath(normalized);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Local VFS file was not found. Path='{normalized}'.",
                    normalized);
            }

            RejectReparsePoint(fullPath);

            var info = new FileInfo(fullPath);

            var content = await File.ReadAllBytesAsync(fullPath)
                .ConfigureAwait(false);

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
                    ["PhysicalPath"] = fullPath
                });
        }

        public async Task WriteFileAsync(string path, byte[] content)
        {
            if (!allowWrite)
            {
                throw new UnauthorizedAccessException("Local VFS provider is read-only.");
            }

            ArgumentNullException.ThrowIfNull(content);

            var normalized = VfsPathRules.Normalize(path);
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

        public Task<IVfsDirectory> GetDirectoryAsync(string path)
        {
            var normalized = VfsPathRules.Normalize(path);
            var fullPath = ResolveDirectoryPath(normalized);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(
                    $"Local VFS directory was not found. Path='{normalized}'.");
            }

            RejectReparsePoint(fullPath);

            var directFiles = Directory
                .EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSafeLocalPath)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(ToVfsFileSnapshot)
                .ToArray();

            var recursiveFiles = Directory
                .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                .Where(IsSafeLocalPath)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(ToVfsFileSnapshot)
                .ToArray();

            var directories = Directory
                .EnumerateDirectories(fullPath, "*", SearchOption.TopDirectoryOnly)
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
                    name: VfsPathRules.GetName(normalized),
                    path: normalized,
                    directFiles: directFiles,
                    recursiveFiles: recursiveFiles,
                    directories: directories,
                    entries: entries));
        }

        public Task<bool> ExistsAsync(string path)
        {
            var fullPath = ResolvePath(path);

            return Task.FromResult(
                File.Exists(fullPath)
                || Directory.Exists(fullPath));
        }

        public Task DeleteAsync(string path)
        {
            if (!allowWrite)
            {
                throw new UnauthorizedAccessException("Local VFS provider is read-only.");
            }

            var fullPath = ResolvePath(path);

            RejectReparsePoint(fullPath);

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

        public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var entries = Directory
                .EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
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
            var normalized = VfsPathRules.Normalize(path);

            var combined = Path.Combine(
                _rootPath,
                normalized.Replace('/', Path.DirectorySeparatorChar));

            var fullPath = Path.GetFullPath(combined);

            if (!IsSafeLocalPath(fullPath))
            {
                throw new UnauthorizedAccessException(
                    $"Path escapes local VFS root. Path='{path}'.");
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
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return;
            }

            var attributes = File.GetAttributes(fullPath);

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Reparse points are not allowed in LocalFileProvider. Path='{fullPath}'.");
            }
        }

        private IVfsFile ToVfsFileSnapshot(string fullPath)
        {
            RejectReparsePoint(fullPath);

            var info = new FileInfo(fullPath);
            var content = File.ReadAllBytes(fullPath);

            // ここではシステムクロックを読んでいない。
            // ファイルが保持する物理 timestamp を VFS Snapshot の観測値として採用している。
            return new VfsFileSnapshot(
                name: info.Name,
                path: ToVirtualPath(fullPath),
                content: content,
                createdAt: info.CreationTimeUtc,
                modifiedAt: info.LastWriteTimeUtc,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PhysicalPath"] = fullPath
                });
        }

        private string ToVirtualPath(string fullPath)
        {
            var relative = Path.GetRelativePath(_rootPath, fullPath);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
