namespace AIKernel.Core.Memory;

/// <summary>
/// Describes a mapped native memory region.
/// </summary>
public sealed record MemoryRegionInfo(
    string Path,
    long Length,
    MemoryAccessMode AccessMode);
