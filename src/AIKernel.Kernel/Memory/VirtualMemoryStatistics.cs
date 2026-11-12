namespace AIKernel.Kernel.Memory;

/// <summary>Resident block counts and completed transfers since the address space was created.</summary>
/// <param name="RamBlocks">Written blocks resident in RAM.</param>
/// <param name="VramBlocks">Written blocks resident in device memory.</param>
/// <param name="SwapBlocks">Written blocks resident in the swap file.</param>
/// <param name="SwapReads">Complete blocks read from the swap file.</param>
/// <param name="DevicePageIns">Complete blocks moved to device memory.</param>
public readonly record struct VirtualMemoryStatistics(
    int RamBlocks, int VramBlocks, int SwapBlocks, long SwapReads, long DevicePageIns)
{
    /// <summary>Complete reads from the immutable block source.</summary>
    public long BackingReads { get; init; }
}
