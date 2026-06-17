using AIKernel.Vfs;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Vfs.Abstractions;

internal sealed class VfsFileSnapshot : IVfsFile
{
    private readonly byte[] _content;
    private readonly IReadOnlyDictionary<string, string>? _metadata;
    /// <summary>
    /// EN: Gets VfsFileSnapshot.
    /// [EN] Documents this public package API member. [JA] VfsFileSnapshot を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets Name.
    /// [EN] Documents this public package API member. [JA] Name を取得します。
    /// </summary>

    public string Name { get; }
    /// <summary>
    /// EN: Gets Path.
    /// [EN] Documents this public package API member. [JA] Path を取得します。
    /// </summary>

    public string Path { get; }
    /// <summary>
    /// EN: Gets Size.
    /// [EN] Documents this public package API member. [JA] Size を取得します。
    /// </summary>

    public long Size { get; }
    /// <summary>
    /// EN: Gets CreatedAt.
    /// [EN] Documents this public package API member. [JA] CreatedAt を取得します。
    /// </summary>

    public DateTime CreatedAt { get; }
    /// <summary>
    /// EN: Gets ModifiedAt.
    /// [EN] Documents this public package API member. [JA] ModifiedAt を取得します。
    /// </summary>

    public DateTime ModifiedAt { get; }
    /// <summary>
    /// EN: Executes ReadAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsync を実行します。
    /// </summary>

    public Task<byte[]> ReadAsync()
    {
        // Side effect: none.
        // Returns defensive copy to avoid shared mutable state.
        return Task.FromResult(_content.ToArray());
    }
    /// <summary>
    /// EN: Executes ReadAsTextAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsTextAsync を実行します。
    /// </summary>

    public async Task<string> ReadAsTextAsync()
    {
        var bytes = await ReadAsync().ConfigureAwait(false);
        return Encoding.UTF8.GetString(bytes);
    }
    /// <summary>
    /// EN: Executes GetMetadata.
    /// [EN] Documents this public package API member. [JA] GetMetadata を実行します。
    /// </summary>

    public IReadOnlyDictionary<string, string>? GetMetadata() => _metadata;
}
