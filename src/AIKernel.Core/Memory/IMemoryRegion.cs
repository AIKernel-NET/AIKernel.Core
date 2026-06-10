namespace AIKernel.Core.Memory;

/// <summary>
/// [EN] Represents a mapped native memory region.
/// [JA] AIKernel の公開参照サーフェスにおける IDisposable を説明します。
/// </summary>
public interface IMemoryRegion : IDisposable
{
    /// <summary>
    /// [EN] Gets metadata for the mapped region.
    /// [JA] マッピングされた領域のメタデータを取得します。
    /// </summary>
    MemoryRegionInfo Info { get; }

    /// <summary>
    /// [EN] Gets the native pointer for the mapped view.
    /// [JA] マッピングされたビューのネイティブポインターを取得します。
    /// </summary>
    IntPtr Pointer { get; }

    /// <summary>
    /// [EN] Gets the mapped region length in bytes.
    /// [JA] マッピングされた領域の長さをバイト単位で取得します。
    /// </summary>
    long Length { get; }

    /// <summary>
    /// [EN] Gets whether the native region is still mapped.
    /// [JA] ネイティブ領域がまだマッピングされているかどうかを取得します。
    /// </summary>
    bool IsMapped { get; }

    /// <summary>
    /// [EN] Unmaps the region.
    /// [JA] 領域のマッピングを解除します。
    /// </summary>
    bool Unmap();
}
