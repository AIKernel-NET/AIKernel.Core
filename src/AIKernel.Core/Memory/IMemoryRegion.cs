namespace AIKernel.Core.Memory;

/// <summary>
/// Represents a mapped native memory region.
/// </summary>
public interface IMemoryRegion : IDisposable
{
    /// <summary>
    /// Gets metadata for the mapped region.
    /// </summary>
    MemoryRegionInfo Info { get; }

    /// <summary>
    /// Gets the native pointer for the mapped view.
    /// </summary>
    IntPtr Pointer { get; }

    /// <summary>
    /// Gets the mapped region length in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets whether the native region is still mapped.
    /// </summary>
    bool IsMapped { get; }

    /// <summary>
    /// Unmaps the region.
    /// </summary>
    bool Unmap();
}
