namespace AIKernel.Kernel.Memory;

/// <summary>The current residency of a logical block.</summary>
public enum VirtualMemoryTier
{
    /// <summary>The block reads as zeros until it is written.</summary>
    Unallocated,
    /// <summary>The block resides in host memory.</summary>
    Ram,
    /// <summary>The block resides in device memory.</summary>
    Vram,
    /// <summary>The block resides in the swap file.</summary>
    Swap
}
