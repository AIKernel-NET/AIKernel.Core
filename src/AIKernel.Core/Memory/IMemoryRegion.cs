using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

public interface IMemoryRegion : IDisposable
{
    MemoryRegionInfo Info { get; }

    IntPtr Pointer { get; }

    long Length { get; }

    bool IsMapped { get; }

    Result<bool> Unmap();
}
