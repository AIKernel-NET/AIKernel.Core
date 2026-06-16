namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Common.Results;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;

internal sealed class VfsDirectorySnapshot : IVfsDirectory
{
    private readonly IReadOnlyList<IVfsFile> _directFiles;
    private readonly IReadOnlyList<IVfsFile> _recursiveFiles;
    private readonly IReadOnlyList<IVfsDirectory> _directories;
    private readonly IReadOnlyList<VfsEntry> _entries;
    private readonly IReadOnlyDictionary<string, string>? _metadata;
    /// <summary>
    /// EN: Gets VfsDirectorySnapshot.
    /// EN: Documentation for public API. JA: VfsDirectorySnapshot を取得します。
    /// </summary>

    public VfsDirectorySnapshot(
        string name,
        string path,
        IReadOnlyList<IVfsFile> directFiles,
        IReadOnlyList<IVfsFile> recursiveFiles,
        IReadOnlyList<IVfsDirectory> directories,
        IReadOnlyList<VfsEntry> entries,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Name = name;
        Path = path;
        _directFiles = directFiles;
        _recursiveFiles = recursiveFiles;
        _directories = directories;
        _entries = entries;
        _metadata = metadata;
    }
    /// <summary>
    /// EN: Gets Name.
    /// EN: Documentation for public API. JA: Name を取得します。
    /// </summary>

    public string Name { get; }
    /// <summary>
    /// EN: Gets Path.
    /// EN: Documentation for public API. JA: Path を取得します。
    /// </summary>

    public string Path { get; }
    /// <summary>
    /// EN: Executes GetFilesAsync.
    /// EN: Documentation for public API. JA: GetFilesAsync を実行します。
    /// </summary>

    public Task<IReadOnlyList<IVfsFile>> GetFilesAsync(bool recursive = false)
    {
        // Side effect: none.
        return Task.FromResult(SelectedFiles(recursive));
    }
    /// <summary>
    /// EN: Executes GetDirectoriesAsync.
    /// EN: Documentation for public API. JA: GetDirectoriesAsync を実行します。
    /// </summary>

    public Task<IReadOnlyList<IVfsDirectory>> GetDirectoriesAsync()
    {
        // Side effect: none.
        return Task.FromResult(_directories);
    }
    /// <summary>
    /// EN: Executes GetEntriesAsync.
    /// EN: Documentation for public API. JA: GetEntriesAsync を実行します。
    /// </summary>

    public Task<IReadOnlyList<VfsEntry>> GetEntriesAsync()
    {
        // Side effect: none.
        return Task.FromResult(_entries);
    }
    /// <summary>
    /// EN: Executes GetSubdirectoryAsync.
    /// EN: Documentation for public API. JA: GetSubdirectoryAsync を実行します。
    /// </summary>

    public Task<IVfsDirectory?> GetSubdirectoryAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Subdirectory name is invalid.", nameof(name));
        }

        var directory = _directories.FirstOrDefault(x => x.Name == name);
        return Task.FromResult(directory);
    }
    /// <summary>
    /// EN: Executes GetMetadata.
    /// EN: Documentation for public API. JA: GetMetadata を実行します。
    /// </summary>

    public IReadOnlyDictionary<string, string>? GetMetadata() => _metadata;

    private IReadOnlyList<IVfsFile> SelectedFiles(bool recursive)
        => FileSelection(recursive).Match(_ => _directFiles, _ => _recursiveFiles);

    private static Either<string, string> FileSelection(bool recursive)
        => recursive
            ? Either<string, string>.FromRight("recursive")
            : Either<string, string>.FromLeft("direct");
}
