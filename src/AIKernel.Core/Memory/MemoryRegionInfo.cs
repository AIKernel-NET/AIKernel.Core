namespace AIKernel.Core.Memory;

/// <summary>
/// [EN] Describes a mapped native memory region.
/// [JA] AIKernel の公開参照サーフェスにおける MemoryRegionInfo を説明します。
/// </summary>
public sealed record MemoryRegionInfo(
    string Path,
    long Length,
    MemoryAccessMode AccessMode);
