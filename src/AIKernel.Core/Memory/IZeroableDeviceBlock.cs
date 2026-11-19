namespace AIKernel.Core.Memory;

/// <summary>A device capable of wiping its blocks and transfer work buffers.</summary>
public interface IZeroableVirtualMemoryDevice : IVirtualMemoryDevice
{
    /// <summary>Clears transient transfer and computation buffers.</summary>
    void ZeroScratch();
}

/// <summary>A device block that can be synchronously overwritten before release.</summary>
public interface IZeroableDeviceBlock : IVirtualMemoryDeviceBlock
{
    /// <summary>Overwrites the entire device block with zeroes.</summary>
    void Zero();
}
