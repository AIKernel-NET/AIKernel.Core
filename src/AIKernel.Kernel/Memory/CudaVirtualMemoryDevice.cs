using System.Buffers;
using System.Text;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>Allocates VRAM and page-locked host buffers through the Native C ABI.</summary>
public sealed unsafe class CudaVirtualMemoryDevice : IZeroableVirtualMemoryDevice, ISharedVirtualMemoryDevice, IVirtualMemoryHostAllocator, IDisposable
{
    internal Lock Gate { get; } = new();
    private nint context;

    /// <summary>Creates a private context on a zero-based CUDA device ordinal.</summary>
    public CudaVirtualMemoryDevice(int deviceOrdinal = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        NativeCompute.Check(NativeCompute.Create(NativeCompute.Version, deviceOrdinal, out context), "CUDA context creation");
    }

    /// <summary>Returns the number of CUDA devices visible to the driver.</summary>
    public static Result<int> GetDeviceCount()
    {
        try
        {
            var status = NativeCompute.GetDeviceCount(out var count);
            if (status == 0 && count >= 0) return Result<int>.Success(count);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
        return TransferFailure("cuda_device_enumeration_failed", "The CUDA device list is unavailable.");
    }

    private CudaVirtualMemoryDevice(nint context) => this.context = context;

    /// <summary>Probes CUDA and reports its initialization status when no device tier can be used.</summary>
    public static CudaVirtualMemoryDevice? TryCreate(int ordinal, out int status)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        status = NativeCompute.Create(NativeCompute.Version, ordinal, out var context);
        if (status is 1 or 6)
        {
            NativeCompute.Check(status, "CUDA ABI initialization");
        }

        return status == 0 ? new CudaVirtualMemoryDevice(context) : null;
    }

    internal nint Context
    {
        get
        {
            ObjectDisposedException.ThrowIf(context == 0, this);
            return context;
        }
    }

