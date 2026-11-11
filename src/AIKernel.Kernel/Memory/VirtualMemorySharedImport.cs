using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

public sealed partial class VirtualMemorySpace
{
    /// <summary>Fills a logical VRAM block directly from an external shared memory range.</summary>
    /// <remarks>Keep the mapping alive and the producer's range stable until the transfer completes.</remarks>
    public Result<int> ImportSharedBlock(long index, IMemoryRegion region, long offset)
    {
        lock (gate)
        {
            if (disposed || (ulong)index >= (ulong)BlockCount || region is null || !region.IsMapped ||
                offset < 0 || offset > region.Length || BlockSize > region.Length - offset ||
                device is not ISharedVirtualMemoryDevice shared)
                return Fail<int>("shared_upload_unavailable", "The mapped range or device tier is unavailable.");

            try
            {
                var block = new Block(index) { Dirty = true };
                blocks.EnsureCapacity(checked(blocks.Count + 1));
                if (vram.Count == vramCapacity) MoveToSwap(vram.First!.Value);
                var transfer = shared.UploadShared(region, offset, BlockSize);
                if (!transfer.IsSuccess) return Result<int>.Fail(transfer.Error!);

                var uploaded = transfer.Value!;
                try
                {
                    if (blocks.TryGetValue(index, out var old)) ReleaseReference(old);
                }
                catch
                {
                    ReleaseDevice(uploaded);
                    throw;
                }
                block.Device = uploaded;
                blocks[index] = block;
                vram.AddLast(block.Node);
                devicePageIns++;
                return Result<int>.Success(BlockSize);
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                return Fail<int>("shared_upload_failed", ex.Message);
            }
        }
    }
}
