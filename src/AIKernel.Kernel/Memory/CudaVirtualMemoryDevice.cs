using System.Runtime.InteropServices;
using System.Text;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>An opt-in VRAM tier backed by an NVIDIA CUDA driver context.</summary>
public sealed unsafe class CudaVirtualMemoryDevice : IVirtualMemoryDevice, IDisposable
{
    private static readonly Lazy<CudaDriver> Driver = new(CudaDriver.Load);
    private readonly Lock gate = new();
    private readonly CudaDriver driver;
    private nint context;
    private int contextFault;
    private nint matvec;

    /// <summary>Creates a private CUDA context on a zero-based CUDA device ordinal.</summary>
    public CudaVirtualMemoryDevice(int deviceOrdinal = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        driver = Driver.Value;
        Check(driver.Init(0), "cuInit");

        int device;
        Check(driver.DeviceGet(&device, deviceOrdinal), "cuDeviceGet");
        nint created;
        Check(driver.ContextCreate(&created, 0, device), "cuCtxCreate_v2");
        try
        {
            // CUDA creates the context on this thread's stack. Each operation pushes it as needed.
            nint popped;
            Check(driver.ContextPop(&popped), "cuCtxPopCurrent_v2");
            context = created;
        }
        catch
        {
            _ = driver.ContextDestroy(created);
            throw;
        }
    }

