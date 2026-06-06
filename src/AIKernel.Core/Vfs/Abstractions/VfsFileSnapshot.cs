using AIKernel.Vfs;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Vfs.Abstractions;

internal sealed class VfsFileSnapshot : IVfsFile
{
    private readonly byte[] _content;
    private readonly IReadOnlyDictionary<string, string>? _metadata;

    public VfsFileSnapshot(
        string name,
        string path,
        byte[] content,
        DateTime createdAt,
        DateTime modifiedAt,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Name = name;
        Path = path;
        _content = content.ToArray();
        Size = _content.Length;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        _metadata = metadata is null
            ? null
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }

    public string Name { get; }

    public string Path { get; }

    public long Size { get; }

    public DateTime CreatedAt { get; }

    public DateTime ModifiedAt { get; }

    public Task<byte[]> ReadAsync()
    {
        // Side effect: none.
        // Returns defensive copy to avoid shared mutable state.
        return Task.FromResult(_content.ToArray());
    }

    public async Task<string> ReadAsTextAsync()
    {
        var bytes = await ReadAsync().ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }

    public IReadOnlyDictionary<string, string>? GetMetadata() => _metadata;
}
