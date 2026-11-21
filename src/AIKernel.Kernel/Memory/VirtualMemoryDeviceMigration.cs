using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

public sealed partial class VirtualMemorySpace
{
    /// <summary>Moves resident pages to backing storage and releases host buffers owned by the current device.</summary>
    /// <remarks>The caller holds the source device lease and keeps both devices alive until rebinding completes.</remarks>
    public Result<int> PrepareDeviceChange() => PrepareDeviceChange(CancellationToken.None);

    /// <summary>Drains resident pages, stopping at a page boundary if cancellation is requested.</summary>
    /// <remarks>On cancellation, drained pages remain readable from backing storage and the device stays bound.</remarks>
    public Result<int> PrepareDeviceChange(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (disposed || device is null)
                return Fail<int>("virtual_memory_device_change_unavailable", "There is no active device tier.");

            try
            {
                var devicePages = vram.Count;
                while (vram.First is { } gpu)
                {
                    if (cancellationToken.IsCancellationRequested) return Cancelled();
                    MoveToSwap(gpu.Value);
                }
                while (ram.First is { } host)
                {
                    if (cancellationToken.IsCancellationRequested) return Cancelled();
                    MoveToSwap(host.Value);
                }
                arena.ReservedRamBlocks = 0;
                while (freeRam.TryPeek(out var buffer))
                {
                    if (cancellationToken.IsCancellationRequested) return Cancelled();
                    ReleaseRam(buffer);
                    freeRam.Pop();
                }
                return cancellationToken.IsCancellationRequested ? Cancelled() : Result<int>.Success(devicePages);
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                return Fail<int>("virtual_memory_device_drain_failed", ex.Message);
            }
        }

        static Result<int> Cancelled()
            => Fail<int>("virtual_memory_device_change_canceled", "Draining the device was canceled.");
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
