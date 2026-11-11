using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

public sealed partial class VirtualMemorySpace
{
    /// <summary>Moves resident pages to backing storage and releases host buffers owned by the current device.</summary>
    /// <remarks>The caller holds the source device lease and keeps both devices alive until rebinding completes.</remarks>
    public Result<int> PrepareDeviceChange()
    {
        lock (gate)
        {
            if (disposed || device is null)
                return Fail<int>("virtual_memory_device_change_unavailable", "There is no active device tier.");

            try
            {
                var devicePages = vram.Count;
                while (vram.First is { } gpu) MoveToSwap(gpu.Value);
                while (ram.First is { } host) MoveToSwap(host.Value);
                while (freeRam.TryPeek(out var buffer))
                {
                    buffer.Dispose();
                    freeRam.Pop();
                }
                return Result<int>.Success(devicePages);
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                return Fail<int>("virtual_memory_device_drain_failed", ex.Message);
            }
        }
    }

    /// <summary>Changes the device of an arena whose resident pages have been drained.</summary>
    /// <remarks>All forks share the device tier. The caller serializes use of the arena across the handoff.</remarks>
    public Result<bool> BindPreparedDevice(IVirtualMemoryDevice replacement)
    {
        lock (gate)
        {
            if (disposed || vramCapacity == 0 || replacement is null || vram.Count != 0 ||
                ram.Count != 0 || freeRam.Count != 0 ||
                (arena.Policy.ZeroWipe && replacement is not IZeroableVirtualMemoryDevice))
                return Fail<bool>("virtual_memory_device_change_unavailable", "The arena is not ready to bind the target device.");

            arena.Device = replacement;
            return Result<bool>.Success(true);
        }
    }
}
