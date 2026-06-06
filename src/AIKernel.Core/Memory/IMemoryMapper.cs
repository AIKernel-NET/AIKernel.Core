using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

public interface IMemoryMapper
{
    Result<IMemoryRegion> Open(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read);
}