    /// <inheritdoc />
    public IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source) => Upload(source, null);

    /// <summary>Creates a device tier that accounts for its own successful CUDA page allocations and releases.</summary>
    public AllocationScope CreateAllocationScope() => new(this);

    private IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source, AllocationScope? scope)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(source.Length);
        var block = new DeviceBlock(this, source.Length, scope);
        lock (Gate)
        {
            NativeCompute.Check(NativeCompute.DeviceAllocate(Context, (nuint)source.Length, out var address), "VRAM allocation");
            scope?.RecordAllocation(source.Length);
            try
            {
                fixed (byte* bytes = source)
                {
                    NativeCompute.Check(NativeCompute.Upload(Context, address, bytes, (nuint)source.Length), "CUDA upload");
                }

                block.Address = address;
                return block;
            }
            catch
            {
                if (NativeCompute.DeviceFree(Context, address) == 0) scope?.RecordRelease(source.Length);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public IMemoryOwner<byte> AllocateHostBlock(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var block = new PinnedBlock(this, length);
        lock (Gate)
        {
            NativeCompute.Check(NativeCompute.HostAllocate(Context, (nuint)length, out var address), "Pinned RAM allocation");
            block.Address = address;
            return block;
        }
    }

    /// <summary>Clears the private CUDA context's working buffers.</summary>
    public void ZeroScratch()
    {
        lock (Gate)
            NativeCompute.Check(NativeCompute.WipeScratch(Context), "CUDA scratch wipe");
    }

    /// <summary>Gets the PCI identifier of the actual CUDA device for NVML matching.</summary>
    public string? GetPciBusId()
    {
        Span<byte> id = stackalloc byte[32];
        id.Clear();
        lock (Gate)
        {
            fixed (byte* address = id)
                if (NativeCompute.GetPciBusId(Context, address, id.Length) != 0) return null;
        }
        var length = id.IndexOf((byte)0);
        return length > 0 ? Encoding.ASCII.GetString(id[..length]) : null;
    }

    /// <summary>Transfers an external shared mapping into an existing VRAM block without staging.</summary>
    /// <remarks>The caller keeps the mapping alive and the producer keeps the range stable until the call returns.</remarks>
    public Result<int> UploadShared(IMemoryRegion region, long offset, int length, IVirtualMemoryDeviceBlock target)
    {
        lock (Gate)
        {
            if (context == 0 || region is null || !region.IsMapped || offset < 0 || length <= 0 ||
                offset > region.Length || length > region.Length - offset || target is not DeviceBlock block ||
                !ReferenceEquals(block.Owner, this) || block.Address == 0 || length > block.Length)
                return TransferFailure("shared_upload_invalid", "The shared range or destination is invalid.");

            try
            {
                var status = NativeCompute.UploadShared(context, block.Address, region.Pointer,
                    checked((nuint)region.Length), checked((nuint)offset), (nuint)length);
                return status == 0 ? Result<int>.Success(length) :
                    TransferFailure("shared_upload_failed", $"CUDA shared transfer returned status {status}.");
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or OverflowException)
            {
                return TransferFailure("shared_upload_unavailable", ex.Message);
            }
        }
    }

    /// <inheritdoc />
    public Result<IVirtualMemoryDeviceBlock> UploadShared(IMemoryRegion region, long offset, int length)
    {
        if (region is null || !region.IsMapped || offset < 0 || length <= 0 ||
            offset > region.Length || length > region.Length - offset)
            return Result<IVirtualMemoryDeviceBlock>.Fail(TransferFailure("shared_upload_invalid", "The shared range is invalid.").Error!);

        DeviceBlock? block = null;
        try
        {
            lock (Gate)
            {
                block = new DeviceBlock(this, length);
                NativeCompute.Check(NativeCompute.DeviceAllocate(Context, (nuint)length, out var address), "VRAM allocation");
                block.Address = address;
                var upload = UploadShared(region, offset, length, block);
                if (!upload.IsSuccess)
                {
                    try { block.Zero(); }
                    finally { block.Dispose(); }
                    return Result<IVirtualMemoryDeviceBlock>.Fail(upload.Error!);
                }
                return Result<IVirtualMemoryDeviceBlock>.Success(block);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or OutOfMemoryException
            or EntryPointNotFoundException or DllNotFoundException or OverflowException)
        {
            return Result<IVirtualMemoryDeviceBlock>.Fail(TransferFailure("shared_upload_failed", ex.Message).Error!);
        }
    }

    private static Result<int> TransferFailure(string code, string message)
        => Result<int>.Fail(new ErrorContext(message, code, false) { FailureKind = FailureKind.FailClosed });

    /// <summary>Multiplies FP32 rows in a resident device block by a host vector.</summary>
    public void MultiplyRows(IVirtualMemoryDeviceBlock weights, ReadOnlySpan<float> input, Span<float> output)
        => new TransformerCompute(this).MultiplyRows(default, weights, input, output);

    /// <summary>Releases the private context after its memory spaces have been disposed.</summary>
    public void Dispose()
    {
        lock (Gate)
        {
            if (context != 0)
            {
                var address = context;
                context = 0;
                NativeCompute.Check(NativeCompute.Destroy(address), "CUDA context destruction");
            }
        }
    }

    /// <summary>Accounts for explicit CUDA page allocations on one device, excluding its shared scratch buffers.</summary>
    public sealed class AllocationScope : IZeroableVirtualMemoryDevice, IVirtualMemoryHostAllocator
    {
        private readonly CudaVirtualMemoryDevice device;
        private long allocatedBytes;
        private long releasedBytes;

        internal AllocationScope(CudaVirtualMemoryDevice device) => this.device = device;

        /// <summary>Reads the bytes requested by successful driver allocations and releases in this scope.</summary>
        public (long AllocatedBytes, long ReleasedBytes) GetStatistics()
        {
            lock (device.Gate) return (allocatedBytes, releasedBytes);
        }

        internal void RecordAllocation(int length) => allocatedBytes += length;
        internal void RecordRelease(int length) => releasedBytes += length;

        IVirtualMemoryDeviceBlock IVirtualMemoryDevice.Upload(ReadOnlySpan<byte> source) => device.Upload(source, this);
        IMemoryOwner<byte> IVirtualMemoryHostAllocator.AllocateHostBlock(int length) => device.AllocateHostBlock(length);
        void IZeroableVirtualMemoryDevice.ZeroScratch() => device.ZeroScratch();
    }

    internal sealed class DeviceBlock(CudaVirtualMemoryDevice owner, int length, AllocationScope? scope = null) : IZeroableDeviceBlock
    {
        internal CudaVirtualMemoryDevice Owner => owner;
        internal ulong Address { get; set; }
        internal int Length { get; } = length;

        public void Zero()
        {
            lock (owner.Gate)
            {
                ObjectDisposedException.ThrowIf(Address == 0, this);
                NativeCompute.Check(NativeCompute.WipeDevice(owner.Context, Address, (nuint)Length), "VRAM wipe");
            }
        }

        public void Download(Span<byte> destination)
        {
            if (destination.Length != Length)
            {
                throw new ArgumentException("The destination must match the device block size.", nameof(destination));
            }

            lock (owner.Gate)
            {
                ObjectDisposedException.ThrowIf(Address == 0, this);
                fixed (byte* bytes = destination)
                {
                    NativeCompute.Check(NativeCompute.Download(owner.Context, Address, bytes, (nuint)Length), "CUDA download");
                }
            }
        }

        public void Dispose()
        {
            lock (owner.Gate)
            {
                if (Address != 0 && owner.context != 0)
                {
                    NativeCompute.Check(NativeCompute.DeviceFree(owner.Context, Address), "VRAM release");
                    scope?.RecordRelease(Length);
                }

                Address = 0;
            }
        }
    }

    private sealed class PinnedBlock(CudaVirtualMemoryDevice owner, int length) : MemoryManager<byte>
    {
        internal nint Address { get; set; }

        public override Span<byte> GetSpan()
        {
            ObjectDisposedException.ThrowIf(Address == 0, this);
            return new Span<byte>((void*)Address, length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, length);
            ObjectDisposedException.ThrowIf(Address == 0, this);
            return new MemoryHandle((byte*)Address + elementIndex);
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing)
        {
            lock (owner.Gate)
            {
                if (Address != 0 && owner.context != 0)
                {
                    NativeCompute.Check(NativeCompute.HostFree(owner.Context, Address), "Pinned RAM release");
                }

                Address = 0;
            }
        }
    }
}
