namespace AIKernel.Kernel.Memory;

/// <summary>Host page buffer usage across a virtual-memory space and all of its forks, in bytes.</summary>
/// <param name="CapacityBytes">Maximum payload held in the RAM tier.</param>
/// <param name="ReservedBytes">Minimum buffer capacity retained after an explicit reservation.</param>
/// <param name="ResidentBytes">Payload currently held in the RAM tier.</param>
/// <param name="SpareBytes">Allocated buffers available for reuse.</param>
/// <param name="AllocatedBytes">Total allocated host page buffers, including spares and buffers in transit.</param>
/// <param name="PeakAllocatedBytes">Highest host page buffer allocation since the arena was created.</param>
/// <remarks>Counts buffer payload sizes. Operating system residency, allocator overhead, and swap I/O buffers are measured separately.</remarks>
public readonly record struct VirtualMemoryRamUsage(
    long CapacityBytes, long ReservedBytes, long ResidentBytes, long SpareBytes,
    long AllocatedBytes, long PeakAllocatedBytes);
