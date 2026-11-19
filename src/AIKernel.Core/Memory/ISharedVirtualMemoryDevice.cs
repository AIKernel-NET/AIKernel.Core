using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>Moves a range from an external mapping to device memory without host staging.</summary>
public interface ISharedVirtualMemoryDevice : IVirtualMemoryDevice
{
    /// <summary>Returns a new device block after the producer's range has been transferred.</summary>
    Result<IVirtualMemoryDeviceBlock> UploadShared(IMemoryRegion region, long offset, int length);
}
