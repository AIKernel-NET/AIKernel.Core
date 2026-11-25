using System.Diagnostics;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

internal static class WeightFilePlatform
{
    internal static Microsoft.Win32.SafeHandles.SafeFileHandle OpenRead(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);

    internal static long? ReadLimit() => ProcessAddressSpace.ReadAddressSpaceLimit();

    internal static int Affordable(long? limit, Process process, VirtualMemorySpace space, long reserve,
        int pageSize, int currentSlots)
    {
        if (!limit.HasValue) return currentSlots;
        long allocated = space.GetRamUsage().AllocatedBytes / pageSize;
        long headroom = limit.Value - process.VirtualMemorySize64 - reserve;
        return (int)Math.Clamp(allocated + headroom / pageSize, 1, currentSlots);
    }
}
