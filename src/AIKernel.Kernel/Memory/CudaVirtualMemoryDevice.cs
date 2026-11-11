using System.Buffers;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>Allocates VRAM and page-locked host buffers through the Native C ABI.</summary>
public sealed unsafe class CudaVirtualMemoryDevice : IVirtualMemoryDevice, IVirtualMemoryHostAllocator, IDisposable
{
    internal Lock Gate { get; } = new();
    private nint context;

    /// <summary>Creates a private context on a zero-based CUDA device ordinal.</summary>
    public CudaVirtualMemoryDevice(int deviceOrdinal = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        NativeCompute.Check(NativeCompute.Create(NativeCompute.Version, deviceOrdinal, out context), "CUDA context creation");
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
    public IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(source.Length);
        var block = new DeviceBlock(this, source.Length);
        lock (Gate)
        {
            NativeCompute.Check(NativeCompute.DeviceAllocate(Context, (nuint)source.Length, out var address), "VRAM allocation");
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
                _ = NativeCompute.DeviceFree(Context, address);
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

    internal sealed class DeviceBlock(CudaVirtualMemoryDevice owner, int length) : IVirtualMemoryDeviceBlock
    {
        internal CudaVirtualMemoryDevice Owner => owner;
        internal ulong Address { get; set; }
        internal int Length { get; } = length;

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
