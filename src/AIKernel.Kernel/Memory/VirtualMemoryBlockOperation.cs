using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>Reads a block in the selected physical compute tier during a synchronous call.</summary>
public delegate void VirtualMemoryBlockOperation(ReadOnlySpan<byte> host, IVirtualMemoryDeviceBlock? device);

/// <summary>Provides immutable backing for logical blocks, such as mapped model tensors.</summary>
public interface IVirtualMemorySource
{
    /// <summary>Fills one logical block on a page fault.</summary>
    void ReadBlock(long blockIndex, Span<byte> destination);
}
