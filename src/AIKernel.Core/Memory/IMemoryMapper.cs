using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>
/// Maps files or native payloads into an addressable memory region.
/// </summary>
public interface IMemoryMapper
{
    /// <summary>
    /// Opens a memory region or throws when mapping fails.
    /// </summary>
    IMemoryRegion Open(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read);

    /// <summary>
    /// Opens a memory region and returns a fail-closed result.
    /// </summary>
    Result<IMemoryRegion> OpenResult(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read);
}