    /// <inheritdoc />
    public IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(source.Length);
        var block = new DeviceBlock(this, source.Length);
        lock (gate)
        {
            EnterContext();
            ulong address;
            try
            {
                Check(driver.Allocate(&address, (nuint)source.Length), "cuMemAlloc_v2");
                try
                {
                    fixed (byte* bytes = source)
                    {
                        Check(driver.CopyToDevice(address, bytes, (nuint)source.Length), "cuMemcpyHtoD_v2");
                        Check(driver.ContextSynchronize(), "cuCtxSynchronize");
                    }
                }
                catch
                {
                    _ = driver.Free(address);
                    throw;
                }
            }
            finally
            {
                ExitContext();
            }

            Check(contextFault, "cuCtxPopCurrent_v2");
            block.Address = address;
            return block;
        }
    }

    /// <summary>Multiplies FP32 rows in a resident device block by a host vector.</summary>
    public void MultiplyRows(IVirtualMemoryDeviceBlock weights, ReadOnlySpan<float> input, Span<float> output)
    {
        if (weights is not DeviceBlock block || !ReferenceEquals(block.Owner, this))
        {
            throw new ArgumentException("The weights must reside on this CUDA device.", nameof(weights));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.Length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(output.Length);
        if ((long)input.Length * output.Length > block.Length / sizeof(float))
        {
            throw new ArgumentException("The matrix does not fit in the device block.", nameof(weights));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(block.Address == 0, block);
            EnterContext();
            ulong x = 0, y = 0;
            try
            {
                LoadMatrixKernel();
                var inputBytes = (nuint)input.Length * sizeof(float);
                var outputBytes = (nuint)output.Length * sizeof(float);
                Check(driver.Allocate(&x, inputBytes), "cuMemAlloc_v2");
                Check(driver.Allocate(&y, outputBytes), "cuMemAlloc_v2");
                var matrix = block.Address;
                var columns = (uint)input.Length;
                var parameters = stackalloc void*[4] { &matrix, &x, &y, &columns };
                fixed (float* src = input, dst = output)
                {
                    Check(driver.CopyToDevice(x, (byte*)src, inputBytes), "cuMemcpyHtoD_v2");
                    Check(driver.Launch(matvec, (uint)output.Length, 1, 1, CudaMatrixKernel.Threads, 1, 1,
                        0, 0, parameters, null), "cuLaunchKernel");
                    Check(driver.CopyToHost((byte*)dst, y, outputBytes), "cuMemcpyDtoH_v2");
                }
            }
            finally
            {
                if (y != 0) _ = driver.Free(y);
                if (x != 0) _ = driver.Free(x);
                ExitContext();
            }

            Check(contextFault, "cuCtxPopCurrent_v2");
        }
    }

    private void LoadMatrixKernel()
    {
        if (matvec != 0)
        {
            return;
        }

        nint module, function;
        var ptx = Encoding.ASCII.GetBytes(CudaMatrixKernel.Ptx + "\0");
        fixed (byte* code = ptx)
        {
            Check(driver.ModuleLoad(&module, code), "cuModuleLoadData");
        }

        try
        {
            ReadOnlySpan<byte> name = "matvec\0"u8;
            fixed (byte* symbol = name)
            {
                Check(driver.ModuleGetFunction(&function, module, symbol), "cuModuleGetFunction");
            }

            matvec = function;
        }
        catch
        {
            _ = driver.ModuleUnload(module);
            throw;
        }
    }

    /// <summary>Destroys the context after its virtual memory spaces have been disposed.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (context != 0)
            {
                Check(driver.ContextDestroy(context), "cuCtxDestroy_v2");
                context = 0;
            }
        }
    }

    private void Download(DeviceBlock block, Span<byte> destination)
    {
        if (destination.Length != block.Length)
        {
            throw new ArgumentException("The destination must match the device block size.", nameof(destination));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(block.Address == 0, block);
            EnterContext();
            try
            {
                fixed (byte* bytes = destination)
                {
                    Check(driver.CopyToHost(bytes, block.Address, (nuint)destination.Length), "cuMemcpyDtoH_v2");
                }
            }
            finally
            {
                ExitContext();
            }

            Check(contextFault, "cuCtxPopCurrent_v2");
        }
    }

    private void Release(DeviceBlock block)
    {
        lock (gate)
        {
            if (block.Address == 0)
            {
                return;
            }

            if (context != 0)
            {
                EnterContext();
                try
                {
                    Check(driver.Free(block.Address), "cuMemFree_v2");
                }
                finally
                {
                    ExitContext();
                }
            }

            block.Address = 0;
        }
    }

    private void EnterContext()
    {
        ObjectDisposedException.ThrowIf(context == 0, this);
        Check(contextFault, "cuCtxPopCurrent_v2");
        Check(driver.ContextPush(context), "cuCtxPushCurrent_v2");
    }

    private void ExitContext()
    {
        nint popped;
        // A successful release remains successful even if restoring the thread's context fails.
        contextFault = driver.ContextPop(&popped);
    }

    private static void Check(int status, string operation)
    {
        if (status == 2)
        {
            throw new OutOfMemoryException($"{operation} reported CUDA_ERROR_OUT_OF_MEMORY.");
        }

        if (status != 0)
        {
            throw new InvalidOperationException($"{operation} failed with CUDA status {status}.");
        }
    }

    private sealed class DeviceBlock(CudaVirtualMemoryDevice owner, int length)
        : IVirtualMemoryDeviceBlock
    {
        internal CudaVirtualMemoryDevice Owner => owner;
        internal ulong Address { get; set; }
        internal int Length { get; } = length;
        public void Download(Span<byte> destination) => owner.Download(this, destination);
        public void Dispose() => owner.Release(this);
    }

    private sealed class CudaDriver(nint library)
    {
        internal readonly delegate* unmanaged<uint, int> Init =
            (delegate* unmanaged<uint, int>)NativeLibrary.GetExport(library, "cuInit");
        internal readonly delegate* unmanaged<int*, int, int> DeviceGet =
            (delegate* unmanaged<int*, int, int>)NativeLibrary.GetExport(library, "cuDeviceGet");
        internal readonly delegate* unmanaged<nint*, uint, int, int> ContextCreate =
            (delegate* unmanaged<nint*, uint, int, int>)NativeLibrary.GetExport(library, "cuCtxCreate_v2");
        internal readonly delegate* unmanaged<nint, int> ContextDestroy =
            (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "cuCtxDestroy_v2");
        internal readonly delegate* unmanaged<nint, int> ContextPush =
            (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "cuCtxPushCurrent_v2");
        internal readonly delegate* unmanaged<nint*, int> ContextPop =
            (delegate* unmanaged<nint*, int>)NativeLibrary.GetExport(library, "cuCtxPopCurrent_v2");
        internal readonly delegate* unmanaged<int> ContextSynchronize =
            (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "cuCtxSynchronize");
        internal readonly delegate* unmanaged<ulong*, nuint, int> Allocate =
            (delegate* unmanaged<ulong*, nuint, int>)NativeLibrary.GetExport(library, "cuMemAlloc_v2");
        internal readonly delegate* unmanaged<ulong, int> Free =
            (delegate* unmanaged<ulong, int>)NativeLibrary.GetExport(library, "cuMemFree_v2");
        internal readonly delegate* unmanaged<ulong, byte*, nuint, int> CopyToDevice =
            (delegate* unmanaged<ulong, byte*, nuint, int>)NativeLibrary.GetExport(library, "cuMemcpyHtoD_v2");
        internal readonly delegate* unmanaged<byte*, ulong, nuint, int> CopyToHost =
            (delegate* unmanaged<byte*, ulong, nuint, int>)NativeLibrary.GetExport(library, "cuMemcpyDtoH_v2");

        internal readonly delegate* unmanaged<nint*, byte*, int> ModuleLoad =
            (delegate* unmanaged<nint*, byte*, int>)NativeLibrary.GetExport(library, "cuModuleLoadData");
        internal readonly delegate* unmanaged<nint, int> ModuleUnload =
            (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "cuModuleUnload");
        internal readonly delegate* unmanaged<nint*, nint, byte*, int> ModuleGetFunction =
            (delegate* unmanaged<nint*, nint, byte*, int>)NativeLibrary.GetExport(library, "cuModuleGetFunction");
        internal readonly delegate* unmanaged<nint, uint, uint, uint, uint, uint, uint, uint, nint, void**, void**, int> Launch =
            (delegate* unmanaged<nint, uint, uint, uint, uint, uint, uint, uint, nint, void**, void**, int>)
            NativeLibrary.GetExport(library, "cuLaunchKernel");

        internal static CudaDriver Load()
        {
            if (!Environment.Is64BitProcess || (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()))
            {
                throw new PlatformNotSupportedException("The CUDA VRAM tier requires 64-bit Windows or Linux.");
            }

            var name = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.SystemDirectory, "nvcuda.dll")
                : "libcuda.so.1";
            var library = NativeLibrary.Load(name);
            try
            {
                // Retain the driver for the lifetime of the process.
                return new CudaDriver(library);
            }
            catch
            {
                NativeLibrary.Free(library);
                throw;
            }
        }
    }
}
