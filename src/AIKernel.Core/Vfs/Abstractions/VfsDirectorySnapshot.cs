namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;

internal sealed class VfsDirectorySnapshot : IVfsDirectory
{
    private readonly IReadOnlyList<IVfsFile> _directFiles;
    private readonly IReadOnlyList<IVfsFile> _recursiveFiles;
    private readonly IReadOnlyList<IVfsDirectory> _directories;
    private readonly IReadOnlyList<VfsEntry> _entries;
    private readonly IReadOnlyDictionary<string, string>? _metadata;

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

    public string Name { get; }

    public string Path { get; }

    public Task<IReadOnlyList<IVfsFile>> GetFilesAsync(bool recursive = false)
    {
        // Side effect: none.
        return Task.FromResult(recursive ? _recursiveFiles : _directFiles);
    }

    public Task<IReadOnlyList<IVfsDirectory>> GetDirectoriesAsync()
    {
        // Side effect: none.
        return Task.FromResult(_directories);
    }

    public Task<IReadOnlyList<VfsEntry>> GetEntriesAsync()
    {
        // Side effect: none.
        return Task.FromResult(_entries);
    }

    public Task<IVfsDirectory?> GetSubdirectoryAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Subdirectory name is invalid.", nameof(name));
        }

        var directory = _directories.FirstOrDefault(x => x.Name == name);
        return Task.FromResult(directory);
    }

    public IReadOnlyDictionary<string, string>? GetMetadata() => _metadata;
}